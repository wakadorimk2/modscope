using System.IO;
using ModScope.Desktop.Contracts;
using ModScope.LocalKnowledge;
using ModScope.Query;

namespace ModScope.Desktop;

public sealed class DesktopSessionController
{
    private readonly ILocalKnowledgeQuery _query;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private KnowledgeSessionReadModel? _session;
    private PageObservation? _observation;
    private IReadOnlyList<ModCandidateSummary> _candidates = Array.Empty<ModCandidateSummary>();
    private IReadOnlyList<ProfileSummaryReadModel> _profiles = Array.Empty<ProfileSummaryReadModel>();
    private SourceDiscoveryReadModel? _sourceDiscovery;
    private string? _selectedSourceCandidateId;
    private LocalContextReadModel? _localContext;
    private InspectorReadModel? _inspector;
    private string _candidateIdentity = string.Empty;
    private string? _selectedLocalModKey;
    private string _statusMessage = "Load a source and observe the current page.";
    private bool _contextVisible = true;
    private KnowledgeOperationUiState _operation = KnowledgeOperationUiState.Idle;
    private long _operationToken;

    internal event EventHandler? OperationStateChanged;

    public DesktopSessionController()
        : this(LocalKnowledgeQueryService.CreateDefault())
    {
    }

    internal DesktopSessionController(ILocalKnowledgeQuery query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
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

    public void DiscoverSources(IReadOnlyList<string>? selectedRoots = null)
    {
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
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("source-discovery", null);
        var progress = CreateProgressReporter(operationToken);
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
                    if (await LoadSourceCandidateCoreAsync(readyCandidates[0].CandidateId, progress))
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
    }

    public bool LoadSourceCandidate(string candidateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
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
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("source-load", null);
        var progress = CreateProgressReporter(operationToken);
        try
        {
            return await LoadSourceCandidateCoreAsync(candidateId, progress);
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }
    }

    public async Task<bool> LoadSourceAsync(Mo2SourceInput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("source-load", source.ProfileName);
        var progress = CreateProgressReporter(operationToken);
        try
        {
            try
            {
                var session = await Task.Run(() => _query.Load(source, progress: progress));
                ApplyLoadedSession(session, null);
                _sourceDiscovery = null;
                return true;
            }
            catch (Exception exception)
            {
                _statusMessage = $"MO2 source loading failed. Existing session was kept. {exception.Message}";
                return false;
            }
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }
    }

    public void LoadSource(Mo2SourceInput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _session = _query.Load(source);
        _candidates = _query.GetModCandidates();
        _profiles = _query.GetProfiles();
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _sourceDiscovery = null;
        _selectedSourceCandidateId = null;
        _statusMessage = $"Loaded {_candidates.Count} MOD records. Confirm the page identity.";
    }

    public void SwitchProfile(string profileName)
    {
        var session = _query.SwitchProfile(profileName);
        _session = session;
        _candidates = _query.GetModCandidates();
        _profiles = _query.GetProfiles();
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _selectedSourceCandidateId = null;
        _statusMessage = $"Switched to profile {session.ProfileName}. Confirm the page identity.";
    }

    public async Task<bool> SwitchProfileAsync(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        await _operationGate.WaitAsync();
        var operationToken = BeginOperation("profile-switch", profileName.Trim());
        var progress = CreateProgressReporter(operationToken);
        try
        {
            try
            {
                var session = await Task.Run(() => _query.SwitchProfile(profileName, progress: progress));
                ApplyProfileSession(session);
                return true;
            }
            catch (Exception exception)
            {
                _statusMessage = $"Profile loading failed. Existing session was kept. {exception.Message}";
                return false;
            }
        }
        finally
        {
            EndOperation(operationToken);
            _operationGate.Release();
        }
    }

    public void SetContextVisible(bool visible)
    {
        _contextVisible = visible;
    }

    public void SetObservation(PageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _observation = observation;
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _statusMessage = $"Observed {observation.Url}. Identity confirmation is required.";
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
        _statusMessage = $"Identity confirmed as {_localContext.Status}.";
    }

    public void OpenInspector(string modKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modKey);
        if (_localContext?.Status != LocalContextStatus.Installed)
        {
            throw new InvalidOperationException("Inspector is available only for an installed MOD.");
        }

        if (!string.Equals(_localContext.LocalModKey, modKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested MOD is not the confirmed local MOD.");
        }

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
            new IdentityUiState(_candidateIdentity, _selectedLocalModKey),
            _localContext,
            _inspector,
            new LayoutUiState(_contextVisible),
            _statusMessage,
            _operation);
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

    private void ApplyProfileSession(KnowledgeSessionReadModel session)
    {
        _session = session;
        _candidates = _query.GetModCandidates();
        _profiles = _query.GetProfiles();
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _selectedSourceCandidateId = null;
        _statusMessage = $"Switched to profile {session.ProfileName}. Confirm the page identity.";
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
        _candidates = _query.GetModCandidates();
        _profiles = _query.GetProfiles();
        _candidateIdentity = string.Empty;
        _selectedLocalModKey = null;
        _localContext = null;
        _inspector = null;
        _selectedSourceCandidateId = candidateId;
        _statusMessage = $"Loaded {_candidates.Count} MOD records. Confirm the page identity.";
    }
}
