namespace ModScope.Query;

public enum PageExtractionStatus
{
    NotRequested,
    Succeeded,
    Partial,
    Failed
}

public enum QueryProfileState
{
    Listed,
    Unlisted,
    Unresolved
}

public enum QueryEnabledState
{
    Enabled,
    Disabled,
    Unknown
}

public enum LocalContextStatus
{
    Installed,
    NotInstalled,
    Unresolved,
    Unknown
}

public enum QueryDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum QueryEvidenceKind
{
    Source,
    Normalized,
    StaticEvidence,
    RuntimeEvidence,
    Inference,
    Uncertainty,
    Diagnostic
}

public enum QuerySourceReferenceKind
{
    ProfileFile,
    InstanceFile,
    ModDirectory,
    ModFile
}

public enum QueryXmlParseStatus
{
    Parsed,
    Malformed,
    DtdBlocked,
    EncodingError
}

public sealed record Mo2SourceInput(
    string InstanceName,
    string ProfileName,
    string InstanceRootPath,
    string ProfilePath,
    string ModsPath)
{
    public string? ProfilesPath { get; init; }
}

public sealed record Mo2SourceCandidateReadModel(
    string CandidateId,
    string InstanceName,
    string GameName,
    string ProfileName,
    string Readiness,
    bool IsReady,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<DiagnosticReadModel> Diagnostics);

public sealed record SourceDiscoveryReadModel(
    IReadOnlyList<Mo2SourceCandidateReadModel> Candidates);

public sealed record SourceReferenceReadModel(
    QuerySourceReferenceKind Kind,
    string RelativePath,
    int? LineNumber = null,
    int? ColumnNumber = null);

public sealed record DiagnosticReadModel(
    string Code,
    QueryDiagnosticSeverity Severity,
    string Message,
    SourceReferenceReadModel? Source = null,
    string? RawValue = null);

public sealed record EvidenceReferenceReadModel(
    QueryEvidenceKind Kind,
    SourceReferenceReadModel Source);

public sealed record PageObservation(
    Uri Url,
    string Title,
    string? ContentPreview,
    DateTimeOffset ObservedAtUtc,
    string Source,
    PageExtractionStatus ExtractionStatus,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public const int MaxContentPreviewLength = 8_000;

    public string? BoundedContentPreview => ContentPreview is null
        ? null
        : ContentPreview.Length <= MaxContentPreviewLength
            ? ContentPreview
            : ContentPreview[..MaxContentPreviewLength];
}

public sealed record KnowledgeSessionReadModel(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    DateTimeOffset CreatedAtUtc,
    string ParserVersion,
    int SchemaVersion,
    IReadOnlyList<DiagnosticReadModel> Diagnostics);

public sealed record ProfileSummaryReadModel(string ProfileName);

public sealed record ModCandidateSummary(
    string ModKey,
    string DirectoryName,
    string? DisplayName,
    string? Version,
    QueryProfileState ProfileState,
    QueryEnabledState EnabledState,
    int? Priority,
    SourceReferenceReadModel Source,
    EvidenceReferenceReadModel? PriorityEvidence,
    IReadOnlyList<DiagnosticReadModel> Diagnostics);

public sealed record IdentityConfirmation(
    PageObservation Page,
    string CandidateIdentity,
    string? LocalModKey);

public sealed record LocalContextReadModel(
    string CandidateIdentity,
    LocalContextStatus Status,
    string InstanceName,
    string ProfileName,
    string? LocalModKey,
    string? DirectoryName,
    QueryEnabledState EnabledState,
    int? Priority,
    string? KnownVersion,
    IReadOnlyList<EvidenceReferenceReadModel> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<DiagnosticReadModel> Diagnostics);

public sealed record ModInfoReadModel(
    string RelativePath,
    QueryXmlParseStatus ParseStatus,
    string? Name,
    string? DisplayName,
    string? Version,
    string? Description,
    string? Author,
    string? Website,
    IReadOnlyList<RawXmlObservationReadModel> UnknownObservations,
    IReadOnlyList<DiagnosticReadModel> Diagnostics,
    SourceReferenceReadModel Source);

public sealed record ModFileReadModel(
    string RelativePath,
    long Size,
    string Sha256,
    SourceReferenceReadModel Source,
    QueryEvidenceKind EvidenceKind);

public sealed record XmlAttributeObservationReadModel(
    string Name,
    string Value);

public sealed record RawXmlObservationReadModel(
    string ElementPath,
    string ElementName,
    IReadOnlyList<XmlAttributeObservationReadModel> Attributes,
    string? InnerText,
    SourceReferenceReadModel Source);

public sealed record XmlXPathCandidateReadModel(
    string RawValue,
    string ElementPath,
    SourceReferenceReadModel Source);

public sealed record XmlFileReadModel(
    string RelativePath,
    QueryXmlParseStatus ParseStatus,
    string? EncodingName,
    string? RootElementName,
    int ElementCount,
    int AttributeCount,
    IReadOnlyList<XmlXPathCandidateReadModel> XPathCandidates,
    IReadOnlyList<RawXmlObservationReadModel> RawObservations,
    IReadOnlyList<DiagnosticReadModel> Diagnostics,
    SourceReferenceReadModel Source);

public sealed record InspectorReadModel(
    string ModKey,
    string DirectoryName,
    QueryProfileState ProfileState,
    QueryEnabledState EnabledState,
    int? Priority,
    ModInfoReadModel? ModInfo,
    IReadOnlyList<ModFileReadModel> Files,
    IReadOnlyList<XmlFileReadModel> XmlFiles,
    IReadOnlyList<DiagnosticReadModel> Diagnostics,
    SourceReferenceReadModel Source);
