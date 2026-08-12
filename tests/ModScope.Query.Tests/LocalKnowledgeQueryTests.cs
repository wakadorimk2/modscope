using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class LocalKnowledgeQueryTests
{
    [Fact]
    public void LoadsSessionAndProjectsCandidateSummaries()
    {
        var query = CreateQuery();

        var session = query.Load(CreateSource(FixtureRoot));
        var alpha = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Alpha Mod");

        Assert.Equal("default", session.ProfileName);
        Assert.Equal(QueryProfileState.Listed, alpha.ProfileState);
        Assert.Equal(QueryEnabledState.Enabled, alpha.EnabledState);
        Assert.Equal(0, alpha.Priority);
        Assert.Equal("Alpha Display", alpha.DisplayName);
        Assert.Equal("1.2.3", alpha.Version);
        Assert.Equal(QuerySourceReferenceKind.ModDirectory, alpha.Source.Kind);
        Assert.Equal(QuerySourceReferenceKind.ProfileFile, alpha.PriorityEvidence?.Source.Kind);
    }

    [Fact]
    public void ResolvesInstalledNotInstalledUnresolvedAndUnknownStates()
    {
        var query = CreateQuery();

        var beforeLoad = LocalKnowledgeQueryService.CreateDefault().ConfirmIdentity(
            new IdentityConfirmation(
                CreatePage("https://example.test/alpha"),
                "Alpha Mod",
                null));

        query.Load(CreateSource(FixtureRoot));
        var alphaKey = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Alpha Mod").ModKey;

        var installed = query.ConfirmIdentity(
            new IdentityConfirmation(CreatePage("https://example.test/alpha"), "Alpha Mod", alphaKey));
        var notInstalled = query.ConfirmIdentity(
            new IdentityConfirmation(CreatePage("https://example.test/missing"), "Missing Web Mod", null));
        var unresolved = query.ConfirmIdentity(
            new IdentityConfirmation(CreatePage("https://example.test/unknown"), string.Empty, null));

        Assert.Equal(LocalContextStatus.Unknown, beforeLoad.Status);
        Assert.Equal(LocalContextStatus.Installed, installed.Status);
        Assert.Equal(QueryEnabledState.Enabled, installed.EnabledState);
        Assert.Equal(0, installed.Priority);
        Assert.Equal("1.2.3", installed.KnownVersion);
        Assert.Equal(LocalContextStatus.NotInstalled, notInstalled.Status);
        Assert.Equal(LocalContextStatus.Unresolved, unresolved.Status);
    }

    [Fact]
    public void ProjectsInspectorMetadataFilesXmlAndDiagnostics()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));
        var betaKey = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Beta Mod").ModKey;

        var inspector = query.GetInspector(betaKey);

        Assert.Equal("Beta Mod", inspector.DirectoryName);
        Assert.Equal("Beta Mod", inspector.ModInfo?.Name);
        Assert.Equal("0.5", inspector.ModInfo?.Version);
        Assert.NotEmpty(inspector.Files);
        Assert.Contains(inspector.XmlFiles, file => file.RelativePath.Contains("malformed.xml", StringComparison.Ordinal));
        Assert.Contains(inspector.XmlFiles.SelectMany(file => file.Diagnostics), diagnostic => diagnostic.Code == "xml.malformed");
    }

    [Fact]
    public void PreservesUnknownXmlObservationsAndBoundsPagePreview()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));
        var alphaKey = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Alpha Mod").ModKey;
        var inspector = query.GetInspector(alphaKey);
        var page = CreatePage("https://example.test/alpha", new string('x', PageObservation.MaxContentPreviewLength + 1));

        Assert.Contains(inspector.ModInfo!.UnknownObservations, observation => observation.ElementName == "UnknownElement");
        Assert.Equal(PageObservation.MaxContentPreviewLength, page.BoundedContentPreview!.Length);
    }

    [Fact]
    public void ListsOnlyExplicitInstanceProfilesAndSwitchesReadOnlySession()
    {
        var root = Directory.CreateTempSubdirectory("modscope-profiles-");
        try
        {
            var profilesPath = Directory.CreateDirectory(Path.Combine(root.FullName, "profiles"));
            var defaultProfile = CreateProfile(profilesPath.FullName, "Default", "+Alpha Mod");
            CreateProfile(profilesPath.FullName, "Alternate", "+Beta Mod");
            CreateProfile(root.FullName, "OutsideCatalog", "+Alpha Mod");
            var modsPath = Directory.CreateDirectory(Path.Combine(root.FullName, "mods"));
            var alphaModPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Alpha Mod"));
            var betaModPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Beta Mod"));
            File.WriteAllText(
                Path.Combine(alphaModPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Alpha Mod\" /></xml>");
            File.WriteAllText(
                Path.Combine(betaModPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Beta Mod\" /></xml>");

            var query = CreateQuery();
            query.Load(new Mo2SourceInput(
                "synthetic-instance",
                "Default",
                root.FullName,
                defaultProfile,
                modsPath.FullName));

            Assert.Equal(
                new[] { "Alternate", "Default" },
                query.GetProfiles().Select(profile => profile.ProfileName));

            var switched = query.SwitchProfile("Alternate");

            Assert.Equal("Alternate", switched.ProfileName);
            Assert.Contains(query.GetModCandidates(), candidate => candidate.DirectoryName == "Beta Mod");
            Assert.DoesNotContain(query.GetProfiles(), profile => profile.ProfileName == "OutsideCatalog");
            Assert.Throws<ArgumentException>(() => query.SwitchProfile("OutsideCatalog"));
            Assert.Equal("Beta Mod", query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Beta Mod").DirectoryName);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void FallsBackToCurrentExplicitProfileWhenProfilesDirectoryIsMissing()
    {
        var root = Directory.CreateTempSubdirectory("modscope-profile-fallback-");
        try
        {
            var profilePath = Directory.CreateDirectory(Path.Combine(root.FullName, "profile"));
            File.WriteAllText(Path.Combine(profilePath.FullName, "modlist.txt"), "");
            var modsPath = Directory.CreateDirectory(Path.Combine(root.FullName, "mods"));

            var query = CreateQuery();
            query.Load(new Mo2SourceInput(
                "synthetic-instance",
                "FixtureProfile",
                root.FullName,
                profilePath.FullName,
                modsPath.FullName));

            var profile = Assert.Single(query.GetProfiles());
            Assert.Equal("FixtureProfile", profile.ProfileName);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static ILocalKnowledgeQuery CreateQuery()
    {
        return LocalKnowledgeQueryService.CreateDefault();
    }

    private static PageObservation CreatePage(string url, string? content = null)
    {
        return new PageObservation(
            new Uri(url),
            "Synthetic page",
            content,
            DateTimeOffset.UtcNow,
            "test",
            PageExtractionStatus.Succeeded,
            Array.Empty<DiagnosticReadModel>());
    }

    private static Mo2SourceInput CreateSource(string root)
    {
        return new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods"));
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "7dtd-mo2-minimal");

    private static string CreateProfile(string profilesRoot, string name, string modList)
    {
        var profilePath = Directory.CreateDirectory(Path.Combine(profilesRoot, name));
        File.WriteAllText(Path.Combine(profilePath.FullName, "modlist.txt"), modList);
        return profilePath.FullName;
    }
}
