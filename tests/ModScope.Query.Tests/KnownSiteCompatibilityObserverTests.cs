using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class KnownSiteCompatibilityObserverTests
{
    [Fact]
    public void ReadsGitHubGameVersionWithRawNormalizedAndBuildValues()
    {
        var result = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Release\n3.1.25.1615\nGame Version: v3.1.0 (b14)");

        var observation = Assert.Single(result.Observations);
        Assert.Equal("GitHub", result.Site);
        Assert.Equal("GitHub Releases", result.Surface);
        Assert.Equal(WebCompatibilityRelation.GameVersion, observation.Relation);
        Assert.Equal("7DTD", observation.GameContext);
        Assert.Equal("v3.1.0 (b14)", observation.RawValue);
        Assert.Equal("3.1.0", observation.NormalizedVersion);
        Assert.Equal("b14", observation.Build);
        Assert.Equal("Game Version: v3.1.0 (b14)", observation.MatchedLine);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ReadsAllSupportedLabelsFromTheGitHubReleaseSurface()
    {
        var result = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases/tag/3.1.25.1615"),
            "Game Version: 3.1.0\n"
            + "Supported Game Version: 3.1.0\n"
            + "Supported for: 3.1.0\n"
            + "Compatible with: 3.1.0\n"
            + "Requires Game Version: 3.1.0");

        Assert.Equal(
            new[]
            {
                WebCompatibilityRelation.GameVersion,
                WebCompatibilityRelation.SupportedGameVersion,
                WebCompatibilityRelation.SupportedFor,
                WebCompatibilityRelation.CompatibleWith,
                WebCompatibilityRelation.RequiresGameVersion
            },
            result.Observations.Select(observation => observation.Relation));
    }

    [Fact]
    public void ReadsNexusFilesAndDescriptionSurfaces()
    {
        var files = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://www.nexusmods.com/7daystodie/mods/123?tab=files"),
            "Files\nSupported for: 7DTD v3.1.0 (b14)");
        var description = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://www.nexusmods.com/7daystodie/mods/123?tab=description"),
            "Description\nCompatible with: 7 Days to Die v3.1.0");

        Assert.Equal("Nexus Files", files.Surface);
        Assert.Equal("Nexus Description", description.Surface);
        Assert.Equal("3.1.0", Assert.Single(files.Observations).NormalizedVersion);
        Assert.Equal("b14", Assert.Single(files.Observations).Build);
        Assert.Equal("7DTD", Assert.Single(description.Observations).GameContext);
    }

    [Fact]
    public void DeduplicatesIdenticalClaimsButPreservesDifferentTargets()
    {
        var duplicate = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Game Version: 3.1.0\nGame Version: 3.1.0");
        var conflict = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Game Version: 3.1.0\nGame Version: 3.2.0");

        Assert.Single(duplicate.Observations);
        Assert.Equal(2, conflict.Observations.Count);
        Assert.Equal(
            new[] { "3.1.0", "3.2.0" },
            conflict.Observations.Select(observation => observation.NormalizedVersion));
    }

    [Fact]
    public void KeepsMissingMultipleUnsupportedAndOtherGameEvidenceDiagnostic()
    {
        var missing = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Game Version:");
        var multiple = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Compatible with: 7DTD 3.1.0 / 3.2.0");
        var otherGame = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Compatible with: Minecraft 1.20");
        var unsupported = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://example.com/mod"),
            "Game Version: 3.1.0");

        Assert.Contains(Assert.Single(missing.Observations).Diagnostics, diagnostic =>
            diagnostic.Code == "web.compatibility.missing-value");
        Assert.Contains(Assert.Single(multiple.Observations).Diagnostics, diagnostic =>
            diagnostic.Code == "web.compatibility.multiple-candidates");
        var otherObservation = Assert.Single(otherGame.Observations);
        Assert.Equal("Minecraft", otherObservation.GameContext);
        Assert.Contains(otherObservation.Diagnostics, diagnostic =>
            diagnostic.Code == "web.compatibility.other-game");
        Assert.Contains(unsupported.Diagnostics, diagnostic =>
            diagnostic.Code == "web.compatibility.unsupported-page");
    }

    [Fact]
    public void KeepsRequiresGameVersionAsConditionEvidence()
    {
        var result = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://www.nexusmods.com/7daystodie/mods/123"),
            "Description\nRequires Game Version: v3.1.0 (b14)");

        var observation = Assert.Single(result.Observations);
        Assert.True(observation.IsCondition);
        Assert.False(observation.IsPositive);
        Assert.Equal("3.1.0", observation.NormalizedVersion);
    }

    [Fact]
    public void AssociatesEachGitHubCompatibilityClaimWithItsReleaseScope()
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

        var result = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "release list",
            scopes);

        Assert.Equal(3, result.Observations.Count);
        Assert.Equal(
            new[] { "3.1.25.1615", "3.1.22.801", "3.1.9.1528" },
            result.Observations.Select(observation => observation.ReleaseScopeVersion));
        Assert.Equal("b14", result.Observations[0].Build);
        Assert.Equal("b13", result.Observations[1].Build);
        Assert.Equal("b11", result.Observations[2].Build);
    }

    [Fact]
    public void KeepsAClaimWithAnUnresolvedScopeDiagnostic()
    {
        var result = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://github.com/Sperell/Sperell.Mods/releases"),
            "Game Version: v3.1.0 (b14)",
            Array.Empty<WebReleaseScopeInput>());

        var observation = Assert.Single(result.Observations);
        Assert.Equal("Game Version: v3.1.0 (b14)", observation.MatchedLine);
        Assert.Contains(observation.Diagnostics, diagnostic =>
            diagnostic.Code == "web.compatibility.release-scope-unresolved");
    }

    [Fact]
    public void UsesPageScopeForNexusDescriptionClaims()
    {
        var pageUrl = new Uri("https://www.nexusmods.com/7daystodie/mods/123?tab=description");
        var result = KnownSiteCompatibilityObserver.Observe(
            pageUrl,
            "Description\nCompatible with: 7 Days to Die v3.1.0",
            new[]
            {
                new WebReleaseScopeInput(
                    "Page",
                    null,
                    null,
                    pageUrl.ToString(),
                    null,
                    "Description\nCompatible with: 7 Days to Die v3.1.0")
            });

        var observation = Assert.Single(result.Observations);
        Assert.Equal("Page", observation.ReleaseScopeKind);
        Assert.Equal(pageUrl.ToString(), observation.ReleaseScopeUrl);
        Assert.Empty(observation.Diagnostics);
    }

    [Fact]
    public void AssociatesNexusFileCompatibilityWithTheFileRowScope()
    {
        var fileUrl = "https://www.nexusmods.com/7daystodie/mods/123?tab=files&file_id=1";
        var result = KnownSiteCompatibilityObserver.Observe(
            new Uri("https://www.nexusmods.com/7daystodie/mods/123?tab=files"),
            "Files",
            new[]
            {
                new WebReleaseScopeInput(
                    "NexusFile",
                    "3.1.25.1615",
                    "3.1.25.1615",
                    fileUrl,
                    "Version: 3.1.25.1615",
                    "File 1\nVersion: 3.1.25.1615\nSupported for: 7DTD v3.1.0 (b14)")
            });

        var observation = Assert.Single(result.Observations);
        Assert.Equal("NexusFile", observation.ReleaseScopeKind);
        Assert.Equal("3.1.25.1615", observation.ReleaseScopeVersion);
        Assert.Equal(fileUrl, observation.ReleaseScopeUrl);
        Assert.Equal("b14", observation.Build);
    }
}
