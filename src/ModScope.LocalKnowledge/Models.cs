using System.Collections.ObjectModel;

namespace ModScope.LocalKnowledge;

public static class ParserMetadata
{
    public const string ParserVersion = "0.1.0";
    public const int SchemaVersion = 1;
}

public sealed record Mo2SourceDefinition(
    string InstanceName,
    string ProfileName,
    string InstanceRootPath,
    string ProfilePath,
    string ModsPath);

public sealed record Mo2ProfileDefinition(
    string Name,
    string ProfilePath);

public interface IMo2SnapshotReader
{
    LocalModSnapshot Read(
        Mo2SourceDefinition source,
        CancellationToken cancellationToken = default);

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
    ModDirectory,
    ModFile
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
    SourceReference Source);

public sealed record XmlXPathCandidate(
    string RawValue,
    string ElementPath,
    SourceReference Source);

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
    SourceReference Source);

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
    SourceReference Source);

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
    SourceReference Source);

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
    IReadOnlyList<Diagnostic> Diagnostics);

internal static class CollectionHelpers
{
    public static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToList());
    }
}
