using System.Text;
using System.Text.RegularExpressions;

namespace ModScope.LocalKnowledge;

public sealed record RuntimeOcdImportRequest(
    string SnapshotId,
    string RuntimeOcdLogsPath,
    string? ToolVersion,
    string? GameVersion,
    DateTimeOffset CapturedAtUtc);

public sealed class RuntimeOcdAdapter
{
    private const string ToolName = "RuntimeOCD";
    private const string SupportedToolVersion = "0.15.2";

    private static readonly Regex CategoryPattern = new(
        @"ConflictDetector_\((?<category>[^)]+)\)_",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SourcePattern = new(
        @"^\s*Source\s+<(?<operation>[A-Za-z][A-Za-z0-9]*)\s+xpath=(?<quote>[""'])(?<xpath>.*?)\k<quote>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AddedModPattern = new(
        @"^(?<mod>.+?)\s+added\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ChangedByModPattern = new(
        @"\b(?:REMOVED|MODIFIED|OVERRIDDEN)\s+by\s+(?<mod>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> KnownOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "append",
        "insertAfter",
        "insertBefore",
        "prepend",
        "remove",
        "removeattribute",
        "set",
        "setattribute"
    };

    public RuntimeEvidenceDocument Import(
        RuntimeOcdImportRequest request,
        SemanticConflictAnalysis staticAnalysis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(staticAnalysis);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RuntimeOcdLogsPath);

        if (!string.Equals(request.SnapshotId, staticAnalysis.SnapshotId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "RuntimeOCD evidence must reference the same snapshot as the static analysis.",
                nameof(request));
        }

        var diagnostics = new List<Diagnostic>();
        AddVersionDiagnostics(request, diagnostics);

        var observations = new List<RuntimeEvidenceObservation>();
        string logsPath;
        try
        {
            logsPath = Path.GetFullPath(request.RuntimeOcdLogsPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.logs.path.invalid",
                DiagnosticSeverity.Error,
                "The RuntimeOCD logs path is invalid."));
            return CreateDocument(request, staticAnalysis, observations, diagnostics);
        }

        if (!Directory.Exists(logsPath))
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.logs.missing",
                DiagnosticSeverity.Warning,
                "The RuntimeOCD logs directory does not exist."));
            return CreateDocument(request, staticAnalysis, observations, diagnostics);
        }

        IReadOnlyList<string> files;
        try
        {
            files = Directory.EnumerateFiles(logsPath, "*.txt", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.logs.enumeration.failed",
                DiagnosticSeverity.Error,
                "The RuntimeOCD logs directory could not be enumerated."));
            return CreateDocument(request, staticAnalysis, observations, diagnostics);
        }

        if (files.Count == 0)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.logs.empty",
                DiagnosticSeverity.Info,
                "The RuntimeOCD logs directory contains no text log files."));
        }

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = NormalizeRelativePath(logsPath, filePath);
            var category = ReadCategory(relativePath, diagnostics, relativePath);

            try
            {
                var lines = File.ReadAllLines(filePath);
                ParseFile(
                    lines,
                    relativePath,
                    category,
                    staticAnalysis,
                    observations,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                diagnostics.Add(new Diagnostic(
                    "runtime.ocd.log.read.failed",
                    DiagnosticSeverity.Warning,
                    "A RuntimeOCD log file could not be read.",
                    new SourceReference(SourceReferenceKind.RuntimeLog, relativePath),
                    exception.GetType().Name));
            }
        }

        return CreateDocument(request, staticAnalysis, observations, diagnostics);
    }

    private static RuntimeEvidenceDocument CreateDocument(
        RuntimeOcdImportRequest request,
        SemanticConflictAnalysis staticAnalysis,
        IReadOnlyList<RuntimeEvidenceObservation> observations,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        return new RuntimeEvidenceDocument(
            new RuntimeEvidenceBinding(
                staticAnalysis.SnapshotId,
                staticAnalysis.InstanceName,
                staticAnalysis.ProfileName),
            ToolName,
            request.ToolVersion,
            request.GameVersion,
            request.CapturedAtUtc,
            observations.ToList().AsReadOnly(),
            diagnostics.ToList().AsReadOnly());
    }

    private static void AddVersionDiagnostics(
        RuntimeOcdImportRequest request,
        ICollection<Diagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(request.ToolVersion))
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.tool-version.missing",
                DiagnosticSeverity.Warning,
                "RuntimeOCD tool version was not provided."));
        }
        else if (!string.Equals(request.ToolVersion.Trim(), SupportedToolVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.tool-version.unsupported",
                DiagnosticSeverity.Warning,
                $"RuntimeOCD tool version '{request.ToolVersion.Trim()}' is not supported. Expected {SupportedToolVersion}.",
                RawValue: request.ToolVersion.Trim()));
        }

        if (string.IsNullOrWhiteSpace(request.GameVersion))
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.game-version.missing",
                DiagnosticSeverity.Info,
                "The game version was not provided explicitly."));
        }

        if (request.CapturedAtUtc == default)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.ocd.capture-time.missing",
                DiagnosticSeverity.Warning,
                "The RuntimeOCD capture time was not provided."));
        }
    }

    private static void ParseFile(
        IReadOnlyList<string> lines,
        string relativePath,
        string? category,
        SemanticConflictAnalysis staticAnalysis,
        ICollection<RuntimeEvidenceObservation> observations,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var source = new SourceReference(SourceReferenceKind.RuntimeLog, relativePath, index + 1);
            if (IsSourceLine(line))
            {
                var orphan = CreateObservation(
                    null,
                    line,
                    line,
                    source,
                    category,
                    staticAnalysis,
                    "runtime.ocd.record.orphan-source",
                    "The RuntimeOCD Source line has no preceding description line.");
                observations.Add(orphan);
                continue;
            }

            if (index + 1 < lines.Count && IsSourceLine(lines[index + 1]))
            {
                var paired = CreateObservation(
                    ExtractModKey(line),
                    line,
                    lines[index + 1],
                    source,
                    category,
                    staticAnalysis);
                observations.Add(paired);
                index++;
                continue;
            }

            observations.Add(CreateObservation(
                ExtractModKey(line),
                line,
                null,
                source,
                category,
                staticAnalysis,
                "runtime.ocd.record.malformed",
                "The RuntimeOCD description line is not followed by an immediate Source line."));
        }
    }

    private static RuntimeEvidenceObservation CreateObservation(
        string? modKey,
        string description,
        string? sourceLine,
        SourceReference source,
        string? category,
        SemanticConflictAnalysis staticAnalysis,
        string? diagnosticCode = null,
        string? diagnosticMessage = null)
    {
        var diagnostics = new List<Diagnostic>();
        if (diagnosticCode is not null && diagnosticMessage is not null)
        {
            diagnostics.Add(new Diagnostic(
                diagnosticCode,
                DiagnosticSeverity.Warning,
                diagnosticMessage,
                source));
        }

        string? operation = null;
        string? xpath = null;
        var rawResult = sourceLine is null
            ? description
            : string.Join(Environment.NewLine, description, sourceLine);

        if (sourceLine is not null)
        {
            var sourceMatch = SourcePattern.Match(sourceLine);
            if (!sourceMatch.Success)
            {
                diagnostics.Add(new Diagnostic(
                    "runtime.ocd.source.malformed",
                    DiagnosticSeverity.Warning,
                    "The RuntimeOCD Source line does not match the supported record shape.",
                    source));
            }
            else
            {
                operation = sourceMatch.Groups["operation"].Value.Trim();
                xpath = sourceMatch.Groups["xpath"].Value.Trim();
                if (string.IsNullOrWhiteSpace(xpath))
                {
                    xpath = null;
                    diagnostics.Add(new Diagnostic(
                        "runtime.ocd.xpath.missing",
                        DiagnosticSeverity.Warning,
                        "The RuntimeOCD Source line does not contain an XPath.",
                        source));
                }

                if (!KnownOperations.Contains(operation))
                {
                    diagnostics.Add(new Diagnostic(
                        "runtime.ocd.operation.unknown",
                        DiagnosticSeverity.Info,
                        "The RuntimeOCD Source line contains an operation that is not in the known operation set.",
                        source,
                        operation));
                }
            }
        }

        var observation = new RuntimeEvidenceObservation(
            modKey,
            null,
            xpath,
            operation,
            rawResult,
            null,
            source,
            diagnostics.AsReadOnly(),
            category);

        return ResolveTargetXml(observation, staticAnalysis);
    }

    private static RuntimeEvidenceObservation ResolveTargetXml(
        RuntimeEvidenceObservation observation,
        SemanticConflictAnalysis staticAnalysis)
    {
        if (observation.TargetXml is not null || observation.XPath is null)
        {
            return observation;
        }

        var candidates = staticAnalysis.Groups
            .Where(group => string.Equals(
                NormalizeXPath(group.XPath),
                NormalizeXPath(observation.XPath),
                StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(group.TargetXml))
            .Select(group => NormalizeTargetXml(group.TargetXml)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 1)
        {
            return observation with
            {
                TargetXml = candidates[0],
                Diagnostics = observation.Diagnostics
                    .Concat(new[]
                    {
                        new Diagnostic(
                            "runtime.targetxml.inferred",
                            DiagnosticSeverity.Info,
                            "Target XML was inferred from one static candidate for the normalized XPath.",
                            observation.RawLogReference)
                    })
                    .ToList()
                    .AsReadOnly()
            };
        }

        var diagnostic = candidates.Count == 0
            ? new Diagnostic(
                "runtime.targetxml.unresolved",
                DiagnosticSeverity.Info,
                "Target XML could not be inferred because no static candidate matched the XPath.",
                observation.RawLogReference)
            : new Diagnostic(
                "runtime.targetxml.ambiguous",
                DiagnosticSeverity.Warning,
                "Target XML could not be inferred because multiple static candidates matched the XPath.",
                observation.RawLogReference);

        return observation with
        {
            Diagnostics = observation.Diagnostics
                .Concat(new[] { diagnostic })
                .ToList()
                .AsReadOnly()
        };
    }

    private static string? ReadCategory(
        string relativePath,
        ICollection<Diagnostic> diagnostics,
        string diagnosticPath)
    {
        var match = CategoryPattern.Match(relativePath);
        if (match.Success)
        {
            return match.Groups["category"].Value.Trim();
        }

        diagnostics.Add(new Diagnostic(
            "runtime.ocd.category.unknown",
            DiagnosticSeverity.Info,
            "The RuntimeOCD category could not be read from the log path.",
            new SourceReference(SourceReferenceKind.RuntimeLog, diagnosticPath)));
        return null;
    }

    private static string? ExtractModKey(string description)
    {
        var addedMatch = AddedModPattern.Match(description);
        if (addedMatch.Success)
        {
            return TrimModKey(addedMatch.Groups["mod"].Value);
        }

        var changedMatch = ChangedByModPattern.Match(description);
        return changedMatch.Success
            ? TrimModKey(changedMatch.Groups["mod"].Value)
            : null;
    }

    private static string? TrimModKey(string value)
    {
        var trimmed = value.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsSourceLine(string line)
    {
        return line.TrimStart().StartsWith("Source ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string rootPath, string filePath)
    {
        return Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
    }

    private static string? NormalizeTargetXml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.StartsWith("Data/Config/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Data/Config/".Length..];
        }
        else if (normalized.StartsWith("Config/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Config/".Length..];
        }

        return normalized.TrimStart('/');
    }

    private static string? NormalizeXPath(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
