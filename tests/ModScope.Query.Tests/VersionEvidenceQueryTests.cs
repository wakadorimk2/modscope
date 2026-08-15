using System.Text.Json;
using ModScope.LocalKnowledge;
using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class VersionEvidenceQueryTests
{
    [Fact]
    public void ProjectsPackageRelationToCandidateAndInspectorWithoutLocalPathsOrRawContent()
    {
        using var fixture = Fixture.Create();
        var query = new LocalKnowledgeQueryService(new Mo2SnapshotReader());

        var session = query.Load(
            new Mo2SourceInput(
                "synthetic-instance",
                "default",
                fixture.Root,
                fixture.ProfilePath,
                fixture.ModsPath)
            {
                VersionEvidenceManifestPath = fixture.ManifestPath
            });

        var candidate = Assert.Single(query.GetModCandidates());
        Assert.NotNull(candidate.PackageRelation);
        var relation = candidate.PackageRelation!;
        Assert.Equal("synthetic-package", relation.PackageDirectoryName);
        Assert.Equal(1, relation.ModletCount);
        Assert.False(relation.SharedAcrossModlets);
        Assert.Equal(QueryEnabledState.Enabled, candidate.EnabledState);
        Assert.Equal(QueryIdentityResolutionState.Exact, relation.IdentityState);
        Assert.Equal(QueryVersionComparisonStatus.Equal, relation.Comparison.Status);
        Assert.NotEmpty(relation.Comparison.Reason);
        Assert.Equal("mods/synthetic-package", relation.PackageSource.RelativePath);
        Assert.Single(relation.SourceArtifacts);
        Assert.Equal(3, relation.VersionObservations.Count);
        Assert.Equal("release-evidence.json", session.VersionEvidenceManifest!.DisplayName);

        var inspector = query.GetInspector(candidate.ModKey)!;
        Assert.NotNull(inspector);
        Assert.NotNull(inspector.PackageRelation);
        Assert.Equal(QueryVersionComparisonStatus.Equal, inspector.PackageRelation!.Comparison.Status);

        var serialized = JsonSerializer.Serialize(new { candidate, inspector, session });
        Assert.DoesNotContain(fixture.Root, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[General]", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-local-content", serialized, StringComparison.Ordinal);
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root)
        {
            Root = root;
            ProfilePath = Path.Combine(root, "profile");
            ModsPath = Path.Combine(root, "mods");
            ManifestPath = Path.Combine(root, "release-evidence.json");
        }

        public string Root { get; }

        public string ProfilePath { get; }

        public string ModsPath { get; }

        public string ManifestPath { get; }

        public static Fixture Create()
        {
            var fixture = new Fixture(Directory.CreateTempSubdirectory("modscope-query-evidence-").FullName);
            var packagePath = Path.Combine(fixture.ModsPath, "synthetic-package");
            var modletPath = Path.Combine(packagePath, "synthetic-modlet");
            Directory.CreateDirectory(fixture.ProfilePath);
            Directory.CreateDirectory(modletPath);
            File.WriteAllText(
                Path.Combine(fixture.ProfilePath, "modlist.txt"),
                "# synthetic profile\n+synthetic-package\n");
            File.WriteAllText(
                Path.Combine(packagePath, "meta.ini"),
                "[General]\nmodid=123\nfileid=456\nversion=1.2.3\nsecret=secret-local-content\nmalformed-secret-local-content\n");
            File.WriteAllText(
                Path.Combine(modletPath, "ModInfo.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><xml><Name value=\"Synthetic\"/><Version value=\"v1.02.003\"/></xml>");
            File.WriteAllText(
                fixture.ManifestPath,
                """
                {
                  "schemaVersion": 1,
                  "observedAtUtc": "2026-08-15T00:00:00Z",
                  "artifacts": [
                    {
                      "artifactId": "artifact-1",
                      "kind": "nexus-file",
                      "name": "Synthetic",
                      "modId": "123",
                      "fileId": "456",
                      "sourceUrl": "https://example.test/file/456"
                    }
                  ],
                  "packageBindings": [
                    { "packageDirectory": "mods/synthetic-package", "artifactIds": ["artifact-1"] }
                  ],
                  "versionObservations": [
                    { "artifactId": "artifact-1", "rawValue": "1.2.3" }
                  ]
                }
                """);
            return fixture;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
