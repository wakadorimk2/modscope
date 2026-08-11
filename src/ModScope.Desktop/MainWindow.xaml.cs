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
    private bool _toolbarReady;
    private bool _contextReady;

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
            await ToolbarShell.EnsureCoreWebView2Async();
            await ContextWebView.EnsureCoreWebView2Async();

            Browser.NavigationCompleted += Browser_NavigationCompleted;
            Browser.CoreWebView2.DocumentTitleChanged += Browser_DocumentTitleChanged;
            ToolbarShell.NavigationStarting += AppShell_NavigationStarting;
            ToolbarShell.NavigationCompleted += AppShell_NavigationCompleted;
            ToolbarShell.CoreWebView2.WebMessageReceived += AppShell_WebMessageReceived;
            ContextWebView.NavigationStarting += AppShell_NavigationStarting;
            ContextWebView.NavigationCompleted += AppShell_NavigationCompleted;
            ContextWebView.CoreWebView2.WebMessageReceived += AppShell_WebMessageReceived;

            var webAssetsPath = Path.Combine(AppContext.BaseDirectory, "WebAssets");
            if (!Directory.Exists(webAssetsPath))
            {
                throw new DirectoryNotFoundException(
                    $"The Web UI assets are missing: {webAssetsPath}. Run scripts/build.ps1.");
            }

            ConfigureFrontend(ToolbarShell, webAssetsPath, "toolbar");
            ConfigureFrontend(ContextWebView, webAssetsPath, "context");

            var demoPage = Path.Combine(AppContext.BaseDirectory, "Fixtures", "alpha-mod.html");
            Browser.Source = File.Exists(demoPage)
                ? new Uri(demoPage)
                : new Uri("about:blank");
        }
        catch (Exception exception)
        {
            _controller.SetStatus("WebView2 initialization failed.");
            if (ToolbarShell.CoreWebView2 is not null || ContextWebView.CoreWebView2 is not null)
            {
                SendError("browser.initialization.failed", exception.Message);
            }
        }
    }

    private static void ConfigureFrontend(
        Microsoft.Web.WebView2.Wpf.WebView2 webView,
        string webAssetsPath,
        string surface)
    {
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            AppHostName,
            webAssetsPath,
            CoreWebView2HostResourceAccessKind.DenyCors);
        webView.Source = new Uri($"https://{AppHostName}/index.html?surface={surface}");
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
        var webView = sender as Microsoft.Web.WebView2.Wpf.WebView2;
        if (webView is null)
        {
            return;
        }

        if (!e.IsSuccess)
        {
            SendError("frontend.navigation.failed", e.WebErrorStatus.ToString());
            return;
        }

        if (ReferenceEquals(webView, ToolbarShell))
        {
            _toolbarReady = true;
        }
        else if (ReferenceEquals(webView, ContextWebView))
        {
            _contextReady = true;
        }

        SendMessageTo(webView, "ready", new { });
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
            case "knowledge.switchProfile":
            {
                var payload = BridgeProtocol.ReadPayload<SwitchProfilePayload>(command.Payload);
                if (string.IsNullOrWhiteSpace(payload.ProfileName))
                {
                    throw new BridgeProtocolException("The profile name is required.");
                }

                _controller.SwitchProfile(payload.ProfileName);
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
            case "layout.setContextVisible":
            {
                if (!command.Payload.TryGetProperty("visible", out var visible)
                    || visible.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                {
                    throw new BridgeProtocolException("The context visibility must be a boolean.");
                }

                var payload = BridgeProtocol.ReadPayload<SetContextVisiblePayload>(command.Payload);
                _controller.SetContextVisible(payload.Visible);
                ContextColumn.Width = payload.Visible
                    ? new GridLength(2, GridUnitType.Star)
                    : new GridLength(0);
                ContextShell.Visibility = payload.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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

    private async void Browser_NavigationCompleted(
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
            SendState();
            return;
        }

        await ObservePageAsync(null);
    }

    private void Browser_DocumentTitleChanged(object? sender, object e)
    {
        SendState();
    }

    private void SendState(string? requestId = null)
    {
        if ((!_toolbarReady && !_contextReady) || Browser.CoreWebView2 is null)
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
        if (!_toolbarReady && !_contextReady)
        {
            return;
        }

        SendMessage("error", new BridgeErrorPayload(code, message), requestId);
        SendState(requestId);
    }

    private void SendMessage<T>(string kind, T payload, string? requestId = null)
    {
        var message = BridgeProtocol.SerializeMessage(kind, payload, requestId);
        if (_toolbarReady && ToolbarShell.CoreWebView2 is not null)
        {
            ToolbarShell.CoreWebView2.PostWebMessageAsJson(message);
        }

        if (_contextReady && ContextWebView.CoreWebView2 is not null)
        {
            ContextWebView.CoreWebView2.PostWebMessageAsJson(message);
        }
    }

    private static void SendMessageTo<T>(
        Microsoft.Web.WebView2.Wpf.WebView2 webView,
        string kind,
        T payload,
        string? requestId = null)
    {
        webView.CoreWebView2?.PostWebMessageAsJson(
            BridgeProtocol.SerializeMessage(kind, payload, requestId));
    }
}
