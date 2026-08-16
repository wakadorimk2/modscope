using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class RegressionFixtureTests
{
    [Fact]
    public void DiscoversAndReadsProductionShapedMo2Fixture()
    {
        var discovery = new Mo2SourceDiscovery(new FixtureDiscoveryEnvironment());
        var candidate = Assert.Single(
            discovery.Discover(new Mo2SourceDiscoveryRequest(null, new[] { FixtureRoot })));

        Assert.Equal(Mo2SourceCandidateReadiness.Ready, candidate.Readiness);
        Assert.Equal("7 Days to Die", candidate.GameName);
        Assert.Equal("Default", candidate.Source.ProfileName);
        Assert.Contains(
            candidate.Evidence,
            evidence => evidence.Kind == Mo2SourceDiscoveryEvidenceKind.NativePicker
                && evidence.EvidenceKind == EvidenceKind.Source);

        var snapshot = new Mo2SnapshotReader().Read(
            candidate.Source with
            {
                VersionEvidenceManifestPath = ManifestPath
            });

        Assert.Equal(3, snapshot.ProfileEntries.Count);
        Assert.Equal(4, snapshot.Mods.Count);
        Assert.True(snapshot.VersionEvidenceManifest!.IsLoaded);

        var toolkit = Assert.Single(
            snapshot.Mods,
            mod => mod.Mo2OuterDirectoryName == "Toolkit__Nexus-1234-5678");
        Assert.Equal("Synthetic Toolkit", toolkit.ModInfo!.Name);
        Assert.Equal("v1.02.003", toolkit.ModInfo.Version);
        Assert.Equal("https://www.nexusmods.com/7daystodie/mods/1234?tab=description", toolkit.ModInfo.Website);
        Assert.Equal("1.2.3", toolkit.PackageMetadata!.Version);
        Assert.Equal("1234", toolkit.PackageMetadata.ModId);
        Assert.Equal("5678", toolkit.PackageMetadata.FileId);
        Assert.Equal(
            "https://metadata.example.invalid/toolkit/1234/5678",
            toolkit.PackageMetadata.Url);
        Assert.Contains("general.customnote", toolkit.PackageMetadata.UnknownValues.Keys);

        var toolkitEvidence = Assert.IsType<PackageVersionEvidence>(toolkit.PackageEvidence);
        Assert.Equal(IdentityResolutionState.Exact, toolkitEvidence.IdentityState);
        Assert.Equal(VersionComparisonStatus.NotComparable, toolkitEvidence.Comparison.Status);
        Assert.Equal(3, toolkitEvidence.VersionObservations.Count);
        var toolkitArtifact = Assert.Single(toolkitEvidence.SourceArtifacts);
        Assert.Equal("1234", toolkitArtifact.ModId);
        Assert.Equal("5678", toolkitArtifact.FileId);
        Assert.Equal(
            "https://evidence.example.invalid/toolkit/1234/5678?observed=manifest",
            toolkitArtifact.SourceUrl);

        var derivedSnapshot = new Mo2SnapshotReader().Read(candidate.Source);
        var derivedToolkit = Assert.Single(
            derivedSnapshot.Mods,
            mod => mod.Mo2OuterDirectoryName == "Toolkit__Nexus-1234-5678");
        var derivedArtifact = Assert.Single(
            Assert.IsType<PackageVersionEvidence>(derivedToolkit.PackageEvidence).SourceArtifacts);
        Assert.Equal(
            "https://www.nexusmods.com/7daystodie/mods/1234?tab=files&file_id=5678",
            derivedArtifact.SourceUrl);

        var bundleRecords = snapshot.Mods
            .Where(mod => mod.Mo2OuterDirectoryName == "Bundle__Nexus-2345-6789")
            .ToList();
        Assert.Equal(2, bundleRecords.Count);
        Assert.All(bundleRecords, record =>
        {
            var evidence = Assert.IsType<PackageVersionEvidence>(record.PackageEvidence);
            Assert.Equal(2, evidence.Package.ModletCount);
            Assert.Equal(IdentityResolutionState.Exact, evidence.IdentityState);
            Assert.Single(evidence.SourceArtifacts);
        });
        var staleBundleRecord = Assert.Single(
            bundleRecords,
            record => record.ModInfo?.Name == "Synthetic Bundle Addons");
        Assert.Contains(
            staleBundleRecord.PackageEvidence!.Diagnostics,
            diagnostic => diagnostic.Code == "package.version.local-conflict");

        var missingMetadata = Assert.Single(
            snapshot.Mods,
            mod => mod.Mo2OuterDirectoryName == "Missing Metadata");
        Assert.Equal(PackageMetadataParseStatus.Missing, missingMetadata.PackageMetadata!.ParseStatus);
        Assert.Equal(IdentityResolutionState.Missing, missingMetadata.PackageEvidence!.IdentityState);
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "regression",
        "7dtd-mo2-provenance");

    private static string ManifestPath => Path.Combine(FixtureRoot, "evidence", "manifest.json");

    private sealed class FixtureDiscoveryEnvironment : IMo2DiscoveryEnvironment
    {
        public string? LocalAppDataPath => null;

        public IReadOnlyList<string> GetRunningModOrganizerExecutablePaths() => Array.Empty<string>();

        public string? GetLastUsedInstanceName() => null;
    }
}
