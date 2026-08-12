using System.Text;
using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class Mo2SourceDiscoveryTests
{
    [Fact]
    public void DiscoversPortableRootFromRunningMo2AndKeepsSourceEvidence()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-running-");
        try
        {
            var instance = CreateInstance(container.FullName, "Portable");
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(
                localAppDataPath: null,
                runningExecutablePaths: new[] { Path.Combine(instance.RootPath, "ModOrganizer.exe") },
                lastUsedInstanceName: null));

            var candidate = Assert.Single(discovery.Discover(new Mo2SourceDiscoveryRequest(null, Array.Empty<string>())));

            Assert.Equal(Mo2SourceCandidateReadiness.Ready, candidate.Readiness);
            Assert.Equal(instance.RootPath, candidate.Source.InstanceRootPath);
            Assert.Equal(Path.Combine(instance.ProfilesPath, "Default"), candidate.Source.ProfilePath);
            Assert.Equal(Mo2SourceDiscoveryEvidenceKind.RunningProcess, Assert.Single(candidate.Evidence).Kind);
            Assert.Equal(EvidenceKind.Source, candidate.Evidence[0].EvidenceKind);
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void DiscoversGlobalInstanceAndSelectsDefaultWhenSelectedProfileIsMissing()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-global-");
        try
        {
            var globalRoot = Directory.CreateDirectory(Path.Combine(container.FullName, "ModOrganizer"));
            var instance = CreateInstance(
                globalRoot.FullName,
                "Global",
                selectedProfile: "Missing",
                createAlternateProfile: true);
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(
                container.FullName,
                Array.Empty<string>(),
                lastUsedInstanceName: null));

            var candidate = Assert.Single(discovery.Discover(new Mo2SourceDiscoveryRequest(null, Array.Empty<string>())));

            Assert.Equal(Mo2SourceCandidateReadiness.Ready, candidate.Readiness);
            Assert.Equal("Default", candidate.Source.ProfileName);
            Assert.Contains(candidate.Evidence, evidence =>
                evidence.Kind == Mo2SourceDiscoveryEvidenceKind.GlobalInstance
                && evidence.EvidenceKind == EvidenceKind.Inference);
            Assert.Contains(candidate.Diagnostics, diagnostic => diagnostic.Code == "mo2.profile.selected_missing");
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void ReturnsAllProfilesWhenSelectionIsAmbiguous()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-profiles-");
        try
        {
            var instance = CreateInstance(
                container.FullName,
                "Portable",
                selectedProfile: "Missing",
                createDefaultProfile: false,
                createAlternateProfile: true,
                createSecondProfile: true);
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(
                null,
                Array.Empty<string>(),
                null));

            var candidates = discovery.Discover(new Mo2SourceDiscoveryRequest(
                null,
                new[] { instance.RootPath }));

            Assert.Equal(new[] { "Alternate", "Second" }, candidates.Select(candidate => candidate.Source.ProfileName));
            Assert.All(candidates, candidate =>
            {
                Assert.Equal(Mo2SourceCandidateReadiness.Ready, candidate.Readiness);
                Assert.Contains(candidate.Diagnostics, diagnostic => diagnostic.Code == "mo2.profile.selection_required");
            });
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void ResolvesLastUsedGlobalInstanceFromInstanceInformation()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-last-used-");
        try
        {
            var instance = CreateInstance(container.FullName, "LastUsed");
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(
                Path.Combine(container.FullName, "empty-appdata"),
                Array.Empty<string>(),
                instance.RootPath));

            var candidate = Assert.Single(discovery.Discover(new Mo2SourceDiscoveryRequest(
                null,
                Array.Empty<string>())));

            Assert.Equal(instance.RootPath, candidate.Source.InstanceRootPath);
            Assert.Contains(candidate.Evidence, evidence =>
                evidence.Kind == Mo2SourceDiscoveryEvidenceKind.GlobalInstance
                && evidence.EvidenceKind == EvidenceKind.Inference);
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void ExpandsBaseDirectoryAndAcceptsExternalReadOnlyPaths()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-external-");
        try
        {
            var instance = CreateInstance(
                container.FullName,
                "Portable",
                settings: new Dictionary<string, string>
                {
                    ["base_directory"] = Path.Combine(container.FullName, "external-data"),
                    ["mod_directory"] = "%BASE_DIR%\\mods",
                    ["profiles_directory"] = "%BASE_DIR%\\profiles"
                });
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(null, Array.Empty<string>(), null));

            var candidate = Assert.Single(discovery.Discover(new Mo2SourceDiscoveryRequest(
                null,
                new[] { instance.RootPath })));

            Assert.Equal(Mo2SourceCandidateReadiness.Ready, candidate.Readiness);
            Assert.Equal(Path.Combine(container.FullName, "external-data", "mods"), candidate.Source.ModsPath);
            Assert.Equal(Path.Combine(container.FullName, "external-data", "profiles", "Default"), candidate.Source.ProfilePath);
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void RejectsMalformedIniAndUnsupportedGamesWithoutReadyCandidates()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-invalid-");
        try
        {
            var malformed = CreateInstance(container.FullName, "Malformed");
            File.AppendAllText(Path.Combine(malformed.RootPath, "ModOrganizer.ini"), "not-an-entry\n");
            var unsupported = CreateInstance(container.FullName, "Unsupported", gameName: "Skyrim Special Edition");
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(
                null,
                Array.Empty<string>(),
                null));

            var candidates = discovery.Discover(new Mo2SourceDiscoveryRequest(
                null,
                new[] { malformed.RootPath, unsupported.RootPath }));

            var malformedCandidate = candidates.Single(candidate => candidate.Source.InstanceName == "Malformed");
            var unsupportedCandidate = candidates.Single(candidate => candidate.Source.InstanceName == "Unsupported");
            Assert.Equal(Mo2SourceCandidateReadiness.Invalid, malformedCandidate.Readiness);
            Assert.Contains(malformedCandidate.Diagnostics, diagnostic => diagnostic.Code == "mo2.ini.malformed");
            Assert.Equal(Mo2SourceCandidateReadiness.UnsupportedGame, unsupportedCandidate.Readiness);
            Assert.Contains(unsupportedCandidate.Diagnostics, diagnostic => diagnostic.Code == "mo2.game.unsupported");
            Assert.NotEqual(Mo2SourceCandidateReadiness.Ready, malformedCandidate.Readiness);
            Assert.NotEqual(Mo2SourceCandidateReadiness.Ready, unsupportedCandidate.Readiness);
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void MergesEvidenceAndIgnoresStaleRememberedRootDeterministically()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-determinism-");
        try
        {
            var instance = CreateInstance(container.FullName, "Portable");
            var request = new Mo2SourceDiscoveryRequest(
                new Mo2SourcePreference(instance.RootPath, "Default"),
                new[] { instance.RootPath });
            var environment = new FakeEnvironment(
                null,
                new[] { Path.Combine(instance.RootPath, "ModOrganizer.exe") },
                null);
            var discovery = new Mo2SourceDiscovery(environment);

            var first = Assert.Single(discovery.Discover(request));
            var second = Assert.Single(discovery.Discover(request));

            Assert.Equal(first.CandidateId, second.CandidateId);
            Assert.Equal(
                new[]
                {
                    Mo2SourceDiscoveryEvidenceKind.RunningProcess,
                    Mo2SourceDiscoveryEvidenceKind.Remembered,
                    Mo2SourceDiscoveryEvidenceKind.NativePicker
                },
                first.Evidence.Select(evidence => evidence.Kind));

            var staleDiscovery = new Mo2SourceDiscovery(new FakeEnvironment(null, Array.Empty<string>(), null));
            var stale = staleDiscovery.Discover(new Mo2SourceDiscoveryRequest(
                new Mo2SourcePreference(Path.Combine(container.FullName, "gone"), "Default"),
                Array.Empty<string>()));
            Assert.Empty(stale);
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void UsesRememberedProfileWhenTheInstanceHasMultipleProfiles()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-remembered-profile-");
        try
        {
            var instance = CreateInstance(
                container.FullName,
                "Portable",
                selectedProfile: "Alternate",
                createAlternateProfile: true);
            var discovery = new Mo2SourceDiscovery(new FakeEnvironment(null, Array.Empty<string>(), null));

            var candidate = Assert.Single(discovery.Discover(new Mo2SourceDiscoveryRequest(
                new Mo2SourcePreference(instance.RootPath, "Default"),
                Array.Empty<string>())));

            Assert.Equal("Default", candidate.Source.ProfileName);
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void DiscoveredAndExplicitSourcesProduceTheSameSnapshotAndIndexInputs()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-snapshot-");
        try
        {
            var instance = CreateInstance(container.FullName, "Portable");
            var modPath = Directory.CreateDirectory(Path.Combine(instance.ModsPath, "Alpha"));
            File.WriteAllText(Path.Combine(modPath.FullName, "ModInfo.xml"), "<xml><Name value=\"Alpha\" /></xml>");
            File.WriteAllText(Path.Combine(instance.ProfilesPath, "Default", "modlist.txt"), "+Alpha\n");

            var candidate = Assert.Single(new Mo2SourceDiscovery(
                new FakeEnvironment(null, Array.Empty<string>(), null)).Discover(new Mo2SourceDiscoveryRequest(
                    null,
                    new[] { instance.RootPath })));
            var explicitSource = new Mo2SourceDefinition(
                "Portable",
                "Default",
                instance.RootPath,
                Path.Combine(instance.ProfilesPath, "Default"),
                instance.ModsPath);
            var reader = new Mo2SnapshotReader();

            var discoveredSnapshot = reader.Read(candidate.Source);
            var explicitSnapshot = reader.Read(explicitSource);

            Assert.Equal(explicitSnapshot.SnapshotId, discoveredSnapshot.SnapshotId);
            Assert.Equal(
                explicitSnapshot.InputManifest.ProfileModListSha256,
                discoveredSnapshot.InputManifest.ProfileModListSha256);
            Assert.Equal(
                explicitSnapshot.InputManifest.ParserVersion,
                discoveredSnapshot.InputManifest.ParserVersion);
            Assert.Equal(
                explicitSnapshot.InputManifest.SchemaVersion,
                discoveredSnapshot.InputManifest.SchemaVersion);
            Assert.Equal(
                ProjectManifest(explicitSnapshot.InputManifest),
                ProjectManifest(discoveredSnapshot.InputManifest));
            Assert.Equal(
                ProjectMods(explicitSnapshot),
                ProjectMods(discoveredSnapshot));
            Assert.Equal(
                ProjectReferences(explicitSnapshot.Index),
                ProjectReferences(discoveredSnapshot.Index));
        }
        finally
        {
            container.Delete(true);
        }
    }

    [Fact]
    public void StoresAndReadsLastSuccessfulPreference()
    {
        var container = Directory.CreateTempSubdirectory("modscope-discovery-preference-");
        try
        {
            var path = Path.Combine(container.FullName, "mo2-source.json");
            var store = new JsonMo2SourcePreferenceStore(path);
            var preference = new Mo2SourcePreference(Path.Combine(container.FullName, "MO2"), "Default");

            store.Write(preference);

            Assert.Equal(preference, store.Read());
        }
        finally
        {
            container.Delete(true);
        }
    }

    private static InstanceLayout CreateInstance(
        string parentPath,
        string instanceName,
        string gameName = "7 Days to Die",
        string selectedProfile = "Default",
        bool createDefaultProfile = true,
        bool createAlternateProfile = false,
        bool createSecondProfile = false,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        var root = Directory.CreateDirectory(Path.Combine(parentPath, instanceName));
        var baseDirectory = settings is not null && settings.TryGetValue("base_directory", out var configuredBase)
            ? ResolveTestPath(configuredBase, root.FullName, root.FullName)
            : root.FullName;
        var modsPath = Directory.CreateDirectory(settings is not null && settings.TryGetValue("mod_directory", out var configuredMods)
            ? ResolveTestPath(configuredMods, root.FullName, baseDirectory)
            : Path.Combine(root.FullName, "mods"));
        var profilesPath = Directory.CreateDirectory(settings is not null && settings.TryGetValue("profiles_directory", out var configuredProfiles)
            ? ResolveTestPath(configuredProfiles, root.FullName, baseDirectory)
            : Path.Combine(root.FullName, "profiles"));

        if (createDefaultProfile)
        {
            CreateProfile(profilesPath.FullName, "Default");
        }

        if (createAlternateProfile)
        {
            CreateProfile(profilesPath.FullName, "Alternate");
        }

        if (createSecondProfile)
        {
            CreateProfile(profilesPath.FullName, "Second");
        }

        var ini = new StringBuilder()
            .AppendLine("[General]")
            .AppendLine($"gameName={gameName}")
            .AppendLine($"selected_profile=@ByteArray({selectedProfile})")
            .AppendLine()
            .AppendLine("[Settings]");
        if (settings is not null)
        {
            foreach (var setting in settings)
            {
                ini.AppendLine($"{setting.Key}={setting.Value}");
            }
        }

        File.WriteAllText(Path.Combine(root.FullName, "ModOrganizer.ini"), ini.ToString());
        return new InstanceLayout(root.FullName, modsPath.FullName, profilesPath.FullName);
    }

    private static void CreateProfile(string profilesPath, string profileName)
    {
        var profilePath = Directory.CreateDirectory(Path.Combine(profilesPath, profileName));
        File.WriteAllText(Path.Combine(profilePath.FullName, "modlist.txt"), string.Empty);
    }

    private static string ResolveTestPath(string value, string rootPath, string baseDirectory)
    {
        var expanded = value.Replace("%BASE_DIR%", baseDirectory, StringComparison.OrdinalIgnoreCase);
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(rootPath, expanded));
    }

    private static IReadOnlyList<string> ProjectMods(LocalModSnapshot snapshot) =>
        snapshot.Mods
            .Select(mod => string.Join(
                "|",
                mod.ModKey,
                mod.DirectoryName,
                mod.ProfileState,
                mod.EnabledState,
                mod.Priority?.ToString() ?? "",
                mod.ModInfo?.Name ?? ""))
            .ToList();

    private static IReadOnlyList<string> ProjectManifest(InputManifest manifest) =>
        manifest.Files
            .Select(file => string.Join("|", file.RelativePath, file.Size, file.Sha256))
            .ToList();

    private static IReadOnlyList<string> ProjectReferences(LocalKnowledgeIndex index) =>
        index.ForwardReferences
            .Select(reference => string.Join(
                "|",
                reference.From.Kind,
                reference.From.Value,
                reference.To.Kind,
                reference.To.Value,
                reference.Relation,
                reference.Evidence.Kind,
                reference.Evidence.Source.Kind,
                reference.Evidence.Source.RelativePath))
            .Concat(index.ReverseReferences.Select(reference => string.Join(
                "|",
                reference.From.Kind,
                reference.From.Value,
                reference.To.Kind,
                reference.To.Value,
                reference.Relation,
                reference.Evidence.Kind,
                reference.Evidence.Source.Kind,
                reference.Evidence.Source.RelativePath)))
            .ToList();

    private sealed record InstanceLayout(string RootPath, string ModsPath, string ProfilesPath);

    private sealed class FakeEnvironment : IMo2DiscoveryEnvironment
    {
        public FakeEnvironment(
            string? localAppDataPath,
            IReadOnlyList<string> runningExecutablePaths,
            string? lastUsedInstanceName)
        {
            LocalAppDataPath = localAppDataPath;
            RunningExecutablePaths = runningExecutablePaths;
            LastUsedInstanceName = lastUsedInstanceName;
        }

        public string? LocalAppDataPath { get; }

        public IReadOnlyList<string> RunningExecutablePaths { get; }

        public string? LastUsedInstanceName { get; }

        public IReadOnlyList<string> GetRunningModOrganizerExecutablePaths() => RunningExecutablePaths;

        public string? GetLastUsedInstanceName() => LastUsedInstanceName;
    }
}
