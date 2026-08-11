using System.IO;
using ModScope.Desktop.Contracts;
using ModScope.Query;

namespace ModScope.Desktop;

public sealed class DesktopSessionController
{
    private readonly ILocalKnowledgeQuery _query;
    private KnowledgeSessionReadModel? _session;
    private PageObservation? _observation;
    private IReadOnlyList<ModCandidateSummary> _candidates = Array.Empty<ModCandidateSummary>();
    private IReadOnlyList<ProfileSummaryReadModel> _profiles = Array.Empty<ProfileSummaryReadModel>();
    private LocalContextReadModel? _localContext;
    private InspectorReadModel? _inspector;
    private string _candidateIdentity = string.Empty;
    private string? _selectedLocalModKey;
    private string _statusMessage = "Load a source and observe the current page.";
    private bool _contextVisible = true;

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
        _statusMessage = $"Switched to profile {session.ProfileName}. Confirm the page identity.";
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
            _session,
            _candidates,
            _profiles,
            new IdentityUiState(_candidateIdentity, _selectedLocalModKey),
            _localContext,
            _inspector,
            new LayoutUiState(_contextVisible),
            _statusMessage);
    }
}
