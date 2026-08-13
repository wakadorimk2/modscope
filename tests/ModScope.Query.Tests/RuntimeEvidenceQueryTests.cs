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
        Assert.Equal("raw-match", item.RuntimeObservations[0].RawResult);
        Assert.Equal(QuerySourceReferenceKind.RuntimeLog, item.RuntimeObservations[0].RawLogReference.Kind);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "runtime.document.warning");
        Assert.Contains(item.Diagnostics, diagnostic => diagnostic.Code == "runtime.observation.warning");
        Assert.DoesNotContain(
            Path.GetFullPath(root),
            JsonSerializer.Serialize(result),
            StringComparison.Ordinal);
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
