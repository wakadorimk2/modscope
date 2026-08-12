using System.Xml.Linq;

namespace ModScope.LocalKnowledge;

internal sealed record FileInventoryItem(
    string FullPath,
    string RelativePath,
    long Size,
    string Sha256,
    SourceReference Source);

internal sealed record ParsedModData(
    ModInfoMetadata? ModInfo,
    IReadOnlyList<XmlFileReference> XmlFiles,
    IReadOnlyList<Diagnostic> Diagnostics);

internal static class SevenDaysToDieParsing
{
    public static ParsedModData Parse(
        string directoryName,
        IReadOnlyList<FileInventoryItem> files,
        IReadOnlyDictionary<string, byte[]>? xmlContents = null)
    {
        var diagnostics = new List<Diagnostic>();
        var modDirectorySource = new SourceReference(
            SourceReferenceKind.ModDirectory,
            ParsingUtilities.BuildSourcePath("mods", directoryName));

        var modInfoFile = files.FirstOrDefault(file =>
            file.RelativePath.Equals("ModInfo.xml", StringComparison.OrdinalIgnoreCase));

        ModInfoMetadata? modInfo;
        if (modInfoFile is null)
        {
            var missingDiagnostic = new Diagnostic(
                "mod.root.not_found",
                DiagnosticSeverity.Warning,
                "The resolved 7DTD MOD root does not contain a root ModInfo.xml file.",
                modDirectorySource);
            diagnostics.Add(missingDiagnostic);
            modInfo = null;
        }
        else
        {
            modInfo = ParseModInfo(modInfoFile, diagnostics, xmlContents);
        }

        var xmlFiles = new List<XmlFileReference>();
        foreach (var file in files.Where(IsConfigXml))
        {
            xmlFiles.Add(ParseConfigXml(file, diagnostics, xmlContents));
        }

        return new ParsedModData(
            modInfo,
            xmlFiles.AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    public static bool IsConfigXml(FileInventoryItem file)
    {
        return file.RelativePath.StartsWith("Config/", StringComparison.OrdinalIgnoreCase)
            && file.RelativePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static ModInfoMetadata ParseModInfo(
        FileInventoryItem file,
        List<Diagnostic> aggregateDiagnostics,
        IReadOnlyDictionary<string, byte[]>? xmlContents)
    {
        var diagnostics = new List<Diagnostic>();
        byte[] bytes;

        try
        {
            bytes = xmlContents is not null && xmlContents.TryGetValue(file.FullPath, out var cachedBytes)
                ? cachedBytes
                : File.ReadAllBytes(file.FullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var diagnostic = new Diagnostic(
                "modinfo.read.failed",
                DiagnosticSeverity.Error,
                $"The ModInfo.xml file could not be read: {exception.Message}",
                file.Source);
            diagnostics.Add(diagnostic);
            aggregateDiagnostics.Add(diagnostic);

            return new ModInfoMetadata(
                file.RelativePath,
                XmlParseStatus.EncodingError,
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<RawXmlObservation>(),
                diagnostics.AsReadOnly(),
                file.Source);
        }

        var parsed = XmlParsing.Parse(bytes, file.Source, collectAllObservations: true);
        diagnostics.AddRange(parsed.Diagnostics);
        aggregateDiagnostics.AddRange(parsed.Diagnostics);

        if (parsed.Document?.Root is null)
        {
            return new ModInfoMetadata(
                file.RelativePath,
                parsed.Status,
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<RawXmlObservation>(),
                diagnostics.AsReadOnly(),
                file.Source)
            {
                RawObservations = parsed.Observations
            };
        }

        if (!parsed.Document.Root.Name.LocalName.Equals("xml", StringComparison.OrdinalIgnoreCase))
        {
            var diagnostic = new Diagnostic(
                "modinfo.root.unexpected",
                DiagnosticSeverity.Warning,
                $"The ModInfo.xml root element is '{parsed.Document.Root.Name.LocalName}', not 'xml'.",
                file.Source);
            diagnostics.Add(diagnostic);
            aggregateDiagnostics.Add(diagnostic);
        }

        var unknown = XmlParsing.CollectUnknownModInfoObservations(parsed.Document, file.Source);
        AddDuplicateModInfoDiagnostics(parsed.Document.Root, file.Source, diagnostics, aggregateDiagnostics);

        return new ModInfoMetadata(
            file.RelativePath,
            parsed.Status,
            XmlParsing.GetModInfoValue(parsed.Document.Root, "Name"),
            XmlParsing.GetModInfoValue(parsed.Document.Root, "DisplayName"),
            XmlParsing.GetModInfoValue(parsed.Document.Root, "Version"),
            XmlParsing.GetModInfoValue(parsed.Document.Root, "Description"),
            XmlParsing.GetModInfoValue(parsed.Document.Root, "Author"),
            XmlParsing.GetModInfoValue(parsed.Document.Root, "Website"),
            unknown,
            diagnostics.AsReadOnly(),
            file.Source)
        {
            RawObservations = parsed.Observations
        };
    }

    private static XmlFileReference ParseConfigXml(
        FileInventoryItem file,
        List<Diagnostic> aggregateDiagnostics,
        IReadOnlyDictionary<string, byte[]>? xmlContents)
    {
        byte[] bytes;
        try
        {
            bytes = xmlContents is not null && xmlContents.TryGetValue(file.FullPath, out var cachedBytes)
                ? cachedBytes
                : File.ReadAllBytes(file.FullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var diagnostic = new Diagnostic(
                "config.read.failed",
                DiagnosticSeverity.Error,
                $"The Config XML file could not be read: {exception.Message}",
                file.Source);
            aggregateDiagnostics.Add(diagnostic);
            return new XmlFileReference(
                file.RelativePath,
                XmlParseStatus.EncodingError,
                null,
                null,
                0,
                0,
                Array.Empty<XmlXPathCandidate>(),
                Array.Empty<RawXmlObservation>(),
                new[] { diagnostic },
                file.Source);
        }

        var parsed = XmlParsing.Parse(bytes, file.Source, collectAllObservations: true);
        var operationDiagnostics = parsed.PatchOperations
            .SelectMany(operation => operation.Diagnostics)
            .ToList();
        var diagnostics = parsed.Diagnostics
            .Concat(operationDiagnostics)
            .ToList()
            .AsReadOnly();
        aggregateDiagnostics.AddRange(diagnostics);

        var inferredTarget = new XmlReferenceCandidate(
            file.RelativePath,
            NormalizeInferredTarget(file.RelativePath),
            string.Empty,
            EvidenceKind.Inference,
            file.Source);
        var patchOperations = parsed.PatchOperations
            .Select(operation => operation with
            {
                TargetXmlCandidates = operation.TargetXmlCandidates
                    .Concat(new[] { inferredTarget })
                    .ToList()
                    .AsReadOnly()
            })
            .ToList()
            .AsReadOnly();

        return new XmlFileReference(
            file.RelativePath,
            parsed.Status,
            parsed.EncodingName,
            parsed.RootElementName,
            parsed.ElementCount,
            parsed.AttributeCount,
            parsed.XPathCandidates,
            parsed.Observations,
            diagnostics,
            file.Source)
        {
            PatchOperations = patchOperations
        };
    }

    private static void AddDuplicateModInfoDiagnostics(
        XElement root,
        SourceReference source,
        ICollection<Diagnostic> diagnostics,
        ICollection<Diagnostic> aggregateDiagnostics)
    {
        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name",
            "DisplayName",
            "Version",
            "Description",
            "Author",
            "Website"
        };

        foreach (var group in root.Elements()
                     .Where(element => knownNames.Contains(element.Name.LocalName))
                     .GroupBy(element => element.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (var duplicate in group.Skip(1))
            {
                var duplicateSource = source with
                {
                    LineNumber = ParsingUtilities.GetLineNumber(duplicate),
                    ColumnNumber = ParsingUtilities.GetColumnNumber(duplicate)
                };
                var diagnostic = new Diagnostic(
                    "modinfo.duplicate_field",
                    DiagnosticSeverity.Warning,
                    $"The ModInfo.xml field '{group.Key}' appears more than once. The first value is used for the normalized field.",
                    duplicateSource,
                    group.Key);
                diagnostics.Add(diagnostic);
                aggregateDiagnostics.Add(diagnostic);
            }
        }
    }

    private static string? NormalizeInferredTarget(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        return normalized.StartsWith("Config/", StringComparison.OrdinalIgnoreCase)
            ? normalized["Config/".Length..]
            : normalized.Length == 0 ? null : normalized;
    }
}
