using System.Text.RegularExpressions;

namespace ModScope.Query;

public sealed record WebReleaseScopeInput(
    string Kind,
    string? RawVersion,
    string? NormalizedVersion,
    string? ScopeUrl,
    string? MatchedLine,
    string? VisibleText);

public sealed record WebVersionObservationResult(
    string Site,
    string? RawValue,
    string Evidence,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public bool HasVersion => !string.IsNullOrWhiteSpace(RawValue);

    public string? ReleaseScopeKind { get; init; }
    public string? ReleaseScopeRawVersion { get; init; }
    public string? ReleaseScopeVersion { get; init; }
    public string? ReleaseScopeUrl { get; init; }
    public string? ReleaseScopeMatchedLine { get; init; }
}

public static class KnownSiteVersionObserver
{
    private const string GitHubReleaseScope = "GitHubRelease";
    private const string NexusFileScope = "NexusFile";
    private const string NexusModPageScope = "NexusModPage";

    private static readonly Regex VersionTokenPattern = new(
        @"(?<![A-Za-z0-9])v?\d+\.\d+\.\d+(?:\.\d+)?(?:[-+][0-9A-Za-z.-]+)?(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionLabelPattern = new(
        @"(?i)^\s*(?:file\s+)?version\s*[:#]?\s*(?<value>[^\s,;\)\]]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NexusModVersionLabelPattern = new(
        @"(?i)^\s*version\b(?:\s*[:#-]?\s*(?<value>.*?))?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReleaseLabelPattern = new(
        @"(?i)\b(?:latest\s+)?release\s*[:#]\s*(?<value>[^\s,;\)\]]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static WebVersionObservationResult Observe(Uri url, string? visibleText)
    {
        return ObserveCore(url, visibleText, null, scopeInputWasProvided: false);
    }

    public static WebVersionObservationResult Observe(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes)
    {
        return ObserveCore(url, visibleText, scopes, scopeInputWasProvided: true);
    }

    private static WebVersionObservationResult ObserveCore(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (IsGitHubReleasePage(url))
        {
            return ObserveGitHub(url, visibleText, scopes, scopeInputWasProvided);
        }

        if (IsNexusFilesPage(url))
        {
            return ObserveNexus(url, visibleText, scopes, scopeInputWasProvided);
        }

        if (IsNexusModPage(url))
        {
            return ObserveNexusModPage(url, visibleText, scopes, scopeInputWasProvided);
        }

        return Unsupported(url);
    }

    private static WebVersionObservationResult ObserveGitHub(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided)
    {
        var tagSegments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tagIndex = Array.FindIndex(
            tagSegments,
            segment => string.Equals(segment, "tag", StringComparison.OrdinalIgnoreCase));
        if (tagIndex >= 0 && tagIndex + 1 < tagSegments.Length)
        {
            var rawTag = Uri.UnescapeDataString(tagSegments[tagIndex + 1]);
            var tagScope = FirstScope(scopes, GitHubReleaseScope)
                ?? (scopeInputWasProvided
                    ? new WebReleaseScopeInput(
                        GitHubReleaseScope,
                        rawTag,
                        null,
                        url.ToString(),
                        rawTag,
                        visibleText)
                    : null);
            return BuildSingleCandidate(
                "GitHub",
                rawTag,
                "GitHub release tag",
                "web.version.github.tag",
                tagScope,
                scopeInputWasProvided);
        }

        if (scopeInputWasProvided)
        {
            var firstScope = FirstScope(scopes, GitHubReleaseScope);
            if (firstScope is not null)
            {
                return BuildScopeCandidate(
                    "GitHub",
                    firstScope,
                    "GitHub first visible release scope",
                    "web.version.github.release-scope");
            }
        }

        if (string.IsNullOrWhiteSpace(visibleText))
        {
            return Missing("GitHub", "GitHub release page content is empty.");
        }

        var lines = Lines(visibleText);
        var labeledLine = lines.FirstOrDefault(line => ReleaseLabelPattern.IsMatch(line));
        if (labeledLine is not null)
        {
            return BuildLabeledLine(
                "GitHub",
                labeledLine,
                "GitHub latest release label",
                "web.version.github.release-label",
                scopeInputWasProvided);
        }

        var candidateLine = lines.FirstOrDefault(line => VersionTokenPattern.IsMatch(line));
        if (candidateLine is null)
        {
            return Missing("GitHub", "GitHub release page has no visible release tag candidate.");
        }

        return BuildFromLine(
            "GitHub",
            candidateLine,
            "GitHub release list first visible release",
            "web.version.github.release-list",
            scopeInputWasProvided);
    }

    private static WebVersionObservationResult ObserveNexus(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided)
    {
        if (scopeInputWasProvided)
        {
            var firstScope = FirstScope(scopes, NexusFileScope);
            if (firstScope is not null)
            {
                return BuildScopeCandidate(
                    "Nexus",
                    firstScope,
                    "Nexus Files first visible File scope",
                    "web.version.nexus.file-scope");
            }
        }

        if (string.IsNullOrWhiteSpace(visibleText))
        {
            return Missing("Nexus", "Nexus Files page content is empty.");
        }

        var lines = Lines(visibleText);
        var filesIndex = lines.ToList().FindIndex(
            line => line.Contains("Files", StringComparison.OrdinalIgnoreCase));
        if (filesIndex < 0)
        {
            return Missing("Nexus", "Nexus page does not expose a visible Files surface.");
        }

        var fileLines = lines.Skip(filesIndex + 1).ToList();
        var labeledLine = fileLines.FirstOrDefault(line => VersionLabelPattern.IsMatch(line));
        if (labeledLine is not null)
        {
            return BuildLabeledLine(
                "Nexus",
                labeledLine,
                "Nexus Files first visible File version",
                "web.version.nexus.file-version",
                scopeInputWasProvided);
        }

        var candidateLine = fileLines.FirstOrDefault(line => VersionTokenPattern.IsMatch(line));
        if (candidateLine is null)
        {
            return Missing("Nexus", "Nexus Files surface has no visible File version candidate.");
        }

        return BuildFromLine(
            "Nexus",
            candidateLine,
            "Nexus Files first visible File version",
            "web.version.nexus.file-list",
            scopeInputWasProvided);
    }

    private static WebVersionObservationResult ObserveNexusModPage(
        Uri url,
        string? visibleText,
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        bool scopeInputWasProvided)
    {
        if (scopeInputWasProvided)
        {
            var pageScopes = scopes?
                .Where(scope => string.Equals(scope.Kind, NexusModPageScope, StringComparison.OrdinalIgnoreCase))
                .ToList()
                ?? new List<WebReleaseScopeInput>();
            if (pageScopes.Count == 0)
            {
                return Missing(
                    "Nexus",
                    "Nexus mod page has no visible Version scope.");
            }

            if (pageScopes.Count > 1)
            {
                return Multiple(
                    "Nexus",
                    pageScopes
                        .Select(scope => string.IsNullOrWhiteSpace(scope.RawVersion)
                            ? "<missing>"
                            : scope.RawVersion!)
                        .ToList(),
                    string.Join(
                        " | ",
                        pageScopes.Select(scope => scope.MatchedLine
                            ?? scope.VisibleText
                            ?? "<missing>")));
            }

            return BuildNexusModPageScopeCandidate(pageScopes[0]);
        }

        if (string.IsNullOrWhiteSpace(visibleText))
        {
            return Missing("Nexus", "Nexus mod page content is empty.");
        }

        var labeledLines = Lines(visibleText)
            .Where(line => NexusModVersionLabelPattern.IsMatch(line))
            .ToList();
        if (labeledLines.Count == 0)
        {
            return Missing(
                "Nexus",
                "Nexus mod page has no visible Version label.");
        }

        if (labeledLines.Count > 1)
        {
            return Multiple(
                "Nexus",
                labeledLines
                    .Select(line => VersionTokenPattern.Match(line) is { Success: true } match
                        ? match.Value
                        : line)
                    .ToList(),
                string.Join(" | ", labeledLines));
        }

        var labeledLine = labeledLines[0];
        var candidates = VersionTokenPattern.Matches(labeledLine)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count > 1)
        {
            return Multiple("Nexus", candidates, labeledLine);
        }

        var rawValue = candidates.Count == 1
            ? candidates[0]
            : NexusModVersionLabelPattern.Match(labeledLine).Groups["value"].Value.Trim();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Missing(
                "Nexus",
                "Nexus mod page Version label has no visible value.",
                labeledLine);
        }

        return BuildSingleCandidate(
            "Nexus",
            rawValue,
            "Nexus mod page visible Version label",
            "web.version.nexus.mod-page-version",
            null,
            scopeRequired: false);
    }

    private static WebVersionObservationResult BuildScopeCandidate(
        string site,
        WebReleaseScopeInput scope,
        string evidence,
        string diagnosticCode)
    {
        var rawValue = scope.RawVersion;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            rawValue = Lines(scope.VisibleText ?? string.Empty)
                .SelectMany(line => VersionTokenPattern.Matches(line).Cast<Match>())
                .Select(match => match.Value)
                .FirstOrDefault()
                ?? scope.NormalizedVersion;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return AttachScope(
                Missing(site, $"{site} release scope has no visible version value."),
                scope,
                scopeRequired: true);
        }

        return BuildSingleCandidate(
            site,
            rawValue,
            evidence,
            diagnosticCode,
            scope,
            scopeRequired: true);
    }

    private static WebVersionObservationResult BuildNexusModPageScopeCandidate(
        WebReleaseScopeInput scope)
    {
        if (string.IsNullOrWhiteSpace(scope.RawVersion))
        {
            return AttachScope(
                Missing(
                    "Nexus",
                    "Nexus mod page Version label has no visible value.",
                    scope.MatchedLine ?? scope.VisibleText),
                scope,
                scopeRequired: true);
        }

        return BuildSingleCandidate(
            "Nexus",
            scope.RawVersion!,
            "Nexus mod page visible Version label",
            "web.version.nexus.mod-page-version",
            scope,
            scopeRequired: true);
    }

    private static WebVersionObservationResult BuildLabeledLine(
        string site,
        string line,
        string evidence,
        string diagnosticCode,
        bool scopeRequired)
    {
        var candidates = VersionTokenPattern.Matches(line)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count > 1)
        {
            return AttachScope(Multiple(site, candidates, line), null, scopeRequired);
        }

        var rawValue = candidates.Count == 1
            ? candidates[0]
            : (VersionLabelPattern.IsMatch(line)
                ? VersionLabelPattern.Match(line).Groups["value"].Value
                : ReleaseLabelPattern.Match(line).Groups["value"].Value);
        return BuildSingleCandidate(site, rawValue, evidence, diagnosticCode, null, scopeRequired);
    }

    private static WebVersionObservationResult BuildFromLine(
        string site,
        string line,
        string evidence,
        string diagnosticCode,
        bool scopeRequired)
    {
        var candidates = VersionTokenPattern.Matches(line)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
        {
            return AttachScope(Missing(site, $"{site} page has no supported version candidate."), null, scopeRequired);
        }

        if (candidates.Count > 1)
        {
            return AttachScope(Multiple(site, candidates, line), null, scopeRequired);
        }

        return BuildSingleCandidate(site, candidates[0], evidence, diagnosticCode, null, scopeRequired);
    }

    private static WebVersionObservationResult BuildSingleCandidate(
        string site,
        string rawValue,
        string evidence,
        string diagnosticCode,
        WebReleaseScopeInput? scope,
        bool scopeRequired)
    {
        var normalized = ModScope.LocalKnowledge.VersionNormalizer.Normalize(rawValue, out var scheme);
        if (normalized is null
            || scheme is not (ModScope.LocalKnowledge.VersionScheme.Semver or ModScope.LocalKnowledge.VersionScheme.NumericDotted))
        {
            return AttachScope(
                new WebVersionObservationResult(
                    site,
                    null,
                    evidence,
                    new[]
                    {
                        new DiagnosticReadModel(
                            "web.version.unsupported-format",
                            QueryDiagnosticSeverity.Warning,
                            $"The observed {site} value is not a supported version.",
                            RawValue: rawValue)
                    }),
                scope,
                scopeRequired,
                normalized);
        }

        return AttachScope(
            new WebVersionObservationResult(site, rawValue, evidence, Array.Empty<DiagnosticReadModel>()),
            scope,
            scopeRequired,
            normalized);
    }

    private static WebVersionObservationResult AttachScope(
        WebVersionObservationResult result,
        WebReleaseScopeInput? scope,
        bool scopeRequired,
        string? normalizedVersion = null)
    {
        if (scope is null)
        {
            return scopeRequired
                ? AddDiagnostic(
                    result,
                    "web.version.release-scope-unresolved",
                    "The release or File version was observed, but its visible release scope could not be resolved.")
                : result;
        }

        var canonicalKind = CanonicalScopeKind(scope.Kind);
        var scopeVersion = normalizedVersion ?? NormalizeScopeVersion(scope);
        var attached = result with
        {
            ReleaseScopeKind = canonicalKind ?? scope.Kind,
            ReleaseScopeRawVersion = scope.RawVersion ?? result.RawValue,
            ReleaseScopeVersion = scopeVersion,
            ReleaseScopeUrl = scope.ScopeUrl,
            ReleaseScopeMatchedLine = scope.MatchedLine
        };

        if (!IsReleaseScopeKind(canonicalKind)
            || string.IsNullOrWhiteSpace(scope.ScopeUrl)
            || string.IsNullOrWhiteSpace(scopeVersion))
        {
            return AddDiagnostic(
                attached,
                "web.version.release-scope-unresolved",
                "The visible release or File scope is incomplete. The raw version evidence remains available.");
        }

        return attached;
    }

    private static WebVersionObservationResult AddDiagnostic(
        WebVersionObservationResult result,
        string code,
        string message)
    {
        var diagnostics = result.Diagnostics
            .Concat(new[]
            {
                new DiagnosticReadModel(
                    code,
                    QueryDiagnosticSeverity.Warning,
                    message,
                    RawValue: result.RawValue)
            })
            .ToList()
            .AsReadOnly();
        return result with { Diagnostics = diagnostics };
    }

    private static WebVersionObservationResult Unsupported(Uri url)
    {
        return new WebVersionObservationResult(
            "Unsupported",
            null,
            "Automatic release observation is limited to GitHub Releases, Nexus Files, and Nexus mod pages.",
            new[]
            {
                new DiagnosticReadModel(
                    "web.version.unsupported-page",
                    QueryDiagnosticSeverity.Info,
                    $"The current page is not a supported release surface: {url.Host}{url.AbsolutePath}.")
            });
    }

    private static WebVersionObservationResult Missing(
        string site,
        string message,
        string? rawValue = null)
    {
        return new WebVersionObservationResult(
            site,
            null,
            message,
            new[]
            {
                new DiagnosticReadModel(
                    "web.version.missing",
                    QueryDiagnosticSeverity.Warning,
                    message,
                    RawValue: rawValue)
            });
    }

    private static WebVersionObservationResult Multiple(string site, IReadOnlyList<string> candidates, string line)
    {
        return new WebVersionObservationResult(
            site,
            null,
            $"{site} page exposed multiple candidates on the first release line.",
            new[]
            {
                new DiagnosticReadModel(
                    "web.version.multiple-candidates",
                    QueryDiagnosticSeverity.Warning,
                    $"The {site} release surface exposed multiple version candidates.",
                    RawValue: string.Join(" | ", candidates) + " | " + line)
            });
    }

    private static WebReleaseScopeInput? FirstScope(
        IReadOnlyList<WebReleaseScopeInput>? scopes,
        string kind)
    {
        return scopes?.FirstOrDefault(scope =>
            string.Equals(scope.Kind, kind, StringComparison.OrdinalIgnoreCase));
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

        if (string.Equals(kind, NexusModPageScope, StringComparison.OrdinalIgnoreCase))
        {
            return NexusModPageScope;
        }

        if (string.Equals(kind, "Page", StringComparison.OrdinalIgnoreCase))
        {
            return "Page";
        }

        return null;
    }

    private static bool IsReleaseScopeKind(string? kind)
    {
        return string.Equals(kind, GitHubReleaseScope, StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, NexusFileScope, StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, NexusModPageScope, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeScopeVersion(WebReleaseScopeInput scope)
    {
        if (!string.IsNullOrWhiteSpace(scope.RawVersion))
        {
            var normalized = ModScope.LocalKnowledge.VersionNormalizer.Normalize(
                scope.RawVersion,
                out var scheme);
            if (scheme is ModScope.LocalKnowledge.VersionScheme.Semver
                or ModScope.LocalKnowledge.VersionScheme.NumericDotted)
            {
                return normalized;
            }

            return null;
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
        if (!IsHostSuffix(url, "nexusmods.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        var hasModPath = path.Contains("/mods/", StringComparison.OrdinalIgnoreCase);
        var hasFilesQuery = url.Query.Contains("tab=files", StringComparison.OrdinalIgnoreCase);
        var hasFilesPath = path.Contains("/files", StringComparison.OrdinalIgnoreCase);
        return hasModPath && (hasFilesQuery || hasFilesPath);
    }

    private static bool IsNexusModPage(Uri url)
    {
        if (!IsHostSuffix(url, "nexusmods.com"))
        {
            return false;
        }

        return url.AbsolutePath.Contains("/mods/", StringComparison.OrdinalIgnoreCase)
            && !IsNexusFilesPage(url);
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
}
