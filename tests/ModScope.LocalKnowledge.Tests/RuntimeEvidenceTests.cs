using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class RuntimeEvidenceTests
{
    [Fact]
    public void ComparesMatchingDifferentRuntimeOnlyAndStaticOnlyResults()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);
        var knownGroups = analysis.Groups
            .Where(group => group.TargetXml is not null
                && group.XPath is not null
                && group.Assessment != SemanticConflictAssessment.Unknown)
            .ToList();
        var matchGroup = knownGroups.FirstOrDefault()
            ?? throw new InvalidOperationException("The fixture must contain a known static conflict group.");
        var differentGroup = knownGroups.Skip(1).FirstOrDefault()
            ?? throw new InvalidOperationException("The fixture must contain two known static conflict groups.");

        var comparison = RuntimeEvidenceComparison.Compare(
            analysis,
            new RuntimeEvidenceDocument(
                new RuntimeEvidenceBinding(
                    snapshot.SnapshotId,
                    snapshot.InstanceName,
                    snapshot.ProfileName),
                "synthetic-runtime",
                "0.1",
                "2.5",
                new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero),
                new[]
                {
                    Observation(matchGroup, matchGroup.Assessment, "match"),
                    Observation(
                        differentGroup,
                        differentGroup.Assessment == SemanticConflictAssessment.Conflict
                            ? SemanticConflictAssessment.Compatible
                            : SemanticConflictAssessment.Conflict,
                        "different"),
                    new RuntimeEvidenceObservation(
                        "Runtime Mod",
                        "Config/runtime-only.xml",
                        "/items/item[@name='R']/@value",
                        "set",
                        "runtime-only",
                        SemanticConflictAssessment.Conflict,
                        new SourceReference(SourceReferenceKind.RuntimeLog, "runtime/runtime.log"),
                        Array.Empty<Diagnostic>())
                },
                Array.Empty<Diagnostic>()));

        Assert.Equal(RuntimeEvidenceComparisonStatus.Match, Find(comparison, matchGroup).Status);
        Assert.Equal(RuntimeEvidenceComparisonStatus.Different, Find(comparison, differentGroup).Status);

        var runtimeOnly = Assert.Single(comparison.Items, item => item.TargetXml == "runtime-only.xml");
        Assert.Equal(RuntimeEvidenceComparisonStatus.RuntimeOnly, runtimeOnly.Status);
        Assert.Contains(comparison.Items, item =>
            item.Status == RuntimeEvidenceComparisonStatus.StaticOnly);
        Assert.Equal("match", Find(comparison, matchGroup).RuntimeObservations[0].RawResult);
        Assert.Equal(SourceReferenceKind.RuntimeLog, Find(comparison, matchGroup).RuntimeObservations[0].RawLogReference.Kind);
    }

    [Fact]
    public void KeepsDuplicateObservationsAndReportsMissingOrConflictingAssessments()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);
        var group = analysis.Groups.FirstOrDefault(candidate =>
            candidate.TargetXml is not null
            && candidate.XPath is not null
            && candidate.Assessment != SemanticConflictAssessment.Unknown)
            ?? throw new InvalidOperationException("The fixture must contain a known static conflict group.");

        var comparison = RuntimeEvidenceComparison.Compare(
            analysis,
            new RuntimeEvidenceDocument(
                new RuntimeEvidenceBinding(snapshot.SnapshotId, snapshot.InstanceName, snapshot.ProfileName),
                "synthetic-runtime",
                null,
                null,
                new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero),
                new[]
                {
                    Observation(group, group.Assessment, "first"),
                    Observation(group, null, "missing"),
                    Observation(
                        group,
                        group.Assessment == SemanticConflictAssessment.Conflict
                            ? SemanticConflictAssessment.Compatible
                            : SemanticConflictAssessment.Conflict,
                        "different")
                },
                new[]
                {
                    new Diagnostic(
                        "runtime.import.warning",
                        DiagnosticSeverity.Warning,
                        "Synthetic import warning.")
                }));

        var item = Find(comparison, group);
        Assert.Equal(RuntimeEvidenceComparisonStatus.Unknown, item.Status);
        Assert.Equal(3, item.RuntimeObservations.Count);
        Assert.Contains(item.Diagnostics, diagnostic => diagnostic.Code == "runtime.assessment.missing");
        Assert.Contains(item.Diagnostics, diagnostic => diagnostic.Code == "runtime.duplicate-assessment");
        Assert.Contains(comparison.Diagnostics, diagnostic => diagnostic.Code == "runtime.import.warning");
    }

    [Fact]
    public void RejectsRuntimeEvidenceForAnotherSnapshot()
    {
        var snapshot = ReadFixture();
        var analysis = Analyze(snapshot);

        Assert.Throws<ArgumentException>(() => RuntimeEvidenceComparison.Compare(
            analysis,
            new RuntimeEvidenceDocument(
                new RuntimeEvidenceBinding(
                    "another-snapshot",
                    snapshot.InstanceName,
                    snapshot.ProfileName),
                "synthetic-runtime",
                null,
                null,
                DateTimeOffset.UtcNow,
                Array.Empty<RuntimeEvidenceObservation>(),
                Array.Empty<Diagnostic>())));
    }

    private static RuntimeEvidenceObservation Observation(
        SemanticConflictGroup group,
        SemanticConflictAssessment? assessment,
        string rawResult)
    {
        return new RuntimeEvidenceObservation(
            "Synthetic Mod",
            $"Config/{group.TargetXml}",
            group.XPath,
            "conflict",
            rawResult,
            assessment,
            new SourceReference(SourceReferenceKind.RuntimeLog, "runtime/runtime.log"),
            Array.Empty<Diagnostic>());
    }

    private static RuntimeEvidenceComparisonItem Find(
        RuntimeEvidenceComparison comparison,
        SemanticConflictGroup group)
    {
        return Assert.Single(comparison.Items, item =>
            item.TargetXml == group.TargetXml
            && item.XPath == group.XPath);
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
