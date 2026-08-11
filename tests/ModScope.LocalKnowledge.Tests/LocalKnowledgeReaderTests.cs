using System.Security.Cryptography;
using ModScope.LocalKnowledge;
using Xunit;

namespace ModScope.LocalKnowledge.Tests;

public sealed class LocalKnowledgeReaderTests
{
    [Fact]
    public void ReadsProfileEntriesAndPreservesRawLines()
    {
        var snapshot = ReadFixture();

        Assert.Equal(7, snapshot.ProfileEntries.Count);

        var alphaEntries = snapshot.ProfileEntries
            .Where(entry => entry.NormalizedModName == "Alpha Mod")
            .ToList();
        Assert.Equal(2, alphaEntries.Count);
        Assert.Equal(" +Alpha Mod", alphaEntries[0].RawLine);
        Assert.Equal(ModEnabledState.Enabled, alphaEntries[0].EnabledState);
        Assert.Equal(0, alphaEntries[0].Priority);
        Assert.Equal(SourceReferenceKind.ProfileFile, alphaEntries[0].PriorityEvidence.Source.Kind);
        Assert.Equal(3, alphaEntries[1].Priority);
        Assert.Contains(alphaEntries[1].Diagnostics, diagnostic => diagnostic.Code == "profile.mod.duplicate");

        var beta = Assert.Single(snapshot.ProfileEntries, entry => entry.NormalizedModName == "Beta Mod");
        Assert.Equal(ModEnabledState.Disabled, beta.EnabledState);
        Assert.Equal(1, beta.Priority);

        var malformed = Assert.Single(
            snapshot.ProfileEntries,
            entry => entry.RawLine.Contains("not a supported", StringComparison.Ordinal));
        Assert.Equal(ModEnabledState.Unknown, malformed.EnabledState);
        Assert.Contains(malformed.Diagnostics, diagnostic => diagnostic.Code == "profile.line.unrecognized");
    }

    [Fact]
    public void BuildsListedUnresolvedAndUnlistedRecords()
    {
        var snapshot = ReadFixture();

        var alpha = GetMod(snapshot, "Alpha Mod");
        Assert.Equal(ModProfileState.Listed, alpha.ProfileState);
        Assert.Equal(ModEnabledState.Enabled, alpha.EnabledState);
        Assert.Equal(0, alpha.Priority);

        var beta = GetMod(snapshot, "Beta Mod");
        Assert.Equal(ModProfileState.Listed, beta.ProfileState);
        Assert.Equal(ModEnabledState.Disabled, beta.EnabledState);
        Assert.Equal(1, beta.Priority);

        var missing = GetMod(snapshot, "Missing Mod");
        Assert.Equal(ModProfileState.Unresolved, missing.ProfileState);
        Assert.Equal(ModEnabledState.Enabled, missing.EnabledState);
        Assert.Null(missing.ResolvedDirectoryRelativePath);
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Code == "mod.unresolved");

        var unlisted = GetMod(snapshot, "Unlisted Mod");
        Assert.Equal(ModProfileState.Unlisted, unlisted.ProfileState);
        Assert.Equal(ModEnabledState.Unknown, unlisted.EnabledState);
        Assert.Null(unlisted.Priority);

        var noInfo = GetMod(snapshot, "No ModInfo");
        Assert.Equal(ModProfileState.Unlisted, noInfo.ProfileState);
        Assert.Null(noInfo.ModInfo);
        Assert.Contains(noInfo.Diagnostics, diagnostic => diagnostic.Code == "modinfo.missing");
    }

    [Fact]
    public void ReadsKnownMetadataAndPreservesUnknownXml()
    {
        var alpha = GetMod(ReadFixture(), "Alpha Mod");

        Assert.NotNull(alpha.ModInfo);
        Assert.Equal("Alpha Mod", alpha.ModInfo!.Name);
        Assert.Equal("Alpha Display", alpha.ModInfo.DisplayName);
        Assert.Equal("1.2.3", alpha.ModInfo.Version);
        Assert.Equal("Synthetic alpha mod.", alpha.ModInfo.Description);
        Assert.Equal("Fixture Author", alpha.ModInfo.Author);
        Assert.Equal("https://example.test/alpha", alpha.ModInfo.Website);
        Assert.Contains(alpha.ModInfo.UnknownObservations, observation => observation.ElementName == "xml");
        Assert.Contains(alpha.ModInfo.UnknownObservations, observation => observation.ElementName == "UnknownElement");
    }

    [Fact]
    public void IndexesFilesAndConfigXmlWithoutInterpretingPatchSemantics()
    {
        var alpha = GetMod(ReadFixture(), "Alpha Mod");

        Assert.Equal(
            new[] { "Config/Sub/recipes.xml", "Config/items.xml", "ModInfo.xml", "readme.txt" },
            alpha.Files.Select(file => file.RelativePath));
        Assert.All(alpha.Files, file => Assert.Matches("^[0-9a-f]{64}$", file.Sha256));

        var items = Assert.Single(alpha.XmlFiles, file => file.RelativePath == "Config/items.xml");
        Assert.Equal(XmlParseStatus.Parsed, items.ParseStatus);
        Assert.Equal("configs", items.RootElementName);
        Assert.Contains(items.XPathCandidates, candidate => candidate.RawValue == "/items/item[@name='Alpha']");
        Assert.Contains(items.RawObservations, observation => observation.ElementName == "item");
    }

    [Fact]
    public void KeepsMalformedAndDtdDiagnosticsPerFile()
    {
        var snapshot = ReadFixture();
        var beta = GetMod(snapshot, "Beta Mod");
        var noInfo = GetMod(snapshot, "No ModInfo");

        var malformed = Assert.Single(beta.XmlFiles, file => file.RelativePath == "Config/malformed.xml");
        Assert.Equal(XmlParseStatus.Malformed, malformed.ParseStatus);
        Assert.Contains(malformed.Diagnostics, diagnostic => diagnostic.Code == "xml.malformed");

        var dtd = Assert.Single(noInfo.XmlFiles, file => file.RelativePath == "Config/dtd.xml");
        Assert.Equal(XmlParseStatus.DtdBlocked, dtd.ParseStatus);
        Assert.Contains(dtd.Diagnostics, diagnostic => diagnostic.Code == "xml.dtd.blocked");
    }

    [Fact]
    public void SnapshotIdIsStableForTheSameInput()
    {
        var first = ReadFixture();
        var second = ReadFixture();

        Assert.Equal(first.SnapshotId, second.SnapshotId);
        Assert.Equal(first.InputManifest.Files, second.InputManifest.Files);
        Assert.Equal(ParserMetadata.ParserVersion, first.ParserVersion);
        Assert.Equal(ParserMetadata.SchemaVersion, first.SchemaVersion);
    }

    [Fact]
    public void SnapshotIdDoesNotDependOnAbsoluteSourcePaths()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"modscope-fixture-{Guid.NewGuid():N}");
        CopyDirectory(FixtureRoot, temporaryRoot);

        try
        {
            var relocated = new Mo2SnapshotReader().Read(CreateSource(temporaryRoot));
            Assert.Equal(ReadFixture().SnapshotId, relocated.SnapshotId);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Fact]
    public void ReaderDoesNotModifyFixtureFiles()
    {
        var before = CaptureFixtureState();

        _ = ReadFixture();

        var after = CaptureFixtureState();
        Assert.Equal(before, after);
    }

    [Fact]
    public void RejectsRelativeSourcePaths()
    {
        var reader = new Mo2SnapshotReader();
        var source = new Mo2SourceDefinition("fixture", "default", ".", ".", ".");

        Assert.Throws<ArgumentException>(() => reader.Read(source));
    }

    private static LocalModSnapshot ReadFixture()
    {
        return new Mo2SnapshotReader().Read(CreateSource(FixtureRoot));
    }

    private static Mo2SourceDefinition CreateSource(string root)
    {
        return new Mo2SourceDefinition(
            "synthetic-instance",
            "default",
            root,
            Path.Combine(root, "profile"),
            Path.Combine(root, "mods"));
    }

    private static LocalModRecord GetMod(LocalModSnapshot snapshot, string directoryName)
    {
        return Assert.Single(snapshot.Mods, mod => mod.DirectoryName == directoryName);
    }

    private static Dictionary<string, FixtureFileState> CaptureFixtureState()
    {
        return Directory.EnumerateFiles(FixtureRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(FixtureRoot, path),
                path => new FixtureFileState(
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    File.GetLastWriteTimeUtc(path)));
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath);
        }
    }

    private static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "7dtd-mo2-minimal");

    private sealed record FixtureFileState(string Sha256, DateTime LastWriteTimeUtc);
}
