using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class SemanticConflictQueryTests
{
    [Fact]
    public void ProjectsConflictAnalysisWithFiltersAndLimit()
    {
        var query = LocalKnowledgeQueryService.CreateDefault();
        var root = FixtureRoot;
        query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));

        var result = query.AnalyzeConflicts(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
            new ConflictAnalysisQuery(
                TargetXml: "Config/items.xml",
                XPath: "/items/item[@name='A']/@value",
                Limit: 1));

        var group = Assert.Single(result.Groups);
        Assert.Equal("items.xml", group.TargetXml);
        Assert.Equal(QuerySemanticConflictAssessment.Conflict, group.Assessment);
        Assert.Equal(QuerySemanticConflictConfidence.High, group.Confidence);
        Assert.Equal(QueryEffectiveResultStatus.Computed, group.EffectiveStatus);
        Assert.Equal(2, group.OperationSequence.Count);
        var operation = group.OperationSequence[0];
        Assert.Equal("First Mod", operation.ModKey);
        Assert.Equal(0, operation.Priority);
        Assert.Equal(QueryXmlPatchOperationKind.Set, operation.NormalizedKind);
        Assert.Equal(QuerySourceReferenceKind.ModFile, operation.Source.Kind);
        Assert.Contains(operation.Evidence, evidence => evidence.Kind == QueryEvidenceKind.Source);

        var possible = query.AnalyzeConflicts(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
            new ConflictAnalysisQuery(XPath: "/items/item[@name='A']/@tags"));
        Assert.Equal(QuerySemanticConflictConfidence.Medium, Assert.Single(possible.Groups).Confidence);

        var baseFile = Assert.Single(result.BaseFiles, file => file.TargetXml == "items.xml");
        Assert.Equal(QuerySourceReferenceKind.GameDataFile, baseFile.Source.Kind);
        Assert.Equal("Data/Config/items.xml", baseFile.Source.RelativePath);
        Assert.DoesNotContain(Path.GetFullPath(root), System.Text.Json.JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectsChildFragmentStateAndValidatesLimitAndLoadState()
    {
        var root = FixtureRoot;
        var query = LocalKnowledgeQueryService.CreateDefault();
        query.Load(new Mo2SourceInput(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods")));

        var result = query.AnalyzeConflicts(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")));
        var childGroup = Assert.Single(result.Groups, group =>
            group.TargetXml == "items.xml"
            && group.XPath == "/items");
        Assert.Contains(childGroup.OperationSequence, operation => operation.HasChildElements);
        Assert.Equal(QuerySemanticConflictAssessment.Unknown, childGroup.Assessment);
        Assert.Equal(QuerySemanticConflictConfidence.Unknown, childGroup.Confidence);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "xml.malformed");

        Assert.Throws<ArgumentOutOfRangeException>(() => query.AnalyzeConflicts(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
            new ConflictAnalysisQuery(Limit: -1)));

        var empty = query.AnalyzeConflicts(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config")),
            new ConflictAnalysisQuery(Limit: 0));
        Assert.Empty(empty.Groups);
        Assert.Empty(empty.BaseFiles);

        var unloaded = LocalKnowledgeQueryService.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => unloaded.AnalyzeConflicts(
            new SevenDaysToDieBaseDataInput(Path.Combine(root, "base", "Data", "Config"))));
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "7dtd-mo2-phase4");
}
