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
}
