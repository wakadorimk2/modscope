using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class KnownSiteVersionObserverTests
{
    [Fact]
    public void ReadsTheFirstVisibleGitHubReleaseTag()
    {
        var result = KnownSiteVersionObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Release list\n3.1.25.1615\n3.1.22.801");

        Assert.Equal("GitHub", result.Site);
        Assert.Equal("3.1.25.1615", result.RawValue);
        Assert.True(result.HasVersion);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ReadsTheFirstFileVersionFromTheNexusFilesSurface()
    {
        var result = KnownSiteVersionObserver.Observe(
            new Uri("https://www.nexusmods.com/7daystodie/mods/123?tab=files"),
            "Description\nFiles\nFile 1\nVersion: 3.1.25.1615\nFile 2\nVersion: 3.1.24.1000");

        Assert.Equal("Nexus", result.Site);
        Assert.Equal("3.1.25.1615", result.RawValue);
        Assert.Equal("Nexus Files first visible File version", result.Evidence);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void SelectsTheFirstVisibleNexusFileScopeAsLatest()
    {
        var result = KnownSiteVersionObserver.Observe(
            new Uri("https://www.nexusmods.com/7daystodie/mods/123?tab=files"),
            "Files",
            new[]
            {
                new WebReleaseScopeInput(
                    "NexusFile",
                    "3.1.25.1615",
                    "3.1.25.1615",
                    "https://www.nexusmods.com/7daystodie/mods/123?tab=files&file_id=1",
                    "Version: 3.1.25.1615",
                    "File 1\nVersion: 3.1.25.1615"),
                new WebReleaseScopeInput(
                    "NexusFile",
                    "3.1.22.801",
                    "3.1.22.801",
                    "https://www.nexusmods.com/7daystodie/mods/123?tab=files&file_id=2",
                    "Version: 3.1.22.801",
                    "File 2\nVersion: 3.1.22.801")
            });

        Assert.Equal("3.1.25.1615", result.RawValue);
        Assert.Equal("NexusFile", result.ReleaseScopeKind);
        Assert.Equal("3.1.25.1615", result.ReleaseScopeVersion);
        Assert.Contains("file_id=1", result.ReleaseScopeUrl);
    }

    [Fact]
    public void ReadsNexusModPageVersionScopeWithRawAndNormalizedEvidence()
    {
        var pageUrl = new Uri("https://www.nexusmods.com/7daystodie/mods/2409");
        var result = KnownSiteVersionObserver.Observe(
            pageUrl,
            "Version v8.0.1\nDescription",
            new[]
            {
                new WebReleaseScopeInput(
                    "NexusModPage",
                    "v8.0.1",
                    "8.0.1",
                    pageUrl.ToString(),
                    "Version v8.0.1",
                    "Version v8.0.1"),
                new WebReleaseScopeInput(
                    "Page",
                    null,
                    null,
                    pageUrl.ToString(),
                    null,
                    "Version v8.0.1\nDescription")
            });

        Assert.Equal("v8.0.1", result.RawValue);
        Assert.Equal("NexusModPage", result.ReleaseScopeKind);
        Assert.Equal("v8.0.1", result.ReleaseScopeRawVersion);
        Assert.Equal("8.0.1", result.ReleaseScopeVersion);
        Assert.Equal(pageUrl.ToString(), result.ReleaseScopeUrl);
        Assert.Equal("Version v8.0.1", result.ReleaseScopeMatchedLine);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void KeepsNexusModPageMissingMultipleAndInvalidVersionEvidence()
    {
        var pageUrl = new Uri("https://www.nexusmods.com/7daystodie/mods/2409");
        var missing = KnownSiteVersionObserver.Observe(
            pageUrl,
            "Version",
            new[]
            {
                new WebReleaseScopeInput(
                    "NexusModPage",
                    null,
                    null,
                    pageUrl.ToString(),
                    "Version",
                    "Version")
            });
        var multiple = KnownSiteVersionObserver.Observe(
            pageUrl,
            "Version v8.0.1\nVersion v8.0.2",
            new[]
            {
                new WebReleaseScopeInput(
                    "NexusModPage",
                    "v8.0.1",
                    "8.0.1",
                    pageUrl.ToString(),
                    "Version v8.0.1",
                    "Version v8.0.1"),
                new WebReleaseScopeInput(
                    "NexusModPage",
                    "v8.0.2",
                    "8.0.2",
                    pageUrl.ToString(),
                    "Version v8.0.2",
                    "Version v8.0.2")
            });
        var invalid = KnownSiteVersionObserver.Observe(
            pageUrl,
            "Version beta",
            new[]
            {
                new WebReleaseScopeInput(
                    "NexusModPage",
                    "beta",
                    null,
                    pageUrl.ToString(),
                    "Version beta",
                    "Version beta")
            });

        Assert.Null(missing.RawValue);
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Code == "web.version.missing");
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.RawValue == "Version");
        Assert.Null(multiple.RawValue);
        Assert.Contains(multiple.Diagnostics, diagnostic =>
            diagnostic.Code == "web.version.multiple-candidates"
            && diagnostic.RawValue is not null
            && diagnostic.RawValue.Contains("v8.0.1", StringComparison.Ordinal)
            && diagnostic.RawValue.Contains("v8.0.2", StringComparison.Ordinal));
        Assert.Null(invalid.RawValue);
        Assert.Contains(invalid.Diagnostics, diagnostic =>
            diagnostic.Code == "web.version.unsupported-format"
            && diagnostic.RawValue == "beta");
    }

    [Fact]
    public void DoesNotUsePageScopeAsNexusModPageVersionFallback()
    {
        var pageUrl = new Uri("https://www.nexusmods.com/7daystodie/mods/2409");
        var result = KnownSiteVersionObserver.Observe(
            pageUrl,
            "Version v8.0.1",
            new[]
            {
                new WebReleaseScopeInput(
                    "Page",
                    null,
                    null,
                    pageUrl.ToString(),
                    null,
                    "Version v8.0.1")
            });

        Assert.Null(result.RawValue);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "web.version.missing");
    }

    [Fact]
    public void KeepsUnsupportedMissingMultipleAndInvalidPagesUnresolved()
    {
        var unsupported = KnownSiteVersionObserver.Observe(
            new Uri("https://example.com/mod"),
            "Latest release: 1.2.3");
        var missing = KnownSiteVersionObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Release list");
        var multiple = KnownSiteVersionObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "1.2.3 / 1.2.4");
        var invalid = KnownSiteVersionObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases/tag/latest"),
            "");

        Assert.Null(unsupported.RawValue);
        Assert.Contains(unsupported.Diagnostics, diagnostic => diagnostic.Code == "web.version.unsupported-page");
        Assert.Null(missing.RawValue);
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Code == "web.version.missing");
        Assert.Null(multiple.RawValue);
        Assert.Contains(multiple.Diagnostics, diagnostic => diagnostic.Code == "web.version.multiple-candidates");
        Assert.Null(invalid.RawValue);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "web.version.unsupported-format");
    }

    [Fact]
    public void SelectsTheFirstVisibleGitHubReleaseScopeAsLatest()
    {
        var scopes = new[]
        {
            new WebReleaseScopeInput(
                "GitHubRelease",
                "3.1.25.1615",
                "3.1.25.1615",
                "https://github.com/Sperell/Sperell.Mods/releases/tag/3.1.25.1615",
                "3.1.25.1615",
                "3.1.25.1615\nGame Version: v3.1.0 (b14)"),
            new WebReleaseScopeInput(
                "GitHubRelease",
                "3.1.22.801",
                "3.1.22.801",
                "https://github.com/Sperell/Sperell.Mods/releases/tag/3.1.22.801",
                "3.1.22.801",
                "3.1.22.801\nGame Version: v3.1.0 (b13)"),
            new WebReleaseScopeInput(
                "GitHubRelease",
                "3.1.9.1528",
                "3.1.9.1528",
                "https://github.com/Sperell/Sperell.Mods/releases/tag/3.1.9.1528",
                "3.1.9.1528",
                "3.1.9.1528\nGame Version: v3.1.0 (b11)")
        };

        var result = KnownSiteVersionObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "release list",
            scopes);

        Assert.Equal("3.1.25.1615", result.RawValue);
        Assert.Equal("GitHubRelease", result.ReleaseScopeKind);
        Assert.Equal("3.1.25.1615", result.ReleaseScopeVersion);
        Assert.Equal(
            "https://github.com/Sperell/Sperell.Mods/releases/tag/3.1.25.1615",
            result.ReleaseScopeUrl);
        Assert.Equal("3.1.25.1615", result.ReleaseScopeMatchedLine);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void KeepsAnObservedVersionWhenReleaseScopeCannotBeResolved()
    {
        var result = KnownSiteVersionObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "3.1.25.1615",
            Array.Empty<WebReleaseScopeInput>());

        Assert.Equal("3.1.25.1615", result.RawValue);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "web.version.release-scope-unresolved");
    }
}
