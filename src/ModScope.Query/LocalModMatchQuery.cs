using System.Text;
using ModScope.LocalKnowledge;

namespace ModScope.Query;

internal static class LocalModMatchQuery
{
    public static IReadOnlyList<LocalModMatchReadModel> Find(
        LocalModSnapshot snapshot,
        PageObservation page)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(page);

        var pageUrl = NormalizeUrl(page.Url);
        var pageTitle = NormalizeName(page.Title);
        if (pageUrl is null && pageTitle is null)
        {
            return Array.Empty<LocalModMatchReadModel>();
        }

        var matches = new Dictionary<string, MatchAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in snapshot.Mods)
        {
            if (record.ModInfo is null && record.ProfileState != ModProfileState.Unresolved)
            {
                continue;
            }

            var names = new[]
            {
                NormalizeName(record.ModKey),
                NormalizeName(record.DirectoryName),
                NormalizeName(record.ModInfo?.DisplayName)
            }
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var website = NormalizeUrl(record.ModInfo?.Website);

            var strongUrl = pageUrl is not null
                && website is not null
                && pageUrl.Value.Equals(website.Value);
            var strongName = pageTitle is not null
                && names.Any(name => string.Equals(pageTitle, name, StringComparison.Ordinal));
            var partialUrl = !strongUrl && IsPartialUrlMatch(pageUrl, website);
            var partialName = !strongName && pageTitle is not null
                && names.Any(name => IsPartialNameMatch(pageTitle, name));

            if (!strongUrl && !strongName && !partialUrl && !partialName)
            {
                continue;
            }

            if (!matches.TryGetValue(record.ModKey, out var accumulator))
            {
                accumulator = new MatchAccumulator(record);
                matches.Add(record.ModKey, accumulator);
            }

            accumulator.Add(strongUrl, strongName, partialUrl, partialName);
        }

        return matches.Values
            .Select(match => match.ToReadModel())
            .OrderByDescending(match => match.Strength)
            .ThenBy(match => match.ModKey, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static bool IsPartialUrlMatch(CanonicalUrl? page, CanonicalUrl? website)
    {
        if (page is null || website is null
            || !string.Equals(page.Value.Host, website.Value.Host, StringComparison.Ordinal)
            || string.Equals(page.Value.Path, website.Value.Path, StringComparison.Ordinal))
        {
            return false;
        }

        return page.Value.Path.StartsWith(website.Value.Path + "/", StringComparison.Ordinal)
            || website.Value.Path.StartsWith(page.Value.Path + "/", StringComparison.Ordinal);
    }

    private static bool IsPartialNameMatch(string pageTitle, string candidateName)
    {
        const int minimumUsefulLength = 3;
        if (pageTitle.Length < minimumUsefulLength || candidateName.Length < minimumUsefulLength)
        {
            return false;
        }

        return pageTitle.Contains(candidateName, StringComparison.Ordinal)
            || candidateName.Contains(pageTitle, StringComparison.Ordinal);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Normalize(System.Text.NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static CanonicalUrl? NormalizeUrl(Uri? value)
    {
        if (value is null || !value.IsAbsoluteUri || string.IsNullOrWhiteSpace(value.Host))
        {
            return null;
        }

        var host = value.Host.ToLowerInvariant();
        if (!value.IsDefaultPort)
        {
            host = $"{host}:{value.Port}";
        }

        var path = string.IsNullOrWhiteSpace(value.AbsolutePath)
            ? "/"
            : value.AbsolutePath;
        while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path[..^1];
        }

        return new CanonicalUrl(host, path);
    }

    private static CanonicalUrl? NormalizeUrl(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            ? NormalizeUrl(uri)
            : null;
    }

    private readonly record struct CanonicalUrl(string Host, string Path);

    private sealed class MatchAccumulator
    {
        private readonly LocalModRecord _record;
        private readonly HashSet<string> _evidence = new(StringComparer.Ordinal);
        private bool _strongUrl;
        private bool _strongName;
        private bool _partialUrl;
        private bool _partialName;

        public MatchAccumulator(LocalModRecord record)
        {
            _record = record;
        }

        public void Add(bool strongUrl, bool strongName, bool partialUrl, bool partialName)
        {
            _strongUrl |= strongUrl;
            _strongName |= strongName;
            _partialUrl |= partialUrl;
            _partialName |= partialName;

            if (strongUrl)
            {
                _evidence.Add("URL: normalized host/path matches ModInfo.website.");
            }
            else if (partialUrl)
            {
                _evidence.Add("URL: page host/path partially matches ModInfo.website.");
            }

            if (strongName)
            {
                _evidence.Add("Title: normalized page title exactly matches a MOD name.");
            }
            else if (partialName)
            {
                _evidence.Add("Title: normalized page title partially matches a MOD name.");
            }
        }

        public LocalModMatchReadModel ToReadModel()
        {
            var hasUrl = _strongUrl || _partialUrl;
            var hasName = _strongName || _partialName;
            var kind = hasUrl && hasName
                ? LocalModMatchKind.UrlAndName
                : hasUrl
                    ? LocalModMatchKind.Url
                    : LocalModMatchKind.Name;
            var strength = _strongUrl || _strongName
                ? LocalModMatchStrength.Strong
                : LocalModMatchStrength.Partial;

            return new LocalModMatchReadModel(
                _record.ModKey,
                _record.DirectoryName,
                _record.ModInfo?.DisplayName ?? _record.ModInfo?.Name,
                QueryProjection.ProfileState(_record.ProfileState),
                QueryProjection.EnabledState(_record.EnabledState),
                kind,
                strength,
                string.Join(" ", _evidence.OrderBy(value => value, StringComparer.Ordinal)),
                strength == LocalModMatchStrength.Strong
                    && _record.ModInfo is not null
                    && _record.ProfileState != ModProfileState.Unresolved);
        }
    }
}
