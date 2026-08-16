using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ModScope.Desktop;

public partial class App : Application
{
    private const string DetachedLaunchMarker = "MODSCOPE_DESKTOP_DETACHED";
    private const string DetachedLaunchMarkerValue = "1";
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private static int _unhandledExceptionReported;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    static App()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        WriteStartupDiagnostic("[ModScope] startup phase=static-constructor");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            Environment.SetEnvironmentVariable(
                "windir",
                Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows");
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (System.Threading.Interlocked.Exchange(ref _unhandledExceptionReported, 1) != 0
            || e.ExceptionObject is not Exception exception)
        {
            return;
        }

        WriteStartupDiagnostic(
            $"[ModScope] startup phase=unhandled type={exception.GetType().Name}"
            + $" hresult=0x{exception.HResult:X8}"
            + $" stack={GetDiagnosticStack(exception)}");
        MessageBox.Show(
            "ModScopeを安全に起動できませんでした。\n\n"
            + $"診断コード: {exception.GetType().Name} (0x{exception.HResult:X8})",
            "ModScope",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            WriteStartupDiagnostic("[ModScope] startup phase=on-startup");

            var decision = PrepareProcessBoundary(out var failureMessage);
            if (decision == ProcessBoundaryDecision.Relaunched)
            {
                Shutdown(0);
                return;
            }

            if (decision == ProcessBoundaryDecision.Failed)
            {
                MessageBox.Show(
                    failureMessage,
                    "ModScope",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(ShowMainWindow));
        }
        catch (Exception exception)
        {
            WriteStartupDiagnostic(
                $"[ModScope] startup phase=failed type={exception.GetType().Name}");
            MessageBox.Show(
                "ModScopeを安全に起動できませんでした。\n\n"
                + "ExplorerからModScopeを起動してください。",
                "ModScope",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ShowMainWindow()
    {
        try
        {
            WriteStartupDiagnostic("[ModScope] startup phase=show-main-window");
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            WriteStartupDiagnostic(
                $"[ModScope] startup phase=window-show-failed type={exception.GetType().Name}"
                + $" hresult=0x{exception.HResult:X8}"
                + $" stack={GetDiagnosticStack(exception)}");
            MessageBox.Show(
                "ModScopeを安全に起動できませんでした。\n\n"
                + $"診断コード: {exception.GetType().Name} (0x{exception.HResult:X8})",
                "ModScope",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void App_DispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteStartupDiagnostic(
            $"[ModScope] startup phase=dispatcher-failed type={e.Exception.GetType().Name}"
            + $" hresult=0x{e.Exception.HResult:X8}"
            + $" stack={GetDiagnosticStack(e.Exception)}");
        e.Handled = true;
        MessageBox.Show(
            "ModScopeで未処理のUI例外が発生しました。\n\n"
            + $"診断コード: {e.Exception.GetType().Name} (0x{e.Exception.HResult:X8})",
            "ModScope",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }

    private static ProcessBoundaryDecision PrepareProcessBoundary(out string failureMessage)
    {
        failureMessage = string.Empty;
        var hasDetachedLaunchMarker = string.Equals(
            Environment.GetEnvironmentVariable(DetachedLaunchMarker),
            DetachedLaunchMarkerValue,
            StringComparison.Ordinal);

        if (!TryGetJobMembership(out var isInJob, out var jobProbeErrorCode))
        {
            WriteStartupDiagnostic(
                $"[ModScope] startup phase=job-probe failed error={jobProbeErrorCode}");
            failureMessage =
                "ModScopeの起動環境を確認できませんでした。\n\n"
                + "ExplorerからModScopeを起動してください。";
            return ProcessBoundaryDecision.Failed;
        }

        WriteStartupDiagnostic(
            $"[ModScope] startup phase=job-probe pid={Environment.ProcessId} inJob={isInJob}");

        if (!isInJob)
        {
            return ProcessBoundaryDecision.Continue;
        }

        if (hasDetachedLaunchMarker)
        {
            WriteStartupDiagnostic(
                "[ModScope] startup phase=job-probe detached-child-still-in-job");
            return ProcessBoundaryDecision.Continue;
        }

        if (!TryRelaunchOutsideJob(out var relaunchErrorCode))
        {
            WriteStartupDiagnostic(
                $"[ModScope] startup phase=breakaway failed error={relaunchErrorCode}");
            WriteStartupDiagnostic(
                "[ModScope] startup phase=breakaway fallback=continue-in-current-job");
            return ProcessBoundaryDecision.Continue;
        }

        return ProcessBoundaryDecision.Relaunched;
    }

    private static bool TryGetJobMembership(out bool isInJob, out int errorCode)
    {
        isInJob = false;
        errorCode = 0;

        if (IsProcessInJob(GetCurrentProcess(), IntPtr.Zero, out isInJob))
        {
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        return errorCode == 0;
    }

    private static bool TryRelaunchOutsideJob(out int errorCode)
    {
        errorCode = 0;
        var previousMarker = Environment.GetEnvironmentVariable(DetachedLaunchMarker);
        Environment.SetEnvironmentVariable(DetachedLaunchMarker, DetachedLaunchMarkerValue);

        try
        {
            var startupInfo = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfo>()
            };
            var creationFlags = CreateBreakawayFromJob | CreateUnicodeEnvironment;
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                errorCode = 2;
                return false;
            }

            string? launchDirectory;
            try
            {
                launchDirectory = Environment.CurrentDirectory;
            }
            catch (DirectoryNotFoundException)
            {
                launchDirectory = null;
            }

            if (string.IsNullOrWhiteSpace(launchDirectory) || !Directory.Exists(launchDirectory))
            {
                launchDirectory = Path.GetDirectoryName(processPath);
            }

            if (!string.IsNullOrWhiteSpace(launchDirectory) && !Directory.Exists(launchDirectory))
            {
                launchDirectory = null;
            }

            var commandLine = new StringBuilder(Environment.CommandLine);

            if (!CreateProcessW(
                    applicationName: processPath,
                    commandLine,
                    processAttributes: IntPtr.Zero,
                    threadAttributes: IntPtr.Zero,
                    inheritHandles: false,
                    creationFlags,
                    environment: IntPtr.Zero,
                    currentDirectory: launchDirectory,
                    ref startupInfo,
                    out var processInformation))
            {
                errorCode = Marshal.GetLastWin32Error();
                return false;
            }

            try
            {
                WriteStartupDiagnostic(
                    $"[ModScope] startup phase=breakaway childPid={processInformation.ProcessId}");
            }
            finally
            {
                CloseHandle(processInformation.ThreadHandle);
                CloseHandle(processInformation.ProcessHandle);
            }

            return true;
        }
        finally
        {
            Environment.SetEnvironmentVariable(DetachedLaunchMarker, previousMarker);
        }
    }

    private static void WriteStartupDiagnostic(string message)
    {
        Trace.WriteLine(message);
        try
        {
            var diagnosticPath = Path.Combine(
                Path.GetTempPath(),
                "ModScope-startup.log");
            File.AppendAllText(
                diagnosticPath,
                $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static string GetDiagnosticStack(Exception exception)
    {
        var frames = new StackTrace(exception, fNeedFileInfo: false).GetFrames();
        if (frames is null || frames.Length == 0)
        {
            return "none";
        }

        return string.Join(
            "<",
            frames
                .Take(8)
                .Select(frame =>
                {
                    var method = frame.GetMethod();
                    return $"{method?.DeclaringType?.Name}.{method?.Name}";
                }));
    }

    private enum ProcessBoundaryDecision
    {
        Continue,
        Relaunched,
        Failed
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(
        IntPtr processHandle,
        IntPtr jobHandle,
        [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public int ProcessId;
        public int ThreadId;
    }
}
