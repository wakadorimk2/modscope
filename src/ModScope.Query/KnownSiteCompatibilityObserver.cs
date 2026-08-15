using System.Text.RegularExpressions;

namespace ModScope.Query;

public enum WebCompatibilityRelation
{
    GameVersion,
    SupportedGameVersion,
    SupportedFor,
    CompatibleWith,
    RequiresGameVersion
}

public sealed record WebCompatibilityObservation(
    WebCompatibilityRelation Relation,
    string GameContext,
    string? RawValue,
    string? NormalizedVersion,
    string? Build,
    string MatchedLine,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public bool IsPositive => Relation is
        WebCompatibilityRelation.GameVersion
        or WebCompatibilityRelation.SupportedGameVersion
        or WebCompatibilityRelation.SupportedFor
        or WebCompatibilityRelation.CompatibleWith;

    public bool IsCondition => Relation == WebCompatibilityRelation.RequiresGameVersion;
}

public sealed record WebCompatibilityObservationResult(
    string Site,
    string Surface,
    IReadOnlyList<WebCompatibilityObservation> Observations,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public bool HasEvidence => Observations.Count > 0 || Diagnostics.Count > 0;
}

public sealed record CompatibilityObservationReadModel(
    string OwnerKey,
    string Relation,
    string GameContext,
    string? RawValue,
    string? NormalizedValue,
    string? Build,
    string MatchedLine,
    SourceReferenceReadModel Source,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public string? SourceSite { get; init; }
    public string? TargetUrl { get; init; }
}

public static class KnownSiteCompatibilityObserver
{
    private static readonly Regex LabelPattern = new(
        @"^\s*(?:[-*•]\s*)?(?<label>Requires\s+Game\s+Version|Supported\s+Game\s+Version|Game\s+Version|Supported\s+for|Compatible\s+with)\s*[:#-]?\s*(?<value>.*?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex VersionPattern = new(
        @"(?<![A-Za-z0-9])v?(?<version>\d+(?:\.\d+){1,3})(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex BuildPattern = new(
        @"\(\s*(?<build>b\d+)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SevenDaysPattern = new(
        @"\b(?:7\s*DTD|7\s+Days\s+to\s+Die)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex GenericContextWordPattern = new(
        @"\b(?:version|game|build|for|with|compatible|supported|requires|the|on)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ContextPunctuationPattern = new(
        @"[\p{P}\p{S}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static WebCompatibilityObservationResult Observe(Uri url, string? visibleText)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (IsGitHubReleasePage(url))
        {
            return ObserveSurface("GitHub", "GitHub Releases", url, visibleText);
        }

        if (IsNexusFilesPage(url))
        {
            return ObserveSurface("Nexus", "Nexus Files", url, visibleText);
        }

        if (IsNexusDescriptionPage(url))
        {
            return ObserveSurface("Nexus", "Nexus Description", url, visibleText);
        }

        return new WebCompatibilityObservationResult(
            "Unsupported",
            "Unsupported",
            Array.Empty<WebCompatibilityObservation>(),
            new[]
            {
                new DiagnosticReadModel(
                    "web.compatibility.unsupported-page",
                    QueryDiagnosticSeverity.Info,
                    $"The current page is not a supported compatibility surface: {url.Host}{url.AbsolutePath}.")
            });
    }

    private static WebCompatibilityObservationResult ObserveSurface(
        string site,
        string surface,
        Uri url,
        string? visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText))
        {
            return Missing(site, surface, $"{surface} content is empty.");
        }

        var observations = Lines(visibleText)
            .Select(ParseLine)
            .Where(observation => observation is not null)
            .Select(observation => observation!)
            .GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList()
            .AsReadOnly();

        if (observations.Count == 0)
        {
            return Missing(site, surface, $"{surface} has no visible supported compatibility label.");
        }

        return new WebCompatibilityObservationResult(
            site,
            surface,
            observations,
            Array.Empty<DiagnosticReadModel>());
    }

    private static WebCompatibilityObservation? ParseLine(string line)
    {
        var labelMatch = LabelPattern.Match(line);
        if (!labelMatch.Success)
        {
            return null;
        }

        var relation = ParseRelation(labelMatch.Groups["label"].Value);
        var rawValue = labelMatch.Groups["value"].Value.Trim();
        var matchedLine = line.Trim();
        var candidates = VersionPattern.Matches(rawValue)
            .Cast<Match>()
            .GroupBy(match => match.Groups["version"].Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (candidates.Count > 1)
        {
            return new WebCompatibilityObservation(
                relation,
                "Unresolved",
                NullIfWhiteSpace(rawValue),
                null,
                null,
                matchedLine,
                new[]
                {
                    new DiagnosticReadModel(
                        "web.compatibility.multiple-candidates",
                        QueryDiagnosticSeverity.Warning,
                        "The compatibility label contains multiple version candidates.",
                        RawValue: rawValue)
                });
        }

        if (candidates.Count == 0)
        {
            var missing = string.IsNullOrWhiteSpace(rawValue);
            var gameContext = ResolveGameContextWithoutVersion(rawValue);
            var diagnostics = new List<DiagnosticReadModel>
            {
                new(
                    missing
                        ? "web.compatibility.missing-value"
                        : "web.compatibility.unsupported-format",
                    QueryDiagnosticSeverity.Warning,
                    missing
                        ? "The compatibility label has no visible value."
                        : "The compatibility label has no supported game-version value.",
                    RawValue: rawValue)
            };
            if (!string.Equals(gameContext, "7DTD", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(gameContext, "Unresolved", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(OtherGameDiagnostic(rawValue));
            }

            return new WebCompatibilityObservation(
                relation,
                gameContext,
                NullIfWhiteSpace(rawValue),
                null,
                null,
                matchedLine,
                diagnostics.AsReadOnly());
        }

        var candidate = candidates[0];
        var normalizedVersion = candidate.Groups["version"].Value;
        var build = BuildPattern.Match(rawValue) is { Success: true } buildMatch
            ? buildMatch.Groups["build"].Value
            : null;
        var gameResolution = ResolveGameContext(rawValue, candidate);
        var parsedDiagnostics = new List<DiagnosticReadModel>();
        if (gameResolution.IsOtherGame)
        {
            parsedDiagnostics.Add(OtherGameDiagnostic(rawValue));
        }

        return new WebCompatibilityObservation(
            relation,
            gameResolution.GameContext,
            rawValue,
            normalizedVersion,
            build,
            matchedLine,
            parsedDiagnostics.AsReadOnly());
    }

    private static WebCompatibilityRelation ParseRelation(string label)
    {
        var normalized = Regex.Replace(label, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        return normalized.ToLowerInvariant() switch
        {
            "game version" => WebCompatibilityRelation.GameVersion,
            "supported game version" => WebCompatibilityRelation.SupportedGameVersion,
            "supported for" => WebCompatibilityRelation.SupportedFor,
            "compatible with" => WebCompatibilityRelation.CompatibleWith,
            "requires game version" => WebCompatibilityRelation.RequiresGameVersion,
            _ => throw new InvalidOperationException($"Unsupported compatibility label: {label}")
        };
    }

    private static string IdentityKey(WebCompatibilityObservation observation)
    {
        return string.Join(
            "|",
            observation.Relation,
            observation.GameContext,
            observation.NormalizedVersion ?? string.Empty,
            observation.Build ?? string.Empty,
            observation.RawValue ?? string.Empty);
    }

    private static (string GameContext, bool IsOtherGame) ResolveGameContext(
        string rawValue,
        Match versionMatch)
    {
        var before = rawValue[..versionMatch.Index];
        var after = rawValue[(versionMatch.Index + versionMatch.Length)..];
        var context = BuildContextText($"{before} {after}");
        if (string.IsNullOrWhiteSpace(context) || SevenDaysPattern.IsMatch(context))
        {
            return ("7DTD", false);
        }

        return (context, true);
    }

    private static string ResolveGameContextWithoutVersion(string rawValue)
    {
        var context = BuildContextText(rawValue);
        if (string.IsNullOrWhiteSpace(context) || SevenDaysPattern.IsMatch(context))
        {
            return "7DTD";
        }

        return context;
    }

    private static string BuildContextText(string value)
    {
        var withoutBuild = BuildPattern.Replace(value, " ");
        var withoutGenericWords = GenericContextWordPattern.Replace(withoutBuild, " ");
        var withoutPunctuation = ContextPunctuationPattern.Replace(withoutGenericWords, " ");
        return Regex.Replace(withoutPunctuation, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static DiagnosticReadModel OtherGameDiagnostic(string rawValue)
    {
        return new DiagnosticReadModel(
            "web.compatibility.other-game",
            QueryDiagnosticSeverity.Warning,
            "The compatibility line names a game other than the current 7DTD context. The value remains raw evidence.",
            RawValue: rawValue);
    }

    private static WebCompatibilityObservationResult Missing(
        string site,
        string surface,
        string message)
    {
        return new WebCompatibilityObservationResult(
            site,
            surface,
            Array.Empty<WebCompatibilityObservation>(),
            new[]
            {
                new DiagnosticReadModel(
                    "web.compatibility.missing",
                    QueryDiagnosticSeverity.Warning,
                    message)
            });
    }

    private static bool IsGitHubReleasePage(Uri url)
    {
        if (!IsHost(url, "github.com"))
        {
            return false;
        }

        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3
            && string.Equals(segments[2], "releases", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNexusFilesPage(Uri url)
    {
        if (!IsNexusModPage(url))
        {
            return false;
        }

        return url.AbsolutePath.Contains("/files", StringComparison.OrdinalIgnoreCase)
            || url.Query.Contains("tab=files", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNexusDescriptionPage(Uri url)
    {
        if (!IsNexusModPage(url) || IsNexusFilesPage(url))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(url.Query)
            || url.Query.Contains("tab=description", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNexusModPage(Uri url)
    {
        return IsHostSuffix(url, "nexusmods.com")
            && url.AbsolutePath.Contains("/mods/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHost(Uri url, string host)
    {
        return string.Equals(url.Host, host, StringComparison.OrdinalIgnoreCase)
            || string.Equals(url.Host, $"www.{host}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostSuffix(Uri url, string host)
    {
        return string.Equals(url.Host, host, StringComparison.OrdinalIgnoreCase)
            || url.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Lines(string text)
    {
        return text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static string? NullIfWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
