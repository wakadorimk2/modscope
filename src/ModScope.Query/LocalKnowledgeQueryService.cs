using ModScope.LocalKnowledge;

namespace ModScope.Query;

public interface ILocalKnowledgeQuery
{
    SourceDiscoveryReadModel DiscoverSources(
        IReadOnlyList<string>? selectedRoots = null,
        CancellationToken cancellationToken = default);

    KnowledgeSessionReadModel LoadSourceCandidate(
        string candidateId,
        CancellationToken cancellationToken = default);

    KnowledgeSessionReadModel Load(
        Mo2SourceInput source,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ModCandidateSummary> GetModCandidates();

    IReadOnlyList<ProfileSummaryReadModel> GetProfiles();

    KnowledgeSessionReadModel SwitchProfile(
        string profileName,
        CancellationToken cancellationToken = default);

    LocalContextReadModel ConfirmIdentity(IdentityConfirmation confirmation);

    InspectorReadModel GetInspector(string modKey);
}

public sealed class LocalKnowledgeQueryService : ILocalKnowledgeQuery
{
    private readonly IMo2SnapshotReader _snapshotReader;
    private readonly IMo2SourceDiscovery _sourceDiscovery;
    private readonly IMo2SourcePreferenceStore _preferenceStore;
    private LocalModSnapshot? _snapshot;
    private Mo2SourceInput? _source;
    private IReadOnlyList<Mo2ProfileDefinition> _profiles = Array.Empty<Mo2ProfileDefinition>();
    private IReadOnlyList<Mo2SourceCandidate> _sourceCandidates = Array.Empty<Mo2SourceCandidate>();

    public LocalKnowledgeQueryService(IMo2SnapshotReader snapshotReader)
        : this(
            snapshotReader,
            new Mo2SourceDiscovery(),
            new JsonMo2SourcePreferenceStore())
    {
    }

    public LocalKnowledgeQueryService(
        IMo2SnapshotReader snapshotReader,
        IMo2SourceDiscovery sourceDiscovery,
        IMo2SourcePreferenceStore preferenceStore)
    {
        _snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        _sourceDiscovery = sourceDiscovery ?? throw new ArgumentNullException(nameof(sourceDiscovery));
        _preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));
    }

    public static LocalKnowledgeQueryService CreateDefault()
    {
        return new LocalKnowledgeQueryService(
            new Mo2SnapshotReader(),
            new Mo2SourceDiscovery(),
            new JsonMo2SourcePreferenceStore());
    }

    public SourceDiscoveryReadModel DiscoverSources(
        IReadOnlyList<string>? selectedRoots = null,
        CancellationToken cancellationToken = default)
    {
        var request = new Mo2SourceDiscoveryRequest(
            _preferenceStore.Read(),
            selectedRoots ?? Array.Empty<string>());
        _sourceCandidates = _sourceDiscovery.Discover(request, cancellationToken);
        return ToSourceDiscovery(_sourceCandidates);
    }

    public KnowledgeSessionReadModel LoadSourceCandidate(
        string candidateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        var candidate = _sourceCandidates.FirstOrDefault(item =>
            string.Equals(item.CandidateId, candidateId.Trim(), StringComparison.Ordinal));
        if (candidate is null)
        {
            throw new KeyNotFoundException($"The MO2 source candidate '{candidateId}' does not exist.");
        }

        if (candidate.Readiness != Mo2SourceCandidateReadiness.Ready)
        {
            throw new InvalidOperationException(
                $"The MO2 source candidate '{candidateId}' is not ready: {candidate.Readiness}.");
        }

        var source = new Mo2SourceInput(
            candidate.Source.InstanceName,
            candidate.Source.ProfileName,
            candidate.Source.InstanceRootPath,
            candidate.Source.ProfilePath,
            candidate.Source.ModsPath);
        var session = Load(source, cancellationToken);
        TryWritePreference(source);
        return session;
    }

    public KnowledgeSessionReadModel Load(
        Mo2SourceInput source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var definition = ToDefinition(source);
        var snapshot = _snapshotReader.Read(definition, cancellationToken);
        var profiles = _snapshotReader.ListProfiles(definition, cancellationToken);

        _source = source;
        _snapshot = snapshot;
        _profiles = profiles;
        return ToSession(snapshot);
    }

    public IReadOnlyList<ModCandidateSummary> GetModCandidates()
    {
        var snapshot = RequireSnapshot();

        return snapshot.Mods
            .OrderBy(mod => mod.Priority ?? int.MaxValue)
            .ThenBy(mod => mod.DirectoryName, StringComparer.OrdinalIgnoreCase)
            .Select(mod => QueryProjection.ModCandidate(
                mod,
                snapshot.ProfileEntries.FirstOrDefault(entry =>
                    string.Equals(entry.NormalizedModName, mod.DirectoryName, StringComparison.OrdinalIgnoreCase))))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ProfileSummaryReadModel> GetProfiles()
    {
        return _profiles
            .Select(profile => new ProfileSummaryReadModel(profile.Name))
            .ToList()
            .AsReadOnly();
    }

    public KnowledgeSessionReadModel SwitchProfile(
        string profileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var source = _source ?? throw new InvalidOperationException(
            "Load an explicit MO2 source before switching profiles.");
        var profile = _profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new ArgumentException(
                $"The profile '{profileName}' is not available in the explicit instance.",
                nameof(profileName));
        }

        var nextSource = source with
        {
            ProfileName = profile.Name,
            ProfilePath = profile.ProfilePath
        };
        var snapshot = _snapshotReader.Read(ToDefinition(nextSource), cancellationToken);

        _source = nextSource;
        _snapshot = snapshot;
        TryWritePreference(nextSource);
        return ToSession(snapshot);
    }

    public LocalContextReadModel ConfirmIdentity(IdentityConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.Page);

        var snapshot = _snapshot;
        if (snapshot is null)
        {
            return new LocalContextReadModel(
                confirmation.CandidateIdentity,
                LocalContextStatus.Unknown,
                string.Empty,
                string.Empty,
                confirmation.LocalModKey,
                null,
                QueryEnabledState.Unknown,
                null,
                null,
                Array.Empty<EvidenceReferenceReadModel>(),
                new[] { "A Local Knowledge snapshot is not loaded." },
                new[]
                {
                    new DiagnosticReadModel(
                        "snapshot.not_loaded",
                        QueryDiagnosticSeverity.Warning,
                        "Load an explicit MO2 source before resolving local context.")
                });
        }

        var identity = confirmation.CandidateIdentity.Trim();
        if (identity.Length == 0)
        {
            return BuildUnresolvedContext(snapshot, identity, confirmation.LocalModKey, "The page identity is empty.");
        }

        if (confirmation.LocalModKey is null)
        {
            return new LocalContextReadModel(
                identity,
                LocalContextStatus.NotInstalled,
                snapshot.InstanceName,
                snapshot.ProfileName,
                null,
                null,
                QueryEnabledState.Unknown,
                null,
                null,
                Array.Empty<EvidenceReferenceReadModel>(),
                new[] { "The user confirmed the page identity without selecting a local MOD record." },
                Array.Empty<DiagnosticReadModel>());
        }

        var record = snapshot.Mods.FirstOrDefault(
            mod => string.Equals(mod.ModKey, confirmation.LocalModKey, StringComparison.OrdinalIgnoreCase));
        if (record is null)
        {
            return BuildUnresolvedContext(
                snapshot,
                identity,
                confirmation.LocalModKey,
                "The selected local MOD record does not exist in the loaded snapshot.");
        }

        if (record.ProfileState == ModProfileState.Unresolved)
        {
            return BuildUnresolvedContext(
                snapshot,
                identity,
                record.ModKey,
                "The selected profile entry has no resolved MOD directory.",
                record);
        }

        var uncertainties = new List<string>();
        if (record.ModInfo?.Version is null)
        {
            uncertainties.Add("Known version is not available from ModInfo.xml.");
        }

        uncertainties.Add("Dependencies and overlap are not assessed in this vertical slice.");

        var evidence = new List<EvidenceReferenceReadModel>
        {
            new(QueryEvidenceKind.Source, QueryProjection.Source(record.Source))
        };

        return new LocalContextReadModel(
            identity,
            LocalContextStatus.Installed,
            snapshot.InstanceName,
            snapshot.ProfileName,
            record.ModKey,
            record.DirectoryName,
            QueryProjection.EnabledState(record.EnabledState),
            record.Priority,
            record.ModInfo?.Version,
            evidence.AsReadOnly(),
            uncertainties.AsReadOnly(),
            QueryProjection.Diagnostics(record.Diagnostics));
    }

    public InspectorReadModel GetInspector(string modKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modKey);
        var record = RequireSnapshot().Mods.FirstOrDefault(
            mod => string.Equals(mod.ModKey, modKey, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            throw new KeyNotFoundException($"The MOD key '{modKey}' does not exist in the loaded snapshot.");
        }

        return new InspectorReadModel(
            record.ModKey,
            record.DirectoryName,
            QueryProjection.ProfileState(record.ProfileState),
            QueryProjection.EnabledState(record.EnabledState),
            record.Priority,
            QueryProjection.ModInfo(record.ModInfo),
            record.Files.Select(QueryProjection.ModFile).ToList().AsReadOnly(),
            record.XmlFiles.Select(QueryProjection.XmlFile).ToList().AsReadOnly(),
            QueryProjection.Diagnostics(record.Diagnostics),
            QueryProjection.Source(record.Source));
    }

    private LocalModSnapshot RequireSnapshot()
    {
        return _snapshot ?? throw new InvalidOperationException(
            "Load an explicit MO2 source before requesting Local Knowledge.");
    }

    private static Mo2SourceDefinition ToDefinition(Mo2SourceInput source)
    {
        return new Mo2SourceDefinition(
            source.InstanceName,
            source.ProfileName,
            source.InstanceRootPath,
            source.ProfilePath,
            source.ModsPath);
    }

    private static SourceDiscoveryReadModel ToSourceDiscovery(
        IReadOnlyList<Mo2SourceCandidate> candidates)
    {
        return new SourceDiscoveryReadModel(
            candidates
                .Select(candidate => new Mo2SourceCandidateReadModel(
                    candidate.CandidateId,
                    candidate.Source.InstanceName,
                    candidate.GameName,
                    candidate.Source.ProfileName,
                    candidate.Readiness.ToString(),
                    candidate.Readiness == Mo2SourceCandidateReadiness.Ready,
                    candidate.Evidence
                        .Select(evidence => $"{evidence.Kind}:{evidence.EvidenceKind}")
                        .ToList()
                        .AsReadOnly(),
                    QueryProjection.Diagnostics(candidate.Diagnostics)))
                .ToList()
                .AsReadOnly());
    }

    private void TryWritePreference(Mo2SourceInput source)
    {
        try
        {
            _preferenceStore.Write(new Mo2SourcePreference(
                source.InstanceRootPath,
                source.ProfileName));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static KnowledgeSessionReadModel ToSession(LocalModSnapshot snapshot)
    {
        return new KnowledgeSessionReadModel(
            snapshot.SnapshotId,
            snapshot.InstanceName,
            snapshot.ProfileName,
            snapshot.CreatedAtUtc,
            snapshot.ParserVersion,
            snapshot.SchemaVersion,
            QueryProjection.Diagnostics(snapshot.Diagnostics));
    }

    private static LocalContextReadModel BuildUnresolvedContext(
        LocalModSnapshot snapshot,
        string identity,
        string? localModKey,
        string message,
        LocalModRecord? record = null)
    {
        var diagnostics = new List<DiagnosticReadModel>
        {
            new("identity.unresolved", QueryDiagnosticSeverity.Warning, message)
        };

        if (record is not null)
        {
            diagnostics.AddRange(QueryProjection.Diagnostics(record.Diagnostics));
        }

        return new LocalContextReadModel(
            identity,
            LocalContextStatus.Unresolved,
            snapshot.InstanceName,
            snapshot.ProfileName,
            localModKey,
            record?.DirectoryName,
            record is null ? QueryEnabledState.Unknown : QueryProjection.EnabledState(record.EnabledState),
            record?.Priority,
            record?.ModInfo?.Version,
            record is null
                ? Array.Empty<EvidenceReferenceReadModel>()
                : new[] { new EvidenceReferenceReadModel(QueryEvidenceKind.Diagnostic, QueryProjection.Source(record.Source)) },
            new[] { message },
            diagnostics.AsReadOnly());
    }
}

internal static class QueryProjection
{
    public static ModCandidateSummary ModCandidate(LocalModRecord record, ProfileModEntry? profileEntry)
    {
        return new ModCandidateSummary(
            record.ModKey,
            record.DirectoryName,
            record.ModInfo?.DisplayName ?? record.ModInfo?.Name,
            record.ModInfo?.Version,
            ProfileState(record.ProfileState),
            EnabledState(record.EnabledState),
            record.Priority,
            Source(record.Source),
            profileEntry is null
                ? null
                : new EvidenceReferenceReadModel(QueryEvidenceKind.Source, Source(profileEntry.Source)),
            Diagnostics(record.Diagnostics));
    }

    public static ModInfoReadModel? ModInfo(ModInfoMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return new ModInfoReadModel(
            metadata.RelativePath,
            MapXmlParseStatus(metadata.ParseStatus),
            metadata.Name,
            metadata.DisplayName,
            metadata.Version,
            metadata.Description,
            metadata.Author,
            metadata.Website,
            metadata.UnknownObservations.Select(RawXmlObservation).ToList().AsReadOnly(),
            Diagnostics(metadata.Diagnostics),
            Source(metadata.Source));
    }

    public static ModFileReadModel ModFile(ModFileRecord file)
    {
        return new ModFileReadModel(
            file.RelativePath,
            file.Size,
            file.Sha256,
            Source(file.Source),
            MapEvidenceKind(file.Evidence.Kind));
    }

    public static XmlFileReadModel XmlFile(XmlFileReference file)
    {
        return new XmlFileReadModel(
            file.RelativePath,
            MapXmlParseStatus(file.ParseStatus),
            file.EncodingName,
            file.RootElementName,
            file.ElementCount,
            file.AttributeCount,
            file.XPathCandidates.Select(candidate => new XmlXPathCandidateReadModel(
                candidate.RawValue,
                candidate.ElementPath,
                Source(candidate.Source))).ToList().AsReadOnly(),
            file.RawObservations.Select(RawXmlObservation).ToList().AsReadOnly(),
            Diagnostics(file.Diagnostics),
            Source(file.Source));
    }

    public static RawXmlObservationReadModel RawXmlObservation(RawXmlObservation observation)
    {
        return new RawXmlObservationReadModel(
            observation.ElementPath,
            observation.ElementName,
            observation.Attributes
                .Select(attribute => new XmlAttributeObservationReadModel(attribute.Name, attribute.Value))
                .ToList()
                .AsReadOnly(),
            observation.InnerText,
            Source(observation.Source));
    }

    public static IReadOnlyList<DiagnosticReadModel> Diagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics.Select(Diagnostic).ToList().AsReadOnly();
    }

    public static DiagnosticReadModel Diagnostic(Diagnostic diagnostic)
    {
        return new DiagnosticReadModel(
            diagnostic.Code,
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Info => QueryDiagnosticSeverity.Info,
                DiagnosticSeverity.Warning => QueryDiagnosticSeverity.Warning,
                DiagnosticSeverity.Error => QueryDiagnosticSeverity.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Severity, null)
            },
            diagnostic.Message,
            diagnostic.Source is null ? null : Source(diagnostic.Source),
            diagnostic.RawValue);
    }

    public static SourceReferenceReadModel Source(SourceReference source)
    {
            return new SourceReferenceReadModel(
            source.Kind switch
            {
                SourceReferenceKind.ProfileFile => QuerySourceReferenceKind.ProfileFile,
                SourceReferenceKind.InstanceFile => QuerySourceReferenceKind.InstanceFile,
                SourceReferenceKind.ModDirectory => QuerySourceReferenceKind.ModDirectory,
                SourceReferenceKind.ModFile => QuerySourceReferenceKind.ModFile,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, null)
            },
            source.RelativePath,
            source.LineNumber,
            source.ColumnNumber);
    }

    public static QueryProfileState ProfileState(ModProfileState state)
    {
        return state switch
        {
            ModProfileState.Listed => QueryProfileState.Listed,
            ModProfileState.Unlisted => QueryProfileState.Unlisted,
            ModProfileState.Unresolved => QueryProfileState.Unresolved,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    public static QueryEnabledState EnabledState(ModEnabledState state)
    {
        return state switch
        {
            ModEnabledState.Enabled => QueryEnabledState.Enabled,
            ModEnabledState.Disabled => QueryEnabledState.Disabled,
            ModEnabledState.Unknown => QueryEnabledState.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    private static QueryEvidenceKind MapEvidenceKind(ModScope.LocalKnowledge.EvidenceKind kind)
    {
        return kind switch
        {
            EvidenceKind.Source => QueryEvidenceKind.Source,
            EvidenceKind.Normalized => QueryEvidenceKind.Normalized,
            EvidenceKind.StaticEvidence => QueryEvidenceKind.StaticEvidence,
            EvidenceKind.RuntimeEvidence => QueryEvidenceKind.RuntimeEvidence,
            EvidenceKind.Inference => QueryEvidenceKind.Inference,
            EvidenceKind.Uncertainty => QueryEvidenceKind.Uncertainty,
            EvidenceKind.Diagnostic => QueryEvidenceKind.Diagnostic,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static QueryXmlParseStatus MapXmlParseStatus(ModScope.LocalKnowledge.XmlParseStatus status)
    {
        return status switch
        {
            XmlParseStatus.Parsed => QueryXmlParseStatus.Parsed,
            XmlParseStatus.Malformed => QueryXmlParseStatus.Malformed,
            XmlParseStatus.DtdBlocked => QueryXmlParseStatus.DtdBlocked,
            XmlParseStatus.EncodingError => QueryXmlParseStatus.EncodingError,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
