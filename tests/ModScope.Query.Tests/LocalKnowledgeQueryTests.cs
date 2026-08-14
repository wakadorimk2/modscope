using ModScope.LocalKnowledge;
using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class LocalKnowledgeQueryTests
{
    [Fact]
    public void ProjectsVerifiedInferredAndUnknownModRolesInModScopeOrder()
    {
        var root = Directory.CreateTempSubdirectory("modscope-role-");
        try
        {
            var profilePath = Directory.CreateDirectory(Path.Combine(root.FullName, "profile"));
            var modsPath = Directory.CreateDirectory(Path.Combine(root.FullName, "mods"));
            File.WriteAllText(
                Path.Combine(profilePath.FullName, "modlist.txt"),
                "+Shared Data\n+Compatibility Patch\n+Content Mod\n+No Evidence");

            var foundationPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Shared Data"));
            File.WriteAllText(
                Path.Combine(foundationPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Shared Data\" /></xml>");
            var foundationConfig = Directory.CreateDirectory(Path.Combine(foundationPath.FullName, "Config"));
            File.WriteAllText(Path.Combine(foundationConfig.FullName, "framework.xml"), "<root />");

            var compatibilityPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Compatibility Patch"));
            File.WriteAllText(
                Path.Combine(compatibilityPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Compatibility Patch\" /></xml>");
            var compatibilityConfig = Directory.CreateDirectory(Path.Combine(compatibilityPath.FullName, "Config"));
            File.WriteAllText(
                Path.Combine(compatibilityConfig.FullName, "changes.xml"),
                "<configs><set targetXml=\"Config/framework.xml\" xpath=\"/root\" /></configs>");

            var contentPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Content Mod"));
            File.WriteAllText(
                Path.Combine(contentPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Content Mod\" /></xml>");
            var contentConfig = Directory.CreateDirectory(Path.Combine(contentPath.FullName, "Config"));
            File.WriteAllText(
                Path.Combine(contentConfig.FullName, "content.xml"),
                "<configs><set xpath=\"/root\" targetXml=\"items.xml\" entity=\"item\" property=\"value\" attribute=\"value\">content</set></configs>");

            var unknownPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "No Evidence"));
            File.WriteAllText(
                Path.Combine(unknownPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"No Evidence\" /></xml>");

            var snapshot = new Mo2SnapshotReader().Read(new Mo2SourceDefinition(
                "synthetic-instance",
                "default",
                root.FullName,
                profilePath.FullName,
                modsPath.FullName));
            var roles = ModRoleClassifier.Classify(snapshot);

            Assert.Equal(QueryModRole.Foundation, roles["Shared Data"].Role);
            Assert.Equal(QueryModRoleAssessment.Inferred, roles["Shared Data"].Assessment);
            Assert.Equal(QueryModRole.Compatibility, roles["Compatibility Patch"].Role);
            Assert.Equal(QueryModRoleAssessment.Verified, roles["Compatibility Patch"].Assessment);
            Assert.Equal(QueryModRole.Content, roles["Content Mod"].Role);
            Assert.Equal(QueryModRoleAssessment.Inferred, roles["Content Mod"].Assessment);
            Assert.Equal(QueryModRole.Unknown, roles["No Evidence"].Role);
            Assert.Equal(QueryModRoleAssessment.Unknown, roles["No Evidence"].Assessment);
            Assert.Contains("dependency", roles["Shared Data"].Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("targets this MOD XML", roles["Shared Data"].Evidence[0].Detail, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(roles["Compatibility Patch"].Evidence);

            var query = new LocalKnowledgeQueryService(new Mo2SnapshotReader());
            query.Load(new Mo2SourceInput(
                "synthetic-instance",
                "default",
                root.FullName,
                profilePath.FullName,
                modsPath.FullName));
            Assert.Equal(
                new[] { "Shared Data", "Compatibility Patch", "Content Mod", "No Evidence" },
                query.GetModCandidates().Select(candidate => candidate.DirectoryName));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void LoadsSessionAndProjectsCandidateSummaries()
    {
        var query = CreateQuery();

        var session = query.Load(CreateSource(FixtureRoot));
        var alpha = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Alpha Mod");

        Assert.Equal("default", session.ProfileName);
        Assert.Equal(QueryProfileState.Listed, alpha.ProfileState);
        Assert.Equal(QueryEnabledState.Enabled, alpha.EnabledState);
        Assert.Equal(3, alpha.Priority);
        Assert.Equal("Alpha Display", alpha.DisplayName);
        Assert.Equal("1.2.3", alpha.Version);
        Assert.Equal("https://example.test/alpha", alpha.Website);
        Assert.Equal(QuerySourceReferenceKind.ModDirectory, alpha.Source.Kind);
        Assert.Equal(QuerySourceReferenceKind.ProfileFile, alpha.PriorityEvidence?.Source.Kind);

        var beta = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Beta Mod");
        Assert.Null(beta.Website);
    }

    [Fact]
    public void InfersBaseDataConfigFromMo2GamePath()
    {
        var query = CreateQuery();
        var source = CreateSource(FixtureRoot) with
        {
            GamePath = Path.Combine(FixtureRoot, "game")
        };

        query.Load(source);

        Assert.Equal(
            Path.Combine(FixtureRoot, "game", "Data", "Config"),
            query.GetInferredBaseDataConfigPath());
        Assert.Null(SevenDaysToDiePathInference.InferBaseDataConfigPath(null));
    }

    [Fact]
    public void FindsStrongUrlAndNormalizedNameMatchesWithoutUsingPageBody()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));

        var matches = query.FindLocalModMatches(CreatePage(
            "HTTP://EXAMPLE.TEST/alpha/?tab=files#description",
            "body mentions another mod",
            "  Ａｌｐｈａ　Ｍｏｄ  "));
        var alpha = Assert.Single(matches, match => match.ModKey == "Alpha Mod");

        Assert.Equal(LocalModMatchStrength.Strong, alpha.Strength);
        Assert.Equal(LocalModMatchKind.UrlAndName, alpha.MatchKind);
        Assert.True(alpha.AutoConfirmEligible);
        Assert.Contains("normalized host/path", alpha.Evidence);
        Assert.Contains("exactly matches", alpha.Evidence);
    }

    [Fact]
    public void ShowsPartialUnlistedAndUnresolvedMatchesWithoutAutoConfirmation()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));

        var partial = Assert.Single(query.FindLocalModMatches(CreatePage(
            "https://example.test/other",
            title: "Alpha")), match => match.ModKey == "Alpha Mod");
        Assert.Equal(LocalModMatchStrength.Partial, partial.Strength);
        Assert.False(partial.AutoConfirmEligible);

        var unlisted = Assert.Single(query.FindLocalModMatches(CreatePage(
            "https://example.test/unlisted",
            title: "Unlisted Display")), match => match.ModKey == "Unlisted Mod");
        Assert.Equal(QueryProfileState.Unlisted, unlisted.ProfileState);
        Assert.True(unlisted.AutoConfirmEligible);

        var disabledQuery = CreateQuery();
        disabledQuery.Load(CreateSource(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "7dtd-mo2-phase4")));
        var disabled = Assert.Single(disabledQuery.FindLocalModMatches(CreatePage(
            "https://example.test/disabled",
            title: "Disabled Mod")), match => match.ModKey == "Disabled Mod");
        Assert.Equal(QueryEnabledState.Disabled, disabled.EnabledState);
        Assert.True(disabled.AutoConfirmEligible);

        var unresolved = Assert.Single(query.FindLocalModMatches(CreatePage(
            "https://example.test/missing",
            title: "Missing Mod")), match => match.ModKey == "Missing Mod");
        Assert.Equal(QueryProfileState.Unresolved, unresolved.ProfileState);
        Assert.False(unresolved.AutoConfirmEligible);
    }

    [Fact]
    public void KeepsMultipleStrongMatchesAsCandidates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"modscope-match-{Guid.NewGuid():N}");
        CopyDirectory(FixtureRoot, root);
        try
        {
            File.WriteAllText(
                Path.Combine(root, "mods", "Beta Mod", "ModInfo.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><xml><Name value=\"Beta Mod\" /><Website value=\"https://example.test/alpha\" /></xml>");
            var query = CreateQuery();
            query.Load(CreateSource(root));

            var strongMatches = query.FindLocalModMatches(CreatePage(
                    "https://example.test/alpha",
                    title: "Unrelated page"))
                .Where(match => match.Strength == LocalModMatchStrength.Strong)
                .ToList();

            Assert.Equal(2, strongMatches.Count);
            Assert.All(strongMatches, match => Assert.True(match.AutoConfirmEligible));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
        Assert.Equal(3, installed.Priority);
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
    public void ProjectsPatchOperationsAndUnknownOperationDetailsInInspector()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));
        var alphaKey = query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Alpha Mod").ModKey;

        var inspector = query.GetInspector(alphaKey);
        var operationsFile = Assert.Single(
            inspector.XmlFiles,
            file => file.RelativePath == "Config/operations.xml");

        Assert.Equal(10, operationsFile.PatchOperations.Count);

        var set = Assert.Single(
            operationsFile.PatchOperations,
            operation => operation.RawOperationName == "set");
        Assert.Equal(QueryXmlPatchOperationKind.Set, set.NormalizedKind);
        Assert.Contains(set.TargetXmlCandidates, candidate => candidate.NormalizedValue == "items.xml");
        Assert.Contains(
            set.XPathCandidates,
            candidate => candidate.RawValue == "/items/item[@name='Alpha']/property[@name='Health']/@value");

        var unknown = Assert.Single(
            operationsFile.PatchOperations,
            operation => operation.RawOperationName == "mystery");
        Assert.Null(unknown.NormalizedKind);
        Assert.Contains(
            unknown.RawObservation.Attributes,
            attribute => attribute.Name == "custom" && attribute.Value == "preserve");
        Assert.Contains(
            unknown.Diagnostics,
            diagnostic => diagnostic.Code == "xml.patch.operation.unknown");
    }

    [Fact]
    public void FindsForwardAndReverseReferencesWithOwnerContext()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));

        var operationKey = "mods/Alpha Mod/Config/operations.xml#configs/set";
        var forward = query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.PatchOperation,
            operationKey.Replace('/', '\\'),
            KnowledgeQueryDirection.Forward));
        var targetReference = Assert.Single(
            forward,
            reference => reference.To.Kind == KnowledgeQueryNodeKind.TargetXml
                && reference.To.Value == "items.xml");

        Assert.Equal(KnowledgeReferenceRelation.Targets, targetReference.Relation);
        Assert.Equal(QueryEvidenceKind.Normalized, targetReference.Evidence.Kind);
        Assert.Equal("Alpha Mod", targetReference.OwnerMod?.DirectoryName);
        Assert.Equal("Alpha Display", targetReference.OwnerMod?.DisplayName);
        Assert.Equal("1.2.3", targetReference.OwnerMod?.Version);
        Assert.Equal(QueryProfileState.Listed, targetReference.OwnerMod?.ProfileState);
        Assert.Equal(QueryEnabledState.Enabled, targetReference.OwnerMod?.EnabledState);
        Assert.Equal(3, targetReference.OwnerMod?.Priority);
        Assert.Equal("set", targetReference.Operation?.RawOperationName);
        Assert.Equal(QueryXmlPatchOperationKind.Set, targetReference.Operation?.NormalizedKind);

        var reverse = query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.TargetXml,
            @"Config\items.xml",
            KnowledgeQueryDirection.Reverse));
        var reverseReference = Assert.Single(
            reverse,
            reference => reference.Operation?.RawOperationName == "set"
                && reference.OwnerMod?.ModKey == "Alpha Mod"
                && reference.Evidence.Kind == QueryEvidenceKind.Normalized);

        Assert.Equal(KnowledgeQueryNodeKind.TargetXml, reverseReference.From.Kind);
        Assert.Equal("items.xml", reverseReference.From.Value);
        Assert.Equal(KnowledgeQueryNodeKind.PatchOperation, reverseReference.To.Kind);
        Assert.Equal(KnowledgeReferenceRelation.Targets, reverseReference.Relation);
        Assert.Equal(QueryEvidenceKind.Normalized, reverseReference.Evidence.Kind);

        var inferredUnknown = Assert.Single(
            query.FindReferences(new KnowledgeReferenceQuery(
                KnowledgeQueryNodeKind.TargetXml,
                "operations.xml",
                KnowledgeQueryDirection.Reverse)),
            reference => reference.OwnerMod?.ModKey == "Alpha Mod"
                && reference.Operation?.RawOperationName == "mystery");
        Assert.Equal(QueryEvidenceKind.Inference, inferredUnknown.Evidence.Kind);
        Assert.Null(inferredUnknown.Operation?.NormalizedKind);
        Assert.Contains(
            inferredUnknown.Diagnostics,
            diagnostic => diagnostic.Code == "xml.patch.operation.unknown");
    }

    [Fact]
    public void SupportsAllKnowledgeNodeKindsWithNormalizedExactMatching()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));
        var operationKey = "mods/Alpha Mod/Config/operations.xml#configs/set";

        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Mod,
            "Alpha Mod",
            KnowledgeQueryDirection.Forward)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.File,
            @"mods\Alpha Mod\Config\operations.xml",
            KnowledgeQueryDirection.Forward)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.XmlFile,
            "mods/Alpha Mod/Config/operations.xml",
            KnowledgeQueryDirection.Forward)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.PatchOperation,
            operationKey,
            KnowledgeQueryDirection.Forward)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.TargetXml,
            "items.xml",
            KnowledgeQueryDirection.Reverse)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.XPath,
            "/items/item[@name='Alpha']/property[@name='Health']/@value",
            KnowledgeQueryDirection.Reverse)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Entity,
            "item",
            KnowledgeQueryDirection.Reverse)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Property,
            "Health",
            KnowledgeQueryDirection.Reverse)));
        Assert.NotEmpty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Attribute,
            "value",
            KnowledgeQueryDirection.Reverse)));

        Assert.Empty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.TargetXml,
            "ITEMS.XML",
            KnowledgeQueryDirection.Reverse)));
        Assert.Empty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.TargetXml,
            "items",
            KnowledgeQueryDirection.Reverse)));
    }

    [Fact]
    public void AppliesReferenceQueryLimitsAndRequiresALoadedSnapshot()
    {
        var query = CreateQuery();
        query.Load(CreateSource(FixtureRoot));

        var all = query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Entity,
            "item",
            KnowledgeQueryDirection.Reverse));
        var limited = query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Entity,
            "item",
            KnowledgeQueryDirection.Reverse,
            1));

        Assert.NotEmpty(all);
        Assert.Single(limited);
        Assert.Equal(all[0].From, limited[0].From);
        Assert.Equal(all[0].To, limited[0].To);
        Assert.Equal(all[0].Relation, limited[0].Relation);
        Assert.Empty(query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Entity,
            "item",
            KnowledgeQueryDirection.Reverse,
            0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => query.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.Entity,
            "item",
            KnowledgeQueryDirection.Reverse,
            -1)));

        var unloaded = LocalKnowledgeQueryService.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => unloaded.FindReferences(new KnowledgeReferenceQuery(
            KnowledgeQueryNodeKind.TargetXml,
            "items.xml",
            KnowledgeQueryDirection.Reverse)));
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
            var loadProgress = new RecordingProgress();
            query.Load(new Mo2SourceInput(
                "synthetic-instance",
                "Default",
                root.FullName,
                defaultProfile,
                modsPath.FullName),
                progress: loadProgress);

            Assert.Contains(
                loadProgress.Values,
                progress => progress.Phase == "scanning-mod-folders");

            Assert.Equal(
                new[] { "Default", "Alternate" },
                query.GetProfiles().Select(profile => profile.ProfileName));

            var switchProgress = new RecordingProgress();
            var switched = query.SwitchProfile("Alternate", progress: switchProgress);

            Assert.Equal("Alternate", switched.ProfileName);
            Assert.Contains(
                switchProgress.Values,
                progress => progress.Phase == "projecting-profile");
            Assert.Contains(query.GetModCandidates(), candidate => candidate.DirectoryName == "Beta Mod");
            Assert.Equal("Alternate", query.GetProfiles().First().ProfileName);
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
    public void WarmsBackgroundProfileWithoutReplacingActiveSnapshot()
    {
        var root = Directory.CreateTempSubdirectory("modscope-warm-profile-");
        try
        {
            var profilesPath = Directory.CreateDirectory(Path.Combine(root.FullName, "profiles"));
            var defaultProfile = CreateProfile(profilesPath.FullName, "Default", "+Alpha Mod");
            CreateProfile(profilesPath.FullName, "Alternate", "+Beta Mod");
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

            query.WarmProfile("Alternate");

            Assert.Equal(QueryProfileState.Listed, query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Alpha Mod").ProfileState);
            Assert.Equal(QueryProfileState.Unlisted, query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Beta Mod").ProfileState);

            var switched = query.SwitchProfile("Alternate");

            Assert.Equal("Alternate", switched.ProfileName);
            Assert.Equal(QueryProfileState.Listed, query.GetModCandidates().Single(candidate => candidate.DirectoryName == "Beta Mod").ProfileState);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ListsAndSwitchesProfilesFromExternalProfilesPath()
    {
        var root = Directory.CreateTempSubdirectory("modscope-external-profiles-");
        try
        {
            var externalRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "external-data"));
            var profilesPath = Directory.CreateDirectory(Path.Combine(externalRoot.FullName, "profiles"));
            var defaultProfile = CreateProfile(profilesPath.FullName, "Default", "+Alpha Mod");
            CreateProfile(profilesPath.FullName, "Alternate", "+Beta Mod");
            var modsPath = Directory.CreateDirectory(Path.Combine(externalRoot.FullName, "mods"));
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
                modsPath.FullName)
            {
                ProfilesPath = profilesPath.FullName
            });

            Assert.Equal(
                new[] { "Default", "Alternate" },
                query.GetProfiles().Select(profile => profile.ProfileName));

            var switched = query.SwitchProfile("Alternate");

            Assert.Equal("Alternate", switched.ProfileName);
            Assert.Contains(query.GetModCandidates(), candidate => candidate.DirectoryName == "Beta Mod");
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void LoadsAndSwitchesProfilesFromDiscoveredExternalProfilesPath()
    {
        var root = Directory.CreateTempSubdirectory("modscope-discovered-external-profiles-");
        try
        {
            var externalRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "external-data"));
            var profilesPath = Directory.CreateDirectory(Path.Combine(externalRoot.FullName, "profiles"));
            var defaultProfile = CreateProfile(profilesPath.FullName, "Default", "+Alpha Mod");
            CreateProfile(profilesPath.FullName, "Alternate", "+Beta Mod");
            var modsPath = Directory.CreateDirectory(Path.Combine(externalRoot.FullName, "mods"));
            var alphaModPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Alpha Mod"));
            var betaModPath = Directory.CreateDirectory(Path.Combine(modsPath.FullName, "Beta Mod"));
            File.WriteAllText(
                Path.Combine(alphaModPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Alpha Mod\" /></xml>");
            File.WriteAllText(
                Path.Combine(betaModPath.FullName, "ModInfo.xml"),
                "<xml><Name value=\"Beta Mod\" /></xml>");

            var source = new Mo2SourceDefinition(
                "synthetic-instance",
                "Default",
                root.FullName,
                defaultProfile,
                modsPath.FullName)
            {
                ProfilesPath = profilesPath.FullName
            };
            var candidate = new Mo2SourceCandidate(
                "external-profile-candidate",
                "7 Days to Die",
                source,
                Mo2SourceCandidateReadiness.Ready,
                Array.Empty<Mo2SourceDiscoveryEvidence>(),
                Array.Empty<Diagnostic>());
            var query = new LocalKnowledgeQueryService(
                new Mo2SnapshotReader(),
                new FakeSourceDiscovery(candidate),
                new FakePreferenceStore());

            query.DiscoverSources(new[] { root.FullName });
            query.LoadSourceCandidate("external-profile-candidate");

            Assert.Equal(
                new[] { "Default", "Alternate" },
                query.GetProfiles().Select(profile => profile.ProfileName));

            var switched = query.SwitchProfile("Alternate");

            Assert.Equal("Alternate", switched.ProfileName);
            Assert.Contains(query.GetModCandidates(), candidate => candidate.DirectoryName == "Beta Mod");
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

    [Fact]
    public void ProjectsSourceDiscoveryDiagnosticsWithInstanceFileReferences()
    {
        var source = new Mo2SourceDefinition(
            "synthetic-instance",
            "Default",
            Path.GetFullPath("C:/synthetic-mo2"),
            Path.GetFullPath("C:/synthetic-mo2/profiles/Default"),
            Path.GetFullPath("C:/synthetic-mo2/mods"));
        var candidate = new Mo2SourceCandidate(
            "candidate-1",
            "7 Days to Die",
            source,
            Mo2SourceCandidateReadiness.Invalid,
            new[]
            {
                new Mo2SourceDiscoveryEvidence(
                    Mo2SourceDiscoveryEvidenceKind.NativePicker,
                    EvidenceKind.Source)
            },
            new[]
            {
                new Diagnostic(
                    "mo2.ini.malformed",
                    DiagnosticSeverity.Error,
                    "Malformed INI.",
                    new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini", 2))
            });
        var query = new LocalKnowledgeQueryService(
            new Mo2SnapshotReader(),
            new FakeSourceDiscovery(candidate),
            new FakePreferenceStore());

        var discovery = query.DiscoverSources();
        var diagnostic = Assert.Single(Assert.Single(discovery.Candidates).Diagnostics);

        Assert.Equal(QuerySourceReferenceKind.InstanceFile, diagnostic.Source?.Kind);
        Assert.Equal("ModOrganizer.ini", diagnostic.Source?.RelativePath);
        Assert.Equal(2, diagnostic.Source?.LineNumber);
    }

    private static ILocalKnowledgeQuery CreateQuery()
    {
        return LocalKnowledgeQueryService.CreateDefault();
    }

    private static PageObservation CreatePage(
        string url,
        string? content = null,
        string title = "Synthetic page")
    {
        return new PageObservation(
            new Uri(url),
            title,
            content,
            DateTimeOffset.UtcNow,
            "test",
            PageExtractionStatus.Succeeded,
            Array.Empty<DiagnosticReadModel>());
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
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

    private sealed class RecordingProgress : IProgress<LocalKnowledgeProgress>
    {
        public List<LocalKnowledgeProgress> Values { get; } = new();

        public void Report(LocalKnowledgeProgress value)
        {
            Values.Add(value);
        }
    }

    private sealed class FakeSourceDiscovery : IMo2SourceDiscovery
    {
        private readonly IReadOnlyList<Mo2SourceCandidate> _candidates;

        public FakeSourceDiscovery(params Mo2SourceCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public IReadOnlyList<Mo2SourceCandidate> Discover(
            Mo2SourceDiscoveryRequest request,
            CancellationToken cancellationToken = default) => _candidates;
    }

    private sealed class FakePreferenceStore : IMo2SourcePreferenceStore
    {
        public Mo2SourcePreference? Read() => null;

        public void Write(Mo2SourcePreference preference)
        {
        }
    }
}
