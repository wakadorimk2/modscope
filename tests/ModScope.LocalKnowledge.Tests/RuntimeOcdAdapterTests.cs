using System.Text.Json;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class RuntimeOcdAdapterTests
{
    [Fact]
    public void ImportsRuntimeOcdCategoriesAndPairsDescriptionWithSource()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);
        var group = KnownGroup(analysis);
        var logsRoot = Directory.CreateTempSubdirectory("modscope-runtime-ocd-");

        try
        {
            WriteRecord(logsRoot.FullName, "AO", "<property> was MODIFIED by Runtime Mod", "set", group.XPath!);
            WriteRecord(logsRoot.FullName, "EO", "<element> was MODIFIED by Runtime Mod", "setattribute", group.XPath!);
            WriteRecord(logsRoot.FullName, "FP", "Runtime Mod added descendants", "insertBefore", group.XPath!);
            WriteRecord(logsRoot.FullName, "R", "<element> was REMOVED by Runtime Mod", "remove", group.XPath!);
            WriteRecord(logsRoot.FullName, "SC", "Runtime Mod added an item", "append", group.XPath!);

            var document = new RuntimeOcdAdapter().Import(
                new RuntimeOcdImportRequest(
                    snapshot.SnapshotId,
                    logsRoot.FullName,
                    "0.15.2",
                    "2.5",
                    new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)),
                analysis);

            Assert.Equal("RuntimeOCD", document.ToolName);
            Assert.Equal("0.15.2", document.ToolVersion);
            Assert.Equal("2.5", document.GameVersion);
            Assert.Equal(new[] { "AO", "EO", "FP", "R", "SC" }, document.Observations.Select(observation => observation.ObservedCategory));
            Assert.All(document.Observations, observation =>
            {
                Assert.Equal(group.TargetXml, observation.TargetXml);
                Assert.Equal(group.XPath, observation.XPath);
                Assert.Contains("Source <", observation.RawResult, StringComparison.Ordinal);
                Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Code == "runtime.targetxml.inferred");
            });
            Assert.Contains(document.Observations, observation => observation.ModKey == "Runtime Mod");
            Assert.DoesNotContain(logsRoot.FullName, JsonSerializer.Serialize(document), StringComparison.Ordinal);
        }
        finally
        {
            logsRoot.Delete(true);
        }
    }

    [Fact]
    public void KeepsDuplicatesMalformedRecordsUnknownOperationsAndStrictVersionDiagnostics()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);
        var logsRoot = Directory.CreateTempSubdirectory("modscope-runtime-ocd-invalid-");

        try
        {
            var categoryPath = Directory.CreateDirectory(
                Path.Combine(logsRoot.FullName, "ConflictDetector_(AO)_Attribute_Overrides"));
            File.WriteAllLines(
                Path.Combine(categoryPath.FullName, "synthetic.txt"),
                new[]
                {
                    "Source <explode xpath=\"/unknown\" ...",
                    "broken description without a Source line",
                    "another broken description",
                    "Source <set xpath=\"/unknown\" ...",
                    "another broken description",
                    "Source <set xpath=\"/unknown\" ..."
                });

            var document = new RuntimeOcdAdapter().Import(
                new RuntimeOcdImportRequest(
                    snapshot.SnapshotId,
                    logsRoot.FullName,
                    "0.15.1",
                    null,
                    new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)),
                analysis);
            var comparison = RuntimeEvidenceComparison.Compare(analysis, document);

            Assert.Equal(4, document.Observations.Count);
            Assert.Contains(document.Observations, observation => observation.RawResult.StartsWith("Source <explode", StringComparison.Ordinal));
            Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "runtime.ocd.tool-version.unsupported");
            Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "runtime.ocd.game-version.missing");
            Assert.Contains(document.Observations.SelectMany(observation => observation.Diagnostics), diagnostic => diagnostic.Code == "runtime.ocd.operation.unknown");
            Assert.Contains(document.Observations.SelectMany(observation => observation.Diagnostics), diagnostic => diagnostic.Code == "runtime.ocd.record.orphan-source");
            Assert.Contains(document.Observations.SelectMany(observation => observation.Diagnostics), diagnostic => diagnostic.Code == "runtime.ocd.record.malformed");
            Assert.All(comparison.Items, item => Assert.Equal(RuntimeEvidenceComparisonStatus.Unknown, item.Status));
        }
        finally
        {
            logsRoot.Delete(true);
        }
    }

    [Fact]
    public void InfersOnlyOneStaticTargetAndLeavesAmbiguousOrMissingTargetsUnknown()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);
        var group = KnownGroup(analysis);
        var logsRoot = Directory.CreateTempSubdirectory("modscope-runtime-ocd-target-");

        try
        {
            WriteRecord(logsRoot.FullName, "AO", "Runtime Mod added a property", "set", group.XPath!);

            var inferred = new RuntimeOcdAdapter().Import(
                Request(snapshot, logsRoot.FullName),
                analysis);
            var inferredObservation = Assert.Single(inferred.Observations);
            Assert.Equal(group.TargetXml, inferredObservation.TargetXml);
            Assert.Contains(inferredObservation.Diagnostics, diagnostic => diagnostic.Code == "runtime.targetxml.inferred");

            var ambiguousAnalysis = analysis with
            {
                Groups = new[]
                {
                    group with { TargetXml = "Config/ambiguous-a.xml" },
                    group with { TargetXml = "Config/ambiguous-b.xml" }
                }
            };
            var ambiguous = new RuntimeOcdAdapter().Import(
                Request(snapshot, logsRoot.FullName),
                ambiguousAnalysis);
            var ambiguousObservation = Assert.Single(ambiguous.Observations);
            Assert.Null(ambiguousObservation.TargetXml);
            Assert.Contains(ambiguousObservation.Diagnostics, diagnostic => diagnostic.Code == "runtime.targetxml.ambiguous");

            File.WriteAllText(
                Path.Combine(logsRoot.FullName, "ConflictDetector_(AO)_Synthetic", "AO.txt"),
                "Runtime Mod added a property\n       Source <set xpath=\"/not-in-static\" ...");
            var missing = new RuntimeOcdAdapter().Import(
                Request(snapshot, logsRoot.FullName),
                analysis);
            var missingObservation = Assert.Single(missing.Observations);
            Assert.Null(missingObservation.TargetXml);
            Assert.Contains(missingObservation.Diagnostics, diagnostic => diagnostic.Code == "runtime.targetxml.unresolved");
        }
        finally
        {
            logsRoot.Delete(true);
        }
    }

    private static RuntimeOcdImportRequest Request(LocalModSnapshot snapshot, string logsPath)
    {
        return new RuntimeOcdImportRequest(
            snapshot.SnapshotId,
            logsPath,
            "0.15.2",
            "2.5",
            new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));
    }

    private static void WriteRecord(
        string logsRoot,
        string category,
        string description,
        string operation,
        string xpath)
    {
        var categoryDirectory = Directory.CreateDirectory(
            Path.Combine(logsRoot, $"ConflictDetector_({category})_Synthetic"));
        File.WriteAllText(
            Path.Combine(categoryDirectory.FullName, $"{category}.txt"),
            $"{description}{Environment.NewLine}       Source <{operation} xpath=\"{xpath}\" ...");
    }

    private static SemanticConflictGroup KnownGroup(SemanticConflictAnalysis analysis)
    {
        return analysis.Groups.FirstOrDefault(group =>
                   group.TargetXml is not null
                   && group.XPath is not null
                   && group.Assessment != SemanticConflictAssessment.Unknown)
               ?? throw new InvalidOperationException("The fixture must contain a known static conflict group.");
    }

    private static SemanticConflictAnalysis Analyze(LocalModSnapshot snapshot)
    {
        return SevenDaysToDieConflictAnalyzer.Analyze(
            snapshot,
            new SevenDaysToDieBaseDataSource(Path.Combine(FixtureRoot, "base", "Data", "Config")));
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

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "7dtd-mo2-phase4");
}
