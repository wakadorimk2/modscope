using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ModScope.Query;

namespace ModScope.Desktop;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ILocalKnowledgeQuery _query;
    private KnowledgeSessionReadModel? _session;
    private PageObservation? _pageObservation;
    private ModCandidateSummary? _selectedCandidate;
    private LocalContextReadModel? _localContext;
    private InspectorReadModel? _inspector;
    private string _candidateIdentity = string.Empty;
    private string _statusMessage = "Load an explicit MO2 source or the synthetic fixture.";
    private string _url = "about:blank";
    private string _instanceName = "synthetic-instance";
    private string _profileName = "default";
    private string _instanceRootPath = string.Empty;
    private string _profilePath = string.Empty;
    private string _modsPath = string.Empty;

    public MainWindowViewModel()
        : this(LocalKnowledgeQueryService.CreateDefault())
    {
    }

    internal MainWindowViewModel(ILocalKnowledgeQuery query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModCandidateSummary> Candidates { get; } = new();

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string InstanceName
    {
        get => _instanceName;
        set => SetProperty(ref _instanceName, value);
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string InstanceRootPath
    {
        get => _instanceRootPath;
        set => SetProperty(ref _instanceRootPath, value);
    }

    public string ProfilePath
    {
        get => _profilePath;
        set => SetProperty(ref _profilePath, value);
    }

    public string ModsPath
    {
        get => _modsPath;
        set => SetProperty(ref _modsPath, value);
    }

    public string CandidateIdentity
    {
        get => _candidateIdentity;
        set => SetProperty(ref _candidateIdentity, value);
    }

    public ModCandidateSummary? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (!SetProperty(ref _selectedCandidate, value) || value is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(CandidateIdentity))
            {
                CandidateIdentity = value.DisplayName ?? value.DirectoryName;
            }
        }
    }

    public LocalContextReadModel? LocalContext
    {
        get => _localContext;
        private set => SetProperty(ref _localContext, value);
    }

    public InspectorReadModel? Inspector
    {
        get => _inspector;
        private set => SetProperty(ref _inspector, value);
    }

    public string SessionSummary => _session is null
        ? "Local Knowledge: not loaded"
        : $"Profile: {_session.InstanceName} / {_session.ProfileName} · Snapshot: {_session.SnapshotId}";

    public string ObservationSummary => _pageObservation is null
        ? "Page observation: not captured"
        : $"Page: {_pageObservation.Title} · {_pageObservation.Url} · {_pageObservation.ExtractionStatus}";

    public string PageContentPreview => _pageObservation?.BoundedContentPreview ?? string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void SetPageObservation(PageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _pageObservation = observation;
        Url = observation.Url.ToString();
        OnPropertyChanged(nameof(ObservationSummary));
        OnPropertyChanged(nameof(PageContentPreview));
        StatusMessage = $"Observed {observation.Url}. Identity confirmation is still required.";
    }

    public void LoadSyntheticFixture()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "7dtd-mo2-minimal");
        InstanceName = "synthetic-instance";
        ProfileName = "default";
        InstanceRootPath = root;
        ProfilePath = Path.Combine(root, "profile");
        ModsPath = Path.Combine(root, "mods");
        StatusMessage = "Synthetic fixture paths loaded. Select Load source.";
    }

    public bool TryLoadSource(out string error)
    {
        error = string.Empty;
        var paths = new[]
        {
            (Name: "instance root", Value: InstanceRootPath),
            (Name: "profile", Value: ProfilePath),
            (Name: "mods", Value: ModsPath)
        };

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path.Value) || !Directory.Exists(path.Value))
            {
                error = $"The explicit {path.Name} path does not exist: {path.Value}";
                StatusMessage = error;
                return false;
            }
        }

        try
        {
            _session = _query.Load(new Mo2SourceInput(
                InstanceName,
                ProfileName,
                InstanceRootPath,
                ProfilePath,
                ModsPath));
            Candidates.Clear();
            foreach (var candidate in _query.GetModCandidates())
            {
                Candidates.Add(candidate);
            }

            SelectedCandidate = null;
            CandidateIdentity = string.Empty;
            LocalContext = null;
            Inspector = null;
            OnPropertyChanged(nameof(SessionSummary));
            StatusMessage = $"Loaded {Candidates.Count} MOD records. Confirm the page identity manually.";
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            StatusMessage = $"Load failed: {error}";
            return false;
        }
    }

    public bool TryConfirmIdentity(bool noLocalMatch, out string error)
    {
        error = string.Empty;
        if (_pageObservation is null)
        {
            error = "Observe the current page before confirming MOD identity.";
            StatusMessage = error;
            return false;
        }

        if (string.IsNullOrWhiteSpace(CandidateIdentity))
        {
            error = "Enter or select the page MOD identity.";
            StatusMessage = error;
            return false;
        }

        if (!noLocalMatch && SelectedCandidate is null)
        {
            error = "Select a local MOD record, or use Confirm not installed.";
            StatusMessage = error;
            return false;
        }

        LocalContext = _query.ConfirmIdentity(new IdentityConfirmation(
            _pageObservation,
            CandidateIdentity,
            noLocalMatch ? null : SelectedCandidate!.ModKey));

        Inspector = LocalContext.Status == LocalContextStatus.Installed && LocalContext.LocalModKey is not null
            ? _query.GetInspector(LocalContext.LocalModKey)
            : null;

        StatusMessage = $"Identity confirmed as {LocalContext.Status}.";
        return true;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
