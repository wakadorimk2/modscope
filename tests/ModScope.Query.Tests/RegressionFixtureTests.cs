using System.Net;
using System.Text;
using System.Text.Json;
using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class RegressionFixtureTests
{
    [Fact]
    public void ProjectsFixtureEvidenceAndSourceUrlsWithoutLocalPathsOrRawMetadata()
    {
        var query = CreateQuery();
        var session = query.Load(CreateSource());
        var candidate = Assert.Single(
            query.GetModCandidates(),
            item => item.DirectoryName == "Toolkit__Nexus-1234-5678");

        Assert.Equal(QueryProfileState.Listed, candidate.ProfileState);
        Assert.Equal(QueryEnabledState.Enabled, candidate.EnabledState);
        Assert.Equal("Synthetic Toolkit", candidate.DisplayName);
        Assert.Equal("https://www.nexusmods.com/7daystodie/mods/1234?tab=description", candidate.Website);

        var relation = Assert.IsType<PackageRelationReadModel>(candidate.PackageRelation);
        Assert.Equal(QueryIdentityResolutionState.Exact, relation.IdentityState);
        Assert.Equal("1234", relation.PackageModId);
        Assert.Equal("5678", relation.PackageFileId);
        Assert.Equal("1.2.3", relation.PackageVersion);
        Assert.Equal(QueryVersionComparisonStatus.NotComparable, relation.Comparison.Status);
        Assert.Equal(3, relation.VersionObservations.Count);

        var artifact = Assert.Single(relation.SourceArtifacts);
        Assert.Equal("toolkit-file", artifact.ArtifactId);
        Assert.Equal("1234", artifact.ModId);
        Assert.Equal("5678", artifact.FileId);
        Assert.Equal(
            "https://evidence.example.invalid/toolkit/1234/5678?observed=manifest",
            artifact.SourceUrl);

        var inspector = query.GetInspector(candidate.ModKey);
        Assert.Equal(QueryIdentityResolutionState.Exact, inspector.PackageRelation!.IdentityState);
        Assert.Equal("manifest.json", session.VersionEvidenceManifest!.DisplayName);

        var serialized = JsonSerializer.Serialize(new { candidate, inspector, session });
        Assert.DoesNotContain(FixtureRoot, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fixture-only-unknown", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("[General]", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchesFixtureWebsiteByHostAndPathWhileIgnoringQueryAndFragment()
    {
        var query = CreateQuery();
        query.Load(CreateSource());

        var matches = query.FindLocalModMatches(new PageObservation(
            new Uri("HTTPS://WWW.NEXUSMODS.COM/7daystodie/mods/1234/?tab=files&file_id=5678#description"),
            "Synthetic Toolkit",
            "page body is not used for matching",
            DateTimeOffset.UtcNow,
            "fixture",
            PageExtractionStatus.Succeeded,
            Array.Empty<DiagnosticReadModel>()));

        var match = Assert.Single(matches, item => item.DirectoryName == "Toolkit__Nexus-1234-5678");
        Assert.Equal(LocalModMatchKind.UrlAndName, match.MatchKind);
        Assert.Equal(LocalModMatchStrength.Strong, match.Strength);
        Assert.True(match.AutoConfirmEligible);
    }

    [Fact]
    public async Task ObservesFixtureArtifactVersionThroughNexusFileClient()
    {
        var query = CreateQuery();
        query.Load(CreateSource());
        var candidate = Assert.Single(
            query.GetModCandidates(),
            item => item.DirectoryName == "Toolkit__Nexus-1234-5678");
        var artifactReadModel = Assert.Single(candidate.PackageRelation!.SourceArtifacts);
        var artifact = new SourceArtifact(
            artifactReadModel.ArtifactId,
            artifactReadModel.Kind,
            artifactReadModel.Name,
            artifactReadModel.ModId,
            artifactReadModel.FileId,
            artifactReadModel.SourceUrl,
            new SourceReference(SourceReferenceKind.EvidenceManifest, artifactReadModel.Source.RelativePath));

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"game_scoped_id\":5678,\"version\":\"v1.2.3\",\"file\":{\"id\":1234}}}",
                Encoding.UTF8,
                "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = new NexusFileVersionClient(httpClient, "ANONYMIZED_API_KEY");

        var observation = await client.ObserveAsync(artifact);

        Assert.Equal("v1.2.3", observation.RawValue);
        Assert.Equal("1.2.3", observation.NormalizedValue);
        Assert.Equal(VersionScheme.Semver, observation.Scheme);
        Assert.Equal(
            "https://api.nexusmods.com/v3/games/7daystodie/mod-file-versions/5678",
            observation.Source.RelativePath);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(observation.Source.RelativePath, handler.Request!.RequestUri!.ToString());
        Assert.Empty(observation.Diagnostics);
    }

    private static LocalKnowledgeQueryService CreateQuery() =>
        new(new Mo2SnapshotReader());

    private static Mo2SourceInput CreateSource()
    {
        return new Mo2SourceInput(
            "synthetic-regression-instance",
            "Default",
            FixtureRoot,
            Path.Combine(FixtureRoot, "profiles", "Default"),
            Path.Combine(FixtureRoot, "mods"))
        {
            ProfilesPath = Path.Combine(FixtureRoot, "profiles"),
            VersionEvidenceManifestPath = ManifestPath
        };
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "regression",
        "7dtd-mo2-provenance");

    private static string ManifestPath => Path.Combine(FixtureRoot, "evidence", "manifest.json");

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public HttpRequestMessage? Request { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestCount++;
            return Task.FromResult(_responder(request));
        }
    }
}
