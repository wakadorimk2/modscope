using ModScope.LocalKnowledge;
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
        Assert.Equal(0, targetReference.OwnerMod?.Priority);
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
                new[] { "Alternate", "Default" },
                query.GetProfiles().Select(profile => profile.ProfileName));

            var switchProgress = new RecordingProgress();
            var switched = query.SwitchProfile("Alternate", progress: switchProgress);

            Assert.Equal("Alternate", switched.ProfileName);
            Assert.Contains(
                switchProgress.Values,
                progress => progress.Phase == "projecting-profile");
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
                new[] { "Alternate", "Default" },
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
                new[] { "Alternate", "Default" },
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
