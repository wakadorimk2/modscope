namespace ModScope.LocalKnowledge;

public sealed record RuntimeEvidenceBinding(
    string SnapshotId,
    string InstanceName,
    string ProfileName);

public sealed record RuntimeEvidenceDocument(
    RuntimeEvidenceBinding Binding,
    string ToolName,
    string? ToolVersion,
    string? GameVersion,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<RuntimeEvidenceObservation> Observations,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public string SnapshotId => Binding.SnapshotId;

    public string InstanceName => Binding.InstanceName;

    public string ProfileName => Binding.ProfileName;

    public string EvidenceSource => ToolName;

    public DateTimeOffset CaptureTimeUtc => CapturedAtUtc;

    public IReadOnlyList<RuntimeEvidenceObservation> Results => Observations;
}

public sealed record RuntimeEvidenceObservation(
    string? ModKey,
    string? TargetXml,
    string? XPath,
    string? ObservedOperation,
    string RawResult,
    SemanticConflictAssessment? NormalizedAssessment,
    SourceReference RawLogReference,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? ObservedCategory = null)
{
    public string? ModIdentity => ModKey;

    public string ObservedResult => RawResult;

    public IReadOnlyList<Diagnostic> ImportDiagnostics => Diagnostics;

    public RuntimeEvidenceObservation(
        string? modKey,
        string? targetXml,
        string? xpath,
        string? observedOperation,
        string rawResult,
        SemanticConflictAssessment? normalizedAssessment,
        string rawLogRelativePath,
        IReadOnlyList<Diagnostic>? diagnostics = null,
        string? observedCategory = null)
        : this(
            modKey,
            targetXml,
            xpath,
            observedOperation,
            rawResult,
            normalizedAssessment,
            new SourceReference(SourceReferenceKind.RuntimeLog, rawLogRelativePath),
            diagnostics ?? Array.Empty<Diagnostic>(),
            observedCategory)
    {
    }
}

public enum RuntimeEvidenceComparisonStatus
{
    Match,
    Different,
    InferredMatch,
    InferredDifferent,
    RuntimeOnly,
    StaticOnly,
    Unknown
}

public sealed record RuntimeEvidenceComparisonItem(
    string? TargetXml,
    string? XPath,
    RuntimeEvidenceComparisonStatus Status,
    SemanticConflictAssessment? StaticAssessment,
    SemanticConflictAssessment? RuntimeAssessment,
    IReadOnlyList<RuntimeEvidenceObservation> RuntimeObservations,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public IReadOnlyList<RuntimeEvidenceObservation> Observations => RuntimeObservations;
}

public sealed record RuntimeEvidenceComparison(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    RuntimeEvidenceDocument RuntimeEvidence,
    IReadOnlyList<RuntimeEvidenceComparisonItem> Items,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public IReadOnlyList<RuntimeEvidenceComparisonItem> Results => Items;

    public static RuntimeEvidenceComparison Compare(
        SemanticConflictAnalysis staticAnalysis,
        RuntimeEvidenceDocument runtimeEvidence)
    {
        ArgumentNullException.ThrowIfNull(staticAnalysis);
        ArgumentNullException.ThrowIfNull(runtimeEvidence);

        if (!string.Equals(
                staticAnalysis.SnapshotId,
                runtimeEvidence.Binding.SnapshotId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Runtime evidence must reference the same snapshot as the static analysis.",
                nameof(runtimeEvidence));
        }

        var staticGroups = staticAnalysis.Groups
            .GroupBy(group => new RuntimeEvidenceComparisonKey(
                NormalizeTargetXml(group.TargetXml),
                NormalizeXPath(group.XPath)))
            .ToDictionary(group => group.Key, group => group.ToList());
        var runtimeGroups = runtimeEvidence.Observations
            .GroupBy(observation => new RuntimeEvidenceComparisonKey(
                NormalizeTargetXml(observation.TargetXml),
                NormalizeXPath(observation.XPath)))
            .ToDictionary(group => group.Key, group => group.ToList());

        var keys = staticGroups.Keys
            .Concat(runtimeGroups.Keys)
            .Distinct()
            .OrderBy(key => key.TargetXml ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(key => key.XPath ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var comparisonDiagnostics = runtimeEvidence.Diagnostics
            .Concat(staticAnalysis.Diagnostics)
            .ToList();
        var blocksComparison = runtimeEvidence.Diagnostics.Any(IsBlockingComparisonDiagnostic);
        var items = new List<RuntimeEvidenceComparisonItem>(keys.Count);

        foreach (var key in keys)
        {
            staticGroups.TryGetValue(key, out var groups);
            runtimeGroups.TryGetValue(key, out var observations);
            groups ??= new List<SemanticConflictGroup>();
            observations ??= new List<RuntimeEvidenceObservation>();

            var itemDiagnostics = groups
                .SelectMany(group => group.Diagnostics)
                .Concat(observations.SelectMany(observation => observation.Diagnostics))
                .ToList();

            var staticAssessment = ConsolidateStaticAssessment(groups, itemDiagnostics);
            var runtimeAssessment = ConsolidateRuntimeAssessment(observations, itemDiagnostics);
            var status = DetermineStatus(
                key,
                groups.Count > 0,
                observations.Count > 0,
                staticAssessment,
                runtimeAssessment,
                blocksComparison,
                observations,
                itemDiagnostics);

            items.Add(new RuntimeEvidenceComparisonItem(
                key.TargetXml,
                key.XPath,
                status,
                staticAssessment,
                runtimeAssessment,
                observations.AsReadOnly(),
                itemDiagnostics.AsReadOnly()));
        }

        return new RuntimeEvidenceComparison(
            staticAnalysis.SnapshotId,
            staticAnalysis.InstanceName,
            staticAnalysis.ProfileName,
            runtimeEvidence,
            items.AsReadOnly(),
            comparisonDiagnostics.AsReadOnly());
    }

    private static SemanticConflictAssessment? ConsolidateStaticAssessment(
        IReadOnlyList<SemanticConflictGroup> groups,
        ICollection<Diagnostic> diagnostics)
    {
        if (groups.Count == 0)
        {
            return null;
        }

        var assessments = groups
            .Select(group => group.Assessment)
            .Distinct()
            .ToList();
        if (assessments.Count > 1)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.static-duplicate-assessment",
                DiagnosticSeverity.Warning,
                "Static conflict groups with the same target and XPath have different assessments."));
            return null;
        }

        return assessments[0];
    }

    private static SemanticConflictAssessment? ConsolidateRuntimeAssessment(
        IReadOnlyList<RuntimeEvidenceObservation> observations,
        ICollection<Diagnostic> diagnostics)
    {
        if (observations.Count == 0)
        {
            return null;
        }

        var hasMissingAssessment = observations.Any(observation => observation.NormalizedAssessment is null);
        if (hasMissingAssessment)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.assessment.missing",
                DiagnosticSeverity.Info,
                "At least one runtime observation has no normalized assessment."));
        }

        var assessments = observations
            .Where(observation => observation.NormalizedAssessment is not null)
            .Select(observation => observation.NormalizedAssessment!.Value)
            .Distinct()
            .ToList();
        if (assessments.Count > 1)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.duplicate-assessment",
                DiagnosticSeverity.Warning,
                "Runtime observations with the same target and XPath have different assessments."));
            return null;
        }

        if (hasMissingAssessment || assessments.Count == 0)
        {
            return null;
        }

        return assessments[0];
    }

    private static RuntimeEvidenceComparisonStatus DetermineStatus(
        RuntimeEvidenceComparisonKey key,
        bool hasStatic,
        bool hasRuntime,
        SemanticConflictAssessment? staticAssessment,
        SemanticConflictAssessment? runtimeAssessment,
        bool blocksComparison,
        IReadOnlyList<RuntimeEvidenceObservation> observations,
        ICollection<Diagnostic> diagnostics)
    {
        if (key.TargetXml is null || key.XPath is null)
        {
            diagnostics.Add(new Diagnostic(
                "runtime.comparison.key.missing",
                DiagnosticSeverity.Info,
                "A target XML and XPath are required for runtime comparison."));
            return RuntimeEvidenceComparisonStatus.Unknown;
        }

        if (blocksComparison)
        {
            return RuntimeEvidenceComparisonStatus.Unknown;
        }

        var hasInferredTarget = observations.Any(HasInferredTarget);
        if (hasInferredTarget && observations.Any(observation => !HasInferredTarget(observation)))
        {
            diagnostics.Add(new Diagnostic(
                "runtime.targetxml.mixed-resolution",
                DiagnosticSeverity.Warning,
                "Runtime observations with the same target and XPath mix inferred and explicit target XML values."));
            return RuntimeEvidenceComparisonStatus.Unknown;
        }

        if (hasStatic && !hasRuntime)
        {
            return staticAssessment is null
                or SemanticConflictAssessment.Unknown
                ? RuntimeEvidenceComparisonStatus.Unknown
                : RuntimeEvidenceComparisonStatus.StaticOnly;
        }

        if (!hasStatic && hasRuntime)
        {
            return runtimeAssessment is null
                or SemanticConflictAssessment.Unknown
                ? RuntimeEvidenceComparisonStatus.Unknown
                : RuntimeEvidenceComparisonStatus.RuntimeOnly;
        }

        if (!hasStatic || !hasRuntime)
        {
            return RuntimeEvidenceComparisonStatus.Unknown;
        }

        if (staticAssessment is null
            or SemanticConflictAssessment.Unknown
            || runtimeAssessment is null
            or SemanticConflictAssessment.Unknown)
        {
            return RuntimeEvidenceComparisonStatus.Unknown;
        }

        if (hasInferredTarget)
        {
            return staticAssessment == runtimeAssessment
                ? RuntimeEvidenceComparisonStatus.InferredMatch
                : RuntimeEvidenceComparisonStatus.InferredDifferent;
        }

        return staticAssessment == runtimeAssessment
            ? RuntimeEvidenceComparisonStatus.Match
            : RuntimeEvidenceComparisonStatus.Different;
    }

    private static bool HasInferredTarget(RuntimeEvidenceObservation observation)
    {
        return observation.Diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.Code, "runtime.targetxml.inferred", StringComparison.Ordinal));
    }

    private static bool IsBlockingComparisonDiagnostic(Diagnostic diagnostic)
    {
        return diagnostic.Code is
            "runtime.ocd.logs.missing"
            or "runtime.ocd.tool-version.missing"
            or "runtime.ocd.tool-version.unsupported";
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

    private readonly record struct RuntimeEvidenceComparisonKey(
        string? TargetXml,
        string? XPath);
}
