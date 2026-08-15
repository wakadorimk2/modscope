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
}
