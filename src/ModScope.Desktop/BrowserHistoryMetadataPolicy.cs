namespace ModScope.Desktop;

internal static class BrowserHistoryMetadataPolicy
{
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token",
        "auth",
        "authorization",
        "client_secret",
        "code",
        "id_token",
        "nonce",
        "redirect_uri",
        "return_to",
        "session",
        "session_id",
        "state",
        "token"
    };

    private static readonly HashSet<string> AuthenticationPathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth",
        "authorize",
        "callback",
        "login",
        "oauth",
        "signin"
    };

    public static bool TryNormalizePersistedUrl(string? value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.AbsoluteUri, "about:history", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = uri.AbsoluteUri;
            return true;
        }

        if ((!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            || string.Equals(uri.Host, "appassets.modscope", StringComparison.OrdinalIgnoreCase)
            || IsAuthenticationNavigation(uri))
        {
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };
        normalizedUrl = builder.Uri.AbsoluteUri;
        return true;
    }

    public static bool TryNormalizeHistoryUrl(string? value, out string normalizedUrl)
    {
        if (!TryNormalizePersistedUrl(value, out normalizedUrl)
            || !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            normalizedUrl = string.Empty;
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAuthenticationNavigation(Uri uri)
    {
        if (string.Equals(uri.Host, "users.nexusmods.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(AuthenticationPathSegments.Contains))
        {
            return true;
        }

        var query = uri.Query.TrimStart('?');
        if (query.Length == 0)
        {
            return false;
        }

        return query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2)[0])
            .Select(Uri.UnescapeDataString)
            .Select(key => key.Replace('+', ' ').Trim())
            .Any(SensitiveQueryKeys.Contains);
    }
}
