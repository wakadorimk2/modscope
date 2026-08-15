using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class VersionEvidenceTests
{
    [Fact]
    public void ReadsKnownUnknownMissingMalformedAndEncodingErrorMetaIni()
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllText(
            Path.Combine(directory.Path, "meta.ini"),
            "[General]\nmodid=123\nfileid=456\nversion=v1.02.003\ncustom=kept\n");

        var parsed = Mo2MetaIniReader.Read(directory.Path, "synthetic-package");

        Assert.Equal(PackageMetadataParseStatus.Parsed, parsed.ParseStatus);
        Assert.Equal("123", parsed.ModId);
        Assert.Equal("456", parsed.FileId);
        Assert.Equal("v1.02.003", parsed.Version);
        Assert.Contains("general.custom", parsed.UnknownValues.Keys);
        Assert.DoesNotContain("general.modid", parsed.UnknownValues.Keys);

        using var missingDirectory = TemporaryDirectory.Create();
        var missing = Mo2MetaIniReader.Read(missingDirectory.Path, "missing-package");
        Assert.Equal(PackageMetadataParseStatus.Missing, missing.ParseStatus);
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Code == "package.meta.missing");

        File.WriteAllText(
            Path.Combine(missingDirectory.Path, "meta.ini"),
            "[General]\nmodid=123\nbroken-line\n");
        var malformed = Mo2MetaIniReader.Read(missingDirectory.Path, "malformed-package");
        Assert.Equal(PackageMetadataParseStatus.Malformed, malformed.ParseStatus);

        File.WriteAllBytes(
            Path.Combine(missingDirectory.Path, "meta.ini"),
            new byte[] { 0xFF, 0xFE, 0x00, 0xD8 });
        var encodingError = Mo2MetaIniReader.Read(missingDirectory.Path, "encoding-package");
        Assert.Equal(PackageMetadataParseStatus.EncodingError, encodingError.ParseStatus);
    }

    [Fact]
    public void ReadsVersionEvidenceManifestAsASeparateProductionInput()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "release-evidence.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "observedAtUtc": "2026-08-15T00:00:00Z",
              "artifacts": [
                {
                  "artifactId": "artifact-1",
                  "kind": "nexus-file",
                  "modId": "123",
                  "fileId": "456",
                  "sourceUrl": "https://example.test/file/456"
                }
              ],
              "packageBindings": [
                { "packageDirectory": "mods/synthetic-package", "artifactIds": ["artifact-1"] }
              ],
              "versionObservations": [
                { "artifactId": "artifact-1", "rawValue": "1.2.3" }
              ]
            }
            """);

        var result = VersionEvidenceManifestReader.Read(path);

        Assert.True(result.IsLoaded);
        Assert.Equal("release-evidence.json", result.DisplayName);
        Assert.Single(result.Document!.SourceArtifacts);
        Assert.Equal(
            new[] { "artifact-1" },
            result.Document.PackageArtifactBindings["synthetic-package"]);
        Assert.Equal("1.2.3", Assert.Single(result.Document.VersionObservations).RawValue);
        Assert.DoesNotContain(Path.GetFullPath(path), result.Document.Diagnostics.SelectMany(diagnostic => new[]
        {
            diagnostic.Message,
            diagnostic.Source?.RelativePath ?? string.Empty
        }));

        var unsupportedPath = Path.Combine(directory.Path, "unsupported-evidence.json");
        File.WriteAllText(unsupportedPath, "{ \"schemaVersion\": 2 }");
        var unsupported = VersionEvidenceManifestReader.Read(unsupportedPath);
        Assert.False(unsupported.IsLoaded);
        Assert.Contains(
            unsupported.Diagnostics,
            diagnostic => diagnostic.Code == "evidence.manifest.schema.unsupported");
    }

    [Fact]
    public void ComparesOnlySupportedSchemesAndSeparatesAssessmentStates()
    {
        var semverEqual = VersionComparator.Compare(
            IdentityResolutionState.Exact,
            new[]
            {
                Observation("modlet", "v1.02.003", VersionObservationSourceKind.ModInfoXml),
                Observation("package", "1.2.3", VersionObservationSourceKind.Mo2MetaIni)
            });
        Assert.Equal(VersionComparisonStatus.Equal, semverEqual.Status);

        var numericMismatch = VersionComparator.Compare(
            IdentityResolutionState.Exact,
            new[]
            {
                Observation("modlet", "1.2.3.4", VersionObservationSourceKind.ModInfoXml),
                Observation("artifact", "1.2.3.5", VersionObservationSourceKind.EvidenceManifest)
            });
        Assert.Equal(VersionComparisonStatus.Mismatch, numericMismatch.Status);

        var schemeMismatch = VersionComparator.Compare(
            IdentityResolutionState.Exact,
            new[]
            {
                Observation("modlet", "1.2.3", VersionObservationSourceKind.ModInfoXml),
                Observation("artifact", "1.2.3.4", VersionObservationSourceKind.EvidenceManifest)
            });
        Assert.Equal(VersionComparisonStatus.NotComparable, schemeMismatch.Status);

        var unresolvedIdentity = VersionComparator.Compare(
            IdentityResolutionState.Ambiguous,
            new[]
            {
                Observation("modlet", "1.2.3", VersionObservationSourceKind.ModInfoXml),
                Observation("artifact", "1.2.3", VersionObservationSourceKind.EvidenceManifest)
            });
        Assert.Equal(VersionComparisonStatus.NotAssessed, unresolvedIdentity.Status);

        var missingVersion = VersionComparator.Compare(
            IdentityResolutionState.Exact,
            new[] { Observation("modlet", null, VersionObservationSourceKind.ModInfoXml) });
        Assert.Equal(VersionComparisonStatus.NotComparable, missingVersion.Status);

        var roleMismatch = VersionComparator.Compare(
            IdentityResolutionState.Exact,
            new[]
            {
                Observation("modlet", "1.2.3", VersionObservationSourceKind.ModInfoXml),
                Observation("artifact", "1.2.3", VersionObservationSourceKind.EvidenceManifest, VersionObservationRole.Unknown)
            });
        Assert.Equal(VersionComparisonStatus.NotComparable, roleMismatch.Status);
    }

    [Fact]
    public void ComparesInstalledAndLatestValuesInReleaseOrder()
    {
        Assert.True(VersionComparator.TryCompareNormalized(
            "3.1.9.1528",
            "3.1.25.1615",
            VersionScheme.NumericDotted,
            out var comparison));

        Assert.True(comparison < 0);
    }

    [Fact]
    public void RetainsUnsupportedManifestRoleAsDiagnosticAndDoesNotCompareIt()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "role-evidence.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "artifacts": [{ "artifactId": "artifact-1", "modId": "123", "fileId": "456" }],
              "packageBindings": [{ "packageDirectory": "synthetic-package", "artifactIds": ["artifact-1"] }],
              "versionObservations": [{ "artifactId": "artifact-1", "rawValue": "1.2.3", "role": "latest" }]
            }
            """);

        var result = VersionEvidenceManifestReader.Read(path);

        var observation = Assert.Single(result.Document!.VersionObservations);
        Assert.Equal(VersionObservationRole.Unknown, observation.Role);
        Assert.Contains(
            observation.Diagnostics,
            diagnostic => diagnostic.Code == "evidence.manifest.observation.role.unsupported");
        Assert.Equal(
            VersionComparisonStatus.NotComparable,
            VersionComparator.Compare(
                    IdentityResolutionState.Exact,
                    new[]
                    {
                        Observation("modlet", "1.2.3", VersionObservationSourceKind.ModInfoXml),
                        observation
                    })
                .Status);
    }

    [Fact]
    public void KeepsPackageRelationAtPackageScopeForSingleAndMultipleModlets()
    {
        var source = new SourceReference(SourceReferenceKind.ModDirectory, "mods/synthetic-package");
        var metadata = new Mo2PackageMetadata(
            "mods/synthetic-package/meta.ini",
            PackageMetadataParseStatus.Parsed,
            "123",
            "456",
            "1.2.3",
            null,
            null,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            Array.Empty<Diagnostic>(),
            new SourceReference(SourceReferenceKind.PackageFile, "mods/synthetic-package/meta.ini"));
        var records = new[]
        {
            Record("synthetic-package/modlet-01", "modlet-01", metadata, source),
            Record("synthetic-package/modlet-02", "modlet-02", metadata, source)
        };
        var artifact = new SourceArtifact(
            "artifact-1",
            "nexus-file",
            "Synthetic",
            "123",
            "456",
            null,
            new SourceReference(SourceReferenceKind.EvidenceManifest, "evidence-manifest/release.json/artifact/artifact-1"));
        var manifest = new VersionEvidenceManifestDocument(
            1,
            DateTimeOffset.UtcNow,
            new[] { artifact },
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["synthetic-package"] = new[] { "artifact-1" }
            },
            new[]
            {
                Observation("artifact-1", "1.2.3", VersionObservationSourceKind.EvidenceManifest)
            },
            Array.Empty<Diagnostic>());

        var attached = VersionEvidenceAssembler.Attach(records, manifest);

        Assert.All(attached, record =>
        {
            Assert.NotNull(record.PackageEvidence);
            Assert.Equal(2, record.PackageEvidence!.Package.ModletCount);
            Assert.True(record.PackageEvidence.Package.ModletCount > 1);
            Assert.Equal(IdentityResolutionState.Exact, record.PackageEvidence.IdentityState);
            Assert.Single(record.PackageEvidence.SourceArtifacts);
            Assert.Equal(VersionComparisonStatus.Equal, record.PackageEvidence.Comparison.Status);
        });
    }

    [Fact]
    public void DistinguishesExactAmbiguousMissingAndConflictingIdentityStates()
    {
        var exact = AttachPackage(
            Metadata("123", "456"),
            new[] { Artifact("artifact-1", "123", "456") },
            new[] { "artifact-1" });
        Assert.Equal(IdentityResolutionState.Exact, exact.IdentityState);

        var ambiguous = AttachPackage(
            Metadata("123", "456"),
            new[]
            {
                Artifact("artifact-1", "123", "456"),
                Artifact("artifact-2", "123", "457")
            },
            new[] { "artifact-1", "artifact-2" });
        Assert.Equal(IdentityResolutionState.Ambiguous, ambiguous.IdentityState);

        var missing = AttachPackage(
            Metadata(null, null),
            Array.Empty<SourceArtifact>(),
            Array.Empty<string>());
        Assert.Equal(IdentityResolutionState.Missing, missing.IdentityState);

        var conflicting = AttachPackage(
            Metadata("123", "456"),
            new[] { Artifact("artifact-1", "999", "456") },
            new[] { "artifact-1" });
        Assert.Equal(IdentityResolutionState.Conflicting, conflicting.IdentityState);
    }

    private static PackageVersionEvidence AttachPackage(
        Mo2PackageMetadata metadata,
        IReadOnlyList<SourceArtifact> artifacts,
        IReadOnlyList<string> artifactIds)
    {
        var source = new SourceReference(SourceReferenceKind.ModDirectory, "mods/synthetic-package");
        var record = Record("synthetic-package/modlet-01", "modlet-01", metadata, source);
        var manifest = new VersionEvidenceManifestDocument(
            1,
            DateTimeOffset.UtcNow,
            artifacts,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["synthetic-package"] = artifactIds
            },
            artifacts.Select(artifact => Observation(
                    artifact.ArtifactId,
                    "1.2.3",
                    VersionObservationSourceKind.EvidenceManifest))
                .ToArray(),
            Array.Empty<Diagnostic>());

        return VersionEvidenceAssembler.Attach(new[] { record }, manifest).Single().PackageEvidence!;
    }

    private static Mo2PackageMetadata Metadata(string? modId, string? fileId)
    {
        return new Mo2PackageMetadata(
            "mods/synthetic-package/meta.ini",
            PackageMetadataParseStatus.Parsed,
            modId,
            fileId,
            "1.2.3",
            null,
            null,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            Array.Empty<Diagnostic>(),
            new SourceReference(SourceReferenceKind.PackageFile, "mods/synthetic-package/meta.ini"));
    }

    private static SourceArtifact Artifact(string artifactId, string modId, string fileId)
    {
        return new SourceArtifact(
            artifactId,
            "nexus-file",
            "Synthetic",
            modId,
            fileId,
            null,
            new SourceReference(
                SourceReferenceKind.EvidenceManifest,
                $"evidence-manifest/release.json/artifact/{artifactId}"));
    }

    private static LocalModRecord Record(
        string modKey,
        string directoryName,
        Mo2PackageMetadata metadata,
        SourceReference source)
    {
        return new LocalModRecord(
            directoryName,
            modKey,
            ModProfileState.Listed,
            ModEnabledState.Enabled,
            0,
            modKey,
            null,
            Array.Empty<ModFileRecord>(),
            Array.Empty<XmlFileReference>(),
            Array.Empty<Diagnostic>(),
            source)
        {
            Mo2OuterDirectoryName = "synthetic-package",
            Mo2OuterSource = source,
            PackageMetadata = metadata
        };
    }

    private static VersionObservation Observation(
        string ownerKey,
        string? rawValue,
        VersionObservationSourceKind sourceKind,
        VersionObservationRole role = VersionObservationRole.Release)
    {
        var source = new SourceReference(
            sourceKind == VersionObservationSourceKind.EvidenceManifest
                ? SourceReferenceKind.EvidenceManifest
                : SourceReferenceKind.ModFile,
            $"evidence/{ownerKey}");
        var normalized = VersionNormalizer.Normalize(rawValue, out var scheme);
        return new VersionObservation(
            ownerKey,
            role,
            sourceKind,
            rawValue,
            normalized,
            scheme,
            source,
            DateTimeOffset.UtcNow,
            Array.Empty<Diagnostic>());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(Directory.CreateTempSubdirectory("modscope-evidence-").FullName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
