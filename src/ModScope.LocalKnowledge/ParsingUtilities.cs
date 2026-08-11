using System.Security.Cryptography;
using System.Text;

namespace ModScope.LocalKnowledge;

internal sealed record DecodedText(
    string Text,
    string EncodingName,
    bool HadDecodingError);

internal static class ParsingUtilities
{
    public static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    public static string NormalizePathForComparison(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool IsWithin(string rootPath, string candidatePath)
    {
        var root = NormalizePathForComparison(rootPath);
        var candidate = NormalizePathForComparison(candidatePath);
        var relative = Path.GetRelativePath(root, candidate);

        return relative == "."
            || (!Path.IsPathRooted(relative)
                && relative != ".."
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }

    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string Sha256File(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);

        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static DecodedText DecodeText(byte[] bytes)
    {
        var offset = 0;
        Encoding encoding;
        string encodingName;

        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            encodingName = "utf-8";
            offset = 3;
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
            encodingName = "utf-16";
            offset = 2;
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
            encodingName = "utf-16BE";
            offset = 2;
        }
        else
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            encodingName = "utf-8";
        }

        try
        {
            return new DecodedText(encoding.GetString(bytes, offset, bytes.Length - offset), encodingName, false);
        }
        catch (DecoderFallbackException)
        {
            var fallback = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
            return new DecodedText(fallback.GetString(bytes), encodingName, true);
        }
    }

    public static IReadOnlyList<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.EndsWith('\n'))
        {
            normalized = normalized[..^1];
        }

        return normalized.Split('\n').ToList().AsReadOnly();
    }

    public static string BuildSourcePath(string prefix, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return string.IsNullOrEmpty(prefix) ? normalized : $"{prefix}/{normalized}";
    }

    public static string BuildElementPath(System.Xml.Linq.XElement element)
    {
        return string.Join(
            "/",
            element.AncestorsAndSelf()
                .Reverse()
                .Select(item => item.Name.LocalName));
    }

    public static int GetLineNumber(System.Xml.Linq.XElement element)
    {
        return element is System.Xml.IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? lineInfo.LineNumber
            : 0;
    }

    public static int GetColumnNumber(System.Xml.Linq.XElement element)
    {
        return element is System.Xml.IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? lineInfo.LinePosition
            : 0;
    }
}
