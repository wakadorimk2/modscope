using System.Text.RegularExpressions;

namespace ModScope.Query;

public sealed record WebVersionObservationResult(
    string Site,
    string? RawValue,
    string Evidence,
    IReadOnlyList<DiagnosticReadModel> Diagnostics)
{
    public bool HasVersion => !string.IsNullOrWhiteSpace(RawValue);
}

public static class KnownSiteVersionObserver
{
    private static readonly Regex VersionTokenPattern = new(
        @"(?<![A-Za-z0-9])v?\d+\.\d+\.\d+(?:\.\d+)?(?:[-+][0-9A-Za-z.-]+)?(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionLabelPattern = new(
        @"(?i)^\s*(?:file\s+)?version\s*[:#]?\s*(?<value>[^\s,;\)\]]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReleaseLabelPattern = new(
        @"(?i)\b(?:latest\s+)?release\s*[:#]\s*(?<value>[^\s,;\)\]]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static WebVersionObservationResult Observe(Uri url, string? visibleText)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (IsGitHubReleasePage(url))
        {
            return ObserveGitHub(url, visibleText);
        }

        if (IsNexusFilesPage(url))
        {
            return ObserveNexus(url, visibleText);
        }

        return Unsupported(url);
    }

    private static WebVersionObservationResult ObserveGitHub(Uri url, string? visibleText)
    {
        var tagSegments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tagIndex = Array.FindIndex(
            tagSegments,
            segment => string.Equals(segment, "tag", StringComparison.OrdinalIgnoreCase));
        if (tagIndex >= 0 && tagIndex + 1 < tagSegments.Length)
        {
            var rawTag = Uri.UnescapeDataString(tagSegments[tagIndex + 1]);
            return BuildSingleCandidate(
                "GitHub",
                rawTag,
                "GitHub release tag",
                "web.version.github.tag");
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
                "web.version.github.release-label");
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
            "web.version.github.release-list");
    }

    private static WebVersionObservationResult ObserveNexus(Uri url, string? visibleText)
    {
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
                "web.version.nexus.file-version");
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
            "web.version.nexus.file-list");
    }

    private static WebVersionObservationResult BuildLabeledLine(
        string site,
        string line,
        string evidence,
        string diagnosticCode)
    {
        var candidates = VersionTokenPattern.Matches(line)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count > 1)
        {
            return Multiple(site, candidates, line);
        }

        var rawValue = candidates.Count == 1
            ? candidates[0]
            : (VersionLabelPattern.IsMatch(line)
                ? VersionLabelPattern.Match(line).Groups["value"].Value
                : ReleaseLabelPattern.Match(line).Groups["value"].Value);
        return BuildSingleCandidate(site, rawValue, evidence, diagnosticCode);
    }

    private static WebVersionObservationResult BuildFromLine(
        string site,
        string line,
        string evidence,
        string diagnosticCode)
    {
        var candidates = VersionTokenPattern.Matches(line)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
        {
            return Missing(site, $"{site} page has no supported version candidate.");
        }

        if (candidates.Count > 1)
        {
            return Multiple(site, candidates, line);
        }

        return BuildSingleCandidate(site, candidates[0], evidence, diagnosticCode);
    }

    private static WebVersionObservationResult BuildSingleCandidate(
        string site,
        string rawValue,
        string evidence,
        string diagnosticCode)
    {
        var normalized = ModScope.LocalKnowledge.VersionNormalizer.Normalize(rawValue, out var scheme);
        if (normalized is null || scheme is not (ModScope.LocalKnowledge.VersionScheme.Semver or ModScope.LocalKnowledge.VersionScheme.NumericDotted))
        {
            return new WebVersionObservationResult(
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
                });
        }

        return new WebVersionObservationResult(site, rawValue, evidence, Array.Empty<DiagnosticReadModel>());
    }

    private static WebVersionObservationResult Unsupported(Uri url)
    {
        return new WebVersionObservationResult(
            "Unsupported",
            null,
            "Automatic release observation is limited to GitHub Releases and Nexus Files.",
            new[]
            {
                new DiagnosticReadModel(
                    "web.version.unsupported-page",
                    QueryDiagnosticSeverity.Info,
                    $"The current page is not a supported release surface: {url.Host}{url.AbsolutePath}.")
            });
    }

    private static WebVersionObservationResult Missing(string site, string message)
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
                    message)
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
