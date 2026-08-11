using System.Text.Json;

namespace ModScope.Desktop.Contracts;

public sealed class BridgeProtocolException : Exception
{
    public BridgeProtocolException(string message)
        : base(message)
    {
    }

    public BridgeProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class BridgeProtocol
{
    public const int ContractVersion = 1;
    public const string AppHostOrigin = "https://appassets.modscope";

    private static readonly HashSet<string> KnownCommands = new(StringComparer.Ordinal)
    {
        "browser.navigate",
        "browser.back",
        "browser.forward",
        "browser.reload",
        "browser.observe",
        "knowledge.useFixture",
        "knowledge.loadSource",
        "identity.confirm",
        "inspector.open"
    };

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static BridgeCommandEnvelope ParseCommand(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new BridgeProtocolException("The bridge message is empty.");
        }

        BridgeCommandEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<BridgeCommandEnvelope>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new BridgeProtocolException("The bridge message is not valid JSON.", exception);
        }

        if (envelope is null)
        {
            throw new BridgeProtocolException("The bridge message is null.");
        }

        if (envelope.ContractVersion != ContractVersion)
        {
            throw new BridgeProtocolException(
                $"Unsupported bridge contract version: {envelope.ContractVersion}.");
        }

        if (string.IsNullOrWhiteSpace(envelope.RequestId))
        {
            throw new BridgeProtocolException("The bridge requestId is required.");
        }

        if (envelope.RequestId.Length > 100)
        {
            throw new BridgeProtocolException("The bridge requestId is too long.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Command) || !KnownCommands.Contains(envelope.Command))
        {
            throw new BridgeProtocolException($"Unknown bridge command: {envelope.Command ?? "<null>"}.");
        }

        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new BridgeProtocolException("The bridge payload is required.");
        }

        if (envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new BridgeProtocolException("The bridge payload must be a JSON object.");
        }

        return envelope;
    }

    public static T ReadPayload<T>(JsonElement payload)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload.GetRawText(), JsonOptions)
                ?? throw new BridgeProtocolException($"The payload could not be read as {typeof(T).Name}.");
        }
        catch (JsonException exception)
        {
            throw new BridgeProtocolException(
                $"The payload could not be read as {typeof(T).Name}.",
                exception);
        }
    }

    public static string SerializeMessage<T>(
        string kind,
        T payload,
        string? requestId = null)
    {
        return JsonSerializer.Serialize(
            new BridgeMessageEnvelope(ContractVersion, kind, requestId, payload!),
            JsonOptions);
    }

    public static bool TryGetSupportedBrowserUri(string value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme is not ("http" or "https" or "file" or "about"))
        {
            return false;
        }

        if (string.Equals(candidate.Host, "appassets.modscope", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    public static bool IsAppHostUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "appassets.modscope", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record BridgeCommandEnvelope(
    int ContractVersion,
    string? RequestId,
    string? Command,
    JsonElement Payload);

public sealed record BridgeMessageEnvelope(
    int ContractVersion,
    string Kind,
    string? RequestId,
    object Payload);

public sealed record NavigatePayload(string Url);

public sealed record LoadSourcePayload(
    string InstanceName,
    string ProfileName,
    string InstanceRootPath,
    string ProfilePath,
    string ModsPath);

public sealed record ConfirmIdentityPayload(
    string CandidateIdentity,
    string? LocalModKey);

public sealed record InspectorOpenPayload(string ModKey);

public sealed record BridgeErrorPayload(string Code, string Message);

public sealed record BrowserUiState(
    string Url,
    string Title,
    bool CanGoBack,
    bool CanGoForward);

public sealed record SourceReferenceUiState(
    string Kind,
    string RelativePath,
    int? LineNumber = null,
    int? ColumnNumber = null);

public sealed record DiagnosticUiState(
    string Code,
    string Severity,
    string Message,
    SourceReferenceUiState? Source = null,
    string? RawValue = null);

public sealed record EvidenceReferenceUiState(
    string Kind,
    SourceReferenceUiState Source);

public sealed record PageObservationUiState(
    string Url,
    string Title,
    string? ContentPreview,
    DateTimeOffset ObservedAtUtc,
    string Source,
    string ExtractionStatus,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record KnowledgeSessionUiState(
    string SnapshotId,
    string InstanceName,
    string ProfileName,
    DateTimeOffset CreatedAtUtc,
    string ParserVersion,
    int SchemaVersion,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record ModCandidateUiState(
    string ModKey,
    string DirectoryName,
    string? DisplayName,
    string? Version,
    string ProfileState,
    string EnabledState,
    int? Priority,
    SourceReferenceUiState Source,
    EvidenceReferenceUiState? PriorityEvidence,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record KnowledgeUiState(
    KnowledgeSessionUiState? Session,
    IReadOnlyList<ModCandidateUiState> Candidates);

public sealed record IdentityUiState(
    string CandidateIdentity,
    string? SelectedLocalModKey);

public sealed record LocalContextUiState(
    string CandidateIdentity,
    string Status,
    string InstanceName,
    string ProfileName,
    string? LocalModKey,
    string? DirectoryName,
    string EnabledState,
    int? Priority,
    string? KnownVersion,
    IReadOnlyList<EvidenceReferenceUiState> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<DiagnosticUiState> Diagnostics);

public sealed record ModInfoUiState(
    string RelativePath,
    string ParseStatus,
    string? Name,
    string? DisplayName,
    string? Version,
    string? Description,
    string? Author,
    string? Website,
    IReadOnlyList<RawXmlObservationUiState> UnknownObservations,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record ModFileUiState(
    string RelativePath,
    long Size,
    string Sha256,
    SourceReferenceUiState Source,
    string EvidenceKind);

public sealed record XmlAttributeObservationUiState(
    string Name,
    string Value);

public sealed record RawXmlObservationUiState(
    string ElementPath,
    string ElementName,
    IReadOnlyList<XmlAttributeObservationUiState> Attributes,
    string? InnerText,
    SourceReferenceUiState Source);

public sealed record XmlXPathCandidateUiState(
    string RawValue,
    string ElementPath,
    SourceReferenceUiState Source);

public sealed record XmlFileUiState(
    string RelativePath,
    string ParseStatus,
    string? EncodingName,
    string? RootElementName,
    int ElementCount,
    int AttributeCount,
    IReadOnlyList<XmlXPathCandidateUiState> XPathCandidates,
    IReadOnlyList<RawXmlObservationUiState> RawObservations,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record InspectorUiState(
    string ModKey,
    string DirectoryName,
    string ProfileState,
    string EnabledState,
    int? Priority,
    ModInfoUiState? ModInfo,
    IReadOnlyList<ModFileUiState> Files,
    IReadOnlyList<XmlFileUiState> XmlFiles,
    IReadOnlyList<DiagnosticUiState> Diagnostics,
    SourceReferenceUiState Source);

public sealed record UiState(
    BrowserUiState Browser,
    PageObservationUiState? Observation,
    KnowledgeUiState Knowledge,
    IdentityUiState Identity,
    LocalContextUiState? LocalContext,
    InspectorUiState? Inspector,
    string StatusMessage,
    IReadOnlyList<DiagnosticUiState> Diagnostics);
