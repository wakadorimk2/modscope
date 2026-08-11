using System.Windows;

namespace ModScope.Desktop;

public partial class App : Application
{
    static App()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            Environment.SetEnvironmentVariable(
                "windir",
                Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows");
        }
    }
}
