using ModScope.Deployment;
using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.Deployment.Tests;

public sealed class ModDeploymentServiceTests
{
    [Fact]
    public void ApplyUsesTemporarySourceAndCreatesManagedJunction()
    {
        using var workspace = TestWorkspace.Create();
        var junctions = new FakeJunctionOperator();
        var service = workspace.CreateService(junctions);
        var draft = new DeploymentDraft(
            "default",
            new[] { new DeploymentEntryDraft("Alpha Mod", true, 0) });

        var plan = service.Preview(workspace.Source, draft);

        Assert.True(plan.CanApply, string.Join("\n", plan.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Single(plan.JunctionChanges, change => change.Action == "create");

        var result = service.Apply(plan);

        Assert.Equal(DeploymentResultStatus.Applied, result.Status);
        Assert.Equal("+Alpha Mod\n", File.ReadAllText(workspace.ModlistPath));
        Assert.True(junctions.Inspect(workspace.GameModsPath + "\\Alpha Mod").Exists);
        Assert.Single(Directory.GetFiles(workspace.ProfilePath, "modlist.txt.bak-*"));
    }

    [Fact]
    public void ApplyRejectsStaleModlistWithoutWritingJunction()
    {
        using var workspace = TestWorkspace.Create();
        var junctions = new FakeJunctionOperator();
        var service = workspace.CreateService(junctions);
        var draft = new DeploymentDraft(
            "default",
            new[] { new DeploymentEntryDraft("Alpha Mod", false, 0) });

        var plan = service.Preview(workspace.Source, draft);
        File.WriteAllText(workspace.ModlistPath, "-Alpha Mod\n");

        var result = service.Apply(plan);

        Assert.Equal(DeploymentResultStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "deployment.plan.stale");
        Assert.False(junctions.Inspect(workspace.GameModsPath + "\\Alpha Mod").Exists);
    }

    [Fact]
    public void PreviewBlocksRealFolderCollision()
    {
        using var workspace = TestWorkspace.Create();
        Directory.CreateDirectory(Path.Combine(workspace.GameModsPath, "Alpha Mod"));
        var service = workspace.CreateService(new FakeJunctionOperator());

        var plan = service.Preview(
            workspace.Source,
            new DeploymentDraft("default", new[] { new DeploymentEntryDraft("Alpha Mod", true, 0) }));

        Assert.False(plan.CanApply);
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == "deployment.junction.collision");
    }

    private sealed class TestWorkspace : IDisposable
    {
        private readonly string _root;

        private TestWorkspace(string root, Mo2SourceDefinition source)
        {
            _root = root;
            Source = source;
            ModlistPath = Path.Combine(source.ProfilePath, "modlist.txt");
            GameModsPath = Path.Combine(source.GamePath!, "Mods");
        }

        public Mo2SourceDefinition Source { get; }

        public string ModlistPath { get; }

        public string GameModsPath { get; }

        public string ProfilePath => Source.ProfilePath;

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "modscope-deployment-tests-" + Guid.NewGuid().ToString("N"));
            var profilePath = Path.Combine(root, "profile");
            var modsPath = Path.Combine(root, "mods");
            var gamePath = Path.Combine(root, "game");
            var alphaPath = Path.Combine(modsPath, "Alpha Mod");
            Directory.CreateDirectory(profilePath);
            Directory.CreateDirectory(alphaPath);
            Directory.CreateDirectory(gamePath);
            File.WriteAllText(Path.Combine(profilePath, "modlist.txt"), "+Alpha Mod\n");
            File.WriteAllText(Path.Combine(alphaPath, "ModInfo.xml"), "<xml><Name value=\"Alpha Mod\" /></xml>");
            File.WriteAllText(Path.Combine(gamePath, "7DaysToDie.exe"), "fixture");

            var source = new Mo2SourceDefinition(
                "fixture-instance",
                "default",
                root,
                profilePath,
                modsPath)
            {
                GamePath = gamePath
            };
            return new TestWorkspace(root, source);
        }

        public ModDeploymentService CreateService(FakeJunctionOperator junctions)
        {
            var statePath = Path.Combine(_root, "deployment-state.json");
            return new ModDeploymentService(
                junctionOperator: junctions,
                processGate: new EmptyProcessGate(),
                stateStore: new FileDeploymentStateStore(statePath));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class EmptyProcessGate : IProcessGate
    {
        public IReadOnlyList<string> GetBlockingProcesses() => Array.Empty<string>();
    }

    private sealed class FakeJunctionOperator : IJunctionOperator
    {
        private readonly Dictionary<string, string> _targets = new(StringComparer.OrdinalIgnoreCase);

        public JunctionInspection Inspect(string path)
        {
            var normalizedPath = Path.GetFullPath(path);
            return _targets.TryGetValue(normalizedPath, out var target)
                ? new JunctionInspection(true, true, true, target)
                : new JunctionInspection(Directory.Exists(normalizedPath), Directory.Exists(normalizedPath), false, null);
        }

        public void Create(string linkPath, string targetPath)
        {
            _targets[Path.GetFullPath(linkPath)] = Path.GetFullPath(targetPath);
        }

        public void Remove(string linkPath)
        {
            _targets.Remove(Path.GetFullPath(linkPath));
        }
    }
}
