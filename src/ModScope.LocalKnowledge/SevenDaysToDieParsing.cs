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
        IReadOnlyList<FileInventoryItem> files)
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
                "modinfo.missing",
                DiagnosticSeverity.Warning,
                "The MOD directory does not contain a root ModInfo.xml file.",
                modDirectorySource);
            diagnostics.Add(missingDiagnostic);
            modInfo = null;
        }
        else
        {
            modInfo = ParseModInfo(modInfoFile, diagnostics);
        }

        var xmlFiles = new List<XmlFileReference>();
        foreach (var file in files.Where(IsConfigXml))
        {
            xmlFiles.Add(ParseConfigXml(file, diagnostics));
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
        List<Diagnostic> aggregateDiagnostics)
    {
        var diagnostics = new List<Diagnostic>();
        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(file.FullPath);
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

        var parsed = XmlParsing.Parse(bytes, file.Source, collectAllObservations: false);
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
                file.Source);
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
            file.Source);
    }

    private static XmlFileReference ParseConfigXml(
        FileInventoryItem file,
        List<Diagnostic> aggregateDiagnostics)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file.FullPath);
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
        aggregateDiagnostics.AddRange(parsed.Diagnostics);
        return new XmlFileReference(
            file.RelativePath,
            parsed.Status,
            parsed.EncodingName,
            parsed.RootElementName,
            parsed.ElementCount,
            parsed.AttributeCount,
            parsed.XPathCandidates,
            parsed.Observations,
            parsed.Diagnostics,
            file.Source);
    }
}
