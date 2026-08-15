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

    public string? ReleaseScopeKind { get; init; }
    public string? ReleaseScopeRawVersion { get; init; }
    public string? ReleaseScopeVersion { get; init; }
    public string? ReleaseScopeUrl { get; init; }
    public string? ReleaseScopeMatchedLine { get; init; }
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
    public string? ReleaseScopeKind { get; init; }
    public string? ReleaseScopeRawVersion { get; init; }
    public string? ReleaseScopeVersion { get; init; }
    public string? ReleaseScopeUrl { get; init; }
    public string? ReleaseScopeMatchedLine { get; init; }
}

public static class KnownSiteCompatibilityObserver
{
    private const string GitHubReleaseScope = "GitHubRelease";
    private const string NexusFileScope = "NexusFile";
    private const string PageScope = "Page";

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
        return ObserveCore(url, visibleText, null, scopeInputWasProvided: false);
    }

    public static WebCompatibilityObservationResult Observe(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes)
    {
        return ObserveCore(url, visibleText, scopes, scopeInputWasProvided: true);
    }

    private static WebCompatibilityObservationResult ObserveCore(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (IsGitHubReleasePage(url))
        {
            return ObserveSurface(
                "GitHub",
                "GitHub Releases",
                url,
                visibleText,
                scopes,
                scopeInputWasProvided,
                isReleaseSurface: true);
        }

        if (IsNexusFilesPage(url))
        {
            return ObserveSurface(
                "Nexus",
                "Nexus Files",
                url,
                visibleText,
                scopes,
                scopeInputWasProvided,
                isReleaseSurface: true);
        }

        if (IsNexusDescriptionPage(url))
        {
            return ObserveSurface(
                "Nexus",
                "Nexus Description",
                url,
                visibleText,
                scopes,
                scopeInputWasProvided,
                isReleaseSurface: false);
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
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided,
        bool isReleaseSurface)
    {
        if (string.IsNullOrWhiteSpace(visibleText)
            && (scopes is null || scopes.Count == 0))
        {
            return Missing(site, surface, $"{surface} content is empty.");
        }

        var scopeTexts = BuildScopeTexts(
            url,
            visibleText,
            scopes,
            scopeInputWasProvided,
            isReleaseSurface,
            surface);
        var parsedObservations = scopeTexts
            .SelectMany(scopeText => Lines(scopeText.Text)
                .Select(line => ParseLine(line, scopeText.Scope, scopeText.ScopeRequired)))
            .Where(observation => observation is not null)
            .Select(observation => observation!)
            .ToList();

        if (scopeInputWasProvided && isReleaseSurface && scopeTexts.Any(scopeText => scopeText.Scope is not null))
        {
            var scopedLines = scopeTexts
                .SelectMany(scopeText => Lines(scopeText.Text))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            parsedObservations.AddRange(
                Lines(visibleText ?? string.Empty)
                    .Where(line => !scopedLines.Contains(line))
                    .Select(line => ParseLine(line, null, scopeRequired: true))
                    .Where(observation => observation is not null)
                    .Select(observation => observation!));
        }

        var observations = parsedObservations
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

    private static IReadOnlyList<ScopeText> BuildScopeTexts(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided,
        bool isReleaseSurface,
        string surface)
    {
        if (!scopeInputWasProvided)
        {
            return new[] { new ScopeText(null, false, visibleText ?? string.Empty) };
        }

        if (scopes is { Count: > 0 })
        {
            return scopes
                .Select(scope => new ScopeText(
                    scope,
                    ScopeRequiresResolution(scope),
                    scope.VisibleText ?? string.Empty))
                .ToList()
                .AsReadOnly();
        }

        if (!isReleaseSurface
            && string.Equals(surface, "Nexus Description", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new ScopeText(
                    new WebReleaseScopeInput(
                        PageScope,
                        null,
                        null,
                        url.ToString(),
                        null,
                        visibleText),
                    false,
                    visibleText ?? string.Empty)
            };
        }

        var tagScope = CreateGitHubTagScope(url, visibleText);
        if (tagScope is not null)
        {
            return new[] { new ScopeText(tagScope, false, visibleText ?? string.Empty) };
        }

        return new[] { new ScopeText(null, true, visibleText ?? string.Empty) };
    }

    private static WebCompatibilityObservation? ParseLine(
        string line,
        WebReleaseScopeInput? scope,
        bool scopeRequired)
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
            return AttachScope(
                new WebCompatibilityObservation(
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
                    }),
                scope,
                scopeRequired);
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

            return AttachScope(
                new WebCompatibilityObservation(
                    relation,
                    gameContext,
                    NullIfWhiteSpace(rawValue),
                    null,
                    null,
                    matchedLine,
                    diagnostics.AsReadOnly()),
                scope,
                scopeRequired);
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

        return AttachScope(
            new WebCompatibilityObservation(
                relation,
                gameResolution.GameContext,
                rawValue,
                normalizedVersion,
                build,
                matchedLine,
                parsedDiagnostics.AsReadOnly()),
            scope,
            scopeRequired);
    }

    private static WebCompatibilityObservation AttachScope(
        WebCompatibilityObservation observation,
        WebReleaseScopeInput? scope,
        bool scopeRequired)
    {
        if (scope is null)
        {
            return scopeRequired
                ? AddDiagnostic(
                    observation,
                    "web.compatibility.release-scope-unresolved",
                    "The compatibility claim was visible, but its release or File scope could not be resolved.")
                : observation;
        }

        var canonicalKind = CanonicalScopeKind(scope.Kind);
        var attached = observation with
        {
            ReleaseScopeKind = canonicalKind ?? scope.Kind,
            ReleaseScopeRawVersion = scope.RawVersion,
            ReleaseScopeVersion = NormalizeScopeVersion(scope),
            ReleaseScopeUrl = scope.ScopeUrl,
            ReleaseScopeMatchedLine = scope.MatchedLine
        };

        if (canonicalKind is null
            || string.IsNullOrWhiteSpace(scope.ScopeUrl)
            || (canonicalKind is GitHubReleaseScope or NexusFileScope
                && string.IsNullOrWhiteSpace(attached.ReleaseScopeVersion)))
        {
            return AddDiagnostic(
                attached,
                "web.compatibility.release-scope-unresolved",
                "The visible release or File scope is incomplete. The compatibility claim remains raw evidence.");
        }

        return attached;
    }

    private static WebCompatibilityObservation AddDiagnostic(
        WebCompatibilityObservation observation,
        string code,
        string message)
    {
        var diagnostics = observation.Diagnostics
            .Concat(new[]
            {
                new DiagnosticReadModel(
                    code,
                    QueryDiagnosticSeverity.Warning,
                    message,
                    RawValue: observation.RawValue)
            })
            .ToList()
            .AsReadOnly();
        return observation with { Diagnostics = diagnostics };
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
            observation.RawValue ?? string.Empty,
            observation.ReleaseScopeKind ?? string.Empty,
            observation.ReleaseScopeVersion ?? string.Empty,
            observation.ReleaseScopeUrl ?? string.Empty);
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

    private static WebReleaseScopeInput? CreateGitHubTagScope(Uri url, string? visibleText)
    {
        if (!IsGitHubReleasePage(url))
        {
            return null;
        }

        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tagIndex = Array.FindIndex(
            segments,
            segment => string.Equals(segment, "tag", StringComparison.OrdinalIgnoreCase));
        if (tagIndex < 0 || tagIndex + 1 >= segments.Length)
        {
            return null;
        }

        var rawTag = Uri.UnescapeDataString(segments[tagIndex + 1]);
        return new WebReleaseScopeInput(
            GitHubReleaseScope,
            rawTag,
            null,
            url.ToString(),
            rawTag,
            visibleText);
    }

    private static string? CanonicalScopeKind(string? kind)
    {
        if (string.Equals(kind, GitHubReleaseScope, StringComparison.OrdinalIgnoreCase))
        {
            return GitHubReleaseScope;
        }

        if (string.Equals(kind, NexusFileScope, StringComparison.OrdinalIgnoreCase))
        {
            return NexusFileScope;
        }

        if (string.Equals(kind, PageScope, StringComparison.OrdinalIgnoreCase))
        {
            return PageScope;
        }

        return null;
    }

    private static bool ScopeRequiresResolution(WebReleaseScopeInput scope)
    {
        return CanonicalScopeKind(scope.Kind) is null
            || string.IsNullOrWhiteSpace(scope.ScopeUrl);
    }

    private static string? NormalizeScopeVersion(WebReleaseScopeInput scope)
    {
        if (!string.IsNullOrWhiteSpace(scope.RawVersion))
        {
            var normalized = ModScope.LocalKnowledge.VersionNormalizer.Normalize(
                scope.RawVersion,
                out _);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return scope.NormalizedVersion;
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

    private sealed record ScopeText(
        WebReleaseScopeInput? Scope,
        bool ScopeRequired,
        string Text);
}
