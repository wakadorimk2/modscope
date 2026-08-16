using ModScope.LocalKnowledge;

namespace ModScope.Query;

public interface ILocalKnowledgeQuery
{
    SourceDiscoveryReadModel DiscoverSources(
        IReadOnlyList<string>? selectedRoots = null,
        CancellationToken cancellationToken = default);

    KnowledgeSessionReadModel LoadSourceCandidate(
        string candidateId,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null);

    KnowledgeSessionReadModel Load(
        Mo2SourceInput source,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null);

    IReadOnlyList<ModCandidateSummary> GetModCandidates();

    IReadOnlyList<LocalModMatchReadModel> FindLocalModMatches(PageObservation page);

    IReadOnlyList<ProfileSummaryReadModel> GetProfiles();

    KnowledgeSessionReadModel SwitchProfile(
        string profileName,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null);

    void WarmProfile(
        string profileName,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null);

    LocalContextReadModel ConfirmIdentity(IdentityConfirmation confirmation);

    InspectorReadModel GetInspector(string modKey);

    IReadOnlyList<KnowledgeReferenceReadModel> FindReferences(
        KnowledgeReferenceQuery query);

    ConflictAnalysisReadModel AnalyzeConflicts(
        SevenDaysToDieBaseDataInput baseData,
        ConflictAnalysisQuery? query = null,
        CancellationToken cancellationToken = default);

    RuntimeEvidenceComparisonReadModel CompareRuntimeEvidence(
        SevenDaysToDieBaseDataInput baseData,
        RuntimeEvidenceInput runtimeEvidence,
        RuntimeEvidenceComparisonQuery? query = null,
        CancellationToken cancellationToken = default);

    RuntimeEvidenceComparisonReadModel CompareRuntimeOcdEvidence(
        SevenDaysToDieBaseDataInput baseData,
        RuntimeOcdEvidenceInput runtimeEvidence,
        RuntimeEvidenceComparisonQuery? query = null,
        CancellationToken cancellationToken = default);

    Mo2SourceInput? GetCurrentSource();

    string? GetInferredBaseDataConfigPath();

    IReadOnlyList<ProfileEditEntryReadModel> GetCurrentProfileEntries();
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
    private readonly Dictionary<string, LocalModSnapshot> _profileSnapshots =
        new(StringComparer.OrdinalIgnoreCase);

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

    public static VersionObservationReadModel ProjectVersionObservation(VersionObservation observation)
    {
        return QueryProjection.VersionObservation(observation);
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
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null)
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
            candidate.Source.ModsPath)
        {
            ProfilesPath = candidate.Source.ProfilesPath,
            GamePath = candidate.Source.GamePath,
            VersionEvidenceManifestPath = _source?.VersionEvidenceManifestPath
        };
        var session = Load(source, cancellationToken, progress);
        TryWritePreference(source);
        return session;
    }

    public KnowledgeSessionReadModel Load(
        Mo2SourceInput source,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var definition = ToDefinition(source);
        var snapshot = _snapshotReader.Read(definition, cancellationToken, progress);
        var profiles = _snapshotReader.ListProfiles(definition, cancellationToken);

        _source = source with
        {
            GamePath = definition.GamePath,
            VersionEvidenceManifestPath = definition.VersionEvidenceManifestPath
        };
        _snapshot = snapshot;
        _profiles = OrderProfiles(profiles, snapshot.ProfileName);
        _profileSnapshots.Clear();
        _profileSnapshots[snapshot.ProfileName] = snapshot;
        return ToSession(snapshot);
    }

    public IReadOnlyList<ModCandidateSummary> GetModCandidates()
    {
        var snapshot = RequireSnapshot();
        var roles = ModRoleClassifier.Classify(snapshot);

        return snapshot.Mods
            .OrderBy(mod => ModRoleRank(roles[mod.ModKey].Role))
            .ThenBy(mod => mod.Priority ?? int.MaxValue)
            .ThenBy(mod => mod.DirectoryName, StringComparer.OrdinalIgnoreCase)
            .Select(mod => QueryProjection.ModCandidate(
                mod,
                snapshot.ProfileEntries.FirstOrDefault(entry =>
                    string.Equals(entry.NormalizedModName, mod.DirectoryName, StringComparison.OrdinalIgnoreCase)),
                roles[mod.ModKey]))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<LocalModMatchReadModel> FindLocalModMatches(PageObservation page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return _snapshot is null
            ? Array.Empty<LocalModMatchReadModel>()
            : LocalModMatchQuery.Find(_snapshot, page);
    }

    public Mo2SourceInput? GetCurrentSource()
    {
        return _source;
    }

    public string? GetInferredBaseDataConfigPath()
    {
        return SevenDaysToDiePathInference.InferBaseDataConfigPath(_source?.GamePath);
    }

    public IReadOnlyList<ProfileEditEntryReadModel> GetCurrentProfileEntries()
    {
        if (_snapshot is null)
        {
            return Array.Empty<ProfileEditEntryReadModel>();
        }

        return _snapshot.ProfileEntries
            .Where(entry => entry.NormalizedModName is not null)
            .OrderBy(entry => entry.Priority ?? int.MaxValue)
            .ThenBy(entry => entry.SourceLineNumber)
            .Select(entry => new ProfileEditEntryReadModel(
                $"line-{entry.SourceLineNumber}",
                entry.NormalizedModName!,
                entry.EnabledState.ToString(),
                entry.Priority,
                entry.NormalizedModName!.EndsWith("_separator", StringComparison.OrdinalIgnoreCase),
                QueryProjection.Diagnostics(entry.Diagnostics)))
            .ToList()
            .AsReadOnly();
    }

    private static int ModRoleRank(QueryModRole role)
    {
        return role switch
        {
            QueryModRole.Foundation => 0,
            QueryModRole.Compatibility => 1,
            QueryModRole.Content => 2,
            _ => 3
        };
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
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null)
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
        var snapshot = _profileSnapshots.TryGetValue(profile.Name, out var cachedSnapshot)
            ? cachedSnapshot
            : _snapshotReader.Read(ToDefinition(nextSource), cancellationToken, progress);
        _profileSnapshots[profile.Name] = snapshot;

        _source = nextSource;
        _snapshot = snapshot;
        _profiles = OrderProfiles(_profiles, snapshot.ProfileName);
        TryWritePreference(nextSource);
        return ToSession(snapshot);
    }

    public void WarmProfile(
        string profileName,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var source = _source ?? throw new InvalidOperationException(
            "Load an explicit MO2 source before warming profiles.");
        var profile = _profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new ArgumentException(
                $"The profile '{profileName}' is not available in the explicit instance.",
                nameof(profileName));
        }

        if (_profileSnapshots.ContainsKey(profile.Name))
        {
            return;
        }

        var nextSource = source with
        {
            ProfileName = profile.Name,
            ProfilePath = profile.ProfilePath
        };
        var snapshot = _snapshotReader.Read(ToDefinition(nextSource), cancellationToken, progress);
        _profileSnapshots[profile.Name] = snapshot;
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
            QueryProjection.Source(record.Source))
        {
            PackageRelation = QueryProjection.PackageRelation(record.PackageEvidence)
        };
    }

    public IReadOnlyList<KnowledgeReferenceReadModel> FindReferences(
        KnowledgeReferenceQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Value, nameof(query));

        if (query.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Limit,
                "The reference query limit cannot be negative.");
        }

        var snapshot = RequireSnapshot();
        if (query.Limit == 0)
        {
            return Array.Empty<KnowledgeReferenceReadModel>();
        }

        var normalizedValue = NormalizeQueryValue(query.NodeKind, query.Value);
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(
                "The reference query value is empty after normalization.",
                nameof(query));
        }

        var localNodeKind = MapNodeKind(query.NodeKind);
        var references = query.Direction switch
        {
            KnowledgeQueryDirection.Forward => snapshot.Index.ForwardReferences,
            KnowledgeQueryDirection.Reverse => snapshot.Index.ReverseReferences,
            _ => throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Direction,
                "The reference query direction is not supported.")
        };
        var lookup = BuildReferenceLookup(snapshot);
        var results = new List<KnowledgeReferenceReadModel>();

        foreach (var reference in references)
        {
            if (reference.From.Kind != localNodeKind
                || !string.Equals(reference.From.Value, normalizedValue, StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(ToReferenceReadModel(reference, snapshot, lookup));
            if (query.Limit is int limit && results.Count >= limit)
            {
                break;
            }
        }

        return results.AsReadOnly();
    }

    public ConflictAnalysisReadModel AnalyzeConflicts(
        SevenDaysToDieBaseDataInput baseData,
        ConflictAnalysisQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseData);
        query ??= new ConflictAnalysisQuery();

        if (query.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Limit,
                "The conflict analysis limit cannot be negative.");
        }

        var snapshot = RequireSnapshot();
        if (query.TargetXml is not null && string.IsNullOrWhiteSpace(query.TargetXml))
        {
            throw new ArgumentException(
                "The conflict analysis target XML filter cannot be empty.",
                nameof(query));
        }

        if (query.XPath is not null && string.IsNullOrWhiteSpace(query.XPath))
        {
            throw new ArgumentException(
                "The conflict analysis XPath filter cannot be empty.",
                nameof(query));
        }

        if (query.Limit == 0)
        {
            return QueryProjection.ConflictAnalysis(new SemanticConflictAnalysis(
                snapshot.SnapshotId,
                snapshot.InstanceName,
                snapshot.ProfileName,
                Array.Empty<BaseDataFileObservation>(),
                Array.Empty<SemanticConflictGroup>(),
                Array.Empty<Diagnostic>()));
        }

        var analysis = SevenDaysToDieConflictAnalyzer.Analyze(
            snapshot,
            new SevenDaysToDieBaseDataSource(baseData.DataConfigPath),
            cancellationToken);
        var targetFilter = query.TargetXml is null
            ? null
            : NormalizeConflictTarget(query.TargetXml);
        var xpathFilter = query.XPath?.Trim();
        var filteredGroups = analysis.Groups
            .Where(group => targetFilter is null
                || string.Equals(group.TargetXml, targetFilter, StringComparison.Ordinal))
            .Where(group => xpathFilter is null
                || string.Equals(group.XPath, xpathFilter, StringComparison.Ordinal));

        if (query.Limit is int limit)
        {
            filteredGroups = filteredGroups.Take(limit);
        }

        return QueryProjection.ConflictAnalysis(analysis with
        {
            Groups = filteredGroups.ToList().AsReadOnly()
        });
    }

    public RuntimeEvidenceComparisonReadModel CompareRuntimeEvidence(
        SevenDaysToDieBaseDataInput baseData,
        RuntimeEvidenceInput runtimeEvidence,
        RuntimeEvidenceComparisonQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseData);
        ArgumentNullException.ThrowIfNull(runtimeEvidence);
        query ??= new RuntimeEvidenceComparisonQuery();

        if (query.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Limit,
                "The runtime evidence comparison limit cannot be negative.");
        }

        var snapshot = RequireSnapshot();
        if (query.TargetXml is not null && string.IsNullOrWhiteSpace(query.TargetXml))
        {
            throw new ArgumentException(
                "The runtime evidence comparison target XML filter cannot be empty.",
                nameof(query));
        }

        if (query.XPath is not null && string.IsNullOrWhiteSpace(query.XPath))
        {
            throw new ArgumentException(
                "The runtime evidence comparison XPath filter cannot be empty.",
                nameof(query));
        }

        if (query.ObservedCategory is not null && string.IsNullOrWhiteSpace(query.ObservedCategory))
        {
            throw new ArgumentException(
                "The runtime evidence comparison category filter cannot be empty.",
                nameof(query));
        }

        var document = ToRuntimeEvidence(runtimeEvidence, snapshot);
        RuntimeEvidenceComparison comparison;
        if (query.Limit == 0)
        {
            comparison = RuntimeEvidenceComparison.Compare(
                new SemanticConflictAnalysis(
                    snapshot.SnapshotId,
                    snapshot.InstanceName,
                    snapshot.ProfileName,
                    Array.Empty<BaseDataFileObservation>(),
                    Array.Empty<SemanticConflictGroup>(),
                    Array.Empty<Diagnostic>()),
                document) with
            {
                Items = Array.Empty<RuntimeEvidenceComparisonItem>()
            };
        }
        else
        {
            var analysis = SevenDaysToDieConflictAnalyzer.Analyze(
                snapshot,
                new SevenDaysToDieBaseDataSource(baseData.DataConfigPath),
                cancellationToken);
            comparison = RuntimeEvidenceComparison.Compare(analysis, document);
        }

        var targetFilter = query.TargetXml is null
            ? null
            : NormalizeConflictTarget(query.TargetXml);
        var xpathFilter = query.XPath?.Trim();
        var categoryFilter = query.ObservedCategory?.Trim();
        var filteredItems = comparison.Items
            .Where(item => targetFilter is null
                || string.Equals(item.TargetXml, targetFilter, StringComparison.Ordinal))
            .Where(item => xpathFilter is null
                || string.Equals(item.XPath, xpathFilter, StringComparison.Ordinal))
            .Where(item => categoryFilter is null
                || item.RuntimeObservations.Any(observation =>
                    string.Equals(observation.ObservedCategory, categoryFilter, StringComparison.OrdinalIgnoreCase)));

        if (query.Status is QueryRuntimeEvidenceComparisonStatus status)
        {
            filteredItems = filteredItems.Where(item =>
                QueryProjection.MapRuntimeEvidenceComparisonStatus(item.Status) == status);
        }

        if (query.Limit is int limit)
        {
            filteredItems = filteredItems.Take(limit);
        }

        return QueryProjection.RuntimeEvidenceComparison(comparison with
        {
            Items = filteredItems.ToList().AsReadOnly()
        });
    }

    public RuntimeEvidenceComparisonReadModel CompareRuntimeOcdEvidence(
        SevenDaysToDieBaseDataInput baseData,
        RuntimeOcdEvidenceInput runtimeEvidence,
        RuntimeEvidenceComparisonQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseData);
        ArgumentNullException.ThrowIfNull(runtimeEvidence);
        query ??= new RuntimeEvidenceComparisonQuery();

        if (query.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Limit,
                "The runtime evidence comparison limit cannot be negative.");
        }

        var snapshot = RequireSnapshot();
        if (string.IsNullOrWhiteSpace(runtimeEvidence.SnapshotId))
        {
            throw new ArgumentException(
                "RuntimeOCD evidence must include an explicit snapshot ID.",
                nameof(runtimeEvidence));
        }

        if (!string.Equals(runtimeEvidence.SnapshotId, snapshot.SnapshotId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "RuntimeOCD evidence must reference the currently loaded snapshot.",
                nameof(runtimeEvidence));
        }

        if (query.TargetXml is not null && string.IsNullOrWhiteSpace(query.TargetXml))
        {
            throw new ArgumentException(
                "The runtime evidence comparison target XML filter cannot be empty.",
                nameof(query));
        }

        if (query.XPath is not null && string.IsNullOrWhiteSpace(query.XPath))
        {
            throw new ArgumentException(
                "The runtime evidence comparison XPath filter cannot be empty.",
                nameof(query));
        }

        if (query.ObservedCategory is not null && string.IsNullOrWhiteSpace(query.ObservedCategory))
        {
            throw new ArgumentException(
                "The runtime evidence comparison category filter cannot be empty.",
                nameof(query));
        }

        SemanticConflictAnalysis analysis;
        if (query.Limit == 0)
        {
            analysis = new SemanticConflictAnalysis(
                snapshot.SnapshotId,
                snapshot.InstanceName,
                snapshot.ProfileName,
                Array.Empty<BaseDataFileObservation>(),
                Array.Empty<SemanticConflictGroup>(),
                Array.Empty<Diagnostic>());
        }
        else
        {
            analysis = SevenDaysToDieConflictAnalyzer.Analyze(
                snapshot,
                new SevenDaysToDieBaseDataSource(baseData.DataConfigPath),
                cancellationToken);
        }

        var document = new RuntimeOcdAdapter().Import(
            new RuntimeOcdImportRequest(
                runtimeEvidence.SnapshotId,
                runtimeEvidence.RuntimeOcdLogsPath,
                runtimeEvidence.ToolVersion,
                runtimeEvidence.GameVersion,
                runtimeEvidence.CapturedAtUtc),
            analysis,
            cancellationToken);
        var comparison = RuntimeEvidenceComparison.Compare(analysis, document);

        var targetFilter = query.TargetXml is null
            ? null
            : NormalizeConflictTarget(query.TargetXml);
        var xpathFilter = query.XPath?.Trim();
        var categoryFilter = query.ObservedCategory?.Trim();
        var filteredItems = comparison.Items
            .Where(item => targetFilter is null
                || string.Equals(item.TargetXml, targetFilter, StringComparison.Ordinal))
            .Where(item => xpathFilter is null
                || string.Equals(item.XPath, xpathFilter, StringComparison.Ordinal))
            .Where(item => categoryFilter is null
                || item.RuntimeObservations.Any(observation =>
                    string.Equals(observation.ObservedCategory, categoryFilter, StringComparison.OrdinalIgnoreCase)));

        if (query.Status is QueryRuntimeEvidenceComparisonStatus status)
        {
            filteredItems = filteredItems.Where(item =>
                QueryProjection.MapRuntimeEvidenceComparisonStatus(item.Status) == status);
        }

        if (query.Limit is int limit)
        {
            filteredItems = filteredItems.Take(limit);
        }

        return QueryProjection.RuntimeEvidenceComparison(comparison with
        {
            Items = filteredItems.ToList().AsReadOnly()
        });
    }

    private static RuntimeEvidenceDocument ToRuntimeEvidence(
        RuntimeEvidenceInput input,
        LocalModSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(input.SnapshotId))
        {
            throw new ArgumentException(
                "Runtime evidence must include an explicit snapshot ID.",
                nameof(input));
        }

        if (!string.Equals(input.SnapshotId, snapshot.SnapshotId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Runtime evidence must reference the currently loaded snapshot.",
                nameof(input));
        }

        ArgumentNullException.ThrowIfNull(input.Observations);
        var observations = input.Observations
            .Select(observation => new RuntimeEvidenceObservation(
                observation.ModKey,
                observation.TargetXml,
                observation.XPath,
                observation.ObservedOperation,
                observation.RawResult,
                observation.NormalizedAssessment is null
                    ? null
                    : ToLocalSemanticConflictAssessment(observation.NormalizedAssessment.Value),
                ToLocalSourceReference(observation.RawLogReference),
                (observation.Diagnostics ?? Array.Empty<DiagnosticReadModel>())
                    .Select(ToLocalDiagnostic)
                    .ToList()
                    .AsReadOnly(),
                observation.ObservedCategory))
            .ToList()
            .AsReadOnly();

        var diagnostics = (input.Diagnostics ?? Array.Empty<DiagnosticReadModel>())
            .Select(ToLocalDiagnostic)
            .ToList()
            .AsReadOnly();

        return new RuntimeEvidenceDocument(
            new RuntimeEvidenceBinding(
                snapshot.SnapshotId,
                snapshot.InstanceName,
                snapshot.ProfileName),
            input.ToolName,
            input.ToolVersion,
            input.GameVersion,
            input.CapturedAtUtc,
            observations,
            diagnostics);
    }

    private static Diagnostic ToLocalDiagnostic(DiagnosticReadModel diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new Diagnostic(
            diagnostic.Code,
            diagnostic.Severity switch
            {
                QueryDiagnosticSeverity.Info => DiagnosticSeverity.Info,
                QueryDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                QueryDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Severity, null)
            },
            diagnostic.Message,
            diagnostic.Source is null ? null : ToLocalSourceReference(diagnostic.Source),
            diagnostic.RawValue);
    }

    private static SourceReference ToLocalSourceReference(SourceReferenceReadModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.RelativePath)
            || Path.IsPathRooted(source.RelativePath))
        {
            throw new ArgumentException(
                "Runtime evidence source references must use a non-empty relative path.",
                nameof(source));
        }

        return new SourceReference(
            source.Kind switch
            {
                QuerySourceReferenceKind.ProfileFile => SourceReferenceKind.ProfileFile,
                QuerySourceReferenceKind.InstanceFile => SourceReferenceKind.InstanceFile,
                QuerySourceReferenceKind.ModDirectory => SourceReferenceKind.ModDirectory,
                QuerySourceReferenceKind.ModFile => SourceReferenceKind.ModFile,
                QuerySourceReferenceKind.GameDataFile => SourceReferenceKind.GameDataFile,
                QuerySourceReferenceKind.RuntimeLog => SourceReferenceKind.RuntimeLog,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, null)
            },
            source.RelativePath,
            source.LineNumber,
            source.ColumnNumber);
    }

    private static SemanticConflictAssessment ToLocalSemanticConflictAssessment(
        QuerySemanticConflictAssessment assessment)
    {
        return assessment switch
        {
            QuerySemanticConflictAssessment.Compatible => SemanticConflictAssessment.Compatible,
            QuerySemanticConflictAssessment.Conflict => SemanticConflictAssessment.Conflict,
            QuerySemanticConflictAssessment.Possible => SemanticConflictAssessment.Possible,
            QuerySemanticConflictAssessment.Unknown => SemanticConflictAssessment.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, null)
        };
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
            source.ModsPath)
        {
            ProfilesPath = source.ProfilesPath,
            GamePath = source.GamePath
                ?? Mo2SourceDiscovery.ReadConfiguredGamePath(source.InstanceRootPath),
            VersionEvidenceManifestPath = source.VersionEvidenceManifestPath
        };
    }

    private static IReadOnlyList<Mo2ProfileDefinition> OrderProfiles(
        IReadOnlyList<Mo2ProfileDefinition> profiles,
        string activeProfileName)
    {
        return profiles
            .OrderBy(profile =>
                string.Equals(profile.Name, activeProfileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
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
                    IsGameTargetReady(candidate.Source.GamePath),
                    candidate.Evidence
                        .Select(evidence => $"{evidence.Kind}:{evidence.EvidenceKind}")
                        .ToList()
                        .AsReadOnly(),
                    QueryProjection.Diagnostics(candidate.Diagnostics)))
                .ToList()
                .AsReadOnly());
    }

    private static bool IsGameTargetReady(string? gamePath)
    {
        return !string.IsNullOrWhiteSpace(gamePath)
            && Directory.Exists(gamePath)
            && File.Exists(Path.Combine(gamePath, "7DaysToDie.exe"));
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
            QueryProjection.Diagnostics(snapshot.Diagnostics))
        {
            VersionEvidenceManifest = QueryProjection.VersionEvidenceManifest(snapshot.VersionEvidenceManifest)
        };
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

    private static KnowledgeReferenceReadModel ToReferenceReadModel(
        LocalKnowledgeReference reference,
        LocalModSnapshot snapshot,
        ReferenceLookup lookup)
    {
        var owner = ResolveOwner(reference, lookup);
        var operation = ResolveOperation(reference, lookup);
        var diagnostics = new List<DiagnosticReadModel>();

        if (owner is null)
        {
            diagnostics.Add(new DiagnosticReadModel(
                "query.owner.unresolved",
                QueryDiagnosticSeverity.Warning,
                "The owner MOD for the indexed reference could not be resolved.",
                QueryProjection.Source(reference.Evidence.Source)));
        }

        if (operation is not null)
        {
            diagnostics.AddRange(QueryProjection.Diagnostics(operation.Operation.Diagnostics));
        }

        return new KnowledgeReferenceReadModel(
            QueryProjection.Node(reference.From),
            QueryProjection.Node(reference.To),
            QueryProjection.ReferenceRelation(reference.Relation),
            QueryProjection.Evidence(reference.Evidence),
            owner is null
                ? null
                : QueryProjection.ModReferenceContext(
                    owner,
                    snapshot.ProfileEntries.FirstOrDefault(entry =>
                        string.Equals(
                            entry.NormalizedModName,
                            owner.Mo2OuterDirectoryName ?? owner.DirectoryName,
                            StringComparison.OrdinalIgnoreCase))),
            operation is null ? null : QueryProjection.PatchOperation(operation.Operation),
            diagnostics.AsReadOnly());
    }

    private static ReferenceLookup BuildReferenceLookup(LocalModSnapshot snapshot)
    {
        var lookup = new ReferenceLookup();
        foreach (var mod in snapshot.Mods)
        {
            lookup.NodeOwners[new ReferenceNodeKey(LocalKnowledgeNodeKind.Mod, mod.ModKey)] = mod;

            foreach (var file in mod.Files)
            {
                lookup.NodeOwners[new ReferenceNodeKey(
                    LocalKnowledgeNodeKind.File,
                    BuildIndexedFileValue(mod, file.RelativePath))] = mod;
            }

            foreach (var xmlFile in mod.XmlFiles)
            {
                var fileValue = BuildIndexedFileValue(mod, xmlFile.RelativePath);
                lookup.NodeOwners[new ReferenceNodeKey(LocalKnowledgeNodeKind.XmlFile, fileValue)] = mod;

                foreach (var operation in xmlFile.PatchOperations)
                {
                    var operationValue = $"{fileValue}#{operation.ElementPath}";
                    var context = new ReferenceOperationContext(mod, operation);
                    lookup.NodeOwners[new ReferenceNodeKey(
                        LocalKnowledgeNodeKind.PatchOperation,
                        operationValue)] = mod;
                    lookup.Operations[operationValue] = context;
                }
            }
        }

        return lookup;
    }

    private static LocalModRecord? ResolveOwner(
        LocalKnowledgeReference reference,
        ReferenceLookup lookup)
    {
        foreach (var node in new[] { reference.From, reference.To })
        {
            if (node.Kind == LocalKnowledgeNodeKind.PatchOperation
                && lookup.Operations.TryGetValue(node.Value, out var operation))
            {
                return operation.Mod;
            }

            if (lookup.NodeOwners.TryGetValue(new ReferenceNodeKey(node.Kind, node.Value), out var owner))
            {
                return owner;
            }
        }

        return null;
    }

    private static ReferenceOperationContext? ResolveOperation(
        LocalKnowledgeReference reference,
        ReferenceLookup lookup)
    {
        foreach (var node in new[] { reference.From, reference.To })
        {
            if (node.Kind == LocalKnowledgeNodeKind.PatchOperation
                && lookup.Operations.TryGetValue(node.Value, out var operation))
            {
                return operation;
            }
        }

        return null;
    }

    private static LocalKnowledgeNodeKind MapNodeKind(KnowledgeQueryNodeKind kind)
    {
        return kind switch
        {
            KnowledgeQueryNodeKind.Mod => LocalKnowledgeNodeKind.Mod,
            KnowledgeQueryNodeKind.File => LocalKnowledgeNodeKind.File,
            KnowledgeQueryNodeKind.XmlFile => LocalKnowledgeNodeKind.XmlFile,
            KnowledgeQueryNodeKind.PatchOperation => LocalKnowledgeNodeKind.PatchOperation,
            KnowledgeQueryNodeKind.TargetXml => LocalKnowledgeNodeKind.TargetXml,
            KnowledgeQueryNodeKind.XPath => LocalKnowledgeNodeKind.XPath,
            KnowledgeQueryNodeKind.Entity => LocalKnowledgeNodeKind.Entity,
            KnowledgeQueryNodeKind.Property => LocalKnowledgeNodeKind.Property,
            KnowledgeQueryNodeKind.Attribute => LocalKnowledgeNodeKind.Attribute,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string NormalizeQueryValue(
        KnowledgeQueryNodeKind kind,
        string value)
    {
        var normalized = value.Trim();
        if (kind is KnowledgeQueryNodeKind.Mod
            or KnowledgeQueryNodeKind.File
            or KnowledgeQueryNodeKind.XmlFile
            or KnowledgeQueryNodeKind.PatchOperation
            or KnowledgeQueryNodeKind.TargetXml)
        {
            normalized = normalized.Replace('\\', '/');
        }

        if (kind == KnowledgeQueryNodeKind.TargetXml
            && normalized.StartsWith("Config/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Config/".Length..];
        }

        return normalized;
    }

    private static string BuildIndexedFileValue(
        LocalModRecord mod,
        string relativePath)
    {
        var directoryPath = (mod.ResolvedDirectoryRelativePath ?? mod.DirectoryName)
            .Replace('\\', '/');
        var normalizedFilePath = relativePath.Replace('\\', '/');
        return $"mods/{directoryPath}/{normalizedFilePath}";
    }

    private static string NormalizeConflictTarget(string value)
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

    private sealed class ReferenceLookup
    {
        public Dictionary<ReferenceNodeKey, LocalModRecord> NodeOwners { get; } = new();

        public Dictionary<string, ReferenceOperationContext> Operations { get; } =
            new(StringComparer.Ordinal);
    }

    private readonly record struct ReferenceNodeKey(
        LocalKnowledgeNodeKind Kind,
        string Value);

    private sealed record ReferenceOperationContext(
        LocalModRecord Mod,
        XmlPatchOperationObservation Operation);
}

internal static class QueryProjection
{
    public static ModCandidateSummary ModCandidate(
        LocalModRecord record,
        ProfileModEntry? profileEntry)
    {
        return ModCandidate(
            record,
            profileEntry,
            new ModRoleReadModel(
                QueryModRole.Unknown,
                QueryModRoleAssessment.Unknown,
                "Role projection is not available in this reference context.",
                Array.Empty<ModRoleEvidenceReadModel>()));
    }

    public static ModCandidateSummary ModCandidate(
        LocalModRecord record,
        ProfileModEntry? profileEntry,
        ModRoleReadModel role)
    {
        return new ModCandidateSummary(
            record.ModKey,
            record.DirectoryName,
            record.ModInfo?.DisplayName ?? record.ModInfo?.Name,
            record.ModInfo?.Version,
            record.ModInfo?.Website,
            ProfileState(record.ProfileState),
            EnabledState(record.EnabledState),
            record.Priority,
            Source(record.Source),
            profileEntry is null
                ? null
                : new EvidenceReferenceReadModel(QueryEvidenceKind.Source, Source(profileEntry.Source)),
            role,
            Diagnostics(record.Diagnostics))
        {
            PackageRelation = PackageRelation(record.PackageEvidence)
        };
    }

    public static ModReferenceContextReadModel ModReferenceContext(
        LocalModRecord record,
        ProfileModEntry? profileEntry)
    {
        var candidate = ModCandidate(record, profileEntry);
        return new ModReferenceContextReadModel(
            candidate.ModKey,
            candidate.DirectoryName,
            candidate.DisplayName,
            candidate.Version,
            candidate.ProfileState,
            candidate.EnabledState,
            candidate.Priority,
            candidate.Source,
            candidate.PriorityEvidence,
            candidate.Diagnostics);
    }

    public static KnowledgeNodeReadModel Node(LocalKnowledgeNode node)
    {
        return new KnowledgeNodeReadModel(
            node.Kind switch
            {
                LocalKnowledgeNodeKind.Mod => KnowledgeQueryNodeKind.Mod,
                LocalKnowledgeNodeKind.File => KnowledgeQueryNodeKind.File,
                LocalKnowledgeNodeKind.XmlFile => KnowledgeQueryNodeKind.XmlFile,
                LocalKnowledgeNodeKind.PatchOperation => KnowledgeQueryNodeKind.PatchOperation,
                LocalKnowledgeNodeKind.TargetXml => KnowledgeQueryNodeKind.TargetXml,
                LocalKnowledgeNodeKind.XPath => KnowledgeQueryNodeKind.XPath,
                LocalKnowledgeNodeKind.Entity => KnowledgeQueryNodeKind.Entity,
                LocalKnowledgeNodeKind.Property => KnowledgeQueryNodeKind.Property,
                LocalKnowledgeNodeKind.Attribute => KnowledgeQueryNodeKind.Attribute,
                _ => throw new ArgumentOutOfRangeException(nameof(node), node.Kind, null)
            },
            node.Value);
    }

    public static KnowledgeReferenceRelation ReferenceRelation(LocalKnowledgeRelation relation)
    {
        return relation switch
        {
            LocalKnowledgeRelation.Contains => KnowledgeReferenceRelation.Contains,
            LocalKnowledgeRelation.Targets => KnowledgeReferenceRelation.Targets,
            LocalKnowledgeRelation.Selects => KnowledgeReferenceRelation.Selects,
            LocalKnowledgeRelation.Mentions => KnowledgeReferenceRelation.Mentions,
            _ => throw new ArgumentOutOfRangeException(nameof(relation), relation, null)
        };
    }

    public static EvidenceReferenceReadModel Evidence(EvidenceReference evidence)
    {
        return new EvidenceReferenceReadModel(
            MapEvidenceKind(evidence.Kind),
            Source(evidence.Source));
    }

    public static VersionEvidenceManifestReadModel? VersionEvidenceManifest(
        VersionEvidenceManifestLoadResult? manifest)
    {
        if (manifest is null)
        {
            return null;
        }

        var status = manifest.IsLoaded
            ? "loaded"
            : manifest.Diagnostics.Count == 0
                ? "not-selected"
                : "invalid";
        return new VersionEvidenceManifestReadModel(
            manifest.IsLoaded,
            manifest.DisplayName,
            status,
            Diagnostics(manifest.Diagnostics));
    }

    public static PackageRelationReadModel? PackageRelation(PackageVersionEvidence? evidence)
    {
        if (evidence is null)
        {
            return null;
        }

        return new PackageRelationReadModel(
            evidence.Package.DirectoryName,
            evidence.Package.ModletCount,
            evidence.Package.ModletCount > 1,
            evidence.IdentityState switch
            {
                IdentityResolutionState.Exact => QueryIdentityResolutionState.Exact,
                IdentityResolutionState.Ambiguous => QueryIdentityResolutionState.Ambiguous,
                IdentityResolutionState.Missing => QueryIdentityResolutionState.Missing,
                IdentityResolutionState.Conflicting => QueryIdentityResolutionState.Conflicting,
                IdentityResolutionState.Unresolved => QueryIdentityResolutionState.Unresolved,
                _ => throw new ArgumentOutOfRangeException(nameof(evidence), evidence.IdentityState, null)
            },
            evidence.IdentityReason,
            evidence.Package.Metadata.ParseStatus.ToString(),
            evidence.Package.Metadata.ModId,
            evidence.Package.Metadata.FileId,
            evidence.Package.Metadata.Version,
            Source(evidence.Package.Source),
            evidence.SourceArtifacts.Select(SourceArtifact).ToList().AsReadOnly(),
            evidence.VersionObservations.Select(VersionObservation).ToList().AsReadOnly(),
            VersionComparison(evidence.Comparison),
            Diagnostics(evidence.Diagnostics));
    }

    private static SourceArtifactReadModel SourceArtifact(SourceArtifact value)
    {
        return new SourceArtifactReadModel(
            value.ArtifactId,
            value.Kind,
            value.Name,
            value.ModId,
            value.FileId,
            value.SourceUrl,
            Source(value.Source));
    }

    public static VersionObservationReadModel VersionObservation(VersionObservation value)
    {
        return new VersionObservationReadModel(
            value.OwnerKey,
            value.Role.ToString(),
            value.SourceKind.ToString(),
            value.Normalization,
            Source(value.Source),
            value.ObservedAtUtc,
            Diagnostics(value.Diagnostics));
    }

    private static VersionComparisonReadModel VersionComparison(VersionComparison value)
    {
        return new VersionComparisonReadModel(
            value.Status switch
            {
                VersionComparisonStatus.Equal => QueryVersionComparisonStatus.Equal,
                VersionComparisonStatus.Mismatch => QueryVersionComparisonStatus.Mismatch,
                VersionComparisonStatus.NotComparable => QueryVersionComparisonStatus.NotComparable,
                VersionComparisonStatus.NotAssessed => QueryVersionComparisonStatus.NotAssessed,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value.Status, null)
            },
            value.Reason,
            value.Observations.Select(VersionObservation).ToList().AsReadOnly());
    }

    public static XmlPatchOperationReadModel PatchOperation(
        XmlPatchOperationObservation operation)
    {
        return new XmlPatchOperationReadModel(
            operation.ElementPath,
            operation.RawOperationName,
            operation.NormalizedKind is null
                ? null
                : MapPatchOperationKind(operation.NormalizedKind.Value),
            RawXmlObservation(operation.RawObservation),
            operation.XPathCandidates
                .Select(candidate => new XmlXPathCandidateReadModel(
                    candidate.RawValue,
                    candidate.ElementPath,
                    Source(candidate.Source)))
                .ToList()
                .AsReadOnly(),
            operation.TargetXmlCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            operation.EntityCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            operation.PropertyCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            operation.AttributeCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            Diagnostics(operation.Diagnostics),
            Source(operation.Source));
    }

    public static XmlReferenceCandidateReadModel ReferenceCandidate(
        XmlReferenceCandidate candidate)
    {
        return new XmlReferenceCandidateReadModel(
            candidate.RawValue,
            candidate.NormalizedValue,
            candidate.ElementPath,
            MapEvidenceKind(candidate.EvidenceKind),
            Source(candidate.Source));
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
            Source(file.Source))
        {
            PatchOperations = file.PatchOperations
                .Select(PatchOperation)
                .ToList()
                .AsReadOnly()
        };
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
            Source(observation.Source))
        {
            HasChildElements = observation.HasChildElements
        };
    }

    public static BaseDataFileReadModel BaseDataFile(BaseDataFileObservation file)
    {
        return new BaseDataFileReadModel(
            file.TargetXml,
            file.Size,
            file.Sha256,
            file.ParseStatus is null ? null : MapXmlParseStatus(file.ParseStatus.Value),
            Source(file.Source),
            Diagnostics(file.Diagnostics));
    }

    public static SemanticConflictOperationReadModel SemanticConflictOperation(
        ModScope.LocalKnowledge.SemanticConflictOperation operation)
    {
        return new SemanticConflictOperationReadModel(
            operation.OperationKey,
            operation.ModKey,
            operation.Priority,
            operation.XmlFileRelativePath,
            operation.ElementPath,
            operation.RawOperationName,
            operation.NormalizedKind is null ? null : MapPatchOperationKind(operation.NormalizedKind.Value),
            operation.TargetXml,
            operation.XPath,
            operation.AttributeName,
            operation.Value,
            Source(operation.Source),
            operation.Evidence.Select(Evidence).ToList().AsReadOnly(),
            Diagnostics(operation.Diagnostics))
        {
            HasChildElements = operation.HasChildElements
        };
    }

    public static EffectiveChangeReadModel EffectiveChange(EffectiveChange change)
    {
        return new EffectiveChangeReadModel(
            change.MatchPath,
            change.AttributeName,
            change.BeforeValue,
            change.AfterValue,
            change.ExistedBefore,
            change.ExistsAfter,
            Source(change.Source));
    }

    public static SemanticConflictGroupReadModel SemanticConflictGroup(
        ModScope.LocalKnowledge.SemanticConflictGroup group)
    {
        return new SemanticConflictGroupReadModel(
            group.TargetXml,
            group.XPath,
            MapSemanticConflictAssessment(group.Assessment),
            MapSemanticConflictConfidence(group.Confidence),
            MapEffectiveResultStatus(group.EffectiveStatus),
            group.OperationSequence.Select(SemanticConflictOperation).ToList().AsReadOnly(),
            group.EffectiveChanges.Select(EffectiveChange).ToList().AsReadOnly(),
            group.Evidence.Select(Evidence).ToList().AsReadOnly(),
            group.Uncertainties,
            Diagnostics(group.Diagnostics));
    }

    public static ConflictAnalysisReadModel ConflictAnalysis(
        ModScope.LocalKnowledge.SemanticConflictAnalysis analysis)
    {
        return new ConflictAnalysisReadModel(
            analysis.SnapshotId,
            analysis.InstanceName,
            analysis.ProfileName,
            analysis.BaseFiles.Select(BaseDataFile).ToList().AsReadOnly(),
            analysis.Groups.Select(SemanticConflictGroup).ToList().AsReadOnly(),
            Diagnostics(analysis.Diagnostics));
    }

    public static RuntimeEvidenceObservationReadModel RuntimeEvidenceObservation(
        ModScope.LocalKnowledge.RuntimeEvidenceObservation observation)
    {
        return new RuntimeEvidenceObservationReadModel(
            observation.ModKey,
            observation.TargetXml,
            observation.XPath,
            observation.ObservedOperation,
            observation.ObservedCategory,
            observation.NormalizedAssessment is null
                ? null
                : MapSemanticConflictAssessment(observation.NormalizedAssessment.Value),
            RuntimeDiagnostics(observation.Diagnostics));
    }

    public static RuntimeEvidenceReadModel RuntimeEvidence(
        ModScope.LocalKnowledge.RuntimeEvidenceDocument runtimeEvidence)
    {
        return new RuntimeEvidenceReadModel(
            runtimeEvidence.SnapshotId,
            runtimeEvidence.InstanceName,
            runtimeEvidence.ProfileName,
            runtimeEvidence.ToolName,
            runtimeEvidence.ToolVersion,
            runtimeEvidence.GameVersion,
            runtimeEvidence.CapturedAtUtc,
            runtimeEvidence.Observations
                .Select(RuntimeEvidenceObservation)
                .ToList()
                .AsReadOnly(),
            RuntimeDiagnostics(runtimeEvidence.Diagnostics));
    }

    public static RuntimeEvidenceComparisonItemReadModel RuntimeEvidenceComparisonItem(
        ModScope.LocalKnowledge.RuntimeEvidenceComparisonItem item)
    {
        return new RuntimeEvidenceComparisonItemReadModel(
            item.TargetXml,
            item.XPath,
            MapRuntimeEvidenceComparisonStatus(item.Status),
            item.StaticAssessment is null
                ? null
                : MapSemanticConflictAssessment(item.StaticAssessment.Value),
            item.RuntimeAssessment is null
                ? null
                : MapSemanticConflictAssessment(item.RuntimeAssessment.Value),
            item.RuntimeObservations
                .Select(RuntimeEvidenceObservation)
                .ToList()
                .AsReadOnly(),
            RuntimeDiagnostics(item.Diagnostics));
    }

    public static RuntimeEvidenceComparisonReadModel RuntimeEvidenceComparison(
        ModScope.LocalKnowledge.RuntimeEvidenceComparison comparison)
    {
        return new RuntimeEvidenceComparisonReadModel(
            comparison.SnapshotId,
            comparison.InstanceName,
            comparison.ProfileName,
            RuntimeEvidence(comparison.RuntimeEvidence),
            comparison.Items
                .Select(RuntimeEvidenceComparisonItem)
                .ToList()
                .AsReadOnly(),
            RuntimeDiagnostics(comparison.Diagnostics));
    }

    public static IReadOnlyList<DiagnosticReadModel> Diagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics.Select(Diagnostic).ToList().AsReadOnly();
    }

    public static IReadOnlyList<DiagnosticReadModel> RuntimeDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Select(diagnostic =>
            {
                var projected = Diagnostic(diagnostic);
                return projected with
                {
                    Source = diagnostic.Source?.Kind == SourceReferenceKind.RuntimeLog
                        ? null
                        : projected.Source,
                    RawValue = null
                };
            })
            .ToList()
            .AsReadOnly();
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
                SourceReferenceKind.GameDataFile => QuerySourceReferenceKind.GameDataFile,
                SourceReferenceKind.RuntimeLog => QuerySourceReferenceKind.RuntimeLog,
                SourceReferenceKind.PackageFile => QuerySourceReferenceKind.PackageFile,
                SourceReferenceKind.EvidenceManifest => QuerySourceReferenceKind.EvidenceManifest,
                SourceReferenceKind.WebObservation => QuerySourceReferenceKind.WebObservation,
                SourceReferenceKind.NexusApi => QuerySourceReferenceKind.NexusApi,
                SourceReferenceKind.Diagnostic => QuerySourceReferenceKind.Diagnostic,
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

    private static QueryXmlPatchOperationKind MapPatchOperationKind(
        XmlPatchOperationKind kind)
    {
        return kind switch
        {
            XmlPatchOperationKind.Set => QueryXmlPatchOperationKind.Set,
            XmlPatchOperationKind.SetAttribute => QueryXmlPatchOperationKind.SetAttribute,
            XmlPatchOperationKind.Remove => QueryXmlPatchOperationKind.Remove,
            XmlPatchOperationKind.RemoveAttribute => QueryXmlPatchOperationKind.RemoveAttribute,
            XmlPatchOperationKind.Append => QueryXmlPatchOperationKind.Append,
            XmlPatchOperationKind.Prepend => QueryXmlPatchOperationKind.Prepend,
            XmlPatchOperationKind.InsertBefore => QueryXmlPatchOperationKind.InsertBefore,
            XmlPatchOperationKind.InsertAfter => QueryXmlPatchOperationKind.InsertAfter,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static QuerySemanticConflictAssessment MapSemanticConflictAssessment(
        SemanticConflictAssessment assessment)
    {
        return assessment switch
        {
            SemanticConflictAssessment.Compatible => QuerySemanticConflictAssessment.Compatible,
            SemanticConflictAssessment.Conflict => QuerySemanticConflictAssessment.Conflict,
            SemanticConflictAssessment.Possible => QuerySemanticConflictAssessment.Possible,
            SemanticConflictAssessment.Unknown => QuerySemanticConflictAssessment.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, null)
        };
    }

    private static QuerySemanticConflictConfidence MapSemanticConflictConfidence(
        SemanticConflictConfidence confidence)
    {
        return confidence switch
        {
            SemanticConflictConfidence.High => QuerySemanticConflictConfidence.High,
            SemanticConflictConfidence.Medium => QuerySemanticConflictConfidence.Medium,
            SemanticConflictConfidence.Unknown => QuerySemanticConflictConfidence.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(confidence), confidence, null)
        };
    }

    private static QueryEffectiveResultStatus MapEffectiveResultStatus(
        EffectiveResultStatus status)
    {
        return status switch
        {
            EffectiveResultStatus.Computed => QueryEffectiveResultStatus.Computed,
            EffectiveResultStatus.Unknown => QueryEffectiveResultStatus.Unknown,
            EffectiveResultStatus.NotAssessed => QueryEffectiveResultStatus.NotAssessed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    public static QueryRuntimeEvidenceComparisonStatus MapRuntimeEvidenceComparisonStatus(
        RuntimeEvidenceComparisonStatus status)
    {
        return status switch
        {
            RuntimeEvidenceComparisonStatus.Match => QueryRuntimeEvidenceComparisonStatus.Match,
            RuntimeEvidenceComparisonStatus.Different => QueryRuntimeEvidenceComparisonStatus.Different,
            RuntimeEvidenceComparisonStatus.InferredMatch => QueryRuntimeEvidenceComparisonStatus.InferredMatch,
            RuntimeEvidenceComparisonStatus.InferredDifferent => QueryRuntimeEvidenceComparisonStatus.InferredDifferent,
            RuntimeEvidenceComparisonStatus.RuntimeOnly => QueryRuntimeEvidenceComparisonStatus.RuntimeOnly,
            RuntimeEvidenceComparisonStatus.StaticOnly => QueryRuntimeEvidenceComparisonStatus.StaticOnly,
            RuntimeEvidenceComparisonStatus.Unknown => QueryRuntimeEvidenceComparisonStatus.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
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
