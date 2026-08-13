using System.Collections.ObjectModel;

namespace ModScope.LocalKnowledge;

public static class ParserMetadata
{
    public const string ParserVersion = "0.3.0";
    public const int SchemaVersion = 3;
}

public sealed record Mo2SourceDefinition(
    string InstanceName,
    string ProfileName,
    string InstanceRootPath,
    string ProfilePath,
    string ModsPath)
{
    public string? ProfilesPath { get; init; }
}

public enum Mo2SourceCandidateReadiness
{
    Ready,
    ProfileSelectionRequired,
    UnsupportedGame,
    Invalid
}

public enum Mo2SourceDiscoveryEvidenceKind
{
    RunningProcess,
    Remembered,
    GlobalInstance,
    NativePicker
}

public sealed record Mo2SourceDiscoveryEvidence(
    Mo2SourceDiscoveryEvidenceKind Kind,
    EvidenceKind EvidenceKind);

public sealed record Mo2SourceCandidate(
    string CandidateId,
    string GameName,
    Mo2SourceDefinition Source,
    Mo2SourceCandidateReadiness Readiness,
    IReadOnlyList<Mo2SourceDiscoveryEvidence> Evidence,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record Mo2SourcePreference(
    string InstanceRootPath,
    string ProfileName);

public sealed record Mo2SourceDiscoveryRequest(
    Mo2SourcePreference? RememberedSource,
    IReadOnlyList<string> SelectedRoots);

public interface IMo2DiscoveryEnvironment
{
    string? LocalAppDataPath { get; }

    IReadOnlyList<string> GetRunningModOrganizerExecutablePaths();

    string? GetLastUsedInstanceName();
}

public interface IMo2SourceDiscovery
{
    IReadOnlyList<Mo2SourceCandidate> Discover(
        Mo2SourceDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMo2SourcePreferenceStore
{
    Mo2SourcePreference? Read();

    void Write(Mo2SourcePreference preference);
}

public sealed record Mo2ProfileDefinition(
    string Name,
    string ProfilePath);

public sealed record LocalKnowledgeProgress(
    string Phase,
    int? Completed = null,
    int? Total = null);

public interface IMo2SnapshotReader
{
    LocalModSnapshot Read(
        Mo2SourceDefinition source,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null);

    IReadOnlyList<Mo2ProfileDefinition> ListProfiles(
        Mo2SourceDefinition source,
        CancellationToken cancellationToken = default);
}

public enum ModEnabledState
{
    Enabled,
    Disabled,
    Unknown
}

public enum ModProfileState
{
    Listed,
    Unlisted,
    Unresolved
}

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum EvidenceKind
{
    Source,
    Normalized,
    StaticEvidence,
    RuntimeEvidence,
    Inference,
    Uncertainty,
    Diagnostic
}

public enum SourceReferenceKind
{
    ProfileFile,
    InstanceFile,
    ModDirectory,
    ModFile,
    GameDataFile
}

public enum XmlParseStatus
{
    Parsed,
    Malformed,
    DtdBlocked,
    EncodingError
}

public sealed record SourceReference(
    SourceReferenceKind Kind,
    string RelativePath,
    int? LineNumber = null,
    int? ColumnNumber = null);

public sealed record EvidenceReference(
    EvidenceKind Kind,
    SourceReference Source);

public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    SourceReference? Source = null,
    string? RawValue = null);

public sealed record XmlAttributeObservation(
    string Name,
    string Value);

public sealed record RawXmlObservation(
    string ElementPath,
    string ElementName,
    IReadOnlyList<XmlAttributeObservation> Attributes,
    string? InnerText,
    SourceReference Source)
{
    public bool HasChildElements { get; init; }
}

public sealed record XmlXPathCandidate(
    string RawValue,
    string ElementPath,
    SourceReference Source)
{
    public string? NormalizedValue => string.IsNullOrWhiteSpace(RawValue)
        ? null
        : RawValue.Trim();
}

public enum XmlPatchOperationKind
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

public sealed record XmlReferenceCandidate(
    string RawValue,
    string? NormalizedValue,
    string ElementPath,
    EvidenceKind EvidenceKind,
    SourceReference Source);

public sealed record XmlPatchOperationObservation(
    string ElementPath,
    string RawOperationName,
    XmlPatchOperationKind? NormalizedKind,
    RawXmlObservation RawObservation,
    IReadOnlyList<XmlXPathCandidate> XPathCandidates,
    IReadOnlyList<XmlReferenceCandidate> TargetXmlCandidates,
    IReadOnlyList<XmlReferenceCandidate> EntityCandidates,
    IReadOnlyList<XmlReferenceCandidate> PropertyCandidates,
    IReadOnlyList<XmlReferenceCandidate> AttributeCandidates,
    IReadOnlyList<Diagnostic> Diagnostics,
    SourceReference Source)
{
    public bool HasChildElements => RawObservation.HasChildElements;
}

public sealed record ProfileModEntry(
    string RawLine,
    int SourceLineNumber,
    ModEnabledState EnabledState,
    string? NormalizedModName,
    int? Priority,
    SourceReference Source,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public EvidenceReference PriorityEvidence => new(EvidenceKind.Source, Source);
}

public sealed record ModFileRecord(
    string RelativePath,
    long Size,
    string Sha256,
    SourceReference Source,
    EvidenceReference Evidence);

public sealed record ModInfoMetadata(
    string RelativePath,
    XmlParseStatus ParseStatus,
    string? Name,
    string? DisplayName,
    string? Version,
    string? Description,
    string? Author,
    string? Website,
    IReadOnlyList<RawXmlObservation> UnknownObservations,
    IReadOnlyList<Diagnostic> Diagnostics,
    SourceReference Source)
{
    public IReadOnlyList<RawXmlObservation> RawObservations { get; init; } = Array.Empty<RawXmlObservation>();
}

public sealed record ModRootResolution(
    string OuterDirectoryRelativePath,
    string InnerDirectoryRelativePath,
    EvidenceKind EvidenceKind,
    SourceReference OuterSource,
    SourceReference InnerSource);

public sealed record XmlFileReference(
    string RelativePath,
    XmlParseStatus ParseStatus,
    string? EncodingName,
    string? RootElementName,
    int ElementCount,
    int AttributeCount,
    IReadOnlyList<XmlXPathCandidate> XPathCandidates,
    IReadOnlyList<RawXmlObservation> RawObservations,
    IReadOnlyList<Diagnostic> Diagnostics,
    SourceReference Source)
{
    public IReadOnlyList<XmlPatchOperationObservation> PatchOperations { get; init; } =
        Array.Empty<XmlPatchOperationObservation>();
}

public sealed record LocalModRecord(
    string DirectoryName,
    string ModKey,
    ModProfileState ProfileState,
    ModEnabledState EnabledState,
    int? Priority,
    string? ResolvedDirectoryRelativePath,
    ModInfoMetadata? ModInfo,
    IReadOnlyList<ModFileRecord> Files,
    IReadOnlyList<XmlFileReference> XmlFiles,
    IReadOnlyList<Diagnostic> Diagnostics,
    SourceReference Source)
{
    public string? Mo2OuterDirectoryName { get; init; }

    public SourceReference? Mo2OuterSource { get; init; }

    public ModRootResolution? RootResolution { get; init; }
}

public sealed record InputManifestFile(
    string RelativePath,
    long Size,
    string Sha256);

public sealed record InputManifest(
    string ProfileModListSha256,
    IReadOnlyList<InputManifestFile> Files,
    string ParserVersion,
    int SchemaVersion);

public sealed record LocalModSnapshot(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    DateTimeOffset CreatedAtUtc,
    string ParserVersion,
    int SchemaVersion,
    IReadOnlyList<ProfileModEntry> ProfileEntries,
    IReadOnlyList<LocalModRecord> Mods,
    InputManifest InputManifest,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public LocalKnowledgeIndex Index { get; init; } = LocalKnowledgeIndex.Empty;
}

public enum LocalKnowledgeNodeKind
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

public enum LocalKnowledgeRelation
{
    Contains,
    Targets,
    Selects,
    Mentions
}

public sealed record LocalKnowledgeNode(
    LocalKnowledgeNodeKind Kind,
    string Value);

public sealed record LocalKnowledgeReference(
    LocalKnowledgeNode From,
    LocalKnowledgeNode To,
    LocalKnowledgeRelation Relation,
    EvidenceReference Evidence);

public sealed record LocalKnowledgeIndex(
    IReadOnlyList<LocalKnowledgeReference> ForwardReferences,
    IReadOnlyList<LocalKnowledgeReference> ReverseReferences)
{
    public static LocalKnowledgeIndex Empty { get; } = new(
        Array.Empty<LocalKnowledgeReference>(),
        Array.Empty<LocalKnowledgeReference>());
}

internal static class CollectionHelpers
{
    public static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToList());
    }
}
