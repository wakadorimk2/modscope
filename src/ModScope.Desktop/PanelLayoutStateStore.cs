using System.IO;
using System.Text.Json;

namespace ModScope.Desktop;

internal sealed record PanelLayoutPersistedState(
    int Version,
    bool ModListVisible,
    bool ContextVisible,
    double ModListWidth,
    double ContextWidth);

internal sealed class PanelLayoutStateStore
{
    internal const int CurrentVersion = 1;

    private const double ModListMinimumWidth = 220;
    private const double ModListMaximumWidth = 480;
    private const double ContextMinimumWidth = 240;
    private const double ContextMaximumWidth = 480;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _filePath;

    private PanelLayoutStateStore(string filePath)
    {
        _filePath = filePath;
    }

    internal static PanelLayoutStateStore CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new PanelLayoutStateStore(
            Path.Combine(localAppData, "ModScope", "panel-layout.json"));
    }

    internal PanelLayoutPersistedState? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<PanelLayoutPersistedState>(
                File.ReadAllText(_filePath),
                JsonOptions);
            return IsValid(state) ? state : null;
        }
        catch
        {
            return null;
        }
    }

    internal void Save(PanelLayoutPersistedState state)
    {
        if (!IsValid(state))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Layout persistence is optional and must not block the UI.
        }
    }

    private static bool IsValid(PanelLayoutPersistedState? state)
    {
        return state is not null
            && state.Version == CurrentVersion
            && IsValidWidth(
                state.ModListWidth,
                ModListMinimumWidth,
                ModListMaximumWidth)
            && IsValidWidth(
                state.ContextWidth,
                ContextMinimumWidth,
                ContextMaximumWidth);
    }

    private static bool IsValidWidth(double width, double minimum, double maximum)
    {
        return double.IsFinite(width)
            && width >= minimum
            && width <= maximum;
    }
}
