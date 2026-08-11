using System.Xml;
using System.Xml.Linq;

namespace ModScope.LocalKnowledge;

internal sealed record ParsedXmlDocument(
    XmlParseStatus Status,
    string? EncodingName,
    string? RootElementName,
    int ElementCount,
    int AttributeCount,
    IReadOnlyList<XmlXPathCandidate> XPathCandidates,
    IReadOnlyList<RawXmlObservation> Observations,
    IReadOnlyList<Diagnostic> Diagnostics,
    XDocument? Document);

internal static class XmlParsing
{
    public static ParsedXmlDocument Parse(
        byte[] bytes,
        SourceReference source,
        bool collectAllObservations)
    {
        var decoded = ParsingUtilities.DecodeText(bytes);
        var diagnostics = new List<Diagnostic>();

        if (decoded.HadDecodingError)
        {
            diagnostics.Add(new Diagnostic(
                "xml.encoding.invalid",
                DiagnosticSeverity.Error,
                "The XML file contains bytes that are not valid for the detected encoding.",
                source));

            return new ParsedXmlDocument(
                XmlParseStatus.EncodingError,
                decoded.EncodingName,
                null,
                0,
                0,
                Array.Empty<XmlXPathCandidate>(),
                Array.Empty<RawXmlObservation>(),
                diagnostics.AsReadOnly(),
                null);
        }

        if (decoded.Text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new Diagnostic(
                "xml.dtd.blocked",
                DiagnosticSeverity.Error,
                "DTD and external entity declarations are not accepted.",
                source));

            return new ParsedXmlDocument(
                XmlParseStatus.DtdBlocked,
                decoded.EncodingName,
                null,
                0,
                0,
                Array.Empty<XmlXPathCandidate>(),
                Array.Empty<RawXmlObservation>(),
                diagnostics.AsReadOnly(),
                null);
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreWhitespace = false
            };

            using var reader = XmlReader.Create(stream, settings);
            var document = XDocument.Load(
                reader,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

            if (document.Root is null)
            {
                diagnostics.Add(new Diagnostic(
                    "xml.root.missing",
                    DiagnosticSeverity.Error,
                    "The XML document has no root element.",
                    source));

                return new ParsedXmlDocument(
                    XmlParseStatus.Malformed,
                    document.Declaration?.Encoding ?? decoded.EncodingName,
                    null,
                    0,
                    0,
                    Array.Empty<XmlXPathCandidate>(),
                    Array.Empty<RawXmlObservation>(),
                    diagnostics.AsReadOnly(),
                    document);
            }

            var elements = document.Root.DescendantsAndSelf().ToList();
            var attributes = elements.SelectMany(element => element.Attributes()).ToList();
            var xpathCandidates = new List<XmlXPathCandidate>();
            var observations = new List<RawXmlObservation>();

            foreach (var element in elements)
            {
                var elementPath = ParsingUtilities.BuildElementPath(element);
                var elementSource = source with
                {
                    LineNumber = ParsingUtilities.GetLineNumber(element),
                    ColumnNumber = ParsingUtilities.GetColumnNumber(element)
                };

                var elementAttributes = element.Attributes()
                    .Select(attribute => new XmlAttributeObservation(attribute.Name.LocalName, attribute.Value))
                    .ToList()
                    .AsReadOnly();

                if (collectAllObservations && (elementAttributes.Count > 0 || !string.IsNullOrWhiteSpace(element.Value)))
                {
                    observations.Add(new RawXmlObservation(
                        elementPath,
                        element.Name.LocalName,
                        elementAttributes,
                        string.IsNullOrWhiteSpace(element.Value) ? null : element.Value,
                        elementSource));
                }

                foreach (var attribute in element.Attributes())
                {
                    if (attribute.Name.LocalName.Equals("xpath", StringComparison.OrdinalIgnoreCase))
                    {
                        xpathCandidates.Add(new XmlXPathCandidate(attribute.Value, elementPath, elementSource));
                    }
                }

                if (element.Name.LocalName.Equals("xpath", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(element.Value))
                {
                    xpathCandidates.Add(new XmlXPathCandidate(element.Value, elementPath, elementSource));
                }
            }

            return new ParsedXmlDocument(
                XmlParseStatus.Parsed,
                document.Declaration?.Encoding ?? decoded.EncodingName,
                document.Root.Name.LocalName,
                elements.Count,
                attributes.Count,
                xpathCandidates.AsReadOnly(),
                observations.AsReadOnly(),
                diagnostics.AsReadOnly(),
                document);
        }
        catch (XmlException exception)
        {
            diagnostics.Add(new Diagnostic(
                "xml.malformed",
                DiagnosticSeverity.Error,
                $"The XML file could not be parsed: {exception.Message}",
                source));

            return new ParsedXmlDocument(
                XmlParseStatus.Malformed,
                decoded.EncodingName,
                null,
                0,
                0,
                Array.Empty<XmlXPathCandidate>(),
                Array.Empty<RawXmlObservation>(),
                diagnostics.AsReadOnly(),
                null);
        }
    }

    public static IReadOnlyList<RawXmlObservation> CollectUnknownModInfoObservations(
        XDocument document,
        SourceReference source)
    {
        if (document.Root is null)
        {
            return Array.Empty<RawXmlObservation>();
        }

        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name",
            "DisplayName",
            "Version",
            "Description",
            "Author",
            "Website"
        };

        var observations = new List<RawXmlObservation>();
        foreach (var element in document.Root.DescendantsAndSelf())
        {
            var isRoot = ReferenceEquals(element, document.Root);
            var isKnownDirectChild = ReferenceEquals(element.Parent, document.Root)
                && knownNames.Contains(element.Name.LocalName);
            var hasUnexpectedAttribute = element.Attributes()
                .Any(attribute => !isKnownDirectChild
                    || !attribute.Name.LocalName.Equals("value", StringComparison.OrdinalIgnoreCase));

            if (!isRoot && isKnownDirectChild && !hasUnexpectedAttribute)
            {
                continue;
            }

            var elementSource = source with
            {
                LineNumber = ParsingUtilities.GetLineNumber(element),
                ColumnNumber = ParsingUtilities.GetColumnNumber(element)
            };

            observations.Add(new RawXmlObservation(
                ParsingUtilities.BuildElementPath(element),
                element.Name.LocalName,
                element.Attributes()
                    .Select(attribute => new XmlAttributeObservation(attribute.Name.LocalName, attribute.Value))
                    .ToList()
                    .AsReadOnly(),
                string.IsNullOrWhiteSpace(element.Value) ? null : element.Value,
                elementSource));
        }

        return observations.AsReadOnly();
    }

    public static string? GetModInfoValue(XElement root, string name)
    {
        var element = root.Elements()
            .FirstOrDefault(item => item.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));

        return element?.Attribute("value")?.Value ?? element?.Value.Trim();
    }
}
