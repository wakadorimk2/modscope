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
    IReadOnlyList<XmlPatchOperationObservation> PatchOperations,
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
                Array.Empty<XmlPatchOperationObservation>(),
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
                Array.Empty<XmlPatchOperationObservation>(),
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
                    Array.Empty<XmlPatchOperationObservation>(),
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

            var patchOperations = collectAllObservations
                ? ExtractPatchOperations(document, source, xpathCandidates, observations)
                : Array.Empty<XmlPatchOperationObservation>();

            return new ParsedXmlDocument(
                XmlParseStatus.Parsed,
                document.Declaration?.Encoding ?? decoded.EncodingName,
                document.Root.Name.LocalName,
                elements.Count,
                attributes.Count,
                xpathCandidates.AsReadOnly(),
                observations.AsReadOnly(),
                diagnostics.AsReadOnly(),
                patchOperations,
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
                Array.Empty<XmlPatchOperationObservation>(),
                null);
        }
    }

    private static IReadOnlyList<XmlPatchOperationObservation> ExtractPatchOperations(
        XDocument document,
        SourceReference source,
        IReadOnlyList<XmlXPathCandidate> xpathCandidates,
        IReadOnlyList<RawXmlObservation> observations)
    {
        if (document.Root is null)
        {
            return Array.Empty<XmlPatchOperationObservation>();
        }

        var operations = new List<XmlPatchOperationObservation>();
        foreach (var element in document.Root.DescendantsAndSelf())
        {
            var elementPath = ParsingUtilities.BuildElementPath(element);
            var elementSource = source with
            {
                LineNumber = ParsingUtilities.GetLineNumber(element),
                ColumnNumber = ParsingUtilities.GetColumnNumber(element)
            };
            var rawOperationName = element.Name.LocalName;
            var normalizedKind = TryGetOperationKind(rawOperationName);
            var hasXPathAttribute = element.Attributes()
                .Any(attribute => attribute.Name.LocalName.Equals("xpath", StringComparison.OrdinalIgnoreCase));

            if (normalizedKind is null && !hasXPathAttribute)
            {
                continue;
            }

            var operationDiagnostics = new List<Diagnostic>();
            if (normalizedKind is null)
            {
                operationDiagnostics.Add(new Diagnostic(
                    "xml.patch.operation.unknown",
                    DiagnosticSeverity.Warning,
                    $"The XML element '{rawOperationName}' is not in the supported patch operation vocabulary.",
                    elementSource,
                    rawOperationName));
            }
            else if (!hasXPathAttribute)
            {
                operationDiagnostics.Add(new Diagnostic(
                    "xml.patch.xpath.missing",
                    DiagnosticSeverity.Warning,
                    $"The patch operation '{rawOperationName}' does not contain an xpath attribute.",
                    elementSource,
                    rawOperationName));
            }

            var rawObservation = observations.FirstOrDefault(observation =>
                observation.ElementPath.Equals(elementPath, StringComparison.Ordinal)
                && observation.Source.RelativePath.Equals(source.RelativePath, StringComparison.Ordinal))
                ?? CreateRawObservation(element, elementPath, elementSource);

            var operationXpaths = xpathCandidates
                .Where(candidate => candidate.ElementPath.Equals(elementPath, StringComparison.Ordinal))
                .ToList()
                .AsReadOnly();

            var targets = ExtractAttributeCandidates(
                element,
                new[] { "target", "targetXml", "targetFile", "file" },
                elementPath,
                elementSource,
                NormalizeXmlTarget);
            var entities = ExtractFieldCandidates(element, "entity", elementPath, elementSource);
            var properties = ExtractFieldCandidates(element, "property", elementPath, elementSource);
            var attributes = ExtractFieldCandidates(element, "attribute", elementPath, elementSource)
                .Concat(ExtractAttributeNameCandidates(element, normalizedKind, elementPath, elementSource))
                .Concat(ExtractXPathAttributeCandidates(operationXpaths))
                .ToList()
                .AsReadOnly();

            operations.Add(new XmlPatchOperationObservation(
                elementPath,
                rawOperationName,
                normalizedKind,
                rawObservation,
                operationXpaths,
                DeduplicateCandidates(targets),
                DeduplicateCandidates(entities),
                DeduplicateCandidates(properties),
                DeduplicateCandidates(attributes),
                operationDiagnostics.AsReadOnly(),
                elementSource));
        }

        return operations.AsReadOnly();
    }

    private static XmlPatchOperationKind? TryGetOperationKind(string operationName)
    {
        return operationName.ToLowerInvariant() switch
        {
            "set" => XmlPatchOperationKind.Set,
            "setattribute" => XmlPatchOperationKind.SetAttribute,
            "remove" => XmlPatchOperationKind.Remove,
            "removeattribute" => XmlPatchOperationKind.RemoveAttribute,
            "append" => XmlPatchOperationKind.Append,
            "prepend" => XmlPatchOperationKind.Prepend,
            "insertbefore" => XmlPatchOperationKind.InsertBefore,
            "insertafter" => XmlPatchOperationKind.InsertAfter,
            _ => null
        };
    }

    private static IReadOnlyList<XmlReferenceCandidate> ExtractAttributeCandidates(
        XElement element,
        IReadOnlyCollection<string> attributeNames,
        string elementPath,
        SourceReference source,
        Func<string, string?> normalizer)
    {
        return element.Attributes()
            .Where(attribute => attributeNames.Any(name =>
                name.Equals(attribute.Name.LocalName, StringComparison.OrdinalIgnoreCase)))
            .Select(attribute => CreateCandidate(attribute.Value, elementPath, source, normalizer, EvidenceKind.Normalized))
            .Where(candidate => candidate is not null)
            .Cast<XmlReferenceCandidate>()
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<XmlReferenceCandidate> ExtractFieldCandidates(
        XElement operation,
        string fieldName,
        string elementPath,
        SourceReference source)
    {
        var candidates = new List<XmlReferenceCandidate>();
        foreach (var element in operation.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(attribute =>
                         attribute.Name.LocalName.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                AddCandidate(candidates, attribute.Value, elementPath, source, EvidenceKind.Normalized);
            }

            if (!element.Name.LocalName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var namedValue = element.Attribute("name")?.Value;
            var textValue = element.Elements().Any() ? null : element.Value;
            AddCandidate(candidates, namedValue ?? textValue, elementPath, source, EvidenceKind.Normalized);
        }

        return DeduplicateCandidates(candidates);
    }

    private static IReadOnlyList<XmlReferenceCandidate> ExtractAttributeNameCandidates(
        XElement operation,
        XmlPatchOperationKind? operationKind,
        string elementPath,
        SourceReference source)
    {
        if (operationKind is not (XmlPatchOperationKind.SetAttribute or XmlPatchOperationKind.RemoveAttribute))
        {
            return Array.Empty<XmlReferenceCandidate>();
        }

        var attributeName = operation.Attribute("name")?.Value;
        return string.IsNullOrWhiteSpace(attributeName)
            ? Array.Empty<XmlReferenceCandidate>()
            : new[] { CreateCandidate(attributeName, elementPath, source, NormalizeXmlReference, EvidenceKind.Normalized)! };
    }

    private static IReadOnlyList<XmlReferenceCandidate> ExtractXPathAttributeCandidates(
        IReadOnlyList<XmlXPathCandidate> xpathCandidates)
    {
        var candidates = new List<XmlReferenceCandidate>();
        foreach (var xpath in xpathCandidates)
        {
            var normalized = xpath.NormalizedValue;
            if (normalized is null)
            {
                continue;
            }

            var marker = normalized.LastIndexOf("/@", StringComparison.Ordinal);
            var value = marker >= 0
                ? normalized[(marker + 2)..]
                : normalized.StartsWith("@", StringComparison.Ordinal)
                    ? normalized[1..]
                    : null;

            if (string.IsNullOrWhiteSpace(value) || value.Contains('/', StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add(new XmlReferenceCandidate(
                value,
                value,
                xpath.ElementPath,
                EvidenceKind.Normalized,
                xpath.Source));
        }

        return DeduplicateCandidates(candidates);
    }

    private static XmlReferenceCandidate? CreateCandidate(
        string? rawValue,
        string elementPath,
        SourceReference source,
        Func<string, string?> normalizer,
        EvidenceKind evidenceKind)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return new XmlReferenceCandidate(
            rawValue,
            normalizer(rawValue),
            elementPath,
            evidenceKind,
            source);
    }

    private static void AddCandidate(
        ICollection<XmlReferenceCandidate> candidates,
        string? rawValue,
        string elementPath,
        SourceReference source,
        EvidenceKind evidenceKind)
    {
        var candidate = CreateCandidate(rawValue, elementPath, source, NormalizeXmlReference, evidenceKind);
        if (candidate is not null)
        {
            candidates.Add(candidate);
        }
    }

    private static IReadOnlyList<XmlReferenceCandidate> DeduplicateCandidates(
        IEnumerable<XmlReferenceCandidate> candidates)
    {
        return candidates
            .GroupBy(candidate => new
            {
                candidate.RawValue,
                candidate.NormalizedValue,
                candidate.ElementPath,
                candidate.EvidenceKind,
                candidate.Source.RelativePath,
                candidate.Source.LineNumber,
                candidate.Source.ColumnNumber
            })
            .Select(group => group.First())
            .ToList()
            .AsReadOnly();
    }

    private static string? NormalizeXmlReference(string rawValue)
    {
        var normalized = rawValue.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeXmlTarget(string rawValue)
    {
        var normalized = NormalizeXmlReference(rawValue)?.Replace('\\', '/');
        if (normalized is null)
        {
            return null;
        }

        return normalized.StartsWith("Config/", StringComparison.OrdinalIgnoreCase)
            ? normalized["Config/".Length..]
            : normalized;
    }

    private static RawXmlObservation CreateRawObservation(
        XElement element,
        string elementPath,
        SourceReference source)
    {
        return new RawXmlObservation(
            elementPath,
            element.Name.LocalName,
            element.Attributes()
                .Select(attribute => new XmlAttributeObservation(attribute.Name.LocalName, attribute.Value))
                .ToList()
                .AsReadOnly(),
            string.IsNullOrWhiteSpace(element.Value) ? null : element.Value,
            source);
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
