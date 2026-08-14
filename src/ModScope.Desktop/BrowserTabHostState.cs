using Microsoft.Web.WebView2.Wpf;

namespace ModScope.Desktop;

internal sealed class BrowserTabHostState
{
    public BrowserTabHostState(string tabId, WebView2 webView)
    {
        TabId = tabId;
        WebView = webView;
    }

    public string TabId { get; }

    public WebView2 WebView { get; }

    public string Title { get; set; } = "New tab";

    public string Url { get; set; } = "about:blank";

    public string? InternalPage { get; set; }

    public bool IsDeploymentPreview =>
        string.Equals(InternalPage, "deployment-preview", StringComparison.Ordinal);

    public string? PendingNexusSearchName { get; set; }

    public void Navigate(Uri uri, string? nexusSearchName = null)
    {
        InternalPage = null;
        PendingNexusSearchName = string.IsNullOrWhiteSpace(nexusSearchName)
            ? null
            : nexusSearchName.Trim();
        WebView.Source = uri;
    }
}
