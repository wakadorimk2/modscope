using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using ModScope.Query;

namespace ModScope.Desktop;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.NavigationCompleted += Browser_NavigationCompleted;
            Browser.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;

            var demoPage = Path.Combine(AppContext.BaseDirectory, "Fixtures", "alpha-mod.html");
            Browser.Source = File.Exists(demoPage)
                ? new Uri(demoPage)
                : new Uri("about:blank");
        }
        catch (Exception exception)
        {
            _viewModel.SetPageObservation(new PageObservation(
                new Uri("about:blank"),
                "WebView2 unavailable",
                null,
                DateTimeOffset.UtcNow,
                "WebView2",
                PageExtractionStatus.Failed,
                new[]
                {
                    new DiagnosticReadModel(
                        "browser.initialization.failed",
                        QueryDiagnosticSeverity.Error,
                        exception.Message)
                }));
        }
    }

    private void NavigateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(_viewModel.Url, UriKind.Absolute, out var uri) || !IsSupportedScheme(uri))
        {
            _viewModel.SetPageObservation(new PageObservation(
                new Uri("about:blank"),
                "Invalid URL",
                null,
                DateTimeOffset.UtcNow,
                "WebView2",
                PageExtractionStatus.Failed,
                new[]
                {
                    new DiagnosticReadModel(
                        "browser.url.invalid",
                        QueryDiagnosticSeverity.Error,
                        "Use an absolute http, https, file, or about URL.",
                        RawValue: _viewModel.Url)
                }));
            return;
        }

        Browser.Source = uri;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack)
        {
            Browser.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoForward)
        {
            Browser.GoForward();
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        Browser.Reload();
    }

    private void LoadFixtureButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadSyntheticFixture();
    }

    private void LoadSourceButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TryLoadSource(out _);
    }

    private void ConfirmInstalledButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TryConfirmIdentity(noLocalMatch: false, out _);
    }

    private void ConfirmNotInstalledButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TryConfirmIdentity(noLocalMatch: true, out _);
    }

    private async void ObserveButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var serializedContent = await Browser.ExecuteScriptAsync("document.body ? document.body.innerText : null");
            var content = JsonSerializer.Deserialize<string>(serializedContent);
            var boundedContent = content is null
                ? null
                : content.Length <= PageObservation.MaxContentPreviewLength
                    ? content
                    : content[..PageObservation.MaxContentPreviewLength];
            var url = Browser.Source ?? new Uri("about:blank");
            var title = Browser.CoreWebView2.DocumentTitle ?? string.Empty;
            var status = content is null ? PageExtractionStatus.Partial : PageExtractionStatus.Succeeded;
            _viewModel.SetPageObservation(new PageObservation(
                url,
                title,
                boundedContent,
                DateTimeOffset.UtcNow,
                "WebView2",
                status,
                Array.Empty<DiagnosticReadModel>()));
        }
        catch (Exception exception)
        {
            var url = Browser.Source ?? new Uri("about:blank");
            _viewModel.SetPageObservation(new PageObservation(
                url,
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
        }
    }

    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _viewModel.SetPageObservation(new PageObservation(
                Browser.Source ?? new Uri("about:blank"),
                Browser.CoreWebView2.DocumentTitle ?? string.Empty,
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
    }

    private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
    {
        // The title is captured by the explicit Observe action.
    }

    private static bool IsSupportedScheme(Uri uri)
    {
        return uri.Scheme is "http" or "https" or "file" or "about";
    }
}
