using System.Text.Json;

namespace ModScope.Desktop.Contracts;

public sealed class BridgeProtocolException : Exception
{
    public BridgeProtocolException(string message)
        : base(message)
    {
    }

    public BridgeProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class BridgeProtocol
{
    public const int ContractVersion = 2;
    public const string AppHostOrigin = "https://appassets.modscope";

    private static readonly HashSet<string> KnownCommands = new(StringComparer.Ordinal)
    {
        "browser.navigate",
        "browser.newTab",
        "browser.selectTab",
        "browser.closeTab",
        "browser.home",
        "browser.history",
        "browser.selectHistory",
        "browser.back",
        "browser.forward",
        "browser.reload",
        "browser.observe",
        "frontend.ready",
        "knowledge.useFixture",
        "knowledge.loadSource",
        "knowledge.discoverSources",
        "knowledge.selectSource",
        "knowledge.selectRoot",
        "knowledge.switchProfile",
        "identity.confirm",
        "inspector.open",
        "deployment.preview",
        "deployment.apply",
        "game.launch",
        "analysis.selectBaseData",
        "analysis.selectRuntimeLogs",
        "analysis.analyzeConflicts",
        "analysis.compareRuntimeEvidence",
        "analysis.useFixture",
        "layout.setContextVisible",
        "layout.setModListVisible",
        "layout.setToolbarExpanded"
    };

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static BridgeCommandEnvelope ParseCommand(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new BridgeProtocolException("The bridge message is empty.");
        }

        BridgeCommandEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<BridgeCommandEnvelope>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new BridgeProtocolException("The bridge message is not valid JSON.", exception);
        }

        if (envelope is null)
        {
            throw new BridgeProtocolException("The bridge message is null.");
        }

        if (envelope.ContractVersion != ContractVersion)
        {
            throw new BridgeProtocolException(
                $"Unsupported bridge contract version: {envelope.ContractVersion}.");
        }

        if (string.IsNullOrWhiteSpace(envelope.RequestId))
        {
            throw new BridgeProtocolException("The bridge requestId is required.");
        }

        if (envelope.RequestId.Length > 100)
        {
            throw new BridgeProtocolException("The bridge requestId is too long.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Command) || !KnownCommands.Contains(envelope.Command))
        {
            throw new BridgeProtocolException($"Unknown bridge command: {envelope.Command ?? "<null>"}.");
        }

        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new BridgeProtocolException("The bridge payload is required.");
        }

        if (envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new BridgeProtocolException("The bridge payload must be a JSON object.");
        }

        return envelope;
    }

    public static T ReadPayload<T>(JsonElement payload)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload.GetRawText(), JsonOptions)
                ?? throw new BridgeProtocolException($"The payload could not be read as {typeof(T).Name}.");
        }
        catch (JsonException exception)
        {
            throw new BridgeProtocolException(
                $"The payload could not be read as {typeof(T).Name}.",
                exception);
        }
    }

    public static string SerializeMessage<T>(
        string kind,
        T payload,
        string? requestId = null)
    {
        return JsonSerializer.Serialize(
            new BridgeMessageEnvelope(ContractVersion, kind, requestId, payload!),
            JsonOptions);
    }

    public static bool TryGetSupportedBrowserUri(string value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme is not ("http" or "https" or "file" or "about"))
        {
            return false;
        }

        if (string.Equals(candidate.Host, "appassets.modscope", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    public static bool IsAppHostUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "appassets.modscope", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record BridgeCommandEnvelope(
    int ContractVersion,
    string? RequestId,
    string? Command,
    JsonElement Payload);

public sealed record BridgeMessageEnvelope(
    int ContractVersion,
    string Kind,
    string? RequestId,
    object Payload);

public sealed record NavigatePayload(string Url, string? NexusSearchName = null);

public sealed record BrowserTabPayload(string TabId);

public sealed record BrowserHistoryPayload(string EntryId);

public sealed record LoadSourcePayload(
    string InstanceName,
    string ProfileName,
    string InstanceRootPath,
    string ProfilePath,
    string ModsPath);

public sealed record DiscoverSourcesPayload(IReadOnlyList<string>? SelectedRoots = null);

public sealed record SelectSourcePayload(string CandidateId);

public sealed record ConfirmIdentityPayload(
    string CandidateIdentity,
    string? LocalModKey);

public sealed record InspectorOpenPayload(string ModKey);

public sealed record CompareRuntimeEvidencePayload(
    string? ToolVersion = null,
    string? GameVersion = null);

public sealed record SwitchProfilePayload(string ProfileName);

public sealed record DeploymentEntryPayload(
    string ModKey,
    bool Enabled,
    int Order);

public sealed record DeploymentPreviewPayload(
    string ProfileName,
    IReadOnlyList<DeploymentEntryPayload> Entries);

public sealed record DeploymentApplyPayload(
    string PlanId,
    bool Approved);

public sealed record SetContextVisiblePayload(bool Visible);

public sealed record SetModListVisiblePayload(bool Visible);

public sealed record SetToolbarExpandedPayload(bool Expanded);

public sealed record BridgeErrorPayload(string Code, string Message);

public sealed record BrowserTabUiState(
    string TabId,
    string Title,
    string Url,
    bool CanGoBack,
    bool CanGoForward,
    bool IsActive);

public sealed record BrowserHistoryEntryUiState(
    string EntryId,
    string Title,
    string Url,
    DateTimeOffset VisitedAtUtc);

public sealed record BrowserUiState(
    string Url,
    string Title,
    bool CanGoBack,
    bool CanGoForward,
    IReadOnlyList<BrowserTabUiState>? Tabs = null,
    string? ActiveTabId = null,
    IReadOnlyList<BrowserHistoryEntryUiState>? History = null);

public sealed record SourceReferenceUiState(
    string Kind,
    string RelativePath,
    int? LineNumber = null,
    int? ColumnNumber = null);

public sealed record DiagnosticUiState(
    string Code,
    string Severity,
    string Message,
    SourceReferenceUiState? Source = null,
    string? RawValue = null);

public sealed record EvidenceReferenceUiState(
    string Kind,
    SourceReferenceUiState Source);

public sealed record PageObservationUiState(
    string Url,
    string Title,
    DateTimeOffset ObservedAtUtc,
    string Source,
    string ExtractionStatus,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record KnowledgeSessionUiState(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    DateTimeOffset CreatedAtUtc,
    string ParserVersion,
    int SchemaVersion,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record ModRoleEvidenceUiState(
    string Kind,
    string Detail,
    SourceReferenceUiState Source);

public sealed record ModRoleUiState(
    string Role,
    string Assessment,
    string Reason,
    IReadOnlyList<ModRoleEvidenceUiState> Evidence);

public sealed record ModCandidateUiState(
    string ModKey,
    string DirectoryName,
    string? DisplayName,
    string? Version,
    string? Website,
    string ProfileState,
    string EnabledState,
    int? Priority,
    SourceReferenceUiState Source,
    EvidenceReferenceUiState? PriorityEvidence,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    ModRoleUiState? Role = null);

public sealed record ProfileUiState(string Name, string LoadState);

public sealed record KnowledgeUiState(
    KnowledgeSessionUiState? Session,
    IReadOnlyList<ModCandidateUiState> Candidates,
    IReadOnlyList<ProfileUiState> Profiles,
    KnowledgeOperationUiState Operation);

public sealed record KnowledgeOperationUiState(
    string Kind,
    bool IsBusy,
    bool IsBackground,
    string? TargetProfileName,
    string Phase,
    int? Completed,
    int? Total)
{
    public static KnowledgeOperationUiState Idle { get; } = new(
        "idle",
        false,
        false,
        null,
        "idle",
        null,
        null);
}

public sealed record SourceCandidateUiState(
    string CandidateId,
    string InstanceName,
    string GameName,
    string ProfileName,
    string Readiness,
    bool IsReady,
    bool GameTargetReady,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record SourceDiscoveryUiState(
    IReadOnlyList<SourceCandidateUiState> Candidates,
    string? SelectedCandidateId);

public sealed record LocalModMatchUiState(
    string ModKey,
    string DirectoryName,
    string? DisplayName,
    string ProfileState,
    string EnabledState,
    string MatchKind,
    string Strength,
    string Evidence,
    bool AutoConfirmEligible);

public sealed record IdentityUiState(
    string CandidateIdentity,
    string? SelectedLocalModKey,
    string RecognitionStatus,
    IReadOnlyList<LocalModMatchUiState> Matches,
    string? AutoInspectToken);

public sealed record LocalContextUiState(
    string CandidateIdentity,
    string Status,
    string InstanceName,
    string ProfileName,
    string? LocalModKey,
    string? DirectoryName,
    string EnabledState,
    int? Priority,
    string? KnownVersion,
    IReadOnlyList<EvidenceReferenceUiState> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record ModInfoUiState(
    string RelativePath,
    string ParseStatus,
    string? Name,
    string? DisplayName,
    string? Version,
    string? Description,
    string? Author,
    string? Website,
    IReadOnlyList<RawXmlObservationUiState> UnknownObservations,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record ModFileUiState(
    string RelativePath,
    long Size,
    string Sha256,
    SourceReferenceUiState Source,
    string EvidenceKind);

public sealed record XmlAttributeObservationUiState(
    string Name,
    string Value);

public sealed record RawXmlObservationUiState(
    string ElementPath,
    string ElementName,
    IReadOnlyList<XmlAttributeObservationUiState> Attributes,
    string? InnerText,
    SourceReferenceUiState Source,
    bool HasChildElements = false);

public sealed record XmlXPathCandidateUiState(
    string RawValue,
    string ElementPath,
    SourceReferenceUiState Source);

public sealed record XmlReferenceCandidateUiState(
    string RawValue,
    string? NormalizedValue,
    string ElementPath,
    string EvidenceKind,
    SourceReferenceUiState Source);

public sealed record XmlPatchOperationUiState(
    string ElementPath,
    string RawOperationName,
    string? NormalizedKind,
    RawXmlObservationUiState RawObservation,
    IReadOnlyList<XmlXPathCandidateUiState> XPathCandidates,
    IReadOnlyList<XmlReferenceCandidateUiState> TargetXmlCandidates,
    IReadOnlyList<XmlReferenceCandidateUiState> EntityCandidates,
    IReadOnlyList<XmlReferenceCandidateUiState> PropertyCandidates,
    IReadOnlyList<XmlReferenceCandidateUiState> AttributeCandidates,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record XmlFileUiState(
    string RelativePath,
    string ParseStatus,
    string? EncodingName,
    string? RootElementName,
    int ElementCount,
    int AttributeCount,
    IReadOnlyList<XmlXPathCandidateUiState> XPathCandidates,
    IReadOnlyList<RawXmlObservationUiState> RawObservations,
    IReadOnlyList<XmlPatchOperationUiState> PatchOperations,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record InspectorUiState(
    string ModKey,
    string DirectoryName,
    string ProfileState,
    string EnabledState,
    int? Priority,
    ModInfoUiState? ModInfo,
    IReadOnlyList<ModFileUiState> Files,
    IReadOnlyList<XmlFileUiState> XmlFiles,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record BaseDataFileUiState(
    string TargetXml,
    long Size,
    string Sha256,
    string? ParseStatus,
    SourceReferenceUiState Source,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record SemanticConflictOperationUiState(
    string OperationKey,
    string ModKey,
    int? Priority,
    string XmlFileRelativePath,
    string ElementPath,
    string RawOperationName,
    string? NormalizedKind,
    string? TargetXml,
    string? XPath,
    string? AttributeName,
    string? Value,
    SourceReferenceUiState Source,
    IReadOnlyList<EvidenceReferenceUiState> Evidence,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    bool HasChildElements);

public sealed record EffectiveChangeUiState(
    string MatchPath,
    string? AttributeName,
    string? BeforeValue,
    string? AfterValue,
    bool ExistedBefore,
    bool ExistsAfter,
    SourceReferenceUiState Source);

public sealed record SemanticConflictGroupUiState(
    string? TargetXml,
    string? XPath,
    string Assessment,
    string Confidence,
    string EffectiveStatus,
    IReadOnlyList<SemanticConflictOperationUiState> Operations,
    IReadOnlyList<EffectiveChangeUiState> EffectiveChanges,
    IReadOnlyList<EvidenceReferenceUiState> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record ConflictAnalysisUiState(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    IReadOnlyList<BaseDataFileUiState> BaseFiles,
    IReadOnlyList<SemanticConflictGroupUiState> Groups,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record RuntimeEvidenceObservationUiState(
    string? ModKey,
    string? TargetXml,
    string? XPath,
    string? ObservedOperation,
    string? ObservedCategory,
    string? NormalizedAssessment,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record RuntimeEvidenceUiState(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    string ToolName,
    string? ToolVersion,
    string? GameVersion,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<RuntimeEvidenceObservationUiState> Observations,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record RuntimeEvidenceComparisonItemUiState(
    string? TargetXml,
    string? XPath,
    string Status,
    string? StaticAssessment,
    string? RuntimeAssessment,
    IReadOnlyList<RuntimeEvidenceObservationUiState> Observations,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record RuntimeEvidenceComparisonUiState(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    RuntimeEvidenceUiState RuntimeEvidence,
    IReadOnlyList<RuntimeEvidenceComparisonItemUiState> Items,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record AnalysisInputUiState(
    bool BaseDataReady,
    bool RuntimeLogsReady,
    string BaseDataStatus = "missing");

public sealed record AnalysisOperationUiState(string Kind, bool IsBusy)
{
    public static AnalysisOperationUiState Idle { get; } = new("idle", false);
}

public sealed record AnalysisUiState(
    AnalysisInputUiState Inputs,
    ConflictAnalysisUiState? Conflict,
    RuntimeEvidenceComparisonUiState? RuntimeComparison,
    AnalysisOperationUiState Operation,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record DeploymentEntryUiState(
    string EntryId,
    string ModKey,
    bool Enabled,
    int? Priority,
    bool IsSeparator,
    bool IsEditable);

public sealed record DeploymentModChangeUiState(
    string ModKey,
    bool BeforeEnabled,
    bool AfterEnabled,
    int BeforeOrder,
    int AfterOrder);

public sealed record DeploymentJunctionChangeUiState(
    string Action,
    string TargetName);

public sealed record DeploymentUiState(
    string Status,
    string ProfileName,
    IReadOnlyList<DeploymentEntryUiState> Entries,
    string? PlanId,
    bool CanApply,
    bool CanLaunch,
    IReadOnlyList<DeploymentModChangeUiState> ModChanges,
    IReadOnlyList<DeploymentJunctionChangeUiState> JunctionChanges,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record LayoutUiState(bool ContextVisible, bool ModListVisible);

public sealed record UiState(
    BrowserUiState Browser,
    PageObservationUiState? Observation,
    SourceDiscoveryUiState SourceDiscovery,
    KnowledgeUiState Knowledge,
    IdentityUiState Identity,
    LocalContextUiState? LocalContext,
    InspectorUiState? Inspector,
    AnalysisUiState Analysis,
    DeploymentUiState Deployment,
    LayoutUiState Layout,
    string StatusMessage,
    IReadOnlyList<DiagnosticUiState> Diagnostics);
