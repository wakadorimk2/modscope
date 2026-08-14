using System.Diagnostics;

namespace ModScope.Desktop;

internal interface IGameLauncher
{
    void Launch();
}

internal sealed class SteamGameLauncher : IGameLauncher
{
    internal const string GameUri = "steam://rungameid/251570";

    public void Launch()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = GameUri,
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Windows could not open the Steam game URI.");
        }
    }
}
