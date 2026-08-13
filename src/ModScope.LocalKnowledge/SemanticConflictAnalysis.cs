using System.Collections;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ModScope.LocalKnowledge;

public sealed record SevenDaysToDieBaseDataSource(string DataConfigPath)
{
    public string DataConfigDirectory => DataConfigPath;
}

public enum SemanticConflictAssessment
{
    Compatible,
    Conflict,
    Possible,
    Unknown
}

public enum EffectiveResultStatus
{
    Computed,
    Unknown,
    NotAssessed
}

public sealed record BaseDataFileObservation(
    string TargetXml,
    long Size,
    string Sha256,
    XmlParseStatus? ParseStatus,
    SourceReference Source,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record SemanticConflictOperation(
    string OperationKey,
    string ModKey,
    int? Priority,
    string XmlFileRelativePath,
    string ElementPath,
    string RawOperationName,
    XmlPatchOperationKind? NormalizedKind,
    string? TargetXml,
    string? XPath,
    string? AttributeName,
    string? Value,
    SourceReference Source,
    IReadOnlyList<EvidenceReference> Evidence,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool HasChildElements { get; init; }
}

public sealed record EffectiveChange(
    string MatchPath,
    string? AttributeName,
    string? BeforeValue,
    string? AfterValue,
    bool ExistedBefore,
    bool ExistsAfter,
    SourceReference Source)
{
    public string? Before => BeforeValue;

    public string? After => AfterValue;
}

public sealed record SemanticConflictGroup(
    string? TargetXml,
    string? XPath,
    SemanticConflictAssessment Assessment,
    EffectiveResultStatus EffectiveStatus,
    IReadOnlyList<SemanticConflictOperation> OperationSequence,
    IReadOnlyList<EffectiveChange> EffectiveChanges,
    IReadOnlyList<EvidenceReference> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public IReadOnlyList<SemanticConflictOperation> Operations => OperationSequence;
}

public sealed record SemanticConflictAnalysis(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    IReadOnlyList<BaseDataFileObservation> BaseFiles,
    IReadOnlyList<SemanticConflictGroup> Groups,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public IReadOnlyList<BaseDataFileObservation> BaseDataFiles => BaseFiles;

    public IReadOnlyList<SemanticConflictGroup> OperationGroups => Groups;
}

public static class SevenDaysToDieConflictAnalyzer
{
    public static SemanticConflictAnalysis Analyze(
        LocalModSnapshot snapshot,
        SevenDaysToDieBaseDataSource baseData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(baseData);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseData.DataConfigPath);

        var dataConfigPath = Path.GetFullPath(baseData.DataConfigPath);
        if (!Directory.Exists(dataConfigPath))
        {
            throw new DirectoryNotFoundException(
                "The explicit 7DTD Data/Config directory does not exist.");
        }

        var analysisDiagnostics = new List<Diagnostic>();
        AddDistinct(analysisDiagnostics, snapshot.Diagnostics);
        var profileEntries = snapshot.ProfileEntries
            .Where(entry => entry.NormalizedModName is not null)
            .GroupBy(entry => entry.NormalizedModName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var analyzedOperations = new List<AnalyzedOperation>();

        foreach (var mod in snapshot.Mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddDistinct(analysisDiagnostics, mod.Diagnostics);

            if (mod.ProfileState != ModProfileState.Listed)
            {
                analysisDiagnostics.Add(new Diagnostic(
                    "conflict.mod.excluded.profile",
                    DiagnosticSeverity.Info,
                    $"The MOD '{mod.ModKey}' is excluded because it is not listed in the active profile.",
                    mod.Source,
                    mod.ModKey));
                continue;
            }

            if (mod.EnabledState != ModEnabledState.Enabled)
            {
                analysisDiagnostics.Add(new Diagnostic(
                    "conflict.mod.excluded.disabled",
                    DiagnosticSeverity.Info,
                    $"The MOD '{mod.ModKey}' is excluded because it is not enabled.",
                    mod.Source,
                    mod.ModKey));
                continue;
            }

            if (mod.Priority is not int)
            {
                analysisDiagnostics.Add(new Diagnostic(
                    "conflict.mod.excluded.priority_unknown",
                    DiagnosticSeverity.Warning,
                    $"The MOD '{mod.ModKey}' is excluded because its priority is unknown.",
                    mod.Source,
                    mod.ModKey));
                continue;
            }

            profileEntries.TryGetValue(mod.ModKey, out var profileEntry);
            foreach (var xmlFile in mod.XmlFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddDistinct(analysisDiagnostics, xmlFile.Diagnostics);

                foreach (var operation in xmlFile.PatchOperations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    analyzedOperations.Add(CreateAnalyzedOperation(
                        mod,
                        xmlFile,
                        operation,
                        profileEntry,
                        analysisDiagnostics));
                }
            }
        }

        var orderedOperations = analyzedOperations
            .OrderBy(item => item.Operation.Priority ?? int.MaxValue)
            .ThenBy(item => item.Operation.ModKey, StringComparer.Ordinal)
            .ThenBy(item => item.Operation.XmlFileRelativePath, StringComparer.Ordinal)
            .ThenBy(item => item.Operation.Source.LineNumber ?? int.MaxValue)
            .ThenBy(item => item.Operation.ElementPath, StringComparer.Ordinal)
            .ToList();

        var baseCache = new Dictionary<string, BaseLoadResult>(StringComparer.Ordinal);
        var groups = new List<SemanticConflictGroup>();
        foreach (var group in orderedOperations.GroupBy(
                     item => new GroupKey(item.Operation.TargetXml, item.Operation.XPath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groupItems = group.ToList();
            var baseResult = group.Key.TargetXml is null
                ? null
                : LoadBaseDataFile(group.Key.TargetXml, dataConfigPath, baseCache);

            if (baseResult is not null)
            {
                AddDistinct(analysisDiagnostics, baseResult.Observation.Diagnostics);
            }

            var groupDiagnostics = new List<Diagnostic>();
            var groupEvidence = new List<EvidenceReference>();
            var groupUncertainties = new List<string>();
            var effectiveChanges = new List<EffectiveChange>();
            var simulations = new List<OperationSimulation>();
            AddDistinct(groupDiagnostics, groupItems.SelectMany(item => item.Operation.Diagnostics));

            if (baseResult is not null)
            {
                AddDistinct(groupDiagnostics, baseResult.Observation.Diagnostics);
                groupEvidence.Add(new EvidenceReference(EvidenceKind.Source, baseResult.Observation.Source));
            }

            foreach (var item in groupItems)
            {
                groupEvidence.AddRange(item.Operation.Evidence);
            }

            var effectiveStatus = EffectiveResultStatus.NotAssessed;
            if (baseResult is not null && group.Key.XPath is not null && baseResult.Document is not null)
            {
                var document = CloneDocument(baseResult.Document);
                effectiveStatus = EffectiveResultStatus.Computed;
                foreach (var item in groupItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var simulation = ApplyOperation(document, item.Operation);
                    simulations.Add(simulation);
                    AddDistinct(groupDiagnostics, simulation.Diagnostics);
                    effectiveChanges.AddRange(simulation.Changes);
                    if (!simulation.IsSupported)
                    {
                        effectiveStatus = EffectiveResultStatus.Unknown;
                    }
                }
            }
            else
            {
                effectiveStatus = EffectiveResultStatus.Unknown;
                if (group.Key.TargetXml is null)
                {
                    groupDiagnostics.Add(new Diagnostic(
                        "conflict.target.unknown",
                        DiagnosticSeverity.Warning,
                        "The target XML file could not be resolved from the MOD evidence.",
                        groupItems[0].Operation.Source));
                }

                if (group.Key.XPath is null)
                {
                    groupDiagnostics.Add(new Diagnostic(
                        "conflict.xpath.unknown",
                        DiagnosticSeverity.Warning,
                        "The XPath expression could not be resolved from the MOD evidence.",
                        groupItems[0].Operation.Source));
                }
            }

            var assessment = Assess(groupItems, simulations, effectiveStatus);
            if (groupItems.Count > 1)
            {
                const string priorityUncertainty =
                    "Priority 0→N represents the active profile sequence. The in-game winner direction is not verified.";
                groupUncertainties.Add(priorityUncertainty);
                var prioritySource = groupItems
                    .SelectMany(item => item.Operation.Evidence)
                    .FirstOrDefault(evidence => evidence.Source.Kind == SourceReferenceKind.ProfileFile)
                    ?.Source;
                if (prioritySource is not null)
                {
                    groupEvidence.Add(new EvidenceReference(EvidenceKind.Inference, prioritySource));
                }
            }

            groups.Add(new SemanticConflictGroup(
                group.Key.TargetXml,
                group.Key.XPath,
                assessment,
                effectiveStatus,
                groupItems.Select(item => item.Operation).ToList().AsReadOnly(),
                effectiveChanges.AsReadOnly(),
                DeduplicateEvidence(groupEvidence),
                groupUncertainties.AsReadOnly(),
                DeduplicateDiagnostics(groupDiagnostics)));
        }

        return new SemanticConflictAnalysis(
            snapshot.SnapshotId,
            snapshot.InstanceName,
            snapshot.ProfileName,
            baseCache.Values
                .Select(result => result.Observation)
                .OrderBy(file => file.TargetXml, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly(),
            groups
                .OrderBy(group => group.TargetXml ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(group => group.XPath ?? string.Empty, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly(),
            DeduplicateDiagnostics(analysisDiagnostics));
    }

    private static AnalyzedOperation CreateAnalyzedOperation(
        LocalModRecord mod,
        XmlFileReference xmlFile,
        XmlPatchOperationObservation operation,
        ProfileModEntry? profileEntry,
        List<Diagnostic> analysisDiagnostics)
    {
        var operationDiagnostics = operation.Diagnostics.ToList();
        var target = ResolveTarget(operation, operationDiagnostics);
        var xpath = ResolveXPath(operation, operationDiagnostics);
        var attributeName = GetAttribute(operation.RawObservation, "name");
        var value = GetOperationValue(operation.RawObservation);
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceKind.Source, operation.Source)
        };

        if (profileEntry is not null)
        {
            evidence.Add(profileEntry.PriorityEvidence);
        }

        var targetEvidence = operation.TargetXmlCandidates
            .FirstOrDefault(candidate => candidate.NormalizedValue is not null
                && string.Equals(
                    NormalizeTarget(candidate.NormalizedValue),
                    target,
                    StringComparison.Ordinal));
        if (targetEvidence?.EvidenceKind == EvidenceKind.Inference)
        {
            evidence.Add(new EvidenceReference(EvidenceKind.Inference, targetEvidence.Source));
        }

        if (target is null)
        {
            operationDiagnostics.Add(new Diagnostic(
                "conflict.target.ambiguous",
                DiagnosticSeverity.Warning,
                "The target XML file is missing or has conflicting values.",
                operation.Source));
        }

        if (xpath is null)
        {
            operationDiagnostics.Add(new Diagnostic(
                "conflict.xpath.ambiguous",
                DiagnosticSeverity.Warning,
                "The XPath expression is missing or has conflicting values.",
                operation.Source));
        }

        var result = new SemanticConflictOperation(
            BuildOperationKey(mod, xmlFile, operation),
            mod.ModKey,
            mod.Priority,
            xmlFile.RelativePath,
            operation.ElementPath,
            operation.RawOperationName,
            operation.NormalizedKind,
            target,
            xpath,
            attributeName,
            value,
            operation.Source,
            DeduplicateEvidence(evidence),
            DeduplicateDiagnostics(operationDiagnostics))
        {
            HasChildElements = operation.RawObservation.HasChildElements
        };

        AddDistinct(analysisDiagnostics, result.Diagnostics);
        return new AnalyzedOperation(result);
    }

    private static string BuildOperationKey(
        LocalModRecord mod,
        XmlFileReference xmlFile,
        XmlPatchOperationObservation operation)
    {
        var directory = mod.ResolvedDirectoryRelativePath ?? mod.DirectoryName;
        var file = ParsingUtilities.BuildSourcePath(directory, xmlFile.RelativePath);
        return $"{ParsingUtilities.BuildSourcePath("mods", file)}#{operation.ElementPath}";
    }

    private static string? ResolveTarget(
        XmlPatchOperationObservation operation,
        List<Diagnostic> diagnostics)
    {
        var candidates = operation.TargetXmlCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.NormalizedValue))
            .Select(candidate => new TargetCandidate(
                NormalizeTarget(candidate.NormalizedValue!),
                candidate.EvidenceKind,
                candidate.Source))
            .Where(candidate => candidate.Value.Length > 0)
            .ToList();
        var normalized = candidates
            .Where(candidate => candidate.EvidenceKind == EvidenceKind.Normalized)
            .Select(candidate => candidate.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalized.Count > 1)
        {
            diagnostics.Add(new Diagnostic(
                "conflict.target.multiple",
                DiagnosticSeverity.Warning,
                "The operation contains multiple normalized target XML values.",
                operation.Source));
            return null;
        }

        if (normalized.Count == 1)
        {
            return normalized[0];
        }

        return candidates
            .Where(candidate => candidate.EvidenceKind == EvidenceKind.Inference)
            .Select(candidate => candidate.Value)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList() switch
        {
            { Count: 1 } inferred => inferred[0],
            { Count: > 1 } => null,
            _ => null
        };
    }

    private static string? ResolveXPath(
        XmlPatchOperationObservation operation,
        List<Diagnostic> diagnostics)
    {
        var values = operation.XPathCandidates
            .Select(candidate => candidate.NormalizedValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (values.Count > 1)
        {
            diagnostics.Add(new Diagnostic(
                "conflict.xpath.multiple",
                DiagnosticSeverity.Warning,
                "The operation contains multiple XPath values.",
                operation.Source));
            return null;
        }

        return values.Count == 1 ? values[0] : null;
    }

    private static string NormalizeTarget(string value)
    {
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

    private static string? GetAttribute(RawXmlObservation observation, string name)
    {
        return observation.Attributes
            .FirstOrDefault(attribute => attribute.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string? GetOperationValue(RawXmlObservation observation)
    {
        return observation.InnerText
            ?? GetAttribute(observation, "value");
    }

    private static BaseLoadResult LoadBaseDataFile(
        string targetXml,
        string dataConfigPath,
        Dictionary<string, BaseLoadResult> cache)
    {
        if (cache.TryGetValue(targetXml, out var cached))
        {
            return cached;
        }

        var relativeTarget = NormalizeTarget(targetXml);
        var relativePath = $"Data/Config/{relativeTarget}";
        var source = new SourceReference(SourceReferenceKind.GameDataFile, relativePath);
        var diagnostics = new List<Diagnostic>();
        var candidatePath = Path.GetFullPath(Path.Combine(
            dataConfigPath,
            relativeTarget.Replace('/', Path.DirectorySeparatorChar)));

        if (!ParsingUtilities.IsWithin(dataConfigPath, candidatePath))
        {
            diagnostics.Add(new Diagnostic(
                "conflict.base.path.invalid",
                DiagnosticSeverity.Error,
                "The target XML path is outside the explicit Data/Config directory.",
                source,
                targetXml));
            var invalid = new BaseLoadResult(
                new BaseDataFileObservation(targetXml, 0, string.Empty, null, source, diagnostics.AsReadOnly()),
                null);
            cache[targetXml] = invalid;
            return invalid;
        }

        if (!File.Exists(candidatePath))
        {
            diagnostics.Add(new Diagnostic(
                "conflict.base.missing",
                DiagnosticSeverity.Warning,
                "The base XML file is missing from the explicit Data/Config directory.",
                source,
                targetXml));
            var missing = new BaseLoadResult(
                new BaseDataFileObservation(targetXml, 0, string.Empty, null, source, diagnostics.AsReadOnly()),
                null);
            cache[targetXml] = missing;
            return missing;
        }

        try
        {
            var bytes = File.ReadAllBytes(candidatePath);
            var parsed = XmlParsing.Parse(bytes, source, collectAllObservations: false);
            diagnostics.AddRange(parsed.Diagnostics);
            var loaded = new BaseLoadResult(
                new BaseDataFileObservation(
                    targetXml,
                    bytes.LongLength,
                    ParsingUtilities.Sha256Hex(bytes),
                    parsed.Status,
                    source,
                    DeduplicateDiagnostics(diagnostics)),
                parsed.Status == XmlParseStatus.Parsed ? parsed.Document : null);
            cache[targetXml] = loaded;
            return loaded;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic(
                "conflict.base.read.failed",
                DiagnosticSeverity.Error,
                "The base XML file could not be read.",
                source,
                exception.GetType().Name));
            var failed = new BaseLoadResult(
                new BaseDataFileObservation(targetXml, 0, string.Empty, null, source, diagnostics.AsReadOnly()),
                null);
            cache[targetXml] = failed;
            return failed;
        }
    }

    private static XDocument CloneDocument(XDocument document)
    {
        return XDocument.Parse(
            document.ToString(SaveOptions.DisableFormatting),
            LoadOptions.PreserveWhitespace);
    }

    private static OperationSimulation ApplyOperation(
        XDocument document,
        SemanticConflictOperation operation)
    {
        var diagnostics = new List<Diagnostic>();
        if (operation.HasChildElements)
        {
            diagnostics.Add(new Diagnostic(
                "conflict.effective.child_fragment",
                DiagnosticSeverity.Warning,
                "The operation contains child elements. Its effective change is unknown.",
                operation.Source,
                operation.RawOperationName));
            return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
        }

        if (operation.NormalizedKind is null)
        {
            diagnostics.Add(new Diagnostic(
                "conflict.effective.operation_unknown",
                DiagnosticSeverity.Warning,
                "The operation kind is unknown. Its effective change is unknown.",
                operation.Source,
                operation.RawOperationName));
            return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
        }

        if (operation.XPath is null)
        {
            return OperationSimulation.Unsupported(OperationEffectKind.Unknown, new[]
            {
                new Diagnostic(
                    "conflict.effective.xpath_missing",
                    DiagnosticSeverity.Warning,
                    "The operation has no standard XPath expression.",
                    operation.Source)
            });
        }

        var matches = EvaluateXPath(document, operation.XPath, operation.Source, diagnostics);
        if (matches is null || matches.Count == 0)
        {
            return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
        }

        return operation.NormalizedKind switch
        {
            XmlPatchOperationKind.Set => ApplySet(matches, operation, diagnostics),
            XmlPatchOperationKind.SetAttribute => ApplySetAttribute(matches, operation, diagnostics),
            XmlPatchOperationKind.Remove => ApplyRemove(matches, operation, diagnostics),
            XmlPatchOperationKind.RemoveAttribute => ApplyRemoveAttribute(matches, operation, diagnostics),
            XmlPatchOperationKind.Append => ApplyAppend(matches, operation, diagnostics),
            XmlPatchOperationKind.Prepend or XmlPatchOperationKind.InsertBefore or XmlPatchOperationKind.InsertAfter =>
                OperationSimulation.Unsupported(
                    OperationEffectKind.Unknown,
                    diagnostics.Concat(new[]
                    {
                        new Diagnostic(
                            "conflict.effective.operation_unverified",
                            DiagnosticSeverity.Warning,
                            "The operation is not evaluated by the Phase4 effective subset.",
                            operation.Source,
                            operation.RawOperationName)
                    }).ToList()),
            _ => OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics)
        };
    }

    private static IReadOnlyList<XObject>? EvaluateXPath(
        XDocument document,
        string xpath,
        SourceReference source,
        List<Diagnostic> diagnostics)
    {
        try
        {
            var result = document.XPathEvaluate(xpath);
            var values = new List<XObject>();
            if (result is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    if (item is not XObject xObject)
                    {
                        diagnostics.Add(new Diagnostic(
                            "conflict.xpath.result.unsupported",
                            DiagnosticSeverity.Warning,
                            "The XPath result is not a node sequence.",
                            source,
                            xpath));
                        return null;
                    }

                    values.Add(xObject);
                }
            }
            else if (result is XObject xObject)
            {
                values.Add(xObject);
            }
            else
            {
                diagnostics.Add(new Diagnostic(
                    "conflict.xpath.result.unsupported",
                    DiagnosticSeverity.Warning,
                    "The XPath result is not a node sequence.",
                    source,
                    xpath));
                return null;
            }

            if (values.Count == 0)
            {
                diagnostics.Add(new Diagnostic(
                    "conflict.xpath.no_match",
                    DiagnosticSeverity.Warning,
                    "The XPath expression matched no nodes in the base XML.",
                    source,
                    xpath));
            }

            return values.AsReadOnly();
        }
        catch (XPathException)
        {
            diagnostics.Add(new Diagnostic(
                "conflict.xpath.invalid",
                DiagnosticSeverity.Warning,
                "The XPath expression could not be evaluated by the standard XPath evaluator.",
                source,
                xpath));
            return null;
        }
    }

    private static OperationSimulation ApplySet(
        IReadOnlyList<XObject> matches,
        SemanticConflictOperation operation,
        List<Diagnostic> diagnostics)
    {
        var changes = new List<EffectiveChange>();
        var value = operation.Value ?? string.Empty;
        foreach (var match in matches)
        {
            switch (match)
            {
                case XAttribute attribute:
                    changes.Add(ChangeForAttribute(attribute, value, operation.Source));
                    attribute.Value = value;
                    break;
                case XElement element:
                    var before = element.Value;
                    element.Value = value;
                    changes.Add(new EffectiveChange(
                        ParsingUtilities.BuildElementPath(element),
                        null,
                        before,
                        element.Value,
                        true,
                        true,
                        operation.Source));
                    break;
                default:
                    diagnostics.Add(new Diagnostic(
                        "conflict.effective.target_unsupported",
                        DiagnosticSeverity.Warning,
                        "The XPath selected a node type that set does not support.",
                        operation.Source,
                        operation.XPath));
                    return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
            }
        }

        return new OperationSimulation(true, OperationEffectKind.Set, value, operation.AttributeName, changes.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static OperationSimulation ApplySetAttribute(
        IReadOnlyList<XObject> matches,
        SemanticConflictOperation operation,
        List<Diagnostic> diagnostics)
    {
        var attributeName = operation.AttributeName;
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            diagnostics.Add(new Diagnostic(
                "conflict.effective.attribute_name_missing",
                DiagnosticSeverity.Warning,
                "The setattribute operation has no name attribute.",
                operation.Source));
            return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
        }

        var changes = new List<EffectiveChange>();
        var value = operation.Value ?? string.Empty;
        foreach (var match in matches)
        {
            if (match is not XElement element)
            {
                diagnostics.Add(new Diagnostic(
                    "conflict.effective.target_unsupported",
                    DiagnosticSeverity.Warning,
                    "The XPath selected a node type that setattribute does not support.",
                    operation.Source,
                    operation.XPath));
                return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
            }

            var attribute = element.Attribute(attributeName);
            changes.Add(new EffectiveChange(
                $"{ParsingUtilities.BuildElementPath(element)}/@{attributeName}",
                attributeName,
                attribute?.Value,
                value,
                attribute is not null,
                true,
                operation.Source));
            element.SetAttributeValue(attributeName, value);
        }

        return new OperationSimulation(true, OperationEffectKind.Set, value, attributeName, changes.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static OperationSimulation ApplyRemove(
        IReadOnlyList<XObject> matches,
        SemanticConflictOperation operation,
        List<Diagnostic> diagnostics)
    {
        var changes = new List<EffectiveChange>();
        foreach (var match in matches)
        {
            switch (match)
            {
                case XAttribute attribute:
                    changes.Add(ChangeForAttribute(attribute, null, operation.Source) with
                    {
                        ExistsAfter = false
                    });
                    attribute.Remove();
                    break;
                case XElement element when element.Parent is not null:
                    changes.Add(new EffectiveChange(
                        ParsingUtilities.BuildElementPath(element),
                        null,
                        element.Value,
                        null,
                        true,
                        false,
                        operation.Source));
                    element.Remove();
                    break;
                default:
                    diagnostics.Add(new Diagnostic(
                        "conflict.effective.remove_root",
                        DiagnosticSeverity.Warning,
                        "The remove operation selected a root node or unsupported node.",
                        operation.Source,
                        operation.XPath));
                    return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
            }
        }

        return new OperationSimulation(true, OperationEffectKind.Remove, null, operation.AttributeName, changes.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static OperationSimulation ApplyRemoveAttribute(
        IReadOnlyList<XObject> matches,
        SemanticConflictOperation operation,
        List<Diagnostic> diagnostics)
    {
        var attributeName = operation.AttributeName;
        if (string.IsNullOrWhiteSpace(attributeName)
            && matches.Any(match => match is XElement))
        {
            diagnostics.Add(new Diagnostic(
                "conflict.effective.attribute_name_missing",
                DiagnosticSeverity.Warning,
                "The removeattribute operation has no name attribute.",
                operation.Source));
            return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
        }

        var changes = new List<EffectiveChange>();
        foreach (var match in matches)
        {
            if (match is XAttribute selectedAttribute)
            {
                changes.Add(ChangeForAttribute(selectedAttribute, null, operation.Source) with
                {
                    ExistsAfter = false
                });
                selectedAttribute.Remove();
                continue;
            }

            if (match is not XElement element)
            {
                diagnostics.Add(new Diagnostic(
                    "conflict.effective.target_unsupported",
                    DiagnosticSeverity.Warning,
                    "The XPath selected a node type that removeattribute does not support.",
                    operation.Source,
                    operation.XPath));
                return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
            }

            var attribute = element.Attribute(attributeName!);
            changes.Add(new EffectiveChange(
                $"{ParsingUtilities.BuildElementPath(element)}/@{attributeName}",
                attributeName,
                attribute?.Value,
                null,
                attribute is not null,
                false,
                operation.Source));
            attribute?.Remove();
        }

        return new OperationSimulation(true, OperationEffectKind.RemoveAttribute, null, attributeName, changes.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static OperationSimulation ApplyAppend(
        IReadOnlyList<XObject> matches,
        SemanticConflictOperation operation,
        List<Diagnostic> diagnostics)
    {
        var changes = new List<EffectiveChange>();
        var appendValue = operation.Value ?? string.Empty;
        foreach (var match in matches)
        {
            if (match is not XAttribute attribute)
            {
                diagnostics.Add(new Diagnostic(
                    "conflict.effective.append_target",
                    DiagnosticSeverity.Warning,
                    "Only simple attribute values are supported for effective append.",
                    operation.Source,
                    operation.XPath));
                return OperationSimulation.Unsupported(OperationEffectKind.Unknown, diagnostics);
            }

            changes.Add(ChangeForAttribute(attribute, attribute.Value + appendValue, operation.Source));
            attribute.Value += appendValue;
        }

        return new OperationSimulation(true, OperationEffectKind.Append, appendValue, operation.AttributeName, changes.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static EffectiveChange ChangeForAttribute(
        XAttribute attribute,
        string? after,
        SourceReference source)
    {
        var parentPath = attribute.Parent is null
            ? attribute.Name.LocalName
            : $"{ParsingUtilities.BuildElementPath(attribute.Parent)}/@{attribute.Name.LocalName}";
        return new EffectiveChange(
            parentPath,
            attribute.Name.LocalName,
            attribute.Value,
            after,
            true,
            after is not null,
            source);
    }

    private static SemanticConflictAssessment Assess(
        IReadOnlyList<AnalyzedOperation> operations,
        IReadOnlyList<OperationSimulation> simulations,
        EffectiveResultStatus effectiveStatus)
    {
        if (operations.Count <= 1)
        {
            return effectiveStatus == EffectiveResultStatus.Unknown
                ? SemanticConflictAssessment.Unknown
                : SemanticConflictAssessment.Compatible;
        }

        var hasStaticRemoval = operations.Any(operation =>
            operation.Operation.NormalizedKind is XmlPatchOperationKind.Remove or XmlPatchOperationKind.RemoveAttribute);
        var hasStaticMutation = operations.Any(operation =>
            operation.Operation.NormalizedKind is XmlPatchOperationKind.Set
                or XmlPatchOperationKind.SetAttribute
                or XmlPatchOperationKind.Append);
        if (effectiveStatus != EffectiveResultStatus.NotAssessed
            && simulations.Any(simulation => simulation.IsSupported)
            && hasStaticRemoval
            && hasStaticMutation)
        {
            return SemanticConflictAssessment.Conflict;
        }

        if (effectiveStatus == EffectiveResultStatus.Unknown
            || simulations.Any(simulation => !simulation.IsSupported))
        {
            return SemanticConflictAssessment.Unknown;
        }

        for (var index = 0; index < simulations.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < simulations.Count; otherIndex++)
            {
                var current = simulations[index];
                var other = simulations[otherIndex];
                if (current.EffectKind is OperationEffectKind.Remove or OperationEffectKind.RemoveAttribute
                    || other.EffectKind is OperationEffectKind.Remove or OperationEffectKind.RemoveAttribute)
                {
                    if (current.EffectKind == other.EffectKind)
                    {
                        continue;
                    }

                    if (IsRemoval(current) != IsRemoval(other))
                    {
                        return SemanticConflictAssessment.Conflict;
                    }

                    return SemanticConflictAssessment.Possible;
                }

                if (current.EffectKind == OperationEffectKind.Set
                    && other.EffectKind == OperationEffectKind.Set
                    && string.Equals(current.AttributeName, other.AttributeName, StringComparison.Ordinal)
                    && string.Equals(current.Value, other.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                if (current.EffectKind == OperationEffectKind.Set
                    && other.EffectKind == OperationEffectKind.Set
                    && string.Equals(current.AttributeName, other.AttributeName, StringComparison.Ordinal))
                {
                    return SemanticConflictAssessment.Conflict;
                }

                if (current.EffectKind == OperationEffectKind.Append
                    && other.EffectKind == OperationEffectKind.Append
                    && string.Equals(current.AttributeName, other.AttributeName, StringComparison.Ordinal)
                    && string.Equals(current.Value, other.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                return SemanticConflictAssessment.Possible;
            }
        }

        return SemanticConflictAssessment.Compatible;
    }

    private static bool IsRemoval(OperationSimulation simulation)
    {
        return simulation.EffectKind is OperationEffectKind.Remove or OperationEffectKind.RemoveAttribute;
    }

    private static IReadOnlyList<Diagnostic> DeduplicateDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<EvidenceReference> DeduplicateEvidence(IEnumerable<EvidenceReference> evidence)
    {
        return evidence
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    private static void AddDistinct<T>(List<T> target, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            if (!target.Contains(value))
            {
                target.Add(value);
            }
        }
    }

    private sealed record AnalyzedOperation(SemanticConflictOperation Operation);

    private sealed record TargetCandidate(
        string Value,
        EvidenceKind EvidenceKind,
        SourceReference Source);

    private sealed record GroupKey(string? TargetXml, string? XPath);

    private sealed record BaseLoadResult(
        BaseDataFileObservation Observation,
        XDocument? Document);

    private enum OperationEffectKind
    {
        Unknown,
        Set,
        Remove,
        RemoveAttribute,
        Append
    }

    private sealed record OperationSimulation(
        bool IsSupported,
        OperationEffectKind EffectKind,
        string? Value,
        string? AttributeName,
        IReadOnlyList<EffectiveChange> Changes,
        IReadOnlyList<Diagnostic> Diagnostics)
    {
        public static OperationSimulation Unsupported(
            OperationEffectKind effectKind,
            IEnumerable<Diagnostic> diagnostics)
        {
            return new OperationSimulation(
                false,
                effectKind,
                null,
                null,
                Array.Empty<EffectiveChange>(),
                DeduplicateDiagnostics(diagnostics));
        }
    }
}
