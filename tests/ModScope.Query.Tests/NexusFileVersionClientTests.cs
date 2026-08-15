using System.Net;
using System.Text;
using ModScope.LocalKnowledge;
using ModScope.Query;
using Xunit;

namespace ModScope.Query.Tests;

public sealed class NexusFileVersionClientTests
{
    [Fact]
    public async Task RequestsOnlyTheNexusFileEndpointAndProjectsVersionEvidence()
    {
        var handler = new RecordingHandler(_ => Response(
            HttpStatusCode.OK,
            "{\"game_scoped_id\":456,\"version\":\"v1.2.3\",\"file\":{\"id\":123}}"));
        using var httpClient = new HttpClient(handler);
        var client = new NexusFileVersionClient(httpClient, "test-api-key", "ModScope.Tests", "0.1.0");

        var observation = await client.ObserveAsync(Artifact());

        Assert.Equal("v1.2.3", observation.RawValue);
        Assert.Equal("1.2.3", observation.NormalizedValue);
        Assert.Equal(VersionScheme.Semver, observation.Scheme);
        Assert.Equal(VersionObservationSourceKind.NexusApi, observation.SourceKind);
        Assert.Equal(VersionObservationRole.Release, observation.Role);
        Assert.Equal(SourceReferenceKind.NexusApi, observation.Source.Kind);
        Assert.Equal(
            "https://api.nexusmods.com/v3/games/7daystodie/mod-file-versions/456",
            observation.Source.RelativePath);
        Assert.Empty(observation.Diagnostics);

        var request = Assert.IsType<HttpRequestMessage>(handler.Request);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(observation.Source.RelativePath, request.RequestUri!.ToString());
        Assert.Equal("test-api-key", request.Headers.GetValues("apikey").Single());
        Assert.Equal("ModScope.Tests", request.Headers.GetValues("Application-Name").Single());
        Assert.Equal("0.1.0", request.Headers.GetValues("Application-Version").Single());
    }

    [Fact]
    public async Task RejectsAResponseForAnotherGameScopedFileId()
    {
        var handler = new RecordingHandler(_ => Response(
            HttpStatusCode.OK,
            "{\"game_scoped_id\":457,\"version\":\"1.2.3\",\"file\":{\"id\":456}}"));
        using var httpClient = new HttpClient(handler);
        var client = new NexusFileVersionClient(httpClient, "test-api-key");

        var observation = await client.ObserveAsync(Artifact());

        Assert.Null(observation.RawValue);
        Assert.Null(observation.NormalizedValue);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Code == "nexus.response.game_scoped_id.mismatch");
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("{\"version\":\"1.2.3\"}")]
    [InlineData("{\"game_scoped_id\":\"not-a-number\",\"version\":\"1.2.3\"}")]
    [InlineData("{\"game_scoped_id\":0,\"version\":\"1.2.3\"}")]
    public async Task RejectsMissingNonNumericOrZeroGameScopedFileId(string body)
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.OK, body));
        using var httpClient = new HttpClient(handler);
        var observation = await new NexusFileVersionClient(httpClient, "test-api-key").ObserveAsync(Artifact());

        Assert.Null(observation.RawValue);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Code == "nexus.response.game_scoped_id.missing");
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "nexus.api.unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "nexus.api.forbidden")]
    [InlineData(HttpStatusCode.NotFound, "nexus.file.not_found")]
    [InlineData((HttpStatusCode)429, "nexus.api.rate_limited")]
    public async Task ConvertsHttpFailuresToDiagnostics(HttpStatusCode statusCode, string diagnosticCode)
    {
        var handler = new RecordingHandler(_ => Response(statusCode, "{\"secret\":\"not retained\"}"));
        using var httpClient = new HttpClient(handler);
        var client = new NexusFileVersionClient(httpClient, "test-api-key");

        var observation = await client.ObserveAsync(Artifact());

        Assert.Null(observation.RawValue);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode);
        Assert.DoesNotContain("not retained", observation.Diagnostics.Select(diagnostic => diagnostic.Message));
    }

    [Fact]
    public async Task ConvertsTimeoutMalformedJsonAndMissingVersionToDiagnostics()
    {
        var timeoutHandler = new RecordingHandler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        using (var timeoutHttpClient = new HttpClient(timeoutHandler))
        {
            var timeout = await new NexusFileVersionClient(timeoutHttpClient, "test-api-key").ObserveAsync(Artifact());
            Assert.Contains(timeout.Diagnostics, diagnostic => diagnostic.Code == "nexus.api.timeout");
        }

        var malformedHandler = new RecordingHandler(_ => Response(HttpStatusCode.OK, "not-json"));
        using (var malformedHttpClient = new HttpClient(malformedHandler))
        {
            var malformed = await new NexusFileVersionClient(malformedHttpClient, "test-api-key").ObserveAsync(Artifact());
            Assert.Contains(malformed.Diagnostics, diagnostic => diagnostic.Code == "nexus.response.invalid_json");
        }

        var missingVersionHandler = new RecordingHandler(_ => Response(HttpStatusCode.OK, "{\"game_scoped_id\":456}"));
        using (var missingVersionHttpClient = new HttpClient(missingVersionHandler))
        {
            var missingVersion = await new NexusFileVersionClient(missingVersionHttpClient, "test-api-key").ObserveAsync(Artifact());
            Assert.Null(missingVersion.RawValue);
            Assert.Contains(missingVersion.Diagnostics, diagnostic => diagnostic.Code == "nexus.file.version.missing");
        }

        var emptyVersionHandler = new RecordingHandler(_ => Response(HttpStatusCode.OK, "{\"game_scoped_id\":456,\"version\":\" \"}"));
        using (var emptyVersionHttpClient = new HttpClient(emptyVersionHandler))
        {
            var emptyVersion = await new NexusFileVersionClient(emptyVersionHttpClient, "test-api-key").ObserveAsync(Artifact());
            Assert.Null(emptyVersion.RawValue);
            Assert.Contains(emptyVersion.Diagnostics, diagnostic => diagnostic.Code == "nexus.file.version.missing");
        }

        var nonStringVersionHandler = new RecordingHandler(_ => Response(HttpStatusCode.OK, "{\"game_scoped_id\":456,\"version\":123}"));
        using (var nonStringVersionHttpClient = new HttpClient(nonStringVersionHandler))
        {
            var nonStringVersion = await new NexusFileVersionClient(nonStringVersionHttpClient, "test-api-key").ObserveAsync(Artifact());
            Assert.Null(nonStringVersion.RawValue);
            Assert.Contains(nonStringVersion.Diagnostics, diagnostic => diagnostic.Code == "nexus.file.version.invalid");
        }
    }

    [Fact]
    public async Task DoesNotRequestWithoutApiKeyOrExactArtifactIdentity()
    {
        var missingKeyHandler = new RecordingHandler(_ => Response(HttpStatusCode.OK, "{\"game_scoped_id\":456,\"version\":\"1.2.3\"}"));
        using (var missingKeyHttpClient = new HttpClient(missingKeyHandler))
        {
            var missingKey = await new NexusFileVersionClient(missingKeyHttpClient, null).ObserveAsync(Artifact());
            Assert.Contains(missingKey.Diagnostics, diagnostic => diagnostic.Code == "nexus.api-key.missing");
            Assert.Null(missingKeyHandler.Request);
            Assert.Equal(0, missingKeyHandler.RequestCount);
        }

        var invalidArtifactHandler = new RecordingHandler(_ => Response(HttpStatusCode.OK, "{\"game_scoped_id\":456,\"version\":\"1.2.3\"}"));
        using (var invalidArtifactHttpClient = new HttpClient(invalidArtifactHandler))
        {
            var invalidArtifact = Artifact() with { FileId = "not-a-number" };
            var observation = await new NexusFileVersionClient(invalidArtifactHttpClient, "test-api-key").ObserveAsync(invalidArtifact);
            Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Code == "nexus.artifact.identity.invalid");
            Assert.Null(invalidArtifactHandler.Request);
            Assert.Equal(0, invalidArtifactHandler.RequestCount);
        }
    }

    [Fact]
    public async Task DoesNotRetainApiKeyOrRawResponseInDiagnostics()
    {
        const string apiKey = "test-api-key";
        const string rawSecret = "raw-response-secret";
        var handler = new RecordingHandler(_ => Response(
            HttpStatusCode.OK,
            $"{{\"game_scoped_id\":457,\"version\":\"1.2.3\",\"secret\":\"{rawSecret}\"}}"));
        using var httpClient = new HttpClient(handler);
        var observation = await new NexusFileVersionClient(httpClient, apiKey).ObserveAsync(Artifact());

        var messages = observation.Diagnostics.Select(diagnostic => diagnostic.Message);
        Assert.DoesNotContain(apiKey, messages);
        Assert.DoesNotContain(rawSecret, messages);
    }

    private static SourceArtifact Artifact()
    {
        return new SourceArtifact(
            "nexus-file:123:456",
            "nexus-file",
            "Synthetic",
            "123",
            "456",
            "https://www.nexusmods.com/7daystodie/mods/123?tab=files&file_id=456",
            new SourceReference(SourceReferenceKind.PackageFile, "mods/synthetic-package/meta.ini"));
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder = responder;

        public HttpRequestMessage? Request { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestCount++;
            return _responder(request);
        }

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(request => Task.FromResult(responder(request)))
        {
        }
    }
}
