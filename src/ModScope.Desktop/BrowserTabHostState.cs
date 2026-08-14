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

    public void Navigate(Uri uri)
    {
        WebView.Source = uri;
    }
}
