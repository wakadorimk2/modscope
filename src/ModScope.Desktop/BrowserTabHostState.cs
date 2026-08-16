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

    public IReadOnlyList<string> PendingNexusSearchNames { get; set; } = Array.Empty<string>();

    public void Navigate(Uri uri, IReadOnlyList<string>? nexusSearchNames = null)
    {
        InternalPage = null;
        PendingNexusSearchNames = nexusSearchNames is null
            ? Array.Empty<string>()
            : nexusSearchNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        WebView.Source = uri;
    }
}
