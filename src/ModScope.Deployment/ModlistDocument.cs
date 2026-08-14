using System.Security.Cryptography;
using System.Text;
using ModScope.LocalKnowledge;

namespace ModScope.Deployment;

public sealed record ModlistLine(
    int Index,
    string RawText,
    string? ModKey,
    bool? IsEnabled,
    bool IsSeparator,
    bool IsEditable);

public sealed class ModlistDocument
{
    private readonly Encoding _encoding;
    private readonly byte[] _preamble;
    private readonly string _newLine;
    private readonly bool _hasTrailingNewLine;

    private ModlistDocument(
        IReadOnlyList<ModlistLine> lines,
        Encoding encoding,
        byte[] preamble,
        string newLine,
        bool hasTrailingNewLine,
        string sha256)
    {
        Lines = lines;
        _encoding = encoding;
        _preamble = preamble;
        _newLine = newLine;
        _hasTrailingNewLine = hasTrailingNewLine;
        Sha256 = sha256;
    }

    public IReadOnlyList<ModlistLine> Lines { get; }

    public string Sha256 { get; }

    public IReadOnlyList<ModlistLine> EditableLines =>
        Lines.Where(line => line.IsEditable).ToList().AsReadOnly();

    public static ModlistDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Read(File.ReadAllBytes(path));
    }

    public static ModlistDocument Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var (text, encoding, preamble, encodingOffset) = Decode(bytes);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : text.Contains('\r')
                ? "\r"
                : "\n";
        var hasTrailingNewLine = text.EndsWith(newLine, StringComparison.Ordinal);
        var lines = SplitLines(text, newLine, hasTrailingNewLine)
            .Select((line, index) => CreateLine(index, line))
            .ToList()
            .AsReadOnly();

        return new ModlistDocument(
            lines,
            encoding,
            preamble,
            newLine,
            hasTrailingNewLine,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    public byte[] Rewrite(IReadOnlyList<DeploymentEntryDraft> draftEntries)
    {
        ArgumentNullException.ThrowIfNull(draftEntries);

        var slots = EditableLines;
        var orderedEntries = draftEntries
            .OrderBy(entry => entry.Order)
            .ToList();
        var duplicateDraftKeys = orderedEntries
            .GroupBy(entry => entry.ModKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDraftKeys is not null)
        {
            throw new InvalidOperationException(
                $"The deployment draft contains the MOD '{duplicateDraftKeys.Key}' more than once.");
        }

        if (orderedEntries.Count != slots.Count)
        {
            throw new InvalidOperationException(
                "The deployment draft does not contain exactly one entry for each editable modlist line.");
        }

        var slotByKey = slots.ToDictionary(
            line => line.ModKey!,
            line => line,
            StringComparer.OrdinalIgnoreCase);
        if (orderedEntries.Any(entry => !slotByKey.ContainsKey(entry.ModKey)))
        {
            throw new InvalidOperationException(
                "The deployment draft contains a MOD that is not present in modlist.txt.");
        }

        var expectedKeys = slots
            .Select(line => line.ModKey!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (orderedEntries.Any(entry => !expectedKeys.Contains(entry.ModKey)))
        {
            throw new InvalidOperationException(
                "The deployment draft does not contain every editable MOD in modlist.txt.");
        }

        var output = Lines.Select(line => line.RawText).ToArray();
        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var visibleEntry = orderedEntries[orderedEntries.Count - 1 - slotIndex];
            var sourceLine = slotByKey[visibleEntry.ModKey];
            output[slots[slotIndex].Index] = WithEnabledState(
                sourceLine.RawText,
                visibleEntry.Enabled);
        }

        var rendered = string.Join(_newLine, output);
        if (_hasTrailingNewLine)
        {
            rendered += _newLine;
        }

        var content = _encoding.GetBytes(rendered);
        if (_preamble.Length == 0)
        {
            return content;
        }

        var result = new byte[_preamble.Length + content.Length];
        Buffer.BlockCopy(_preamble, 0, result, 0, _preamble.Length);
        Buffer.BlockCopy(content, 0, result, _preamble.Length, content.Length);
        return result;
    }

    private static ModlistLine CreateLine(int index, string rawText)
    {
        var trimmed = rawText.Trim();
        if (trimmed.Length <= 1 || (trimmed[0] != '+' && trimmed[0] != '-'))
        {
            return new ModlistLine(index, rawText, null, null, false, false);
        }

        var modKey = trimmed[1..].Trim();
        if (modKey.Length == 0)
        {
            return new ModlistLine(index, rawText, null, null, false, false);
        }

        var isSeparator = modKey.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);
        return new ModlistLine(
            index,
            rawText,
            modKey,
            trimmed[0] == '+',
            isSeparator,
            !isSeparator);
    }

    private static string WithEnabledState(string rawText, bool enabled)
    {
        var firstContentIndex = 0;
        while (firstContentIndex < rawText.Length
            && char.IsWhiteSpace(rawText[firstContentIndex]))
        {
            firstContentIndex++;
        }

        if (firstContentIndex >= rawText.Length)
        {
            return rawText;
        }

        return rawText[..firstContentIndex]
            + (enabled ? '+' : '-')
            + rawText[(firstContentIndex + 1)..];
    }

    private static IReadOnlyList<string> SplitLines(
        string text,
        string newLine,
        bool hasTrailingNewLine)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        if (hasTrailingNewLine && lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines.AsReadOnly();
    }

    private static (string Text, Encoding Encoding, byte[] Preamble, int Offset) Decode(
        byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return (
                new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3),
                new UTF8Encoding(false, true),
                new byte[] { 0xEF, 0xBB, 0xBF },
                3);
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return (
                new UnicodeEncoding(false, false, true).GetString(bytes, 2, bytes.Length - 2),
                new UnicodeEncoding(false, false, true),
                new byte[] { 0xFF, 0xFE },
                2);
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return (
                new UnicodeEncoding(true, false, true).GetString(bytes, 2, bytes.Length - 2),
                new UnicodeEncoding(true, false, true),
                new byte[] { 0xFE, 0xFF },
                2);
        }

        var encoding = new UTF8Encoding(false, true);
        return (encoding.GetString(bytes), encoding, Array.Empty<byte>(), 0);
    }
}
