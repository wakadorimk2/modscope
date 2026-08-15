using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using ModScope.Desktop.Contracts;
using ModScope.Query;

namespace ModScope.Desktop;

public partial class MainWindow : Window
{
    private const double ToolbarCollapsedHeight = 76;
    private const double ToolbarExpandedHeight = 440;

    private const string AppHostName = "appassets.modscope";
    private readonly DesktopSessionController _controller = new();
    private bool _toolbarReady;
    private bool _modListReady;
    private bool _contextReady;
    private bool _sourceDiscoveryStarted;
    private bool _startupLoading = true;
    private readonly BrowserStateStore _browserStateStore = BrowserStateStore.CreateDefault();
    private readonly Dictionary<string, BrowserTabHostState> _browserTabs = new(StringComparer.Ordinal);
    private IReadOnlyList<BrowserHistoryEntryUiState> _browserHistory = Array.Empty<BrowserHistoryEntryUiState>();
    private string? _activeBrowserTabId;
    private int _browserTabSequence;
    private string? _webAssetsPath;

    private const string HomeHtml = """
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>ModScope Home</title>
        <style>body{font-family:Segoe UI,sans-serif;background:#202124;color:#e8eaed;padding:48px;line-height:1.6}main{max-width:720px;margin:auto;background:#303134;border:1px solid #3c4043;border-radius:14px;padding:32px}h1{margin-top:0;color:#e8eaed}p{color:#bdc1c6}.hint{color:#8ab4f8}</style></head>
        <body><main><h1>ModScope Browse Home</h1><p>Open a page from your normal browser workflow, then use Recognize to connect it to local MOD knowledge.</p><p class="hint">Tabs, titles, and bounded history are stored as metadata only.</p></main></body>
        </html>
        """;

    public MainWindow()
    {
        InitializeComponent();
        UpdateLoadingOverlay();
        _controller.OperationStateChanged += Controller_OperationStateChanged;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ToolbarShell.EnsureCoreWebView2Async();
            await ModListWebView.EnsureCoreWebView2Async();
            await ContextWebView.EnsureCoreWebView2Async();
            UpdateLoadingOverlay();

            ToolbarShell.NavigationStarting += AppShell_NavigationStarting;
            ToolbarShell.NavigationCompleted += AppShell_NavigationCompleted;
            ToolbarShell.CoreWebView2.WebMessageReceived += AppShell_WebMessageReceived;
            ModListWebView.NavigationStarting += AppShell_NavigationStarting;
            ModListWebView.NavigationCompleted += AppShell_NavigationCompleted;
            ModListWebView.CoreWebView2.WebMessageReceived += AppShell_WebMessageReceived;
            ContextWebView.NavigationStarting += AppShell_NavigationStarting;
            ContextWebView.NavigationCompleted += AppShell_NavigationCompleted;
            ContextWebView.CoreWebView2.WebMessageReceived += AppShell_WebMessageReceived;

            var webAssetsPath = Path.Combine(AppContext.BaseDirectory, "WebAssets");
            if (!Directory.Exists(webAssetsPath))
            {
                throw new DirectoryNotFoundException(
                    $"The Web UI assets are missing: {webAssetsPath}. Run scripts/build.ps1.");
            }

            _webAssetsPath = webAssetsPath;

            ConfigureFrontend(ToolbarShell, webAssetsPath, "toolbar");
            ConfigureFrontend(ModListWebView, webAssetsPath, "mod-list");
            ConfigureFrontend(ContextWebView, webAssetsPath, "context");

            if (!_sourceDiscoveryStarted)
            {
                _sourceDiscoveryStarted = true;
                var discoveryTask = _controller.DiscoverSourcesAsync();
                SendState();
                await discoveryTask;
                SendState();
            }

            _startupLoading = false;
            UpdateLoadingOverlay();
            await RestoreBrowserTabsAsync();
            SendState();
        }
        catch (Exception exception)
        {
            _startupLoading = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            SetClientInteractionEnabled(true);
            _controller.SetStatus("WebView2 initialization failed.");
            if (ToolbarShell.CoreWebView2 is not null
                || ModListWebView.CoreWebView2 is not null
                || ContextWebView.CoreWebView2 is not null)
            {
                SendError("browser.initialization.failed", exception.Message);
            }
        }
    }

    private async Task RestoreBrowserTabsAsync()
    {
        var persisted = _browserStateStore.Load();
        _browserHistory = persisted.History;

        foreach (var tab in persisted.Tabs)
        {
            await CreateBrowserTabAsync(tab.TabId, tab.Url, tab.Title, activate: false);
        }

        if (_browserTabs.Count == 0)
        {
            await CreateBrowserTabAsync(null, null, null, activate: true);
            return;
        }

        var activeTabId = persisted.ActiveTabId is not null && _browserTabs.ContainsKey(persisted.ActiveTabId)
            ? persisted.ActiveTabId
            : _browserTabs.Keys.First();
        await ActivateBrowserTabAsync(activeTabId);
    }

    private async Task<BrowserTabHostState> CreateBrowserTabAsync(
        string? requestedTabId,
        string? initialUrl,
        string? initialTitle,
        bool activate)
    {
        var tabId = string.IsNullOrWhiteSpace(requestedTabId)
            ? NextBrowserTabId()
            : requestedTabId.Trim();
        while (_browserTabs.ContainsKey(tabId))
        {
            tabId = NextBrowserTabId();
        }

        var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
        var tab = new BrowserTabHostState(tabId, webView)
        {
            Title = string.IsNullOrWhiteSpace(initialTitle) ? "New tab" : initialTitle.Trim()
        };
        _browserTabs.Add(tabId, tab);
        BrowserHost.Children.Add(webView);
        webView.Visibility = Visibility.Collapsed;

        await webView.EnsureCoreWebView2Async();
        webView.NavigationCompleted += Browser_NavigationCompleted;
        webView.CoreWebView2.DocumentTitleChanged += Browser_DocumentTitleChanged;
        webView.CoreWebView2.WebMessageReceived += Browser_WebMessageReceived;

        if (IsDeploymentPreviewUrl(initialUrl))
        {
            NavigateDeploymentPreview(tab);
        }
        else if (IsHistoryUrl(initialUrl))
        {
            NavigateHistory(tab);
        }
        else if (IsExternalBrowserUrl(initialUrl, out var initialUri))
        {
            tab.Url = initialUri.ToString();
            tab.InternalPage = null;
            webView.Source = initialUri;
        }
        else
        {
            NavigateHome(tab);
        }

        SetClientInteractionEnabled(!IsForegroundLoading());

        if (activate)
        {
            await ActivateBrowserTabAsync(tabId);
        }

        return tab;
    }

    private async Task ActivateBrowserTabAsync(string tabId)
    {
        if (!_browserTabs.TryGetValue(tabId, out var tab))
        {
            throw new BridgeProtocolException("The browser tab was not found.");
        }

        foreach (var item in _browserTabs.Values)
        {
            item.WebView.Visibility = ReferenceEquals(item, tab)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        _activeBrowserTabId = tab.TabId;
        if (tab.WebView.CoreWebView2 is not null)
        {
            await ObservePageAsync(null, null, tab);
        }

        SaveBrowserState();
        SendState();
    }

    private async Task CloseBrowserTabAsync(string tabId)
    {
        if (!_browserTabs.TryGetValue(tabId, out var tab))
        {
            throw new BridgeProtocolException("The browser tab was not found.");
        }

        if (_browserTabs.Count == 1)
        {
            _activeBrowserTabId = tab.TabId;
            NavigateHome(tab);
            SaveBrowserState();
            SendState();
            return;
        }

        var wasActive = string.Equals(_activeBrowserTabId, tab.TabId, StringComparison.Ordinal);
        var nextTabId = _browserTabs.Keys.FirstOrDefault(id => !string.Equals(id, tabId, StringComparison.Ordinal));
        BrowserHost.Children.Remove(tab.WebView);
        _browserTabs.Remove(tabId);
        if (wasActive && nextTabId is not null)
        {
            await ActivateBrowserTabAsync(nextTabId);
        }
        else
        {
            SaveBrowserState();
            SendState();
        }
    }

    private async Task OpenDeploymentPreviewTabAsync()
    {
        var existingTab = _browserTabs.Values.FirstOrDefault(tab => tab.IsDeploymentPreview);
        if (existingTab is not null)
        {
            NavigateDeploymentPreview(existingTab);
            await ActivateBrowserTabAsync(existingTab.TabId);
            return;
        }

        await CreateBrowserTabAsync(
            null,
            "about:deployment-preview",
            "Deployment preview",
            activate: true);
    }

    private void NavigateHome(BrowserTabHostState tab)
    {
        tab.InternalPage = "home";
        tab.PendingNexusSearchName = null;
        tab.Url = "about:blank";
        tab.Title = "ModScope Home";
        tab.WebView.NavigateToString(HomeHtml);
        _controller.SetStatus("Browse Home is open.");
    }

    private void NavigateHistory(BrowserTabHostState tab)
    {
        tab.InternalPage = "history";
        tab.PendingNexusSearchName = null;
        tab.Url = "about:history";
        tab.Title = "History";
        tab.WebView.NavigateToString(RenderHistoryHtml());
        _controller.SetStatus("Browser history is open.");
    }

    private void NavigateDeploymentPreview(BrowserTabHostState tab)
    {
        if (string.IsNullOrWhiteSpace(_webAssetsPath))
        {
            throw new InvalidOperationException("The Web UI assets are not initialized.");
        }

        tab.InternalPage = "deployment-preview";
        tab.PendingNexusSearchName = null;
        tab.Url = "about:deployment-preview";
        tab.Title = "Deployment preview";
        ConfigureFrontend(tab.WebView, _webAssetsPath, "deployment-preview");
    }

    private string RenderHistoryHtml()
    {
        var entries = _browserHistory
            .Select(entry =>
            {
                var title = System.Net.WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(entry.Title) ? "Untitled page" : entry.Title);
                var url = System.Net.WebUtility.HtmlEncode(entry.Url);
                var visitedAt = System.Net.WebUtility.HtmlEncode(entry.VisitedAtUtc.ToString("u"));
                return $"<li><a href=\"{url}\"><strong>{title}</strong><span>{url}</span><time datetime=\"{System.Net.WebUtility.HtmlEncode(entry.VisitedAtUtc.ToString("O"))}\">{visitedAt} UTC</time></a></li>";
            });
        var list = string.Join(Environment.NewLine, entries);
        var body = string.IsNullOrWhiteSpace(list)
            ? "<p class=\"empty\">No visited pages.</p>"
            : $"<ol>{list}</ol>";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>History</title>
            <style>
            :root{color-scheme:dark}
            *{box-sizing:border-box}
            body{margin:0;padding:40px;font-family:Segoe UI,sans-serif;background:#202124;color:#e8eaed;line-height:1.5}
            main{max-width:900px;margin:0 auto;padding:28px;background:#303134;border:1px solid #3c4043;border-radius:14px}
            h1{margin:0 0 6px;font-size:24px}p{color:#bdc1c6}ol{display:grid;gap:8px;margin:24px 0 0;padding:0;list-style:none}
            li{margin:0}a{display:grid;gap:3px;padding:12px 14px;border:1px solid #3c4043;border-radius:10px;color:#e8eaed;text-decoration:none;background:#292a2d}
            a:hover{background:#35363a;border-color:#5f6368}a span,a time{overflow:hidden;color:#9aa0a6;font-size:12px;text-overflow:ellipsis;white-space:nowrap}a time{font-size:11px}.empty{margin:24px 0 4px}
            </style></head>
            <body><main><h1>History</h1><p>Saved page metadata only: URL, title, and visited time.</p>{{body}}</main></body>
            </html>
            """;
    }

    private BrowserTabHostState? ActiveBrowserTab =>
        _activeBrowserTabId is not null && _browserTabs.TryGetValue(_activeBrowserTabId, out var tab)
            ? tab
            : null;

    private BrowserTabHostState? FindBrowserTab(object? sender)
    {
        if (sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            return _browserTabs.Values.FirstOrDefault(tab => ReferenceEquals(tab.WebView, webView));
        }

        if (sender is CoreWebView2 coreWebView)
        {
            return _browserTabs.Values.FirstOrDefault(tab => ReferenceEquals(tab.WebView.CoreWebView2, coreWebView));
        }

        return null;
    }

    private string NextBrowserTabId()
    {
        do
        {
            _browserTabSequence++;
        }
        while (_browserTabs.ContainsKey($"tab-{_browserTabSequence}"));

        return $"tab-{_browserTabSequence}";
    }

    private void AddBrowserHistory(BrowserTabHostState tab)
    {
        if (!IsExternalBrowserUrl(tab.Url, out var uri))
        {
            return;
        }

        var entry = new BrowserHistoryEntryUiState(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(tab.Title) ? uri.Host : tab.Title,
            uri.ToString(),
            DateTimeOffset.UtcNow);
        _browserHistory = new[] { entry }
            .Concat(_browserHistory.Where(item => !string.Equals(item.Url, entry.Url, StringComparison.OrdinalIgnoreCase)))
            .Take(100)
            .ToList()
            .AsReadOnly();
    }

    private void SaveBrowserState()
    {
        var tabs = _browserTabs.Values
            .Select(tab => new BrowserTabUiState(
                tab.TabId,
                tab.Title,
                tab.Url,
                tab.WebView.CanGoBack,
                tab.WebView.CanGoForward,
                string.Equals(tab.TabId, _activeBrowserTabId, StringComparison.Ordinal)))
            .ToList();
        _browserStateStore.Save(tabs, _activeBrowserTabId, _browserHistory);
    }

    private static bool IsExternalBrowserUrl(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && uri.Scheme is "http" or "https")
        {
            return true;
        }

        uri = new Uri("about:blank");
        return false;
    }

    private static bool IsHistoryUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.AbsoluteUri, "about:history", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeploymentPreviewUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.AbsoluteUri, "about:deployment-preview", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInternalBrowserPage(string? value)
    {
        return value is "home" or "history" or "deployment-preview";
    }

    private static bool IsNexusSearchUri(Uri uri)
    {
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || !uri.Host.Equals("www.nexusmods.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        return (path.Equals("/7daystodie/search", StringComparison.OrdinalIgnoreCase)
                && uri.Query.Contains("gsearch=", StringComparison.OrdinalIgnoreCase))
            || (path.Equals("/games/7daystodie/mods", StringComparison.OrdinalIgnoreCase)
                && uri.Query.Contains("keyword=", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> TryResolveNexusSearchAsync(BrowserTabHostState tab)
    {
        var targetName = tab.PendingNexusSearchName;
        tab.PendingNexusSearchName = null;
        if (string.IsNullOrWhiteSpace(targetName)
            || tab.WebView.CoreWebView2 is null
            || tab.WebView.Source is not { } source
            || !IsNexusSearchUri(source))
        {
            return false;
        }

        var normalizedTargetName = NormalizeNexusName(targetName);
        if (normalizedTargetName.Length == 0)
        {
            _controller.SetStatus("Nexus search needs a usable MOD name.");
            return false;
        }

        try
        {
            var serializedLinks = await tab.WebView.ExecuteScriptAsync(
                "JSON.stringify(Array.from(document.querySelectorAll('a[href]')).map(anchor => ({ href: anchor.href, text: (anchor.innerText || anchor.textContent || '').trim() })))");
            var linksJson = JsonSerializer.Deserialize<string>(serializedLinks);
            if (string.IsNullOrWhiteSpace(linksJson))
            {
                _controller.SetStatus("Nexus search results could not be inspected.");
                return false;
            }

            using var document = JsonDocument.Parse(linksJson);
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in document.RootElement.EnumerateArray())
            {
                if (link.ValueKind is not JsonValueKind.Object
                    || !link.TryGetProperty("href", out var hrefElement)
                    || hrefElement.ValueKind is not JsonValueKind.String
                    || !link.TryGetProperty("text", out var textElement)
                    || textElement.ValueKind is not JsonValueKind.String)
                {
                    continue;
                }

                var linkText = textElement.GetString();
                if (NormalizeNexusName(linkText ?? string.Empty) != normalizedTargetName)
                {
                    continue;
                }

                if (TryGetNexusModUrl(hrefElement.GetString(), out var modUri))
                {
                    matches.Add(modUri.ToString());
                }
            }

            if (matches.Count == 1)
            {
                var resolvedUri = new Uri(matches.Single(), UriKind.Absolute);
                tab.Navigate(resolvedUri);
                _controller.SetStatus($"Resolved Nexus MOD page for {targetName}.");
                return true;
            }

            _controller.SetStatus(matches.Count == 0
                ? "Nexus search found no exact MOD page."
                : "Nexus search found multiple exact MOD pages.");
        }
        catch (JsonException)
        {
            _controller.SetStatus("Nexus search results could not be inspected.");
        }
        catch (Exception)
        {
            _controller.SetStatus("Nexus search results could not be inspected.");
        }

        return false;
    }

    private static bool TryGetNexusModUrl(string? value, out Uri canonicalUri)
    {
        canonicalUri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || !uri.Host.Equals("www.nexusmods.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var idSegment = segments switch
        {
            [var game, var mods, var id]
                when game.Equals("7daystodie", StringComparison.OrdinalIgnoreCase)
                    && mods.Equals("mods", StringComparison.OrdinalIgnoreCase) => id,
            [var games, var game, var mods, var id]
                when games.Equals("games", StringComparison.OrdinalIgnoreCase)
                    && game.Equals("7daystodie", StringComparison.OrdinalIgnoreCase)
                    && mods.Equals("mods", StringComparison.OrdinalIgnoreCase) => id,
            _ => null
        };

        if (!long.TryParse(
                idSegment,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var modId)
            || modId <= 0)
        {
            return false;
        }

        canonicalUri = new Uri(
            $"https://www.nexusmods.com/7daystodie/mods/{modId}",
            UriKind.Absolute);
        return true;
    }

    private static string NormalizeNexusName(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormKD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
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
        else if (ReferenceEquals(webView, ModListWebView))
        {
            _modListReady = true;
        }

        SendMessageTo(webView, "ready", new { });
        SendState();
    }

    private async void AppShell_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        var sourceWebView = ResolveSourceWebView(sender);
        if (sourceWebView is null)
        {
            return;
        }

        BridgeCommandEnvelope? command = null;
        try
        {
            if (!BridgeProtocol.IsAppHostUri(e.Source))
            {
                return;
            }

            command = BridgeProtocol.ParseCommand(e.WebMessageAsJson);
            await HandleCommandAsync(sourceWebView, command);
        }
        catch (BridgeProtocolException exception)
        {
            SendError("bridge.message.invalid", exception.Message, command?.RequestId, sourceWebView);
        }
        catch (Exception exception)
        {
            SendError("bridge.command.failed", exception.Message, command?.RequestId, sourceWebView);
        }
    }

    private async void Browser_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        var tab = FindBrowserTab(sender);
        if (tab is null
            || !tab.IsDeploymentPreview)
        {
            return;
        }

        BridgeCommandEnvelope? command = null;
        try
        {
            if (!BridgeProtocol.IsAppHostUri(e.Source))
            {
                return;
            }

            command = BridgeProtocol.ParseCommand(e.WebMessageAsJson);
            if (command.Command is not ("frontend.ready" or "deployment.apply"))
            {
                return;
            }

            if (command.Command == "deployment.apply"
                && !ReferenceEquals(tab, ActiveBrowserTab))
            {
                return;
            }

            await HandleCommandAsync(tab.WebView, command);
        }
        catch (BridgeProtocolException exception)
        {
            SendError("bridge.message.invalid", exception.Message, command?.RequestId, tab.WebView);
        }
        catch (Exception exception)
        {
            SendError("bridge.command.failed", exception.Message, command?.RequestId, tab.WebView);
        }
    }

    private Microsoft.Web.WebView2.Wpf.WebView2? ResolveSourceWebView(object? sender)
    {
        if (ReferenceEquals(sender, ToolbarShell.CoreWebView2))
        {
            return ToolbarShell;
        }

        if (ReferenceEquals(sender, ContextWebView.CoreWebView2))
        {
            return ContextWebView;
        }

        if (ReferenceEquals(sender, ModListWebView.CoreWebView2))
        {
            return ModListWebView;
        }

        return null;
    }

    private async Task HandleCommandAsync(
        Microsoft.Web.WebView2.Wpf.WebView2 sourceWebView,
        BridgeCommandEnvelope command)
    {
        switch (command.Command)
        {
            case "frontend.ready":
                SendState(command.RequestId, sourceWebView);
                break;
            case "browser.newTab":
                await CreateBrowserTabAsync(null, null, null, activate: true);
                SendState(command.RequestId, sourceWebView);
                break;
            case "browser.selectTab":
            {
                var payload = BridgeProtocol.ReadPayload<BrowserTabPayload>(command.Payload);
                await ActivateBrowserTabAsync(payload.TabId);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.closeTab":
            {
                var payload = BridgeProtocol.ReadPayload<BrowserTabPayload>(command.Payload);
                await CloseBrowserTabAsync(payload.TabId);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.home":
            {
                var browserTab = ActiveBrowserTab
                    ?? throw new InvalidOperationException("The browser tab is not initialized.");
                NavigateHome(browserTab);
                SaveBrowserState();
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.history":
            {
                await CreateBrowserTabAsync(null, "about:history", "History", activate: true);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.selectHistory":
            {
                var payload = BridgeProtocol.ReadPayload<BrowserHistoryPayload>(command.Payload);
                var entry = _browserHistory.FirstOrDefault(item =>
                    string.Equals(item.EntryId, payload.EntryId, StringComparison.Ordinal));
                if (entry is null || !IsExternalBrowserUrl(entry.Url, out var historyUri))
                {
                    throw new BridgeProtocolException("The browser history entry was not found.");
                }

                var browserTab = ActiveBrowserTab
                    ?? throw new InvalidOperationException("The browser tab is not initialized.");
                browserTab.Navigate(historyUri);
                browserTab.Url = historyUri.ToString();
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.navigate":
            {
                var payload = BridgeProtocol.ReadPayload<NavigatePayload>(command.Payload);
                if (!BridgeProtocol.TryGetSupportedBrowserUri(payload.Url, out var uri)
                    || uri is null)
                {
                    throw new BridgeProtocolException(
                        "Use an absolute http, https, file, or about URL.");
                }

                _controller.SetStatus($"Navigating to {uri}.");
                if (_activeBrowserTabId is not { } activeTabId)
                {
                    throw new InvalidOperationException("The browser tab is not initialized.");
                }
                if (!_browserTabs.TryGetValue(activeTabId, out var activeTab)
                    || activeTab is null)
                {
                    throw new InvalidOperationException("The browser tab is not initialized.");
                }
                if (IsHistoryUrl(uri.ToString()))
                {
                    NavigateHistory(activeTab);
                    SaveBrowserState();
                    SendState(command.RequestId, sourceWebView);
                    break;
                }
                activeTab.Url = uri.ToString();
                activeTab.Navigate(
                    uri,
                    IsNexusSearchUri(uri) ? payload.NexusSearchName : null);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.back":
            {
                var browser = ActiveBrowserTab?.WebView
                    ?? throw new InvalidOperationException("The browser tab is not initialized.");
                if (browser.CanGoBack)
                {
                    browser.GoBack();
                }
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.forward":
            {
                var browser = ActiveBrowserTab?.WebView
                    ?? throw new InvalidOperationException("The browser tab is not initialized.");
                if (browser.CanGoForward)
                {
                    browser.GoForward();
                }
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "browser.reload":
                (ActiveBrowserTab?.WebView
                    ?? throw new InvalidOperationException("The browser tab is not initialized.")).Reload();
                SendState(command.RequestId, sourceWebView);
                break;
            case "browser.observe":
                await ObservePageAsync(sourceWebView, command.RequestId);
                break;
            case "knowledge.useFixture":
            {
                var fixtureTask = _controller.UseFixtureAsync();
                SendState(targetWebView: sourceWebView);
                await fixtureTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.selectEvidenceManifest":
            {
                var path = ChooseFile(
                    "Select the version evidence manifest",
                    "JSON manifest (*.json)|*.json|All files (*.*)|*.*");
                if (path is not null)
                {
                    var loadTask = _controller.LoadVersionEvidenceManifestAsync(path);
                    SendState(targetWebView: sourceWebView);
                    await loadTask;
                }

                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.setWebVersionObservation":
            {
                var payload = BridgeProtocol.ReadPayload<SetWebVersionObservationPayload>(command.Payload);
                _controller.SetSessionWebVersionObservation(payload.RawValue);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "analysis.selectBaseData":
            {
                var path = ChooseFolder("Select the 7 Days to Die base Data/Config folder");
                if (path is not null)
                {
                    _controller.SetBaseDataPath(path);
                }

                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "analysis.selectRuntimeLogs":
            {
                var path = ChooseFolder("Select the RuntimeOCD logs folder");
                if (path is not null)
                {
                    _controller.SetRuntimeLogsPath(path);
                }

                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "analysis.analyzeConflicts":
            {
                var analysisTask = _controller.AnalyzeConflictsAsync();
                SendState(targetWebView: sourceWebView);
                await analysisTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "analysis.compareRuntimeEvidence":
            {
                var payload = BridgeProtocol.ReadPayload<CompareRuntimeEvidencePayload>(command.Payload);
                var comparisonTask = _controller.CompareRuntimeEvidenceAsync(
                    payload.ToolVersion,
                    payload.GameVersion);
                SendState(targetWebView: sourceWebView);
                await comparisonTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "analysis.useFixture":
            {
                var fixtureTask = _controller.UseAnalysisFixtureAsync();
                SendState(targetWebView: sourceWebView);
                await fixtureTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.loadSource":
            {
                var payload = BridgeProtocol.ReadPayload<LoadSourcePayload>(command.Payload);
                if (string.IsNullOrWhiteSpace(payload.CandidateId))
                {
                    throw new BridgeProtocolException("Select a discovered MO2 source before loading it.");
                }

                var loadTask = _controller.LoadSourceCandidateAsync(payload.CandidateId);
                SendState(targetWebView: sourceWebView);
                await loadTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.discoverSources":
            {
                var payload = BridgeProtocol.ReadPayload<DiscoverSourcesPayload>(command.Payload);
                var discoveryTask = _controller.DiscoverSourcesAsync(payload.SelectedRoots);
                SendState(targetWebView: sourceWebView);
                await discoveryTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.selectSource":
            {
                var payload = BridgeProtocol.ReadPayload<SelectSourcePayload>(command.Payload);
                var loadTask = _controller.LoadSourceCandidateAsync(payload.CandidateId);
                SendState(targetWebView: sourceWebView);
                await loadTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.selectRoot":
            {
                var root = ChooseMo2Root();
                if (root is not null)
                {
                    var discoveryTask = _controller.DiscoverSourcesAsync(new[] { root });
                    SendState(targetWebView: sourceWebView);
                    await discoveryTask;
                }

                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "knowledge.switchProfile":
            {
                var payload = BridgeProtocol.ReadPayload<SwitchProfilePayload>(command.Payload);
                if (string.IsNullOrWhiteSpace(payload.ProfileName))
                {
                    throw new BridgeProtocolException("The profile name is required.");
                }

                var switchTask = _controller.SwitchProfileAsync(payload.ProfileName);
                SendState(targetWebView: sourceWebView);
                await switchTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "identity.confirm":
            {
                var payload = BridgeProtocol.ReadPayload<ConfirmIdentityPayload>(command.Payload);
                _controller.ConfirmIdentity(payload.CandidateIdentity, payload.LocalModKey);
                SendState(command.RequestId, sourceWebView);
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
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "layout.setModListVisible":
            {
                if (!command.Payload.TryGetProperty("visible", out var visible)
                    || visible.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new BridgeProtocolException("The MOD list visibility must be a boolean.");
                }

                var payload = BridgeProtocol.ReadPayload<SetModListVisiblePayload>(command.Payload);
                _controller.SetModListVisible(payload.Visible);
                ModListColumn.Width = payload.Visible
                    ? new GridLength(280)
                    : new GridLength(0);
                ModListShell.Visibility = payload.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "layout.setToolbarExpanded":
            {
                if (!command.Payload.TryGetProperty("expanded", out var expanded)
                    || expanded.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new BridgeProtocolException("The toolbar expanded state must be a boolean.");
                }

                var payload = BridgeProtocol.ReadPayload<SetToolbarExpandedPayload>(command.Payload);
                ToolbarRow.Height = new GridLength(
                    payload.Expanded ? ToolbarExpandedHeight : ToolbarCollapsedHeight);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "layout.setContextMode":
            {
                var payload = BridgeProtocol.ReadContextModePayload(command.Payload);
                _controller.SetContextMode(payload.Mode);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "layout.setModListMode":
            {
                var payload = BridgeProtocol.ReadModListModePayload(command.Payload);
                _controller.SetModListMode(payload.Mode);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "inspector.open":
            {
                var payload = BridgeProtocol.ReadPayload<InspectorOpenPayload>(command.Payload);
                _controller.OpenInspector(payload.ModKey);
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "deployment.preview":
            {
                var payload = BridgeProtocol.ReadPayload<DeploymentPreviewPayload>(command.Payload);
                var draft = new ModScope.Deployment.DeploymentDraft(
                    payload.ProfileName,
                    payload.Entries
                        .Select(entry => new ModScope.Deployment.DeploymentEntryDraft(
                            entry.ModKey,
                            entry.Enabled,
                            entry.Order))
                        .ToList()
                        .AsReadOnly());
                var previewTask = _controller.PreviewDeploymentAsync(draft);
                SendState(targetWebView: sourceWebView);
                await previewTask;
                await OpenDeploymentPreviewTabAsync();
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "deployment.apply":
            {
                var payload = BridgeProtocol.ReadPayload<DeploymentApplyPayload>(command.Payload);
                var applyTask = _controller.ApplyDeploymentAsync(payload.PlanId, payload.Approved);
                SendState(targetWebView: sourceWebView);
                await applyTask;
                SendState(command.RequestId, sourceWebView);
                break;
            }
            case "game.launch":
                _controller.LaunchGame();
                SendState(command.RequestId, sourceWebView);
                break;
            default:
                throw new BridgeProtocolException($"Unhandled bridge command: {command.Command}.");
        }
    }

    private async Task ObservePageAsync(
        Microsoft.Web.WebView2.Wpf.WebView2? targetWebView,
        string? requestId,
        BrowserTabHostState? browserTab = null)
    {
        var tab = browserTab ?? ActiveBrowserTab;
        if (tab?.WebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("The browser WebView2 is not initialized.");
        }

        var browser = tab.WebView;
        tab.Url = tab.InternalPage switch
        {
            "history" => "about:history",
            "deployment-preview" => "about:deployment-preview",
            _ => browser.Source?.ToString() ?? "about:blank"
        };
        tab.Title = string.IsNullOrWhiteSpace(browser.CoreWebView2.DocumentTitle)
            ? tab.Title
            : browser.CoreWebView2.DocumentTitle;
        AddBrowserHistory(tab);
        SaveBrowserState();

        if (tab.InternalPage is "history" or "deployment-preview")
        {
            SendState(requestId, targetWebView);
            return;
        }

        if (!ReferenceEquals(tab, ActiveBrowserTab))
        {
            SendState();
            return;
        }

        try
        {
            var serializedContent = await browser.ExecuteScriptAsync(
                "document.body ? document.body.innerText : null");
            var content = JsonSerializer.Deserialize<string>(serializedContent);
            var pageUri = tab.InternalPage == "history"
                ? new Uri("about:history")
                : browser.Source ?? new Uri("about:blank");
            var title = browser.CoreWebView2.DocumentTitle ?? string.Empty;
            var extractionStatus = content is null
                ? PageExtractionStatus.Partial
                : PageExtractionStatus.Succeeded;
            var observedAtUtc = DateTimeOffset.UtcNow;

            _controller.SetObservation(new PageObservation(
                pageUri,
                title,
                content,
                observedAtUtc,
                "WebView2",
                extractionStatus,
                Array.Empty<DiagnosticReadModel>()));
            _controller.SetDetectedWebVersionObservation(
                KnownSiteVersionObserver.Observe(pageUri, content),
                pageUri,
                observedAtUtc);
            _controller.SetDetectedWebCompatibilityObservations(
                KnownSiteCompatibilityObserver.Observe(pageUri, content),
                pageUri,
                observedAtUtc);
            SendState(requestId, targetWebView);
        }
        catch (Exception exception)
        {
            var pageUri = tab.InternalPage == "history"
                ? new Uri("about:history")
                : browser.Source ?? new Uri("about:blank");
            _controller.SetObservation(new PageObservation(
                pageUri,
                browser.CoreWebView2.DocumentTitle ?? string.Empty,
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
            SendError("browser.observation.failed", exception.Message, requestId, targetWebView);
        }
    }

    private async void Browser_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        var tab = FindBrowserTab(sender);
        if (tab is null)
        {
            return;
        }

        var source = tab.WebView.Source?.ToString() ?? "about:blank";
        var documentTitle = tab.WebView.CoreWebView2?.DocumentTitle;
        if (tab.InternalPage is null
            && string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            tab.InternalPage = documentTitle switch
            {
                "History" => "history",
                "ModScope Home" => "home",
                _ => null
            };
        }

        if (!IsInternalBrowserPage(tab.InternalPage)
            && !string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            tab.InternalPage = null;
        }

        tab.Url = tab.InternalPage switch
        {
            "history" => "about:history",
            "deployment-preview" => "about:deployment-preview",
            _ => source
        };
        tab.Title = string.IsNullOrWhiteSpace(documentTitle)
            ? tab.Title
            : documentTitle;
        if (!e.IsSuccess)
        {
            tab.PendingNexusSearchName = null;
            if (!ReferenceEquals(tab, ActiveBrowserTab))
            {
                SaveBrowserState();
                SendState();
                return;
            }

            _controller.SetObservation(new PageObservation(
                tab.InternalPage == "history"
                    ? new Uri("about:history")
                    : tab.WebView.Source ?? new Uri("about:blank"),
                documentTitle ?? string.Empty,
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

        if (await TryResolveNexusSearchAsync(tab))
        {
            return;
        }

        await ObservePageAsync(null, null, tab);
    }

    private void Browser_DocumentTitleChanged(object? sender, object e)
    {
        var tab = FindBrowserTab(sender);
        if (tab is not null && tab.WebView.CoreWebView2 is not null)
        {
            var title = tab.WebView.CoreWebView2.DocumentTitle;
            if (tab.InternalPage is null
                && string.Equals(tab.WebView.Source?.ToString(), "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                tab.InternalPage = title switch
                {
                    "History" => "history",
                    "ModScope Home" => "home",
                    _ => null
                };
            }

            if (tab.InternalPage == "history")
            {
                tab.Url = "about:history";
            }
            else if (tab.InternalPage == "deployment-preview")
            {
                tab.Url = "about:deployment-preview";
            }

            tab.Title = string.IsNullOrWhiteSpace(title)
                ? tab.Title
                : title;
            SaveBrowserState();
        }

        SendState();
    }

    private void SendState(
        string? requestId = null,
        Microsoft.Web.WebView2.Wpf.WebView2? targetWebView = null)
    {
        var browserTab = ActiveBrowserTab;
        if (browserTab?.WebView.CoreWebView2 is null
            || ((!_toolbarReady && !_modListReady && !_contextReady)
                && targetWebView?.CoreWebView2 is null))
        {
            return;
        }

        var browser = browserTab.WebView;
        var browserState = new BrowserUiState(
            browserTab.Url,
            browserTab.Title,
            browser.CanGoBack,
            browser.CanGoForward,
            _browserTabs.Values
                .Select(tab => new BrowserTabUiState(
                    tab.TabId,
                    tab.Title,
                    tab.Url,
                    tab.WebView.CanGoBack,
                    tab.WebView.CanGoForward,
                    string.Equals(tab.TabId, _activeBrowserTabId, StringComparison.Ordinal)))
                .ToList(),
            _activeBrowserTabId,
            _browserHistory);
        var state = _controller.BuildState(browserState);
        SendMessage("state", state, requestId, targetWebView);
    }

    private void Controller_OperationStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateLoadingOverlay();
            SendState();
            return;
        }

        _ = Dispatcher.InvokeAsync(() =>
        {
            UpdateLoadingOverlay();
            SendState();
        });
    }

    private void UpdateLoadingOverlay()
    {
        var operation = _controller.CurrentOperation;
        var showOverlay = IsForegroundLoading(operation);
        LoadingOverlay.Visibility = showOverlay ? Visibility.Visible : Visibility.Collapsed;
        SetClientInteractionEnabled(!showOverlay);
        if (!showOverlay)
        {
            return;
        }

        LoadingOverlayTitle.Text = string.IsNullOrWhiteSpace(operation.TargetProfileName)
            ? "Loading local MO2 knowledge..."
            : $"Loading profile {operation.TargetProfileName}...";
        LoadingOverlayPhase.Text = string.IsNullOrWhiteSpace(operation.Phase) || operation.Phase == "idle"
            ? "Preparing local profile..."
            : operation.Phase.Replace('-', ' ');

        if (operation.Completed is int completed
            && operation.Total is int total
            && total > 0)
        {
            LoadingOverlayProgress.IsIndeterminate = false;
            LoadingOverlayProgress.Maximum = total;
            LoadingOverlayProgress.Value = Math.Clamp(completed, 0, total);
        }
        else
        {
            LoadingOverlayProgress.IsIndeterminate = true;
        }
    }

    private bool IsForegroundLoading()
    {
        return IsForegroundLoading(_controller.CurrentOperation);
    }

    private bool IsForegroundLoading(KnowledgeOperationUiState operation)
    {
        return _startupLoading || (operation.IsBusy && !operation.IsBackground);
    }

    private void SetClientInteractionEnabled(bool enabled)
    {
        ToolbarHost.IsEnabled = enabled;
        ToolbarHost.IsHitTestVisible = enabled;
        ModListShell.IsEnabled = enabled;
        ModListShell.IsHitTestVisible = enabled;
        ContextShell.IsEnabled = enabled;
        ContextShell.IsHitTestVisible = enabled;
        SetWebViewInteractionEnabled(ToolbarShell, enabled);
        SetWebViewInteractionEnabled(ModListWebView, enabled);
        SetWebViewInteractionEnabled(ContextWebView, enabled);
        BrowserHost.IsEnabled = enabled;
        BrowserHost.IsHitTestVisible = enabled;

        foreach (var tab in _browserTabs.Values)
        {
            SetWebViewInteractionEnabled(tab.WebView, enabled);
        }

        LoadingOverlay.IsHitTestVisible = !enabled;
    }

    private static void SetWebViewInteractionEnabled(
        Microsoft.Web.WebView2.Wpf.WebView2 webView,
        bool enabled)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        webView.IsEnabled = enabled;
        webView.IsHitTestVisible = enabled;
    }

    private static string? ChooseMo2Root()
    {
        return ChooseFolder("Select the MO2 instance or portable root");
    }

    private static string? ChooseFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string? ChooseFile(string title, string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void SendError(
        string code,
        string message,
        string? requestId = null,
        Microsoft.Web.WebView2.Wpf.WebView2? targetWebView = null)
    {
        _controller.SetStatus(message);
        if (targetWebView?.CoreWebView2 is null
            && !_toolbarReady
            && !_modListReady
            && !_contextReady)
        {
            return;
        }

        SendState(requestId, targetWebView);
        SendMessage("error", new BridgeErrorPayload(code, message), requestId, targetWebView);
    }

    private void SendMessage<T>(
        string kind,
        T payload,
        string? requestId = null,
        Microsoft.Web.WebView2.Wpf.WebView2? targetWebView = null)
    {
        var message = BridgeProtocol.SerializeMessage(kind, payload, requestId);

        if (targetWebView?.CoreWebView2 is not null)
        {
            targetWebView.CoreWebView2.PostWebMessageAsJson(message);
        }

        if (_toolbarReady && ToolbarShell.CoreWebView2 is not null)
        {
            if (!ReferenceEquals(targetWebView, ToolbarShell))
            {
                ToolbarShell.CoreWebView2.PostWebMessageAsJson(message);
            }
        }

        if (_contextReady && ContextWebView.CoreWebView2 is not null)
        {
            if (!ReferenceEquals(targetWebView, ContextWebView))
            {
                ContextWebView.CoreWebView2.PostWebMessageAsJson(message);
            }
        }

        if (_modListReady && ModListWebView.CoreWebView2 is not null)
        {
            if (!ReferenceEquals(targetWebView, ModListWebView))
            {
                ModListWebView.CoreWebView2.PostWebMessageAsJson(message);
            }
        }

        foreach (var browserTab in _browserTabs.Values.Where(tab => tab.IsDeploymentPreview))
        {
            if (browserTab.WebView.CoreWebView2 is not null
                && !ReferenceEquals(targetWebView, browserTab.WebView))
            {
                browserTab.WebView.CoreWebView2.PostWebMessageAsJson(message);
            }
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
