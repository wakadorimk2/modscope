using System.Globalization;
using System.Net;
using System.Text.Json;
using ModScope.LocalKnowledge;

namespace ModScope.Query;

public interface INexusFileVersionClient
{
    Task<VersionObservation> ObserveAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default);
}

public sealed class NexusFileVersionClient : INexusFileVersionClient
{
    public const string ApiKeyEnvironmentVariable = "MODSCOPE_NEXUS_API_KEY";
    public const string ApiBaseUrl = "https://api.nexusmods.com/v3/games/7daystodie/mod-file-versions";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _applicationName;
    private readonly string _applicationVersion;

    public NexusFileVersionClient(
        HttpClient httpClient,
        string? apiKey,
        string applicationName = "ModScope",
        string applicationVersion = "0.1.0")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _applicationName = string.IsNullOrWhiteSpace(applicationName) ? "ModScope" : applicationName.Trim();
        _applicationVersion = string.IsNullOrWhiteSpace(applicationVersion) ? "0.1.0" : applicationVersion.Trim();
    }

    public async Task<VersionObservation> ObserveAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var observedAt = DateTimeOffset.UtcNow;
        if (!TryNormalizePositiveId(artifact.ModId, out _)
            || !TryNormalizePositiveId(artifact.FileId, out var gameScopedFileId))
        {
            return CreateObservation(
                artifact,
                BuildEndpoint(artifact.FileId),
                observedAt,
                null,
                new[]
                {
                    new Diagnostic(
                        "nexus.artifact.identity.invalid",
                        DiagnosticSeverity.Warning,
                        "The SourceArtifact does not contain positive numeric Nexus modId and gameScopedFileId values.",
                        artifact.Source)
                });
        }

        var endpoint = $"{ApiBaseUrl}/{gameScopedFileId}";
        var source = new SourceReference(SourceReferenceKind.NexusApi, endpoint);
        if (_apiKey is null)
        {
            return CreateObservation(
                artifact,
                endpoint,
                observedAt,
                null,
                new[]
                {
                    new Diagnostic(
                        "nexus.api-key.missing",
                        DiagnosticSeverity.Warning,
                        $"The {ApiKeyEnvironmentVariable} environment variable is not set. No Nexus API request was made.",
                        source)
                });
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.TryAddWithoutValidation("apikey", _apiKey);
        request.Headers.TryAddWithoutValidation("Application-Name", _applicationName);
        request.Headers.TryAddWithoutValidation("Application-Version", _applicationVersion);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateObservation(
                artifact,
                endpoint,
                observedAt,
                null,
                new[]
                {
                    new Diagnostic(
                        "nexus.api.timeout",
                        DiagnosticSeverity.Warning,
                        "The Nexus API request timed out. No retry was attempted.",
                        source)
                });
        }
        catch (HttpRequestException)
        {
            return CreateObservation(
                artifact,
                endpoint,
                observedAt,
                null,
                new[]
                {
                    new Diagnostic(
                        "nexus.api.request_failed",
                        DiagnosticSeverity.Warning,
                        "The Nexus API request failed. No retry was attempted.",
                        source)
                });
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return CreateObservation(
                    artifact,
                    endpoint,
                    observedAt,
                    null,
                    new[] { HttpDiagnostic(response.StatusCode, source) });
            }

            try
            {
                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return CreateObservation(
                        artifact,
                        endpoint,
                        observedAt,
                        null,
                        new[]
                        {
                            new Diagnostic(
                                "nexus.response.invalid",
                                DiagnosticSeverity.Warning,
                                "The Nexus API response is not a JSON object.",
                                source)
                        });
                }

                if (!TryReadPositiveId(root, "game_scoped_id", out var responseGameScopedFileId))
                {
                    return CreateObservation(
                        artifact,
                        endpoint,
                        observedAt,
                        null,
                        new[]
                        {
                            new Diagnostic(
                                "nexus.response.game_scoped_id.missing",
                                DiagnosticSeverity.Warning,
                                "The Nexus API response has no valid game_scoped_id. The version was not adopted.",
                                source)
                        });
                }

                if (!string.Equals(responseGameScopedFileId, gameScopedFileId, StringComparison.Ordinal))
                {
                    return CreateObservation(
                        artifact,
                        endpoint,
                        observedAt,
                        null,
                        new[]
                        {
                            new Diagnostic(
                                "nexus.response.game_scoped_id.mismatch",
                                DiagnosticSeverity.Warning,
                                "The Nexus API response game_scoped_id does not match the requested gameScopedFileId. The version was not adopted.",
                                source,
                                responseGameScopedFileId)
                        });
                }

                if (!root.TryGetProperty("version", out var versionElement)
                    || versionElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return CreateObservation(
                        artifact,
                        endpoint,
                        observedAt,
                        null,
                        new[]
                        {
                            new Diagnostic(
                                "nexus.file.version.missing",
                                DiagnosticSeverity.Warning,
                                "The Nexus API response has no file version.",
                                source)
                        });
                }

                if (versionElement.ValueKind != JsonValueKind.String)
                {
                    return CreateObservation(
                        artifact,
                        endpoint,
                        observedAt,
                        null,
                        new[]
                        {
                            new Diagnostic(
                                "nexus.file.version.invalid",
                                DiagnosticSeverity.Warning,
                                "The Nexus API file version is not a string.",
                                source)
                        });
                }

                var rawValue = versionElement.GetString();
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    return CreateObservation(
                        artifact,
                        endpoint,
                        observedAt,
                        null,
                        new[]
                        {
                            new Diagnostic(
                                "nexus.file.version.missing",
                                DiagnosticSeverity.Warning,
                                "The Nexus API response has no file version.",
                                source)
                        });
                }

                return CreateObservation(artifact, endpoint, observedAt, rawValue, Array.Empty<Diagnostic>());
            }
            catch (JsonException)
            {
                return CreateObservation(
                    artifact,
                    endpoint,
                    observedAt,
                    null,
                    new[]
                    {
                        new Diagnostic(
                            "nexus.response.invalid_json",
                            DiagnosticSeverity.Warning,
                            "The Nexus API response is not valid JSON.",
                            source)
                    });
            }
        }
    }

    private static VersionObservation CreateObservation(
        SourceArtifact artifact,
        string endpoint,
        DateTimeOffset observedAt,
        string? rawValue,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        var source = new SourceReference(SourceReferenceKind.NexusApi, endpoint);
        var normalized = VersionNormalizer.Normalize(rawValue, out var scheme);
        return new VersionObservation(
            artifact.ArtifactId,
            VersionObservationRole.Release,
            VersionObservationSourceKind.NexusApi,
            rawValue,
            normalized,
            scheme,
            source,
            observedAt,
            diagnostics);
    }

    private static Diagnostic HttpDiagnostic(HttpStatusCode statusCode, SourceReference source)
    {
        var (code, message) = statusCode switch
        {
            HttpStatusCode.Unauthorized => (
                "nexus.api.unauthorized",
                "The Nexus API rejected the API key."),
            HttpStatusCode.Forbidden => (
                "nexus.api.forbidden",
                "The Nexus API denied access to the requested file."),
            HttpStatusCode.NotFound => (
                "nexus.file.not_found",
                "The Nexus API did not find the requested file."),
            (HttpStatusCode)429 => (
                "nexus.api.rate_limited",
                "The Nexus API rate limit was reached. No retry was attempted."),
            _ => (
                "nexus.api.http_error",
                $"The Nexus API returned HTTP {(int)statusCode}. No retry was attempted.")
        };

        return new Diagnostic(code, DiagnosticSeverity.Warning, message, source, ((int)statusCode).ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryReadPositiveId(JsonElement root, string propertyName, out string normalized)
    {
        normalized = string.Empty;
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var numericValue))
        {
            return numericValue > 0
                && TryNormalizePositiveId(numericValue.ToString(CultureInfo.InvariantCulture), out normalized);
        }

        return value.ValueKind == JsonValueKind.String
            && TryNormalizePositiveId(value.GetString(), out normalized);
    }

    private static bool TryNormalizePositiveId(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !ulong.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed == 0)
        {
            return false;
        }

        normalized = parsed.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static string BuildEndpoint(string? gameScopedFileId)
    {
        return $"{ApiBaseUrl}/{Uri.EscapeDataString(gameScopedFileId?.Trim() ?? "unknown")}";
    }
}
