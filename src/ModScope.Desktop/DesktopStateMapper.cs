using ModScope.Desktop.Contracts;
using ModScope.Query;

namespace ModScope.Desktop;

internal static class DesktopStateMapper
{
    public static UiState Map(
        BrowserUiState browser,
        PageObservation? observation,
        SourceDiscoveryReadModel? sourceDiscovery,
        string? selectedSourceCandidateId,
        KnowledgeSessionReadModel? session,
        IReadOnlyList<ModCandidateSummary> candidates,
        IReadOnlyList<ProfileSummaryReadModel> profiles,
        IdentityUiState identity,
        LocalContextReadModel? localContext,
        InspectorReadModel? inspector,
        LayoutUiState layout,
        string statusMessage,
        KnowledgeOperationUiState operation,
        IReadOnlyDictionary<string, string>? profileLoadStates = null)
    {
        return new UiState(
            browser,
            observation is null ? null : PageObservation(observation),
            sourceDiscovery is null
                ? new SourceDiscoveryUiState(Array.Empty<SourceCandidateUiState>(), selectedSourceCandidateId)
                : SourceDiscovery(sourceDiscovery, selectedSourceCandidateId),
            new KnowledgeUiState(
                session is null ? null : KnowledgeSession(session),
                candidates.Select(Candidate).ToList().AsReadOnly(),
                profiles
                    .Select(profile => Profile(profile, profileLoadStates))
                    .ToList()
                    .AsReadOnly(),
                operation),
            identity,
            localContext is null ? null : LocalContext(localContext),
            inspector is null ? null : Inspector(inspector),
            layout,
            statusMessage,
            session is null ? Array.Empty<DiagnosticUiState>() : Diagnostics(session.Diagnostics));
    }

    private static SourceDiscoveryUiState SourceDiscovery(
        SourceDiscoveryReadModel value,
        string? selectedCandidateId)
    {
        return new SourceDiscoveryUiState(
            value.Candidates.Select(SourceCandidate).ToList().AsReadOnly(),
            selectedCandidateId);
    }

    private static SourceCandidateUiState SourceCandidate(Mo2SourceCandidateReadModel value)
    {
        return new SourceCandidateUiState(
            value.CandidateId,
            value.InstanceName,
            value.GameName,
            value.ProfileName,
            value.Readiness,
            value.IsReady,
            value.Evidence,
            Diagnostics(value.Diagnostics));
    }

    private static PageObservationUiState PageObservation(PageObservation value)
    {
        return new PageObservationUiState(
            value.Url.ToString(),
            value.Title,
            value.BoundedContentPreview,
            value.ObservedAtUtc,
            value.Source,
            EnumText(value.ExtractionStatus),
            Diagnostics(value.Diagnostics));
    }

    private static KnowledgeSessionUiState KnowledgeSession(KnowledgeSessionReadModel value)
    {
        return new KnowledgeSessionUiState(
            value.SnapshotId,
            value.InstanceName,
            value.ProfileName,
            value.CreatedAtUtc,
            value.ParserVersion,
            value.SchemaVersion,
            Diagnostics(value.Diagnostics));
    }

    private static ModCandidateUiState Candidate(ModCandidateSummary value)
    {
        return new ModCandidateUiState(
            value.ModKey,
            value.DirectoryName,
            value.DisplayName,
            value.Version,
            value.Website,
            EnumText(value.ProfileState),
            EnumText(value.EnabledState),
            value.Priority,
            Source(value.Source),
            value.PriorityEvidence is null ? null : Evidence(value.PriorityEvidence),
            Diagnostics(value.Diagnostics));
    }

    private static ProfileUiState Profile(
        ProfileSummaryReadModel value,
        IReadOnlyDictionary<string, string>? profileLoadStates)
    {
        var loadState = profileLoadStates is not null
            && profileLoadStates.TryGetValue(value.ProfileName, out var state)
            ? state
            : "ready";
        return new ProfileUiState(value.ProfileName, loadState);
    }

    private static LocalContextUiState LocalContext(LocalContextReadModel value)
    {
        return new LocalContextUiState(
            value.CandidateIdentity,
            EnumText(value.Status),
            value.InstanceName,
            value.ProfileName,
            value.LocalModKey,
            value.DirectoryName,
            EnumText(value.EnabledState),
            value.Priority,
            value.KnownVersion,
            value.Evidence.Select(Evidence).ToList().AsReadOnly(),
            value.Uncertainties,
            Diagnostics(value.Diagnostics));
    }

    private static InspectorUiState Inspector(InspectorReadModel value)
    {
        return new InspectorUiState(
            value.ModKey,
            value.DirectoryName,
            EnumText(value.ProfileState),
            EnumText(value.EnabledState),
            value.Priority,
            value.ModInfo is null ? null : ModInfo(value.ModInfo),
            value.Files.Select(File).ToList().AsReadOnly(),
            value.XmlFiles.Select(XmlFile).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source));
    }

    private static ModInfoUiState ModInfo(ModInfoReadModel value)
    {
        return new ModInfoUiState(
            value.RelativePath,
            EnumText(value.ParseStatus),
            value.Name,
            value.DisplayName,
            value.Version,
            value.Description,
            value.Author,
            value.Website,
            value.UnknownObservations.Select(RawObservation).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source));
    }

    private static ModFileUiState File(ModFileReadModel value)
    {
        return new ModFileUiState(
            value.RelativePath,
            value.Size,
            value.Sha256,
            Source(value.Source),
            EnumText(value.EvidenceKind));
    }

    private static XmlFileUiState XmlFile(XmlFileReadModel value)
    {
        return new XmlFileUiState(
            value.RelativePath,
            EnumText(value.ParseStatus),
            value.EncodingName,
            value.RootElementName,
            value.ElementCount,
            value.AttributeCount,
            value.XPathCandidates.Select(XPath).ToList().AsReadOnly(),
            value.RawObservations.Select(RawObservation).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source));
    }

    private static XmlXPathCandidateUiState XPath(XmlXPathCandidateReadModel value)
    {
        return new XmlXPathCandidateUiState(
            value.RawValue,
            value.ElementPath,
            Source(value.Source));
    }

    private static RawXmlObservationUiState RawObservation(RawXmlObservationReadModel value)
    {
        return new RawXmlObservationUiState(
            value.ElementPath,
            value.ElementName,
            value.Attributes
                .Select(attribute => new XmlAttributeObservationUiState(attribute.Name, attribute.Value))
                .ToList()
                .AsReadOnly(),
            value.InnerText,
            Source(value.Source));
    }

    private static EvidenceReferenceUiState Evidence(EvidenceReferenceReadModel value)
    {
        return new EvidenceReferenceUiState(EnumText(value.Kind), Source(value.Source));
    }

    private static DiagnosticUiState Diagnostic(DiagnosticReadModel value)
    {
        return new DiagnosticUiState(
            value.Code,
            EnumText(value.Severity),
            value.Message,
            value.Source is null ? null : Source(value.Source),
            value.RawValue);
    }

    private static IReadOnlyList<DiagnosticUiState> Diagnostics(
        IReadOnlyList<DiagnosticReadModel> values)
    {
        return values.Select(Diagnostic).ToList().AsReadOnly();
    }

    private static SourceReferenceUiState Source(SourceReferenceReadModel value)
    {
        return new SourceReferenceUiState(
            EnumText(value.Kind),
            value.RelativePath,
            value.LineNumber,
            value.ColumnNumber);
    }

    private static string EnumText<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0
            ? string.Empty
            : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
