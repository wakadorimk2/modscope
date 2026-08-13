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
    ModFile,
    GameDataFile
}

public enum QueryXmlParseStatus
{
    Parsed,
    Malformed,
    DtdBlocked,
    EncodingError
}

public enum KnowledgeQueryNodeKind
{
    Mod,
    File,
    XmlFile,
    PatchOperation,
    TargetXml,
    XPath,
    Entity,
    Property,
    Attribute
}

public enum KnowledgeQueryDirection
{
    Forward,
    Reverse
}

public enum KnowledgeReferenceRelation
{
    Contains,
    Targets,
    Selects,
    Mentions
}

public enum QueryXmlPatchOperationKind
{
    Set,
    SetAttribute,
    Remove,
    RemoveAttribute,
    Append,
    Prepend,
    InsertBefore,
    InsertAfter
}

public enum QuerySemanticConflictAssessment
{
    Compatible,
    Conflict,
    Possible,
    Unknown
}

public enum QueryEffectiveResultStatus
{
    Computed,
    Unknown,
    NotAssessed
}

public sealed record KnowledgeReferenceQuery(
    KnowledgeQueryNodeKind NodeKind,
    string Value,
    KnowledgeQueryDirection Direction,
    int? Limit = null);

public sealed record KnowledgeNodeReadModel(
    KnowledgeQueryNodeKind Kind,
    string Value);

public sealed record XmlReferenceCandidateReadModel(
    string RawValue,
    string? NormalizedValue,
    string ElementPath,
    QueryEvidenceKind EvidenceKind,
    SourceReferenceReadModel Source);

public sealed record XmlPatchOperationReadModel(
    string ElementPath,
    string RawOperationName,
    QueryXmlPatchOperationKind? NormalizedKind,
    RawXmlObservationReadModel RawObservation,
    IReadOnlyList<XmlXPathCandidateReadModel> XPathCandidates,
    IReadOnlyList<XmlReferenceCandidateReadModel> TargetXmlCandidates,
    IReadOnlyList<XmlReferenceCandidateReadModel> EntityCandidates,
    IReadOnlyList<XmlReferenceCandidateReadModel> PropertyCandidates,
    IReadOnlyList<XmlReferenceCandidateReadModel> AttributeCandidates,
    IReadOnlyList<DiagnosticReadModel> Diagnostics,
    SourceReferenceReadModel Source)
{
    public bool HasChildElements => RawObservation.HasChildElements;
}

public sealed record ModReferenceContextReadModel(
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

public sealed record KnowledgeReferenceReadModel(
    KnowledgeNodeReadModel From,
    KnowledgeNodeReadModel To,
    KnowledgeReferenceRelation Relation,
    EvidenceReferenceReadModel Evidence,
    ModReferenceContextReadModel? OwnerMod,
    XmlPatchOperationReadModel? Operation,
    IReadOnlyList<DiagnosticReadModel> Diagnostics);

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
    string? Website,
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
    SourceReferenceReadModel Source)
{
    public bool HasChildElements { get; init; }
}

public sealed record SevenDaysToDieBaseDataInput(string DataConfigPath)
{
    public string DataConfigDirectory => DataConfigPath;
}

public sealed record ConflictAnalysisQuery(
    string? TargetXml = null,
    string? XPath = null,
    int? Limit = null);

public sealed record BaseDataFileReadModel(
    string TargetXml,
    long Size,
    string Sha256,
    QueryXmlParseStatus? ParseStatus,
    SourceReferenceReadModel Source,
    IReadOnlyList<DiagnosticReadModel> Diagnostics);

public sealed record SemanticConflictOperationReadModel(
    string OperationKey,
    string ModKey,
    int? Priority,
    string XmlFileRelativePath,
    string ElementPath,
    string RawOperationName,
    QueryXmlPatchOperationKind? NormalizedKind,
    string? TargetXml,
    string? XPath,
    string? AttributeName,
    string? Value,
    SourceReferenceReadModel Source,
    IReadOnlyList<EvidenceReferenceReadModel> Evidence,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public bool HasChildElements { get; init; }
}

public sealed record EffectiveChangeReadModel(
    string MatchPath,
    string? AttributeName,
    string? BeforeValue,
    string? AfterValue,
    bool ExistedBefore,
    bool ExistsAfter,
    SourceReferenceReadModel Source)
{
    public string? Before => BeforeValue;

    public string? After => AfterValue;
}

public sealed record SemanticConflictGroupReadModel(
    string? TargetXml,
    string? XPath,
    QuerySemanticConflictAssessment Assessment,
    QueryEffectiveResultStatus EffectiveStatus,
    IReadOnlyList<SemanticConflictOperationReadModel> OperationSequence,
    IReadOnlyList<EffectiveChangeReadModel> EffectiveChanges,
    IReadOnlyList<EvidenceReferenceReadModel> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public IReadOnlyList<SemanticConflictOperationReadModel> Operations => OperationSequence;
}

public sealed record ConflictAnalysisReadModel(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    IReadOnlyList<BaseDataFileReadModel> BaseFiles,
    IReadOnlyList<SemanticConflictGroupReadModel> Groups,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public IReadOnlyList<BaseDataFileReadModel> BaseDataFiles => BaseFiles;

    public IReadOnlyList<SemanticConflictGroupReadModel> OperationGroups => Groups;
}

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
    SourceReferenceReadModel Source)
{
    public IReadOnlyList<XmlPatchOperationReadModel> PatchOperations { get; init; } =
        Array.Empty<XmlPatchOperationReadModel>();
}

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
