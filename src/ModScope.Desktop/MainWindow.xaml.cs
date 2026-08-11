using System.Text.Json;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using ModScope.Desktop.Contracts;
using ModScope.Query;

namespace ModScope.Desktop;

public partial class MainWindow : Window
{
    private const string AppHostName = "appassets.modscope";
    private readonly DesktopSessionController _controller = new();
    private bool _appShellReady;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            await AppShell.EnsureCoreWebView2Async();

            Browser.NavigationCompleted += Browser_NavigationCompleted;
            Browser.CoreWebView2.DocumentTitleChanged += Browser_DocumentTitleChanged;
            AppShell.NavigationStarting += AppShell_NavigationStarting;
            AppShell.NavigationCompleted += AppShell_NavigationCompleted;
            AppShell.CoreWebView2.WebMessageReceived += AppShell_WebMessageReceived;

            var webAssetsPath = Path.Combine(AppContext.BaseDirectory, "WebAssets");
            if (!Directory.Exists(webAssetsPath))
            {
                throw new DirectoryNotFoundException(
                    $"The Web UI assets are missing: {webAssetsPath}. Run scripts/build.ps1.");
            }

            AppShell.CoreWebView2.SetVirtualHostNameToFolderMapping(
                AppHostName,
                webAssetsPath,
                CoreWebView2HostResourceAccessKind.DenyCors);
            AppShell.Source = new Uri($"https://{AppHostName}/index.html");

            var demoPage = Path.Combine(AppContext.BaseDirectory, "Fixtures", "alpha-mod.html");
            Browser.Source = File.Exists(demoPage)
                ? new Uri(demoPage)
                : new Uri("about:blank");
        }
        catch (Exception exception)
        {
            _controller.SetStatus("WebView2 initialization failed.");
            if (AppShell.CoreWebView2 is not null)
            {
                SendError("browser.initialization.failed", exception.Message);
            }
        }
    }

    private void AppShell_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (!BridgeProtocol.IsAppHostUri(e.Uri))
        {
            e.Cancel = true;
        }
    }

    private void AppShell_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            SendError("frontend.navigation.failed", e.WebErrorStatus.ToString());
            return;
        }

        _appShellReady = true;
        SendMessage("ready", new { });
        SendState();
    }

    private async void AppShell_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        BridgeCommandEnvelope? command = null;
        try
        {
            if (!BridgeProtocol.IsAppHostUri(e.Source))
            {
                return;
            }

            command = BridgeProtocol.ParseCommand(e.WebMessageAsJson);
            await HandleCommandAsync(command);
        }
        catch (BridgeProtocolException exception)
        {
            SendError("bridge.message.invalid", exception.Message, command?.RequestId);
        }
        catch (Exception exception)
        {
            SendError("bridge.command.failed", exception.Message, command?.RequestId);
        }
    }

    private async Task HandleCommandAsync(BridgeCommandEnvelope command)
    {
        switch (command.Command)
        {
            case "browser.navigate":
            {
                var payload = BridgeProtocol.ReadPayload<NavigatePayload>(command.Payload);
                if (!BridgeProtocol.TryGetSupportedBrowserUri(payload.Url, out var uri))
                {
                    throw new BridgeProtocolException(
                        "Use an absolute http, https, file, or about URL.");
                }

                _controller.SetStatus($"Navigating to {uri}.");
                Browser.Source = uri;
                SendState(command.RequestId);
                break;
            }
            case "browser.back":
                if (Browser.CanGoBack)
                {
                    Browser.GoBack();
                }
                SendState(command.RequestId);
                break;
            case "browser.forward":
                if (Browser.CanGoForward)
                {
                    Browser.GoForward();
                }
                SendState(command.RequestId);
                break;
            case "browser.reload":
                Browser.Reload();
                SendState(command.RequestId);
                break;
            case "browser.observe":
                await ObservePageAsync(command.RequestId);
                break;
            case "knowledge.useFixture":
                _controller.UseFixture();
                SendState(command.RequestId);
                break;
            case "knowledge.loadSource":
            {
                var payload = BridgeProtocol.ReadPayload<LoadSourcePayload>(command.Payload);
                _controller.LoadSource(new Mo2SourceInput(
                    payload.InstanceName,
                    payload.ProfileName,
                    payload.InstanceRootPath,
                    payload.ProfilePath,
                    payload.ModsPath));
                SendState(command.RequestId);
                break;
            }
            case "identity.confirm":
            {
                var payload = BridgeProtocol.ReadPayload<ConfirmIdentityPayload>(command.Payload);
                _controller.ConfirmIdentity(payload.CandidateIdentity, payload.LocalModKey);
                SendState(command.RequestId);
                break;
            }
            case "inspector.open":
            {
                var payload = BridgeProtocol.ReadPayload<InspectorOpenPayload>(command.Payload);
                _controller.OpenInspector(payload.ModKey);
                SendState(command.RequestId);
                break;
            }
            default:
                throw new BridgeProtocolException($"Unhandled bridge command: {command.Command}.");
        }
    }

    private async Task ObservePageAsync(string? requestId)
    {
        if (Browser.CoreWebView2 is null)
        {
            throw new InvalidOperationException("The browser WebView2 is not initialized.");
        }

        try
        {
            var serializedContent = await Browser.ExecuteScriptAsync(
                "document.body ? document.body.innerText : null");
            var content = JsonSerializer.Deserialize<string>(serializedContent);
            var pageUri = Browser.Source ?? new Uri("about:blank");
            var title = Browser.CoreWebView2.DocumentTitle ?? string.Empty;
            var extractionStatus = content is null
                ? PageExtractionStatus.Partial
                : PageExtractionStatus.Succeeded;

            _controller.SetObservation(new PageObservation(
                pageUri,
                title,
                content,
                DateTimeOffset.UtcNow,
                "WebView2",
                extractionStatus,
                Array.Empty<DiagnosticReadModel>()));
            SendState(requestId);
        }
        catch (Exception exception)
        {
            var pageUri = Browser.Source ?? new Uri("about:blank");
            _controller.SetObservation(new PageObservation(
                pageUri,
                Browser.CoreWebView2.DocumentTitle ?? string.Empty,
                null,
                DateTimeOffset.UtcNow,
                "WebView2",
                PageExtractionStatus.Failed,
                new[]
                {
                    new DiagnosticReadModel(
                        "browser.observation.failed",
                        QueryDiagnosticSeverity.Error,
                        exception.Message)
                }));
            SendError("browser.observation.failed", exception.Message, requestId);
        }
    }

    private void Browser_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _controller.SetObservation(new PageObservation(
                Browser.Source ?? new Uri("about:blank"),
                Browser.CoreWebView2?.DocumentTitle ?? string.Empty,
                null,
                DateTimeOffset.UtcNow,
                "WebView2",
                PageExtractionStatus.Failed,
                new[]
                {
                    new DiagnosticReadModel(
                        "browser.navigation.failed",
                        QueryDiagnosticSeverity.Warning,
                        e.WebErrorStatus.ToString())
                }));
        }

        SendState();
    }

    private void Browser_DocumentTitleChanged(object? sender, object e)
    {
        SendState();
    }

    private void SendState(string? requestId = null)
    {
        if (!_appShellReady || AppShell.CoreWebView2 is null)
        {
            return;
        }

        var browserState = new BrowserUiState(
            Browser.Source?.ToString() ?? "about:blank",
            Browser.CoreWebView2.DocumentTitle ?? string.Empty,
            Browser.CanGoBack,
            Browser.CanGoForward);
        var state = _controller.BuildState(browserState);
        SendMessage("state", state, requestId);
    }

    private void SendError(string code, string message, string? requestId = null)
    {
        _controller.SetStatus(message);
        if (!_appShellReady || AppShell.CoreWebView2 is null)
        {
            return;
        }

        SendMessage("error", new BridgeErrorPayload(code, message), requestId);
        SendState(requestId);
    }

    private void SendMessage<T>(string kind, T payload, string? requestId = null)
    {
        if (AppShell.CoreWebView2 is null)
        {
            return;
        }

        AppShell.CoreWebView2.PostWebMessageAsJson(
            BridgeProtocol.SerializeMessage(kind, payload, requestId));
    }
}
