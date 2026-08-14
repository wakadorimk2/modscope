using System.IO;
using System.Text.Json;
using ModScope.Desktop.Contracts;

namespace ModScope.Desktop;

internal sealed record BrowserTabRestoreState(
    string TabId,
    string Title,
    string Url);

internal sealed record BrowserPersistedState(
    IReadOnlyList<BrowserTabRestoreState> Tabs,
    string? ActiveTabId,
    IReadOnlyList<BrowserHistoryEntryUiState> History);

internal sealed class BrowserStateStore
{
    private const int MaxHistoryEntries = 100;
    private const int MaxRestoredTabs = 8;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _path;

    public BrowserStateStore(string path)
    {
        _path = path;
    }

    public static BrowserStateStore CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new BrowserStateStore(Path.Combine(localAppData, "ModScope", "browser-state.json"));
    }

    public BrowserPersistedState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return Empty();
            }

            var json = File.ReadAllText(_path);
            var value = JsonSerializer.Deserialize<BrowserPersistedState>(json, JsonOptions);
            if (value is null)
            {
                return Empty();
            }

            var tabs = value.Tabs
                .Where(tab => IsPersistableUrl(tab.Url))
                .Take(MaxRestoredTabs)
                .ToList()
                .AsReadOnly();
            var tabIds = tabs.Select(tab => tab.TabId).ToHashSet(StringComparer.Ordinal);
            var history = value.History
                .Where(entry => IsPersistableUrl(entry.Url))
                .Take(MaxHistoryEntries)
                .ToList()
                .AsReadOnly();
            var activeTabId = value.ActiveTabId is not null && tabIds.Contains(value.ActiveTabId)
                ? value.ActiveTabId
                : tabs.FirstOrDefault()?.TabId;

            return new BrowserPersistedState(tabs, activeTabId, history);
        }
        catch
        {
            return Empty();
        }
    }

    public void Save(
        IEnumerable<BrowserTabUiState> tabs,
        string? activeTabId,
        IEnumerable<BrowserHistoryEntryUiState> history)
    {
        try
        {
            var persistedTabs = tabs
                .Where(tab => IsPersistableUrl(tab.Url))
                .Take(MaxRestoredTabs)
                .Select(tab => new BrowserTabRestoreState(tab.TabId, tab.Title, tab.Url))
                .ToList()
                .AsReadOnly();
            var persistedTabIds = persistedTabs.Select(tab => tab.TabId).ToHashSet(StringComparer.Ordinal);
            var persistedHistory = history
                .Where(entry => IsPersistableUrl(entry.Url))
                .Take(MaxHistoryEntries)
                .ToList()
                .AsReadOnly();
            var state = new BrowserPersistedState(
                persistedTabs,
                activeTabId is not null && persistedTabIds.Contains(activeTabId) ? activeTabId : null,
                persistedHistory);

            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Browser persistence is optional. A write failure must not block browsing.
        }
    }

    private static BrowserPersistedState Empty()
    {
        return new BrowserPersistedState(
            Array.Empty<BrowserTabRestoreState>(),
            null,
            Array.Empty<BrowserHistoryEntryUiState>());
    }

    private static bool IsPersistableUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && !string.Equals(uri.Host, "appassets.modscope", StringComparison.OrdinalIgnoreCase);
    }
}
