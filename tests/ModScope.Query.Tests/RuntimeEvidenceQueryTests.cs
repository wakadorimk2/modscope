using System.Text.Json;
using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class RuntimeEvidenceQueryTests
{
    [Fact]
    public void ProjectsRuntimeEvidenceAndFiltersComparisonResults()
    {
        var root = FixtureRoot;
        var query = LocalKnowledgeQueryService.CreateDefault();
        var session = query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        var input = CreateInput(session.SnapshotId);

        var result = query.CompareRuntimeEvidence(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
            input,
            new RuntimeEvidenceComparisonQuery(
                TargetXml: "Config/items.xml",
                XPath: "/items/item[@name='A']/@value",
                Status: QueryRuntimeEvidenceComparisonStatus.Match,
                Limit: 1));

        var item = Assert.Single(result.Items);
        Assert.Equal(QueryRuntimeEvidenceComparisonStatus.Match, item.Status);
        Assert.Equal(QuerySemanticConflictAssessment.Conflict, item.StaticAssessment);
        Assert.Equal(QuerySemanticConflictAssessment.Conflict, item.RuntimeAssessment);
        Assert.Equal("synthetic-runtime", result.RuntimeEvidence.ToolName);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "runtime.document.warning");
        Assert.Contains(item.Diagnostics, diagnostic => diagnostic.Code == "runtime.observation.warning");
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(Path.GetFullPath(root), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-match", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime/runtime.log", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesExplicitSnapshotAndSupportsEmptyAndUnloadedQueries()
    {
        var root = FixtureRoot;
        var query = LocalKnowledgeQueryService.CreateDefault();
        var session = query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        var baseData = new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config"));

        var empty = query.CompareRuntimeEvidence(
            baseData,
            CreateInput(session.SnapshotId),
            new RuntimeEvidenceComparisonQuery(Limit: 0));
        Assert.Empty(empty.Items);
        Assert.Single(empty.RuntimeEvidence.Observations);

        Assert.Throws<ArgumentException>(() => query.CompareRuntimeEvidence(
            baseData,
            CreateInput("different-snapshot")));

        var unloaded = LocalKnowledgeQueryService.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => unloaded.CompareRuntimeEvidence(
            baseData,
            CreateInput(session.SnapshotId)));
    }

    [Fact]
    public void ComparesRuntimeOcdLogsWithCategoryFilterAndSafeProjection()
    {
        var root = FixtureRoot;
        var query = LocalKnowledgeQueryService.CreateDefault();
        var session = query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        var logsRoot = Directory.CreateTempSubdirectory("modscope-runtime-ocd-query-");

        try
        {
            var categoryDirectory = Directory.CreateDirectory(
                Path.Combine(logsRoot.FullName, "ConflictDetector_(AO)_Attribute_Overrides"));
            File.WriteAllText(
                Path.Combine(categoryDirectory.FullName, "synthetic.txt"),
                "Runtime Mod added a property\n       Source <set xpath=\"/items/item[@name='A']/@value\" ...");

            var result = query.CompareRuntimeOcdEvidence(
                new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
                new RuntimeOcdEvidenceInput(
                    session.SnapshotId,
                    logsRoot.FullName,
                    "0.15.2",
                    "2.5",
                    new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)),
                new RuntimeEvidenceComparisonQuery(
                    ObservedCategory: "AO",
                    Limit: 1));

            var item = Assert.Single(result.Items);
            Assert.Equal(QueryRuntimeEvidenceComparisonStatus.Unknown, item.Status);
            var observation = Assert.Single(item.RuntimeObservations);
            Assert.Equal("AO", observation.ObservedCategory);
            Assert.Equal("set", observation.ObservedOperation);
            Assert.Equal("Runtime Mod", observation.ModKey);

            var serialized = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(logsRoot.FullName, serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("Source <set", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime Mod added a property", serialized, StringComparison.Ordinal);

            var empty = query.CompareRuntimeOcdEvidence(
                new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
                new RuntimeOcdEvidenceInput(
                    session.SnapshotId,
                    logsRoot.FullName,
                    "0.15.2",
                    "2.5",
                    new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)),
                new RuntimeEvidenceComparisonQuery(Limit: 0));
            Assert.Empty(empty.Items);
        }
        finally
        {
            logsRoot.Delete(true);
        }
    }

    [Fact]
    public void RequiresLoadedMatchingSnapshotBeforeReadingRuntimeOcdLogs()
    {
        var root = FixtureRoot;
        var baseData = new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config"));
        var query = LocalKnowledgeQueryService.CreateDefault();
        var session = query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));
        var missingLogsPath = Path.Combine(root, "not-a-runtime-ocd-log-directory");

        Assert.Throws<ArgumentException>(() => query.CompareRuntimeOcdEvidence(
            baseData,
            new RuntimeOcdEvidenceInput(
                "different-snapshot",
                missingLogsPath,
                "0.15.2",
                "2.5",
                DateTimeOffset.UtcNow)));

        var unloaded = LocalKnowledgeQueryService.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => unloaded.CompareRuntimeOcdEvidence(
            baseData,
            new RuntimeOcdEvidenceInput(
                session.SnapshotId,
                missingLogsPath,
                "0.15.2",
                "2.5",
                DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void ComparesPhase6FixtureRuntimeLogsWithSafeProjection()
    {
        var root = FixtureRoot;
        var query = LocalKnowledgeQueryService.CreateDefault();
        var session = query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));

        var result = query.CompareRuntimeOcdEvidence(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
            new RuntimeOcdEvidenceInput(
                session.SnapshotId,
                Path.Combine(root, "runtime-logs"),
                "0.15.2",
                "7DTD-synthetic",
                new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("RuntimeOCD", result.RuntimeEvidence.ToolName);
        Assert.NotEmpty(result.RuntimeEvidence.Observations);
        Assert.Contains(result.RuntimeEvidence.Observations, observation => observation.ModKey == "First Mod");
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, item => item.Status == QueryRuntimeEvidenceComparisonStatus.Unknown);

        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(Path.GetFullPath(root), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Source <set", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("First Mod added", serialized, StringComparison.Ordinal);
    }

    private static RuntimeEvidenceInput CreateInput(string snapshotId)
    {
        return new RuntimeEvidenceInput(
            snapshotId,
            "synthetic-runtime",
            "0.1",
            "2.5",
            new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero),
            new[]
            {
                new RuntimeEvidenceObservationInput(
                    "Synthetic Mod",
                    "Config/items.xml",
                    "/items/item[@name='A']/@value",
                    "conflict",
                    "raw-match",
                    QuerySemanticConflictAssessment.Conflict,
                    new SourceReferenceReadModel(
                        QuerySourceReferenceKind.RuntimeLog,
                        "runtime/runtime.log"),
                    new[]
                    {
                        new DiagnosticReadModel(
                            "runtime.observation.warning",
                            QueryDiagnosticSeverity.Warning,
                            "Synthetic observation warning.")
                    })
            },
            new[]
            {
                new DiagnosticReadModel(
                    "runtime.document.warning",
                    QueryDiagnosticSeverity.Warning,
                    "Synthetic document warning.")
            });
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "7dtd-mo2-phase4");
}
