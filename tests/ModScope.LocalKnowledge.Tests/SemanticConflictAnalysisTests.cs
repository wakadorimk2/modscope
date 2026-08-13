using System.Security.Cryptography;
using System.Text.Json;
using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class SemanticConflictAnalysisTests
{
    [Fact]
    public void AnalyzesEnabledOperationsInPriorityOrder()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);

        var group = FindGroup(analysis, "/items/item[@name='A']/@value");

        Assert.Equal(SemanticConflictAssessment.Conflict, group.Assessment);
        Assert.Equal(SemanticConflictConfidence.High, group.Confidence);
        Assert.Equal(EffectiveResultStatus.Computed, group.EffectiveStatus);
        Assert.Equal(new[] { "First Mod", "Second Mod" }, group.OperationSequence.Select(operation => operation.ModKey));
        Assert.Equal(new int?[] { 0, 1 }, group.OperationSequence.Select(operation => operation.Priority));
        Assert.Equal("base", group.EffectiveChanges[0].BeforeValue);
        Assert.Equal("one", group.EffectiveChanges[0].AfterValue);
        Assert.Equal("one", group.EffectiveChanges[1].BeforeValue);
        Assert.Equal("two", group.EffectiveChanges[1].AfterValue);
    }

    [Fact]
    public void ClassifiesSameValuesAsCompatibleAndRemoveMutationAsConflict()
    {
        var analysis = Analyze(ReadFixture());

        var sameValue = FindGroup(analysis, "/items/item[@name='C']/@value");
        Assert.Equal(SemanticConflictAssessment.Compatible, sameValue.Assessment);
        Assert.Equal(SemanticConflictConfidence.High, sameValue.Confidence);

        var removeMutation = FindGroup(analysis, "/items/item[@name='B']");
        Assert.Equal(SemanticConflictAssessment.Conflict, removeMutation.Assessment);
        Assert.Contains(removeMutation.OperationSequence, operation => operation.NormalizedKind == XmlPatchOperationKind.Remove);
        Assert.Contains(removeMutation.OperationSequence, operation => operation.NormalizedKind == XmlPatchOperationKind.Set);

        var attributeMutation = FindGroup(analysis, "/items/item[@name='D']");
        Assert.Equal(SemanticConflictAssessment.Conflict, attributeMutation.Assessment);
        Assert.Equal("restored", attributeMutation.EffectiveChanges[^1].AfterValue);

        var setAttribute = FindGroup(analysis, "/items/item[@name='E']");
        Assert.Equal(SemanticConflictAssessment.Conflict, setAttribute.Assessment);
        Assert.Equal("group", setAttribute.OperationSequence[0].AttributeName);
    }

    [Fact]
    public void ComputesSimpleAttributeAppendAndMultipleMatches()
    {
        var analysis = Analyze(ReadFixture());

        var append = FindGroup(analysis, "/items/item[@name='A']/@tags");
        Assert.Equal(EffectiveResultStatus.Computed, append.EffectiveStatus);
        Assert.Equal("seed,first,second", append.EffectiveChanges[^1].AfterValue);
        Assert.Equal(SemanticConflictAssessment.Possible, append.Assessment);
        Assert.Equal(SemanticConflictConfidence.Medium, append.Confidence);

        var multiple = FindGroup(analysis, "/items/item[@name='Many']/@tags");
        Assert.Equal(4, multiple.EffectiveChanges.Count);
        Assert.All(multiple.EffectiveChanges, change => Assert.Equal("tags", change.AttributeName));
    }

    [Fact]
    public void KeepsUnsupportedEffectiveOperationsAndDiagnosticsUnknown()
    {
        var analysis = Analyze(ReadFixture());

        var childFragment = FindGroup(analysis, "/items");
        Assert.Equal(SemanticConflictAssessment.Unknown, childFragment.Assessment);
        Assert.Equal(SemanticConflictConfidence.Unknown, childFragment.Confidence);
        Assert.Equal(EffectiveResultStatus.Unknown, childFragment.EffectiveStatus);
        Assert.Contains(childFragment.OperationSequence, operation => operation.HasChildElements);

        var unknownOperation = FindGroup(analysis, "/items/item[@name='A']");
        Assert.Contains(unknownOperation.OperationSequence, operation => operation.NormalizedKind is null);
        Assert.Equal(SemanticConflictAssessment.Unknown, unknownOperation.Assessment);
        Assert.Equal(SemanticConflictConfidence.Unknown, unknownOperation.Confidence);

        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code == "xml.malformed");
        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code == "xml.dtd.blocked");

        var missingBase = Assert.Single(analysis.Groups, group =>
            group.TargetXml == "missing.xml"
            && group.XPath == "/items/item[@name='Missing']");
        Assert.Equal(SemanticConflictAssessment.Unknown, missingBase.Assessment);
        Assert.Equal(EffectiveResultStatus.Unknown, missingBase.EffectiveStatus);
        Assert.Contains(missingBase.Diagnostics, diagnostic => diagnostic.Code == "conflict.base.missing");

        var invalidXPath = FindGroup(analysis, "/items/[");
        Assert.Contains(invalidXPath.Diagnostics, diagnostic => diagnostic.Code == "conflict.xpath.invalid");

        var noMatch = FindGroup(analysis, "/items/item[@name='Missing']/@value");
        Assert.Contains(noMatch.Diagnostics, diagnostic => diagnostic.Code == "conflict.xpath.no_match");

        var malformedBase = Assert.Single(analysis.BaseFiles, file => file.TargetXml == "malformed.xml");
        Assert.Equal(XmlParseStatus.Malformed, malformedBase.ParseStatus);
        var malformedGroup = Assert.Single(analysis.Groups, group => group.TargetXml == "malformed.xml");
        Assert.Equal(SemanticConflictAssessment.Unknown, malformedGroup.Assessment);

        var dtdBase = Assert.Single(analysis.BaseFiles, file => file.TargetXml == "base-dtd.xml");
        Assert.Equal(XmlParseStatus.DtdBlocked, dtdBase.ParseStatus);
        var dtdGroup = Assert.Single(analysis.Groups, group => group.TargetXml == "base-dtd.xml");
        Assert.Equal(SemanticConflictAssessment.Unknown, dtdGroup.Assessment);
    }

    [Fact]
    public void ExcludesDisabledAndPriorityUnknownMods()
    {
        var snapshot = ReadFixture();
        var priorityUnknown = snapshot.Mods
            .Select(mod => mod.ModKey.Equals("First Mod", StringComparison.Ordinal)
                ? mod with { Priority = null }
                : mod)
            .ToList()
            .AsReadOnly();
        var analysis = Analyze(snapshot with { Mods = priorityUnknown });

        var valueGroup = FindGroup(analysis, "/items/item[@name='A']/@value");
        Assert.DoesNotContain(valueGroup.OperationSequence, operation => operation.ModKey == "Disabled Mod");
        Assert.Equal(new[] { "Second Mod" }, valueGroup.OperationSequence.Select(operation => operation.ModKey));
        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code == "conflict.mod.excluded.priority_unknown");
        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code == "conflict.mod.excluded.disabled");
        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code == "conflict.mod.excluded.profile");
    }

    [Fact]
    public void RecordsRelativeBaseReferenceHashAndDoesNotModifyInputs()
    {
        var before = CaptureFixtureState();
        var baseRoot = Path.Combine(FixtureRoot, "base", "Data", "Config");
        var analysis = SevenDaysToDieConflictAnalyzer.Analyze(
            ReadFixture(),
            new SevenDaysToDieBaseDataSource(baseRoot));
        var after = CaptureFixtureState();

        Assert.Equal(before, after);
        var baseFile = Assert.Single(analysis.BaseFiles, file => file.TargetXml == "items.xml");
        Assert.Equal("Data/Config/items.xml", baseFile.Source.RelativePath);
        Assert.Equal(SourceReferenceKind.GameDataFile, baseFile.Source.Kind);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(baseRoot, "items.xml")))).ToLowerInvariant(),
            baseFile.Sha256);
        Assert.DoesNotContain(Path.GetFullPath(baseRoot), JsonSerializer.Serialize(analysis), StringComparison.Ordinal);
    }

    private static SemanticConflictAnalysis Analyze(LocalModSnapshot snapshot)
    {
        return SevenDaysToDieConflictAnalyzer.Analyze(
            snapshot,
            new SevenDaysToDieBaseDataSource(Path.Combine(FixtureRoot, "base", "Data", "Config")));
    }

    private static SemanticConflictGroup FindGroup(SemanticConflictAnalysis analysis, string xpath)
    {
        return Assert.Single(analysis.Groups, group =>
            group.TargetXml == "items.xml"
            && group.XPath == xpath);
    }

    private static LocalModSnapshot ReadFixture()
    {
        return new Mo2SnapshotReader().Read(new Mo2SourceDefinition(
            "synthetic-instance",
            "default",
            FixtureRoot,
            Path.Combine(FixtureRoot, "profile"),
            Path.Combine(FixtureRoot, "mods")));
    }

    private static Dictionary<string, string> CaptureFixtureState()
    {
        return Directory.EnumerateFiles(FixtureRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(FixtureRoot, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                StringComparer.Ordinal);
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "7dtd-mo2-phase4");
}
