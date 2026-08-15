using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModScope.LocalKnowledge;

public enum PackageMetadataParseStatus
{
    Parsed,
    Missing,
    Malformed,
    EncodingError
}

public enum VersionScheme
{
    Unknown,
    Semver,
    NumericDotted
}

public enum VersionObservationSourceKind
{
    ModInfoXml,
    Mo2MetaIni,
    EvidenceManifest,
    WebObservation
}

public enum VersionObservationRole
{
    Release,
    Unknown
}

public enum IdentityResolutionState
{
    Exact,
    Ambiguous,
    Missing,
    Conflicting,
    Unresolved
}

public enum VersionComparisonStatus
{
    Equal,
    Mismatch,
    NotComparable,
    NotAssessed
}

public sealed record Mo2PackageMetadata(
    string RelativePath,
    PackageMetadataParseStatus ParseStatus,
    string? ModId,
    string? FileId,
    string? Version,
    string? NewestVersion,
    string? Url,
    IReadOnlyDictionary<string, string> KnownValues,
    IReadOnlyDictionary<string, string> UnknownValues,
    IReadOnlyList<Diagnostic> Diagnostics,
    SourceReference Source);

public sealed record SourceArtifact(
    string ArtifactId,
    string Kind,
    string? Name,
    string? ModId,
    string? FileId,
    string? SourceUrl,
    SourceReference Source);

public sealed record MO2Package(
    string DirectoryName,
    SourceReference Source,
    Mo2PackageMetadata Metadata,
    int ModletCount);

public sealed record Modlet(
    string ModKey,
    string DirectoryName,
    string PackageDirectoryName,
    SourceReference Source);

public sealed record VersionObservation(
    string OwnerKey,
    VersionObservationRole Role,
    VersionObservationSourceKind SourceKind,
    string? RawValue,
    string? NormalizedValue,
    VersionScheme Scheme,
    SourceReference Source,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record VersionComparison(
    VersionComparisonStatus Status,
    string Reason,
    IReadOnlyList<VersionObservation> Observations);

public sealed record PackageVersionEvidence(
    MO2Package Package,
    IdentityResolutionState IdentityState,
    string IdentityReason,
    IReadOnlyList<SourceArtifact> SourceArtifacts,
    IReadOnlyList<VersionObservation> VersionObservations,
    VersionComparison Comparison,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record VersionEvidenceManifestDocument(
    int SchemaVersion,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<SourceArtifact> SourceArtifacts,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PackageArtifactBindings,
    IReadOnlyList<VersionObservation> VersionObservations,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record VersionEvidenceManifestLoadResult(
    VersionEvidenceManifestDocument? Document,
    string? DisplayName,
    string? Sha256,
    long? Size,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsLoaded => Document is not null
        && Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

public static class Mo2MetaIniReader
{
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "modid",
        "fileid",
        "version",
        "newestversion",
        "url",
        "name"
    };

    public static Mo2PackageMetadata Read(
        string packageDirectoryPath,
        string packageDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectoryName);

        var relativePath = $"mods/{ParsingUtilities.NormalizeRelativePath(packageDirectoryName)}/meta.ini";
        var source = new SourceReference(SourceReferenceKind.PackageFile, relativePath);
        var path = Path.Combine(packageDirectoryPath, "meta.ini");
        if (!File.Exists(path))
        {
            return new Mo2PackageMetadata(
                relativePath,
                PackageMetadataParseStatus.Missing,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new[]
                {
                    new Diagnostic(
                        "package.meta.missing",
                        DiagnosticSeverity.Warning,
                        "The MO2 package does not contain meta.ini.",
                        source)
                },
                source);
        }

        try
        {
            var decoded = ParsingUtilities.DecodeText(File.ReadAllBytes(path));
            var diagnostics = new List<Diagnostic>();
            if (decoded.HadDecodingError)
            {
                diagnostics.Add(new Diagnostic(
                    "package.meta.encoding.invalid",
                    DiagnosticSeverity.Error,
                    "The MO2 meta.ini contains bytes that are not valid for the detected encoding.",
                    source));
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unknownValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = string.Empty;
            var lineNumber = 0;
            foreach (var rawLine in decoded.Text.Split('\n'))
            {
                lineNumber += 1;
                var line = rawLine.TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1].Trim();
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    diagnostics.Add(new Diagnostic(
                        "package.meta.line.malformed",
                        DiagnosticSeverity.Warning,
                        $"The MO2 meta.ini line {lineNumber} has no key/value separator.",
                        source));
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (key.Length == 0)
                {
                    diagnostics.Add(new Diagnostic(
                        "package.meta.key.missing",
                        DiagnosticSeverity.Warning,
                        $"The MO2 meta.ini line {lineNumber} has an empty key.",
                        source));
                    continue;
                }

                var normalizedKey = key.ToLowerInvariant();
                if (values.ContainsKey(normalizedKey) || unknownValues.ContainsKey(normalizedKey))
                {
                    diagnostics.Add(new Diagnostic(
                        "package.meta.key.duplicate",
                        DiagnosticSeverity.Warning,
                        $"The MO2 meta.ini key '{key}' occurs more than once.",
                        source));
                }

                var scopedKey = string.IsNullOrWhiteSpace(section)
                    ? normalizedKey
                    : $"{section.ToLowerInvariant()}.{normalizedKey}";
                if (KnownKeys.Contains(normalizedKey))
                {
                    values[normalizedKey] = value;
                }
                else
                {
                    unknownValues[scopedKey] = value;
                }
            }

            var status = decoded.HadDecodingError
                ? PackageMetadataParseStatus.EncodingError
                : diagnostics.Any(diagnostic => diagnostic.Code == "package.meta.line.malformed")
                    ? PackageMetadataParseStatus.Malformed
                    : PackageMetadataParseStatus.Parsed;
            return new Mo2PackageMetadata(
                relativePath,
                status,
                Get(values, "modid"),
                Get(values, "fileid"),
                Get(values, "version"),
                Get(values, "newestversion"),
                Get(values, "url"),
                values,
                unknownValues,
                diagnostics.AsReadOnly(),
                source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Mo2PackageMetadata(
                relativePath,
                PackageMetadataParseStatus.Malformed,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new[]
                {
                    new Diagnostic(
                        "package.meta.read_failed",
                        DiagnosticSeverity.Error,
                        $"The MO2 meta.ini could not be read: {exception.Message}",
                        source)
                },
                source);
        }
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}

public static class VersionEvidenceManifestReader
{
    public const int SupportedSchemaVersion = 1;

    public static VersionEvidenceManifestLoadResult Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new VersionEvidenceManifestLoadResult(
                null,
                null,
                null,
                null,
                Array.Empty<Diagnostic>());
        }

        var displayName = Path.GetFileName(path);
        var source = new SourceReference(
            SourceReferenceKind.EvidenceManifest,
            $"evidence-manifest/{displayName}");
        if (!File.Exists(path))
        {
            return new VersionEvidenceManifestLoadResult(
                null,
                displayName,
                null,
                null,
                new[]
                {
                    new Diagnostic(
                        "evidence.manifest.missing",
                        DiagnosticSeverity.Error,
                        "The explicit version evidence manifest does not exist.",
                        source)
                });
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var sha256 = ParsingUtilities.Sha256Hex(bytes);
            using var document = JsonDocument.Parse(bytes);
            var diagnostics = new List<Diagnostic>();
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid(displayName, sha256, bytes.LongLength, source, "The version evidence manifest root must be an object.");
            }

            var schemaVersion = ReadInt(root, "schemaVersion") ?? 0;
            if (schemaVersion != SupportedSchemaVersion)
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.schema.unsupported",
                    DiagnosticSeverity.Error,
                    $"The version evidence manifest schema version '{schemaVersion}' is not supported.",
                    source,
                    schemaVersion.ToString(CultureInfo.InvariantCulture)));
            }

            var observedAt = ReadDateTime(root, "observedAtUtc") ?? DateTimeOffset.UtcNow;
            var artifacts = ReadArtifacts(root, source, diagnostics);
            var bindings = ReadBindings(root, source, diagnostics);
            var observations = ReadObservations(root, source, observedAt, diagnostics);
            var manifest = new VersionEvidenceManifestDocument(
                schemaVersion,
                observedAt,
                artifacts.AsReadOnly(),
                bindings,
                observations.AsReadOnly(),
                diagnostics.AsReadOnly());
            return new VersionEvidenceManifestLoadResult(
                manifest,
                displayName,
                sha256,
                bytes.LongLength,
                diagnostics.AsReadOnly());
        }
        catch (JsonException exception)
        {
            return Invalid(displayName, null, null, source, $"The version evidence manifest is not valid JSON: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new VersionEvidenceManifestLoadResult(
                null,
                displayName,
                null,
                null,
                new[]
                {
                    new Diagnostic(
                        "evidence.manifest.read_failed",
                        DiagnosticSeverity.Error,
                        $"The version evidence manifest could not be read: {exception.Message}",
                        source)
                });
        }
    }

    private static VersionEvidenceManifestLoadResult Invalid(
        string? displayName,
        string? sha256,
        long? size,
        SourceReference source,
        string message)
    {
        return new VersionEvidenceManifestLoadResult(
            null,
            displayName,
            sha256,
            size,
            new[]
            {
                new Diagnostic("evidence.manifest.invalid", DiagnosticSeverity.Error, message, source)
            });
    }

    private static List<SourceArtifact> ReadArtifacts(
        JsonElement root,
        SourceReference manifestSource,
        List<Diagnostic> diagnostics)
    {
        var artifacts = new List<SourceArtifact>();
        var artifactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("artifacts", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new Diagnostic(
                "evidence.manifest.artifacts.missing",
                DiagnosticSeverity.Warning,
                "The version evidence manifest has no artifacts array.",
                manifestSource));
            return artifacts;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.artifact.invalid",
                    DiagnosticSeverity.Warning,
                    "A version evidence artifact is not a JSON object.",
                    manifestSource));
                continue;
            }

            var artifactId = ReadString(item, "artifactId");
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.artifact.id_missing",
                    DiagnosticSeverity.Warning,
                    "A version evidence artifact has no artifactId.",
                    manifestSource));
                continue;
            }

            artifactId = artifactId.Trim();
            if (!artifactIds.Add(artifactId))
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.artifact.duplicate",
                    DiagnosticSeverity.Warning,
                    "The version evidence manifest contains a duplicate artifactId.",
                    manifestSource));
                continue;
            }

            artifacts.Add(new SourceArtifact(
                artifactId,
                ReadString(item, "kind") ?? "external",
                ReadString(item, "name"),
                ReadString(item, "modId"),
                ReadString(item, "fileId"),
                ReadString(item, "sourceUrl"),
                new SourceReference(
                    SourceReferenceKind.EvidenceManifest,
                    $"{manifestSource.RelativePath}/artifact/{artifactId}")));
        }

        return artifacts;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadBindings(
        JsonElement root,
        SourceReference manifestSource,
        List<Diagnostic> diagnostics)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("packageBindings", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.binding.invalid",
                    DiagnosticSeverity.Warning,
                    "A package binding is not a JSON object.",
                    manifestSource));
                continue;
            }

            var packageDirectory = NormalizePackageDirectory(ReadString(item, "packageDirectory"));
            if (string.IsNullOrWhiteSpace(packageDirectory))
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.binding.package_missing",
                    DiagnosticSeverity.Warning,
                    "A package binding has no packageDirectory.",
                    manifestSource));
                continue;
            }

            if (!item.TryGetProperty("artifactIds", out var artifactIds) || artifactIds.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.binding.artifacts_missing",
                    DiagnosticSeverity.Warning,
                    $"The package binding for '{packageDirectory}' has no artifactIds array.",
                    manifestSource,
                    packageDirectory));
                continue;
            }

            var ids = artifactIds.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
            result[packageDirectory] = ids;
        }

        return result;
    }

    private static List<VersionObservation> ReadObservations(
        JsonElement root,
        SourceReference manifestSource,
        DateTimeOffset defaultObservedAt,
        List<Diagnostic> diagnostics)
    {
        var result = new List<VersionObservation>();
        if (!root.TryGetProperty("versionObservations", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.observation.invalid",
                    DiagnosticSeverity.Warning,
                    "A version observation is not a JSON object.",
                    manifestSource));
                continue;
            }

            var artifactId = ReadString(item, "artifactId");
            var rawValue = ReadString(item, "rawValue");
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                diagnostics.Add(new Diagnostic(
                    "evidence.manifest.observation.artifact_missing",
                    DiagnosticSeverity.Warning,
                    "A version observation has no artifactId.",
                    manifestSource));
                continue;
            }

            var source = new SourceReference(
                SourceReferenceKind.EvidenceManifest,
                $"{manifestSource.RelativePath}/observation/{artifactId.Trim()}");
            var normalized = VersionNormalizer.Normalize(rawValue, out var scheme);
            var observationDiagnostics = new List<Diagnostic>();
            var role = ReadObservationRole(item, manifestSource, diagnostics, observationDiagnostics);
            result.Add(new VersionObservation(
                artifactId.Trim(),
                role,
                VersionObservationSourceKind.EvidenceManifest,
                rawValue,
                normalized,
                scheme,
                source,
                ReadDateTime(item, "observedAtUtc") ?? defaultObservedAt,
                observationDiagnostics.AsReadOnly()));
        }

        return result;
    }

    private static VersionObservationRole ReadObservationRole(
        JsonElement item,
        SourceReference manifestSource,
        List<Diagnostic> diagnostics,
        List<Diagnostic> observationDiagnostics)
    {
        var role = ReadString(item, "role");
        if (string.IsNullOrWhiteSpace(role)
            || string.Equals(role, "release", StringComparison.OrdinalIgnoreCase))
        {
            return VersionObservationRole.Release;
        }

        var diagnostic = new Diagnostic(
            "evidence.manifest.observation.role.unsupported",
            DiagnosticSeverity.Warning,
            "The version observation role is not supported for comparison.",
            manifestSource);
        diagnostics.Add(diagnostic);
        observationDiagnostics.Add(diagnostic);
        return VersionObservationRole.Unknown;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateTimeOffset? ReadDateTime(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizePackageDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = ParsingUtilities.NormalizeRelativePath(value.Trim()).Trim('/');
        return normalized.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)
            ? normalized[5..]
            : normalized;
    }
}

public static class VersionEvidenceAssembler
{
    public static IReadOnlyList<LocalModRecord> Attach(
        IReadOnlyList<LocalModRecord> records,
        VersionEvidenceManifestDocument? manifest)
    {
        ArgumentNullException.ThrowIfNull(records);
        var packageCounts = records
            .Where(record => !string.IsNullOrWhiteSpace(record.Mo2OuterDirectoryName))
            .GroupBy(record => record.Mo2OuterDirectoryName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var artifactsById = manifest?.SourceArtifacts
            .ToDictionary(artifact => artifact.ArtifactId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, SourceArtifact>(StringComparer.OrdinalIgnoreCase);

        return records
            .Select(record =>
            {
                var packageDirectory = record.Mo2OuterDirectoryName ?? record.DirectoryName;
                var count = packageCounts.TryGetValue(packageDirectory, out var packageCount) ? packageCount : 1;
                var metadata = record.PackageMetadata ?? MissingMetadata(packageDirectory);
                var artifactIds = manifest?.PackageArtifactBindings.TryGetValue(packageDirectory, out var boundIds) == true
                    ? boundIds
                    : Array.Empty<string>();
                var artifacts = artifactIds
                    .Where(artifactsById.ContainsKey)
                    .Select(id => artifactsById[id])
                    .ToList()
                    .AsReadOnly();
                var identity = ResolveIdentity(metadata, artifacts, artifactIds.Count);
                var package = new MO2Package(
                    packageDirectory,
                    record.Mo2OuterSource ?? record.Source,
                    metadata,
                    count);
                var observations = BuildObservations(record, package, artifacts, manifest);
                var comparison = VersionComparator.Compare(identity.State, observations);
                var diagnostics = metadata.Diagnostics
                    .Concat(comparison.Observations.SelectMany(observation => observation.Diagnostics))
                    .ToList()
                    .AsReadOnly();
                var evidence = new PackageVersionEvidence(
                    package,
                    identity.State,
                    identity.Reason,
                    artifacts,
                    observations,
                    comparison,
                    diagnostics);
                return record with { PackageEvidence = evidence };
            })
            .ToList()
            .AsReadOnly();
    }

    private static (IdentityResolutionState State, string Reason) ResolveIdentity(
        Mo2PackageMetadata metadata,
        IReadOnlyList<SourceArtifact> artifacts,
        int boundArtifactCount)
    {
        var hasModId = !string.IsNullOrWhiteSpace(metadata.ModId);
        var hasFileId = !string.IsNullOrWhiteSpace(metadata.FileId);
        if (artifacts.Count > 1 || boundArtifactCount > 1)
        {
            return (IdentityResolutionState.Ambiguous, "The package is bound to multiple source artifacts.");
        }

        if (artifacts.Count == 1)
        {
            var artifact = artifacts[0];
            if ((hasModId && !string.Equals(metadata.ModId, artifact.ModId, StringComparison.OrdinalIgnoreCase))
                || (hasFileId && !string.Equals(metadata.FileId, artifact.FileId, StringComparison.OrdinalIgnoreCase)))
            {
                return (IdentityResolutionState.Conflicting, "MO2 meta.ini identifiers conflict with the bound source artifact.");
            }

            return (IdentityResolutionState.Exact, "The package has one explicit source artifact binding.");
        }

        if (hasModId && hasFileId)
        {
            return (IdentityResolutionState.Exact, "MO2 meta.ini contains both modId and fileId.");
        }

        if (hasModId || hasFileId)
        {
            return (IdentityResolutionState.Ambiguous, "MO2 meta.ini contains only one source identifier.");
        }

        return (IdentityResolutionState.Missing, "No explicit source artifact identity was observed.");
    }

    private static Mo2PackageMetadata MissingMetadata(string packageDirectory)
    {
        var source = new SourceReference(
            SourceReferenceKind.PackageFile,
            $"mods/{ParsingUtilities.NormalizeRelativePath(packageDirectory)}/meta.ini");
        return new Mo2PackageMetadata(
            source.RelativePath,
            PackageMetadataParseStatus.Missing,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new[]
            {
                new Diagnostic(
                    "package.meta.missing",
                    DiagnosticSeverity.Warning,
                    "The MO2 package does not contain meta.ini.",
                    source)
            },
            source);
    }

    private static IReadOnlyList<VersionObservation> BuildObservations(
        LocalModRecord record,
        MO2Package package,
        IReadOnlyList<SourceArtifact> artifacts,
        VersionEvidenceManifestDocument? manifest)
    {
        var result = new List<VersionObservation>();
        var observedAt = DateTimeOffset.UtcNow;
        Add(result, record.ModKey, VersionObservationSourceKind.ModInfoXml, record.ModInfo?.Version, record.ModInfo?.Source, observedAt);
        Add(result, package.DirectoryName, VersionObservationSourceKind.Mo2MetaIni, package.Metadata.Version, package.Metadata.Source, observedAt);
        if (manifest is not null)
        {
            var artifactIds = manifest.PackageArtifactBindings.TryGetValue(package.DirectoryName, out var ids)
                ? ids
                : Array.Empty<string>();
            result.AddRange(manifest.VersionObservations
                .Where(observation => artifactIds.Contains(observation.OwnerKey, StringComparer.OrdinalIgnoreCase)));
        }

        return result.AsReadOnly();
    }

    private static void Add(
        List<VersionObservation> result,
        string ownerKey,
        VersionObservationSourceKind sourceKind,
        string? rawValue,
        SourceReference? source,
        DateTimeOffset observedAt)
    {
        if (source is null && rawValue is null)
        {
            return;
        }

        var normalized = VersionNormalizer.Normalize(rawValue, out var scheme);
        result.Add(new VersionObservation(
            ownerKey,
            VersionObservationRole.Release,
            sourceKind,
            rawValue,
            normalized,
            scheme,
            source ?? new SourceReference(SourceReferenceKind.Diagnostic, "version-evidence/unknown"),
            observedAt,
            Array.Empty<Diagnostic>()));
    }
}

public static class VersionComparator
{
    public static bool TryCompareNormalized(
        string? left,
        string? right,
        VersionScheme scheme,
        out int comparison)
    {
        comparison = 0;
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftNormalized = VersionNormalizer.Normalize(left, out var leftScheme);
        var rightNormalized = VersionNormalizer.Normalize(right, out var rightScheme);
        if (leftNormalized is null
            || rightNormalized is null
            || leftScheme != scheme
            || rightScheme != scheme
            || scheme is not (VersionScheme.Semver or VersionScheme.NumericDotted))
        {
            return false;
        }

        comparison = scheme == VersionScheme.Semver
            ? CompareSemver(leftNormalized, rightNormalized)
            : CompareNumericDotted(leftNormalized, rightNormalized);
        return true;
    }

    public static VersionComparison Compare(
        IdentityResolutionState identityState,
        IReadOnlyList<VersionObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (identityState != IdentityResolutionState.Exact)
        {
            return new VersionComparison(
                VersionComparisonStatus.NotAssessed,
                "Identity is not exact, so version comparison was not assessed.",
                observations);
        }

        var comparable = observations
            .Where(observation => !string.IsNullOrWhiteSpace(observation.NormalizedValue))
            .ToList();
        if (comparable.Count < 2)
        {
            return new VersionComparison(
                VersionComparisonStatus.NotComparable,
                "At least two version observations are required.",
                observations);
        }

        var schemes = comparable.Select(observation => observation.Scheme).Distinct().ToList();
        if (schemes.Count != 1 || schemes[0] is not (VersionScheme.Semver or VersionScheme.NumericDotted))
        {
            return new VersionComparison(
                VersionComparisonStatus.NotComparable,
                "The observations do not use one supported version scheme.",
                observations);
        }

        var roles = comparable.Select(observation => observation.Role).Distinct().ToList();
        if (roles.Count != 1 || roles[0] != VersionObservationRole.Release)
        {
            return new VersionComparison(
                VersionComparisonStatus.NotComparable,
                "The observations do not use one comparable version role.",
                observations);
        }

        var first = comparable[0].NormalizedValue;
        var equal = comparable.All(observation => string.Equals(observation.NormalizedValue, first, StringComparison.OrdinalIgnoreCase));
        return new VersionComparison(
            equal ? VersionComparisonStatus.Equal : VersionComparisonStatus.Mismatch,
            equal
                ? "All supported observations have the same normalized value."
                : "Supported observations have different normalized values.",
            observations);
    }

    private static int CompareNumericDotted(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var count = Math.Max(leftParts.Length, rightParts.Length);
        for (var index = 0; index < count; index++)
        {
            var leftPart = index < leftParts.Length ? leftParts[index] : "0";
            var rightPart = index < rightParts.Length ? rightParts[index] : "0";
            if (!long.TryParse(leftPart, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber)
                || !long.TryParse(rightPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber))
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }

            var comparison = leftNumber.CompareTo(rightNumber);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static int CompareSemver(string left, string right)
    {
        var leftParts = left.Split('-', 2);
        var rightParts = right.Split('-', 2);
        var coreComparison = CompareNumericDotted(leftParts[0], rightParts[0]);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        var leftPrerelease = leftParts.Length == 2 ? leftParts[1] : null;
        var rightPrerelease = rightParts.Length == 2 ? rightParts[1] : null;
        if (leftPrerelease is null && rightPrerelease is null)
        {
            return 0;
        }

        if (leftPrerelease is null)
        {
            return 1;
        }

        if (rightPrerelease is null)
        {
            return -1;
        }

        var leftIdentifiers = leftPrerelease.Split('.');
        var rightIdentifiers = rightPrerelease.Split('.');
        var count = Math.Max(leftIdentifiers.Length, rightIdentifiers.Length);
        for (var index = 0; index < count; index++)
        {
            if (index >= leftIdentifiers.Length)
            {
                return -1;
            }

            if (index >= rightIdentifiers.Length)
            {
                return 1;
            }

            var leftIdentifier = leftIdentifiers[index];
            var rightIdentifier = rightIdentifiers[index];
            var leftIsNumeric = long.TryParse(leftIdentifier, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumeric = long.TryParse(rightIdentifier, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            if (leftIsNumeric && rightIsNumeric)
            {
                var numericComparison = leftNumber.CompareTo(rightNumber);
                if (numericComparison != 0)
                {
                    return numericComparison;
                }

                continue;
            }

            if (leftIsNumeric != rightIsNumeric)
            {
                return leftIsNumeric ? -1 : 1;
            }

            var identifierComparison = string.Compare(leftIdentifier, rightIdentifier, StringComparison.Ordinal);
            if (identifierComparison != 0)
            {
                return identifierComparison;
            }
        }

        return 0;
    }
}

public static class VersionNormalizer
{
    private static readonly Regex SemverPattern = new(
        "^v?(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<patch>\\d+)(?<suffix>[-+][0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NumericDottedPattern = new(
        "^\\d+(?:\\.\\d+){3,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? Normalize(string? rawValue, out VersionScheme scheme)
    {
        scheme = VersionScheme.Unknown;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = rawValue.Trim();
        var semver = SemverPattern.Match(value);
        if (semver.Success)
        {
            scheme = VersionScheme.Semver;
            var suffix = semver.Groups["suffix"].Value;
            var plus = suffix.IndexOf('+');
            if (plus >= 0)
            {
                suffix = suffix[..plus];
            }

            return string.Join(
                ".",
                ParseNumber(semver.Groups["major"].Value),
                ParseNumber(semver.Groups["minor"].Value),
                ParseNumber(semver.Groups["patch"].Value))
                + suffix.ToLowerInvariant();
        }

        if (NumericDottedPattern.IsMatch(value))
        {
            scheme = VersionScheme.NumericDotted;
            return string.Join(
                ".",
                value.Split('.').Select(ParseNumber));
        }

        return value;
    }

    private static string ParseNumber(string value)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : value;
    }
}
