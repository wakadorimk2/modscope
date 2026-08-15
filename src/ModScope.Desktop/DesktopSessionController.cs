using System.IO;
using ModScope.Deployment;
using ModScope.Desktop.Contracts;
using ModScope.LocalKnowledge;
using ModScope.Query;

namespace ModScope.Desktop;

public sealed class DesktopSessionController
{
    private readonly ILocalKnowledgeQuery _query;
    private readonly IModDeploymentService _deployment;
    private readonly IGameLauncher _gameLauncher;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly SemaphoreSlim _analysisGate = new(1, 1);
    private KnowledgeSessionReadModel? _session;
    private PageObservation? _observation;
    private VersionObservationReadModel? _sessionWebVersionObservation;
    private IReadOnlyList<ModCandidateSummary> _candidates = Array.Empty<ModCandidateSummary>();
    private IReadOnlyList<ProfileSummaryReadModel> _profiles = Array.Empty<ProfileSummaryReadModel>();
    private SourceDiscoveryReadModel? _sourceDiscovery;
    private string? _selectedSourceCandidateId;
    private LocalContextReadModel? _localContext;
    private InspectorReadModel? _inspector;
    private string _candidateIdentity = string.Empty;
    private string? _selectedLocalModKey;
    private IReadOnlyList<LocalModMatchReadModel> _localModMatches =
        Array.Empty<LocalModMatchReadModel>();
    private string _recognitionStatus = "not-searched";
    private string? _autoInspectToken;
    private string _statusMessage = "Load a source and observe the current page.";
    private bool _contextVisible = true;
    private bool _modListVisible = true;
    private KnowledgeOperationUiState _operation = KnowledgeOperationUiState.Idle;
    private long _operationToken;
    private readonly Dictionary<string, string> _profileLoadStates =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _profilePreloadCancellation;
    private Task? _profilePreloadTask;
    private bool _startProfilePreloadAfterOperation;
    private string? _baseDataPath;
    private bool _baseDataIsInferred;
    private string? _runtimeLogsPath;
    private ConflictAnalysisReadModel? _conflictAnalysis;
    private RuntimeEvidenceComparisonReadModel? _runtimeComparison;
    private AnalysisOperationUiState _analysisOperation = AnalysisOperationUiState.Idle;
    private IReadOnlyList<DiagnosticUiState> _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
    private IReadOnlyList<ProfileEditEntryReadModel> _profileEditEntries =
        Array.Empty<ProfileEditEntryReadModel>();
    private DeploymentPlan? _deploymentPlan;
    private string _deploymentStatus = "idle";
    private bool _canLaunchGame;
    private IReadOnlyList<DeploymentDiagnostic> _deploymentDiagnostics =
        Array.Empty<DeploymentDiagnostic>();

    internal event EventHandler? OperationStateChanged;

    internal KnowledgeOperationUiState CurrentOperation => _operation;

    public DesktopSessionController()
        : this(
            LocalKnowledgeQueryService.CreateDefault(),
            new ModDeploymentService(),
            new SteamGameLauncher())
    {
    }

    internal DesktopSessionController(ILocalKnowledgeQuery query)
        : this(query, new ModDeploymentService(), new SteamGameLauncher())
    {
    }

    internal DesktopSessionController(
        ILocalKnowledgeQuery query,
        IModDeploymentService deployment,
        IGameLauncher gameLauncher)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        _gameLauncher = gameLauncher ?? throw new ArgumentNullException(nameof(gameLauncher));
    }

    public void UseFixture()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "7dtd-mo2-minimal");
        LoadSource(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        _statusMessage = "Synthetic Local Knowledge fixture loaded.";
    }

    public async Task<bool> UseFixtureAsync()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "7dtd-mo2-minimal");
        var loaded = await LoadSourceAsync(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        if (loaded)
        {
            _statusMessage = "Synthetic Local Knowledge fixture loaded.";
        }

        return loaded;
    }

    public async Task<bool> UseAnalysisFixtureAsync()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "7dtd-mo2-phase4");
        var loaded = await LoadSourceAsync(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        if (!loaded)
        {
            return false;
        }

        SetBaseDataPath(Path.Combine(root, "base", "Data", "Config"));
        SetRuntimeLogsPath(Path.Combine(root, "runtime-logs"));
        var conflicts = await AnalyzeConflictsAsync();
        var runtime = await CompareRuntimeEvidenceAsync("0.15.2", "7DTD-synthetic");
        return conflicts && runtime;
    }

    public void SetBaseDataPath(string path)
    {
        ThrowIfAnalysisBusy();
        _baseDataPath = ValidateDirectoryPath(path, "The base Data/Config path");
        _baseDataIsInferred = false;
        _conflictAnalysis = null;
        _runtimeComparison = null;
        _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
        _statusMessage = "Base Data/Config folder selected. Run an analysis.";
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetRuntimeLogsPath(string path)
    {
        ThrowIfAnalysisBusy();
        _runtimeLogsPath = ValidateDirectoryPath(path, "The runtime logs path");
        _runtimeComparison = null;
        _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
        _statusMessage = "Runtime logs folder selected. Run a comparison.";
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> AnalyzeConflictsAsync()
    {
        ThrowIfForegroundKnowledgeOperationBusy();
        if (!await _analysisGate.WaitAsync(0))
        {
            _statusMessage = "An analysis operation is already running.";
            _analysisDiagnostics = new[]
            {
                new DiagnosticUiState(
                    "analysis.busy",
                    "warning",
                    "An analysis operation is already running.")
            };
            OperationStateChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }

        SetAnalysisOperation(new AnalysisOperationUiState("conflict-analysis", true));
        try
        {
            if (_session is null)
            {
                throw new InvalidOperationException("Load Local Knowledge before analysis.");
            }

            var baseDataPath = GetReadyDirectory(_baseDataPath, "Select a base Data/Config folder first.");
            var result = await Task.Run(() => _query.AnalyzeConflicts(
                new SevenDaysToDieBaseDataInput(baseDataPath)));
            _conflictAnalysis = result;
            _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
            _statusMessage = "Conflict analysis completed.";
            return true;
        }
        catch (Exception exception)
        {
            var message = SanitizeAnalysisMessage(exception.Message);
            _statusMessage = $"Conflict analysis failed. Existing result was kept. {message}";
            _analysisDiagnostics = new[]
            {
                new DiagnosticUiState("analysis.conflict.failed", "error", message)
            };
            return false;
        }
        finally
        {
            SetAnalysisOperation(AnalysisOperationUiState.Idle);
            _analysisGate.Release();
        }
    }

    public async Task<bool> CompareRuntimeEvidenceAsync(string? toolVersion, string? gameVersion)
    {
        ThrowIfForegroundKnowledgeOperationBusy();
        if (!await _analysisGate.WaitAsync(0))
        {
            _statusMessage = "An analysis operation is already running.";
            _analysisDiagnostics = new[]
            {
                new DiagnosticUiState(
                    "analysis.busy",
                    "warning",
                    "An analysis operation is already running.")
            };
            OperationStateChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }

        SetAnalysisOperation(new AnalysisOperationUiState("runtime-comparison", true));
        try
        {
            if (_session is null)
            {
                throw new InvalidOperationException("Load Local Knowledge before comparison.");
            }

            var baseDataPath = GetReadyDirectory(_baseDataPath, "Select a base Data/Config folder first.");
            var runtimeLogsPath = GetReadyDirectory(_runtimeLogsPath, "Select a runtime logs folder first.");
            var runtime = new RuntimeOcdEvidenceInput(
                _session.SnapshotId,
                runtimeLogsPath,
                NormalizeOptionalVersion(toolVersion),
                NormalizeOptionalVersion(gameVersion),
                DateTimeOffset.UtcNow);
            var result = await Task.Run(() => _query.CompareRuntimeOcdEvidence(
                new SevenDaysToDieBaseDataInput(baseDataPath),
                runtime));
            _runtimeComparison = result;
            _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
            _statusMessage = "Runtime evidence comparison completed.";
            return true;
        }
        catch (Exception exception)
        {
            var message = SanitizeAnalysisMessage(exception.Message);
            _statusMessage = $"Runtime evidence comparison failed. Existing result was kept. {message}";
            _analysisDiagnostics = new[]
            {
                new DiagnosticUiState("analysis.runtime.failed", "error", message)
            };
            return false;
        }
        finally
        {
            SetAnalysisOperation(AnalysisOperationUiState.Idle);
            _analysisGate.Release();
        }
    }

    public void DiscoverSources(IReadOnlyList<string>? selectedRoots = null)
    {
        ThrowIfAnalysisBusy();
        _sourceDiscovery = _query.DiscoverSources(selectedRoots);
        _selectedSourceCandidateId = null;

        var readyCandidates = _sourceDiscovery.Candidates
            .Where(candidate => candidate.IsReady)
            .ToList();
        if (readyCandidates.Count == 1)
        {
            if (LoadSourceCandidate(readyCandidates[0].CandidateId))
            {
                _statusMessage = $"Detected MO2 source {readyCandidates[0].InstanceName} / {readyCandidates[0].ProfileName}.";
            }
        }
        else if (readyCandidates.Count > 1)
        {
            _statusMessage = $"Found {readyCandidates.Count} MO2 source candidates. Choose one.";
        }
        else
        {
            _statusMessage = "No ready 7 Days to Die MO2 source was found.";
        }
    }

    public async Task DiscoverSourcesAsync(IReadOnlyList<string>? selectedRoots = null)
    {
        ThrowIfAnalysisBusy();
        await StopBackgroundProfilePreloadAsync();
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("source-discovery", null);
        var progress = CreateProgressReporter(operationToken);
        var loaded = false;
        try
        {
            try
            {
                var sourceDiscovery = await Task.Run(() => _query.DiscoverSources(selectedRoots));
                _sourceDiscovery = sourceDiscovery;
                _selectedSourceCandidateId = null;

                var readyCandidates = sourceDiscovery.Candidates
                    .Where(candidate => candidate.IsReady)
                    .ToList();
                if (readyCandidates.Count == 1)
                {
                    SetOperationPhase(operationToken, "reading-profile", readyCandidates[0].ProfileName);
                    loaded = await LoadSourceCandidateCoreAsync(readyCandidates[0].CandidateId, progress);
                    if (loaded)
                    {
                        _statusMessage = $"Detected MO2 source {readyCandidates[0].InstanceName} / {readyCandidates[0].ProfileName}.";
                    }
                }
                else if (readyCandidates.Count > 1)
                {
                    _statusMessage = $"Found {readyCandidates.Count} MO2 source candidates. Choose one.";
                }
                else
                {
                    _statusMessage = "No ready 7 Days to Die MO2 source was found.";
                }
            }
            catch (Exception exception)
            {
                _statusMessage = $"MO2 source discovery failed. {exception.Message}";
            }
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }

        if (loaded)
        {
            await AnalyzeInferredBaseDataAsync();
        }
    }

    public bool LoadSourceCandidate(string candidateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ThrowIfAnalysisBusy();
        try
        {
            var session = _query.LoadSourceCandidate(candidateId);
            ApplyLoadedSession(session, candidateId.Trim());
            return true;
        }
        catch (Exception exception)
        {
            MarkCandidateLoadFailure(candidateId.Trim(), exception);
            return false;
        }
    }

    public async Task<bool> LoadSourceCandidateAsync(string candidateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ThrowIfAnalysisBusy();
        await StopBackgroundProfilePreloadAsync();
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("source-load", null);
        var progress = CreateProgressReporter(operationToken);
        var loaded = false;
        try
        {
            loaded = await LoadSourceCandidateCoreAsync(candidateId, progress);
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }

        if (loaded)
        {
            await AnalyzeInferredBaseDataAsync();
        }

        return loaded;
    }

    public async Task<bool> LoadSourceAsync(Mo2SourceInput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfAnalysisBusy();
        await StopBackgroundProfilePreloadAsync();
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("source-load", source.ProfileName);
        var progress = CreateProgressReporter(operationToken);
        var loaded = false;
        try
        {
            try
            {
                var session = await Task.Run(() => _query.Load(source, progress: progress));
                ApplyLoadedSession(session, null);
                _sourceDiscovery = null;
                loaded = true;
            }
            catch (Exception exception)
            {
                _statusMessage = $"MO2 source loading failed. Existing session was kept. {exception.Message}";
            }
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }

        if (loaded)
        {
            await AnalyzeInferredBaseDataAsync();
        }

        return loaded;
    }

    public void LoadSource(Mo2SourceInput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfAnalysisBusy();
        var session = _query.Load(source);
        ApplyLoadedSession(session, null);
        _sourceDiscovery = null;
        StartBackgroundProfilePreload();
    }

    public async Task<bool> LoadVersionEvidenceManifestAsync(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var source = _query.GetCurrentSource();
        if (source is null)
        {
            _statusMessage = "Load an explicit MO2 source before selecting a version evidence manifest.";
            return false;
        }

        var fullPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullPath))
        {
            _statusMessage = "The selected version evidence manifest does not exist.";
            return false;
        }

        return await LoadSourceAsync(source with
        {
            VersionEvidenceManifestPath = fullPath
        });
    }

    public void SwitchProfile(string profileName)
    {
        ThrowIfAnalysisBusy();
        var session = _query.SwitchProfile(profileName);
        ApplyProfileSession(session);
        StartBackgroundProfilePreload();
    }

    public async Task<bool> SwitchProfileAsync(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ThrowIfAnalysisBusy();
        await StopBackgroundProfilePreloadAsync();
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("profile-switch", profileName.Trim());
        var progress = CreateProgressReporter(operationToken);
        var switched = false;
        try
        {
            try
            {
                var session = await Task.Run(() => _query.SwitchProfile(profileName, progress: progress));
                ApplyProfileSession(session);
                switched = true;
            }
            catch (Exception exception)
            {
                _statusMessage = $"Profile loading failed. Existing session was kept. {exception.Message}";
            }
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }

        if (switched)
        {
            await AnalyzeInferredBaseDataAsync();
        }

        return switched;
    }

    public async Task<bool> PreviewDeploymentAsync(DeploymentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ThrowIfAnalysisBusy();
        await StopBackgroundProfilePreloadAsync();
        await _operationGate.WaitAsync();
        try
        {
            var source = _query.GetCurrentSource()
                ?? throw new InvalidOperationException("Load an explicit MO2 source before previewing deployment.");
            var definition = ToDefinition(source);
            var plan = await Task.Run(() => _deployment.Preview(definition, draft));
            _deploymentPlan = plan;
            _deploymentStatus = plan.CanApply ? "preview-ready" : "blocked";
            _deploymentDiagnostics = plan.Diagnostics;
            _canLaunchGame = false;
            _statusMessage = plan.CanApply
                ? "Deployment preview is ready for explicit approval."
                : "Deployment preview is blocked by diagnostics.";
            return plan.CanApply;
        }
        catch (Exception)
        {
            _deploymentPlan = null;
            _deploymentStatus = "blocked";
            _deploymentDiagnostics = new[]
            {
                new DeploymentDiagnostic(
                    "deployment.preview.failed",
                    "Deployment preview failed. Review the blocking diagnostics.",
                    true)
            };
            _canLaunchGame = false;
            _statusMessage = "Deployment preview failed.";
            return false;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<bool> ApplyDeploymentAsync(string planId, bool approved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        if (!approved)
        {
            _deploymentStatus = "blocked";
            _statusMessage = "Deployment approval was not provided.";
            return false;
        }

        ThrowIfAnalysisBusy();
        await StopBackgroundProfilePreloadAsync();
        await _operationGate.WaitAsync();
        try
        {
            if (_deploymentPlan is null
                || !string.Equals(_deploymentPlan.PlanId, planId.Trim(), StringComparison.Ordinal))
            {
                _deploymentStatus = "blocked";
                _statusMessage = "The deployment plan was not found. Preview the current state again.";
                return false;
            }

            var plan = _deploymentPlan;
            var source = _query.GetCurrentSource()
                ?? throw new InvalidOperationException("Load an explicit MO2 source before applying deployment.");
            var result = await Task.Run(() => _deployment.Apply(plan));
            _deploymentDiagnostics = result.Diagnostics;
            _deploymentStatus = result.Status switch
            {
                DeploymentResultStatus.Applied => "applied",
                DeploymentResultStatus.RecoveryRequired => "recovery-required",
                _ => "blocked"
            };
            _statusMessage = result.Message;

            if (result.Status == DeploymentResultStatus.Applied)
            {
                var session = await Task.Run(() => _query.Load(source));
                ApplyLoadedSession(session, _selectedSourceCandidateId);
                _deploymentStatus = "applied";
                _deploymentDiagnostics = result.Diagnostics;
                _canLaunchGame = true;
                _deploymentPlan = null;
                _profileEditEntries = _query.GetCurrentProfileEntries();
                _statusMessage = result.Message;
                return true;
            }

            _deploymentPlan = plan with
            {
                Diagnostics = result.Diagnostics
            };
            _canLaunchGame = false;
            return false;
        }
        catch (Exception)
        {
            _deploymentStatus = "recovery-required";
            _deploymentDiagnostics = new[]
            {
                new DeploymentDiagnostic(
                    "deployment.apply.failed",
                    "Deployment apply failed. Review the recovery diagnostics.",
                    true)
            };
            _canLaunchGame = false;
            _statusMessage = "Deployment apply failed.";
            return false;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public bool LaunchGame()
    {
        if (!_canLaunchGame)
        {
            _statusMessage = "Apply and verify the deployment before launching 7 Days to Die.";
            return false;
        }

        try
        {
            _gameLauncher.Launch();
            _statusMessage = "Steam launch requested for 7 Days to Die.";
            return true;
        }
        catch (Exception exception)
        {
            _deploymentDiagnostics = new[]
            {
                new DeploymentDiagnostic(
                    "game.launch.failed",
                    $"Steam launch failed: {exception.Message}.",
                    true)
            };
            _statusMessage = "Steam launch failed.";
            return false;
        }
    }

    public void SetContextVisible(bool visible)
    {
        _contextVisible = visible;
    }

    public void SetModListVisible(bool visible)
    {
        _modListVisible = visible;
    }

    public void SetObservation(PageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _observation = observation;
        _sessionWebVersionObservation = null;
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        RefreshPageRecognition();
    }

    public void SetSessionWebVersionObservation(string rawValue)
    {
        if (_observation is null || _inspector is null)
        {
            _statusMessage = "Open a local MOD Inspector before adding a Web version observation.";
            return;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.Length == 0)
        {
            _sessionWebVersionObservation = null;
            _statusMessage = "The session Web version observation was cleared.";
            return;
        }

        if (trimmed.Length > 100)
        {
            _statusMessage = "The Web version observation is too long.";
            return;
        }

        var normalized = ModScope.LocalKnowledge.VersionNormalizer.Normalize(trimmed, out var scheme);
        _sessionWebVersionObservation = new VersionObservationReadModel(
            _inspector.ModKey,
            "Release",
            "WebObservation",
            trimmed,
            normalized,
            scheme switch
            {
                ModScope.LocalKnowledge.VersionScheme.Semver => QueryVersionScheme.Semver,
                ModScope.LocalKnowledge.VersionScheme.NumericDotted => QueryVersionScheme.NumericDotted,
                _ => QueryVersionScheme.Unknown
            },
            new SourceReferenceReadModel(
                QuerySourceReferenceKind.WebObservation,
                $"web-session/{_observation.Url.Host}"),
            DateTimeOffset.UtcNow,
            Array.Empty<DiagnosticReadModel>());
        _statusMessage = "The session Web version observation was added.";
    }

    public void ConfirmIdentity(string candidateIdentity, string? localModKey)
    {
        if (_observation is null)
        {
            throw new InvalidOperationException("Observe the current page before confirming identity.");
        }

        if (string.IsNullOrWhiteSpace(candidateIdentity))
        {
            throw new ArgumentException("Enter or select the page MOD identity.", nameof(candidateIdentity));
        }

        _candidateIdentity = candidateIdentity.Trim();
        _selectedLocalModKey = localModKey;
        _localContext = _query.ConfirmIdentity(new IdentityConfirmation(
            _observation,
            _candidateIdentity,
            localModKey));
        _inspector = null;
        _recognitionStatus = "manual-confirmed";
        _autoInspectToken = null;
        _statusMessage = $"Identity confirmed as {_localContext.Status}.";
    }

    public void OpenInspector(string modKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modKey);
        _inspector = _query.GetInspector(modKey);
        _statusMessage = $"Inspector opened for {_inspector.DirectoryName}.";
    }

    public void SetStatus(string message)
    {
        _statusMessage = message;
    }

    public UiState BuildState(BrowserUiState browser)
    {
        return DesktopStateMapper.Map(
            browser,
            _observation,
            _sourceDiscovery,
            _selectedSourceCandidateId,
            _session,
            _candidates,
            _profiles,
            new IdentityUiState(
                _candidateIdentity,
                _selectedLocalModKey,
                _recognitionStatus,
                _localModMatches
                    .Select(DesktopStateMapper.LocalModMatch)
                    .ToList()
                    .AsReadOnly(),
                _autoInspectToken),
            _localContext,
            _inspector,
            DesktopStateMapper.MapAnalysis(
                _conflictAnalysis,
                _runtimeComparison,
                _baseDataPath is not null && Directory.Exists(_baseDataPath),
                _runtimeLogsPath is not null && Directory.Exists(_runtimeLogsPath),
                GetBaseDataStatus(),
                _analysisOperation,
                _analysisDiagnostics),
            new LayoutUiState(_contextVisible, _modListVisible),
            _statusMessage,
            _operation,
            _profileLoadStates,
            _profileEditEntries,
            _deploymentPlan,
            _deploymentStatus,
            _canLaunchGame,
            _deploymentDiagnostics,
            _sessionWebVersionObservation);
    }

    private async Task<bool> LoadSourceCandidateCoreAsync(
        string candidateId,
        IProgress<LocalKnowledgeProgress> progress)
    {
        try
        {
            var session = await Task.Run(() => _query.LoadSourceCandidate(
                candidateId,
                progress: progress));
            ApplyLoadedSession(session, candidateId.Trim());
            return true;
        }
        catch (Exception exception)
        {
            MarkCandidateLoadFailure(candidateId.Trim(), exception);
            return false;
        }
    }

    private long BeginOperation(string kind, string? targetProfileName)
    {
        var operationToken = Interlocked.Increment(ref _operationToken);
        _operation = new KnowledgeOperationUiState(
            kind,
            true,
            false,
            targetProfileName,
            InitialPhase(kind),
            null,
            null);
        _statusMessage = targetProfileName is null
            ? "Loading local MO2 knowledge."
            : $"Loading profile {targetProfileName}.";
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
        return operationToken;
    }

    private long BeginBackgroundOperation(
        string kind,
        string? targetProfileName,
        int total)
    {
        var operationToken = Interlocked.Increment(ref _operationToken);
        _operation = new KnowledgeOperationUiState(
            kind,
            true,
            true,
            targetProfileName,
            "preloading-profile",
            0,
            total);
        _statusMessage = "Preparing other profiles in the background.";
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
        return operationToken;
    }

    private void SetOperationPhase(
        long operationToken,
        string phase,
        string? targetProfileName = null)
    {
        if (operationToken != _operationToken || !_operation.IsBusy)
        {
            return;
        }

        _operation = _operation with
        {
            Phase = phase,
            TargetProfileName = targetProfileName ?? _operation.TargetProfileName,
            Completed = null,
            Total = null
        };
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyOperationProgress(
        long operationToken,
        LocalKnowledgeProgress progress)
    {
        if (operationToken != _operationToken || !_operation.IsBusy)
        {
            return;
        }

        var completed = progress.Completed;
        var total = progress.Total;
        if (total is int totalValue && totalValue < 0)
        {
            completed = null;
            total = null;
        }
        else if (completed is int completedValue && total is int boundedTotal)
        {
            completed = Math.Clamp(completedValue, 0, boundedTotal);
        }

        _operation = _operation with
        {
            Phase = progress.Phase,
            Completed = completed,
            Total = total
        };
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetBackgroundProgress(
        long operationToken,
        int completed,
        int total,
        string? targetProfileName = null)
    {
        if (operationToken != _operationToken || !_operation.IsBusy)
        {
            return;
        }

        _operation = _operation with
        {
            Phase = "preloading-profile",
            TargetProfileName = targetProfileName ?? _operation.TargetProfileName,
            Completed = Math.Clamp(completed, 0, total),
            Total = total
        };
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private IProgress<LocalKnowledgeProgress> CreateProgressReporter(long operationToken)
    {
        var uiProgress = new Progress<LocalKnowledgeProgress>(progress =>
            ApplyOperationProgress(operationToken, progress));
        return new ThrottledProgress(uiProgress);
    }

    private void EndOperation(long operationToken)
    {
        if (operationToken != _operationToken)
        {
            return;
        }

        _operation = KnowledgeOperationUiState.Idle;
        OperationStateChanged?.Invoke(this, EventArgs.Empty);

        if (_startProfilePreloadAfterOperation)
        {
            _startProfilePreloadAfterOperation = false;
            StartBackgroundProfilePreload();
        }
    }

    private void InitializeProfileLoadStates(string activeProfileName)
    {
        _profileLoadStates.Clear();
        foreach (var profile in _profiles)
        {
            _profileLoadStates[profile.ProfileName] = string.Equals(
                profile.ProfileName,
                activeProfileName,
                StringComparison.OrdinalIgnoreCase)
                ? "ready"
                : "pending";
        }
    }

    private void SetProfileLoadState(string profileName, string loadState)
    {
        _profileLoadStates[profileName] = loadState;
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StartBackgroundProfilePreload()
    {
        if (_session is null)
        {
            return;
        }

        var activeProfileName = _session.ProfileName;
        var pendingProfiles = _profiles
            .Where(profile => !string.Equals(
                profile.ProfileName,
                activeProfileName,
                StringComparison.OrdinalIgnoreCase))
            .Where(profile => !_profileLoadStates.TryGetValue(profile.ProfileName, out var state)
                || !string.Equals(state, "ready", StringComparison.OrdinalIgnoreCase))
            .Select(profile => profile.ProfileName)
            .ToList();
        if (pendingProfiles.Count == 0)
        {
            return;
        }

        _profilePreloadCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _profilePreloadCancellation = cancellation;
        var operationToken = BeginBackgroundOperation(
            "profile-preload",
            pendingProfiles[0],
            pendingProfiles.Count);
        _profilePreloadTask = Task.Run(async () =>
        {
            try
            {
                for (var index = 0; index < pendingProfiles.Count; index++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var profileName = pendingProfiles[index];
                    SetProfileLoadState(profileName, "loading");
                    SetBackgroundProgress(operationToken, index, pendingProfiles.Count, profileName);

                    try
                    {
                        await Task.Run(
                            () => _query.WarmProfile(profileName, cancellation.Token),
                            cancellation.Token);
                        SetProfileLoadState(profileName, "ready");
                        SetBackgroundProgress(operationToken, index + 1, pendingProfiles.Count, profileName);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        SetProfileLoadState(profileName, "pending");
                        throw;
                    }
                    catch (Exception exception)
                    {
                        SetProfileLoadState(profileName, "failed");
                        _statusMessage = $"Profile preload failed for {profileName}. {exception.Message}";
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            finally
            {
                EndOperation(operationToken);
                if (ReferenceEquals(_profilePreloadCancellation, cancellation))
                {
                    _profilePreloadCancellation = null;
                    _profilePreloadTask = null;
                }
                cancellation.Dispose();
            }
        }, cancellation.Token);
    }

    private async Task StopBackgroundProfilePreloadAsync()
    {
        var cancellation = _profilePreloadCancellation;
        var preloadTask = _profilePreloadTask;
        if (cancellation is null || preloadTask is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            await preloadTask;
        }
        catch (OperationCanceledException)
        {
        }

        _profilePreloadCancellation = null;
        _profilePreloadTask = null;
        foreach (var profile in _profileLoadStates.Keys.ToList())
        {
            if (string.Equals(_profileLoadStates[profile], "loading", StringComparison.OrdinalIgnoreCase))
            {
                _profileLoadStates[profile] = "pending";
            }
        }
    }

    private static string InitialPhase(string kind)
    {
        return string.Equals(kind, "source-discovery", StringComparison.Ordinal)
            ? "discovering-source"
            : "reading-profile";
    }

    private sealed class ThrottledProgress : IProgress<LocalKnowledgeProgress>
    {
        private readonly IProgress<LocalKnowledgeProgress> _inner;
        private readonly object _gate = new();
        private long _lastReportedAt;
        private string? _lastPhase;
        private int? _lastCompleted;

        public ThrottledProgress(IProgress<LocalKnowledgeProgress> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Report(LocalKnowledgeProgress value)
        {
            LocalKnowledgeProgress? report = null;
            lock (_gate)
            {
                var phaseChanged = !string.Equals(
                    _lastPhase,
                    value.Phase,
                    StringComparison.Ordinal);
                if (phaseChanged)
                {
                    _lastCompleted = null;
                }

                if (value.Completed is int completed
                    && _lastCompleted is int previousCompleted
                    && completed < previousCompleted)
                {
                    return;
                }

                if (value.Completed is int nextCompleted)
                {
                    _lastCompleted = nextCompleted;
                }

                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                var interval = System.Diagnostics.Stopwatch.Frequency / 20;
                var elapsed = _lastReportedAt == 0
                    ? long.MaxValue
                    : now - _lastReportedAt;
                var isTerminal = value.Completed is int terminalCompleted
                    && value.Total is int terminalTotal
                    && terminalCompleted >= terminalTotal;
                if (!phaseChanged && !isTerminal && elapsed < interval)
                {
                    return;
                }

                _lastPhase = value.Phase;
                _lastReportedAt = now;
                report = value;
            }

            _inner.Report(report);
        }
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
        };
    }

    private void ResetDeploymentState()
    {
        _profileEditEntries = _query.GetCurrentProfileEntries();
        _deploymentPlan = null;
        _deploymentStatus = "idle";
        _canLaunchGame = false;
        _deploymentDiagnostics = Array.Empty<DeploymentDiagnostic>();
    }

    private void ApplyProfileSession(KnowledgeSessionReadModel session)
    {
        _session = session;
        _candidates = _query.GetModCandidates();
        _profiles = _query.GetProfiles();
        InitializeProfileLoadStates(session.ProfileName);
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        ResetDeploymentState();
        _selectedSourceCandidateId = null;
        ClearAnalysis();
        ApplyInferredBaseData();
        RefreshPageRecognition();
        if (_observation is null)
        {
            _statusMessage = $"Switched to profile {session.ProfileName}. Observe a page to search local MODs.";
        }
        _startProfilePreloadAfterOperation = true;
    }

    private void MarkCandidateLoadFailure(string candidateId, Exception exception)
    {
        if (_sourceDiscovery is not null)
        {
            var failure = new DiagnosticReadModel(
                "mo2.source.load.failed",
                QueryDiagnosticSeverity.Error,
                "The MO2 source could not be loaded. Check the source card for details.",
                RawValue: exception.Message);
            var candidates = _sourceDiscovery.Candidates
                .Select(candidate => string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal)
                    ? candidate with
                    {
                        Readiness = "LoadFailed",
                        IsReady = false,
                        Diagnostics = candidate.Diagnostics
                            .Append(failure)
                            .ToList()
                            .AsReadOnly()
                    }
                    : candidate)
                .ToList()
                .AsReadOnly();
            _sourceDiscovery = new SourceDiscoveryReadModel(candidates);
        }

        _selectedSourceCandidateId = null;
        _statusMessage = "MO2 source loading failed. Review the source card and choose another source.";
    }

    private void ApplyLoadedSession(KnowledgeSessionReadModel session, string? candidateId)
    {
        _session = session;
        _sessionWebVersionObservation = null;
        _candidates = _query.GetModCandidates();
        _profiles = _query.GetProfiles();
        InitializeProfileLoadStates(session.ProfileName);
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _selectedSourceCandidateId = candidateId;
        ResetDeploymentState();
        ClearAnalysis();
        ApplyInferredBaseData();
        RefreshPageRecognition();
        if (_observation is null)
        {
            _statusMessage = $"Loaded {_candidates.Count} MOD records. Observe a page to search local MODs.";
        }
        _startProfilePreloadAfterOperation = true;
    }

    private void ApplyInferredBaseData()
    {
        var inferredPath = _query.GetInferredBaseDataConfigPath();
        if (inferredPath is not null && Directory.Exists(inferredPath))
        {
            _baseDataPath = inferredPath;
            _baseDataIsInferred = true;
            _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
            _statusMessage = "MO2 gamePath Data/Config detected. Static analysis will start.";
            return;
        }

        _baseDataPath = null;
        _baseDataIsInferred = false;
        _analysisDiagnostics = new[]
        {
            new DiagnosticUiState(
                "analysis.base-data.inferred-missing",
                "warning",
                "Data/Config was not found from MO2 gamePath. Select another Data/Config folder.")
        };
        _statusMessage = "Data/Config was not found from MO2 gamePath. Select another Data/Config folder.";
    }

    private async Task AnalyzeInferredBaseDataAsync()
    {
        if (!_baseDataIsInferred
            || _baseDataPath is null
            || !Directory.Exists(_baseDataPath))
        {
            return;
        }

        await AnalyzeConflictsAsync();
    }

    private void RefreshPageRecognition()
    {
        _localModMatches = Array.Empty<LocalModMatchReadModel>();
        _autoInspectToken = null;

        if (_observation is null)
        {
            _recognitionStatus = "not-searched";
            return;
        }

        if (_session is null)
        {
            _recognitionStatus = "source-not-loaded";
            _statusMessage = "Observe complete. Load Local Knowledge to search local MODs.";
            return;
        }

        _localModMatches = _query.FindLocalModMatches(_observation);
        var strongMatches = _localModMatches
            .Where(match => match.Strength == LocalModMatchStrength.Strong
                && match.AutoConfirmEligible)
            .GroupBy(match => match.ModKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (strongMatches.Count == 1)
        {
            var match = strongMatches[0];
            _candidateIdentity = match.DisplayName ?? match.DirectoryName;
            _selectedLocalModKey = match.ModKey;
            _localContext = _query.ConfirmIdentity(new IdentityConfirmation(
                _observation,
                _candidateIdentity,
                match.ModKey));
            _inspector = _query.GetInspector(match.ModKey);
            _recognitionStatus = "auto-confirmed";
            _autoInspectToken = Guid.NewGuid().ToString("N");
            _statusMessage = $"Local MOD identity auto-confirmed as {_candidateIdentity}. Inspector opened.";
            return;
        }

        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _recognitionStatus = _localModMatches.Count == 0 ? "no-match" : "candidates";
        _statusMessage = _localModMatches.Count == 0
            ? "No local MOD candidates matched the observed page."
            : "Local MOD candidates found. Confirm the page identity manually.";
    }

    private string GetBaseDataStatus()
    {
        if (_baseDataPath is null || !Directory.Exists(_baseDataPath))
        {
            return "missing";
        }

        return _baseDataIsInferred ? "inferred" : "manual";
    }

    private void ClearAnalysis()
    {
        _baseDataPath = null;
        _baseDataIsInferred = false;
        _runtimeLogsPath = null;
        _conflictAnalysis = null;
        _runtimeComparison = null;
        _analysisOperation = AnalysisOperationUiState.Idle;
        _analysisDiagnostics = Array.Empty<DiagnosticUiState>();
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetAnalysisOperation(AnalysisOperationUiState operation)
    {
        _analysisOperation = operation;
        _statusMessage = operation.IsBusy
            ? operation.Kind == "conflict-analysis"
                ? "Analyzing static XML conflicts."
                : "Comparing runtime evidence."
            : _statusMessage;
        OperationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfAnalysisBusy()
    {
        if (_analysisOperation.IsBusy)
        {
            throw new InvalidOperationException("Analysis is running. Wait until the analysis is complete.");
        }
    }

    private void ThrowIfForegroundKnowledgeOperationBusy()
    {
        if (_operation.IsBusy && !_operation.IsBackground)
        {
            throw new InvalidOperationException("Local Knowledge is loading. Wait until the operation is complete.");
        }
    }

    private static string ValidateDirectoryPath(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path.Trim());
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"{label} was not found.");
        }

        return fullPath;
    }

    private static string GetReadyDirectory(string? path, string message)
    {
        if (path is null || !Directory.Exists(path))
        {
            throw new InvalidOperationException(message);
        }

        return path;
    }

    private static string? NormalizeOptionalVersion(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string SanitizeAnalysisMessage(string message)
    {
        var sanitized = message;
        foreach (var path in new[] { _baseDataPath, _runtimeLogsPath }.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            sanitized = sanitized.Replace(path!, "[selected folder]", StringComparison.OrdinalIgnoreCase);
        }

        return sanitized;
    }
}
