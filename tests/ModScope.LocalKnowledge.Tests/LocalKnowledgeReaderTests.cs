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

        Assert.Equal(10, snapshot.ProfileEntries.Count);

        var alphaEntries = snapshot.ProfileEntries
            .Where(entry => entry.NormalizedModName == "Alpha Mod")
            .ToList();
        Assert.Equal(2, alphaEntries.Count);
        Assert.Equal("+Alpha Mod", alphaEntries[0].RawLine);
        Assert.Equal(ModEnabledState.Enabled, alphaEntries[0].EnabledState);
        Assert.Equal(3, alphaEntries[0].Priority);
        Assert.Equal(SourceReferenceKind.ProfileFile, alphaEntries[0].PriorityEvidence.Source.Kind);
        Assert.Equal(0, alphaEntries[1].Priority);
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
        Assert.Equal(3, alpha.Priority);

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

        Assert.DoesNotContain(snapshot.Mods, mod => mod.Mo2OuterDirectoryName == "No ModInfo");
        Assert.Contains(snapshot.Diagnostics, diagnostic =>
            diagnostic.Code == "mod.root.not_found" &&
            diagnostic.Source?.RelativePath == "mods/No ModInfo");
        Assert.Contains(snapshot.InputManifest.Files, file =>
            file.RelativePath == "mods/No ModInfo/Config/dtd.xml");
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
            new[]
            {
                "Config/Sub/recipes.xml",
                "Config/items.xml",
                "Config/operations.xml",
                "ModInfo.xml",
                "readme.txt"
            },
            alpha.Files.Select(file => file.RelativePath));
        Assert.All(alpha.Files, file => Assert.Matches("^[0-9a-f]{64}$", file.Sha256));

        var items = Assert.Single(alpha.XmlFiles, file => file.RelativePath == "Config/items.xml");
        Assert.Equal(XmlParseStatus.Parsed, items.ParseStatus);
        Assert.Equal("configs", items.RootElementName);
        Assert.Contains(items.XPathCandidates, candidate => candidate.RawValue == "/items/item[@name='Alpha']");
        Assert.Contains(items.RawObservations, observation => observation.ElementName == "item");
    }

    [Fact]
    public void ReadsTextModInfoSchemaAndPreservesDuplicateAndUnknownObservations()
    {
        var textMod = GetMod(ReadFixture(), "Text Mod");

        Assert.NotNull(textMod.ModInfo);
        Assert.Equal("Text Mod", textMod.ModInfo!.Name);
        Assert.Equal("Text Display", textMod.ModInfo.DisplayName);
        Assert.Equal("2.0", textMod.ModInfo.Version);
        Assert.Contains(textMod.ModInfo.RawObservations, observation => observation.ElementName == "Name");
        Assert.Contains(textMod.ModInfo.UnknownObservations, observation => observation.ElementName == "UnknownField");
        Assert.Contains(textMod.Diagnostics, diagnostic => diagnostic.Code == "modinfo.duplicate_field");
    }

    [Fact]
    public void ExtractsKnownAndUnknownPatchOperationsWithCandidates()
    {
        var alpha = GetMod(ReadFixture(), "Alpha Mod");
        var operationsFile = Assert.Single(alpha.XmlFiles, file => file.RelativePath == "Config/operations.xml");

        Assert.Equal(10, operationsFile.PatchOperations.Count);

        var set = Assert.Single(operationsFile.PatchOperations, operation => operation.RawOperationName == "set");
        Assert.Equal(XmlPatchOperationKind.Set, set.NormalizedKind);
        Assert.Equal(
            "/items/item[@name='Alpha']/property[@name='Health']/@value",
            Assert.Single(set.XPathCandidates).RawValue);
        var explicitTarget = Assert.Single(
            set.TargetXmlCandidates,
            candidate => candidate.EvidenceKind == EvidenceKind.Normalized);
        Assert.Equal("items.xml", explicitTarget.NormalizedValue);
        Assert.Contains(set.EntityCandidates, candidate => candidate.NormalizedValue == "item");
        Assert.Contains(set.PropertyCandidates, candidate => candidate.NormalizedValue == "Health");
        Assert.Contains(set.AttributeCandidates, candidate => candidate.NormalizedValue == "value");

        var append = Assert.Single(operationsFile.PatchOperations, operation => operation.RawOperationName == "append");
        Assert.Equal(XmlPatchOperationKind.Append, append.NormalizedKind);
        Assert.Contains(append.PropertyCandidates, candidate => candidate.NormalizedValue == "Tags");
        Assert.Contains(append.TargetXmlCandidates, candidate =>
            candidate.NormalizedValue == "operations.xml"
            && candidate.EvidenceKind == EvidenceKind.Inference);

        var unknown = Assert.Single(operationsFile.PatchOperations, operation => operation.RawOperationName == "mystery");
        Assert.Null(unknown.NormalizedKind);
        Assert.Contains(unknown.RawObservation.Attributes, attribute =>
            attribute.Name == "custom" && attribute.Value == "preserve");
        Assert.Contains(unknown.Diagnostics, diagnostic => diagnostic.Code == "xml.patch.operation.unknown");
        Assert.Contains(operationsFile.Diagnostics, diagnostic => diagnostic.Code == "xml.patch.operation.unknown");

        var csv = Assert.Single(operationsFile.PatchOperations, operation => operation.RawOperationName == "csv");
        Assert.Null(csv.NormalizedKind);
        Assert.Contains(csv.Diagnostics, diagnostic => diagnostic.RawValue == "csv");
    }

    [Fact]
    public void BuildsDeterministicForwardAndReverseKnowledgeReferences()
    {
        var first = ReadFixture();
        var second = ReadFixture();

        Assert.NotEmpty(first.Index.ForwardReferences);
        Assert.NotEmpty(first.Index.ReverseReferences);
        Assert.Equal(first.Index.ForwardReferences, second.Index.ForwardReferences);
        Assert.Equal(first.Index.ReverseReferences, second.Index.ReverseReferences);

        Assert.Contains(first.Index.ForwardReferences, reference =>
            reference.From.Kind == LocalKnowledgeNodeKind.PatchOperation
            && reference.To.Kind == LocalKnowledgeNodeKind.XPath
            && reference.To.Value == "/items/item[@name='Alpha']/property[@name='Health']/@value"
            && reference.Relation == LocalKnowledgeRelation.Selects);
        Assert.Contains(first.Index.ReverseReferences, reference =>
            reference.From.Kind == LocalKnowledgeNodeKind.XPath
            && reference.From.Value == "/items/item[@name='Alpha']/property[@name='Health']/@value"
            && reference.To.Kind == LocalKnowledgeNodeKind.PatchOperation);
        Assert.Contains(first.Index.ReverseReferences, reference =>
            reference.From.Kind == LocalKnowledgeNodeKind.TargetXml
            && reference.From.Value == "items.xml"
            && reference.To.Kind == LocalKnowledgeNodeKind.PatchOperation
            && reference.Evidence.Kind == EvidenceKind.Normalized);
        Assert.Contains(first.Index.ReverseReferences, reference =>
            reference.From.Kind == LocalKnowledgeNodeKind.TargetXml
            && reference.From.Value == "operations.xml"
            && reference.To.Kind == LocalKnowledgeNodeKind.PatchOperation
            && reference.Evidence.Kind == EvidenceKind.Inference);
    }

    [Fact]
    public void ReportsInvalidEncodingWithoutDiscardingTheDiagnostic()
    {
        var temporaryRoot = Directory.CreateTempSubdirectory("modscope-encoding-");
        try
        {
            CopyDirectory(FixtureRoot, temporaryRoot.FullName);
            var invalidPath = Path.Combine(
                temporaryRoot.FullName,
                "mods",
                "Alpha Mod",
                "Config",
                "invalid-encoding.xml");
            File.WriteAllBytes(invalidPath, new byte[] { 0xEF, 0xBB, 0xBF, 0xFF });

            var snapshot = new Mo2SnapshotReader().Read(CreateSource(temporaryRoot.FullName));
            var alpha = GetMod(snapshot, "Alpha Mod");
            var invalid = Assert.Single(alpha.XmlFiles, file => file.RelativePath == "Config/invalid-encoding.xml");

            Assert.Equal(XmlParseStatus.EncodingError, invalid.ParseStatus);
            Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "xml.encoding.invalid");
        }
        finally
        {
            Directory.Delete(temporaryRoot.FullName, recursive: true);
        }
    }

    [Fact]
    public void KeepsMalformedAndDtdDiagnosticsPerFile()
    {
        var snapshot = ReadFixture();
        var beta = GetMod(snapshot, "Beta Mod");
        var malformed = Assert.Single(beta.XmlFiles, file => file.RelativePath == "Config/malformed.xml");
        Assert.Equal(XmlParseStatus.Malformed, malformed.ParseStatus);
        Assert.Contains(malformed.Diagnostics, diagnostic => diagnostic.Code == "xml.malformed");

        var dtd = Assert.Single(beta.XmlFiles, file => file.RelativePath == "Config/dtd.xml");
        Assert.Equal(XmlParseStatus.DtdBlocked, dtd.ParseStatus);
        Assert.Contains(dtd.Diagnostics, diagnostic => diagnostic.Code == "xml.dtd.blocked");
    }

    [Fact]
    public void DiscoversInferredRootsAndPreservesOuterAndInnerSources()
    {
        var snapshot = ReadFixture();

        var wrapped = Assert.Single(snapshot.Mods, mod => mod.ModKey == "Wrapped Outer/Wrapped Inner");
        Assert.Equal("Wrapped Inner", wrapped.DirectoryName);
        Assert.Equal("Wrapped Outer", wrapped.Mo2OuterDirectoryName);
        Assert.Equal(ModProfileState.Listed, wrapped.ProfileState);
        Assert.Equal(ModEnabledState.Disabled, wrapped.EnabledState);
        Assert.Equal(4, wrapped.Priority);
        Assert.Equal(EvidenceKind.Inference, wrapped.RootResolution!.EvidenceKind);
        Assert.Equal("mods/Wrapped Outer", wrapped.RootResolution.OuterSource.RelativePath);
        Assert.Equal("mods/Wrapped Outer/Wrapped Inner", wrapped.RootResolution.InnerSource.RelativePath);
        Assert.Equal("mods/Wrapped Outer/Wrapped Inner/ModInfo.xml", wrapped.ModInfo!.Source.RelativePath);
        Assert.Contains(wrapped.Files, file =>
            file.Source.RelativePath == "mods/Wrapped Outer/Wrapped Inner/Config/items.xml");

        var direct = GetMod(snapshot, "Alpha Mod");
        Assert.Equal(EvidenceKind.Source, direct.RootResolution!.EvidenceKind);
        Assert.Equal("mods/Alpha Mod", direct.RootResolution.OuterSource.RelativePath);
        Assert.Equal("mods/Alpha Mod", direct.RootResolution.InnerSource.RelativePath);
    }

    [Fact]
    public void DiscoversMultipleInnerRootsAndReportsDeepCandidates()
    {
        var snapshot = ReadFixture();

        var first = Assert.Single(snapshot.Mods, mod => mod.ModKey == "Multi Outer/First Inner");
        var second = Assert.Single(snapshot.Mods, mod => mod.ModKey == "Multi Outer/Second Inner");
        Assert.Equal(ModEnabledState.Enabled, first.EnabledState);
        Assert.Equal(ModEnabledState.Enabled, second.EnabledState);
        Assert.Equal(5, first.Priority);
        Assert.Equal(5, second.Priority);
        Assert.Equal(EvidenceKind.Inference, first.RootResolution!.EvidenceKind);
        Assert.Equal(EvidenceKind.Inference, second.RootResolution!.EvidenceKind);

        var deep = Assert.Single(snapshot.Mods, mod => mod.ModKey == "Deep Outer/Deep Inner");
        Assert.DoesNotContain(snapshot.Mods, mod => mod.ModKey.Contains("Nested", StringComparison.Ordinal));
        Assert.Contains(deep.XmlFiles, file => file.RelativePath == "Config/Nested/ModInfo.xml");
        Assert.Contains(snapshot.Diagnostics, diagnostic =>
            diagnostic.Code == "mod.root.depth_exceeded" &&
            diagnostic.RawValue == "Deep Inner/Config/Nested/ModInfo.xml");
    }

    [Fact]
    public void IndexUsesResolvedInnerPathsAndExcludesUnresolvedOuterDirectories()
    {
        var snapshot = ReadFixture();

        Assert.Contains(snapshot.Index.ForwardReferences, reference =>
            reference.From.Kind == LocalKnowledgeNodeKind.Mod &&
            reference.From.Value == "Wrapped Outer/Wrapped Inner" &&
            reference.To.Kind == LocalKnowledgeNodeKind.File &&
            reference.To.Value == "mods/Wrapped Outer/Wrapped Inner/Config/items.xml");
        Assert.DoesNotContain(snapshot.Index.ForwardReferences, reference =>
            reference.From.Kind == LocalKnowledgeNodeKind.Mod &&
            reference.From.Value == "No ModInfo");
    }

    [Fact]
    public void ReusesStaticKnowledgeAcrossProfilesAndInvalidatesOnMetadataChanges()
    {
        var root = Directory.CreateTempSubdirectory("modscope-cache-");
        try
        {
            CopyDirectory(FixtureRoot, root.FullName);
            var alternateProfilePath = Directory.CreateDirectory(Path.Combine(root.FullName, "alternate"));
            File.WriteAllText(
                Path.Combine(alternateProfilePath.FullName, "modlist.txt"),
                "-Alpha Mod\n+Beta Mod\n");

            var reader = new Mo2SnapshotReader();
            var first = reader.Read(CreateSource(root.FullName));
            var alternate = reader.Read(new Mo2SourceDefinition(
                "synthetic-instance",
                "alternate",
                root.FullName,
                alternateProfilePath.FullName,
                Path.Combine(root.FullName, "mods")));

            Assert.NotEqual(first.SnapshotId, alternate.SnapshotId);
            Assert.NotEqual(first.ProfileEntries[0].RawLine, alternate.ProfileEntries[0].RawLine);
            Assert.Equal(first.Index.ForwardReferences, alternate.Index.ForwardReferences);
            Assert.Equal(first.Index.ReverseReferences, alternate.Index.ReverseReferences);
            Assert.Equal(
                first.Mods
                    .Where(mod => mod.ResolvedDirectoryRelativePath is not null)
                    .OrderBy(mod => mod.ModKey, StringComparer.Ordinal)
                    .Select(mod => string.Join(
                        "|",
                        mod.ModKey,
                        mod.ModInfo?.Name ?? string.Empty,
                        string.Join(",", mod.Files.Select(file => $"{file.RelativePath}:{file.Sha256}")))),
                alternate.Mods
                    .Where(mod => mod.ResolvedDirectoryRelativePath is not null)
                    .OrderBy(mod => mod.ModKey, StringComparer.Ordinal)
                    .Select(mod => string.Join(
                        "|",
                        mod.ModKey,
                        mod.ModInfo?.Name ?? string.Empty,
                        string.Join(",", mod.Files.Select(file => $"{file.RelativePath}:{file.Sha256}")))));

            var alphaInfoPath = Path.Combine(root.FullName, "mods", "Alpha Mod", "ModInfo.xml");
            var beforeHash = first.Mods
                .Single(mod => mod.ModKey == "Alpha Mod")
                .Files
                .Single(file => file.RelativePath.Equals("ModInfo.xml", StringComparison.OrdinalIgnoreCase))
                .Sha256;
            File.AppendAllText(alphaInfoPath, "\n");

            var changed = reader.Read(CreateSource(root.FullName));
            var afterHash = changed.Mods
                .Single(mod => mod.ModKey == "Alpha Mod")
                .Files
                .Single(file => file.RelativePath.Equals("ModInfo.xml", StringComparison.OrdinalIgnoreCase))
                .Sha256;

            Assert.NotEqual(beforeHash, afterHash);

            var extraFilePath = Path.Combine(root.FullName, "mods", "Alpha Mod", "cache-test.txt");
            File.WriteAllText(extraFilePath, "cache metadata");
            var added = reader.Read(CreateSource(root.FullName));
            Assert.Contains(
                added.Mods.Single(mod => mod.ModKey == "Alpha Mod").Files,
                file => file.RelativePath.Equals("cache-test.txt", StringComparison.Ordinal));

            File.Delete(extraFilePath);
            var removed = reader.Read(CreateSource(root.FullName));
            Assert.DoesNotContain(
                removed.Mods.Single(mod => mod.ModKey == "Alpha Mod").Files,
                file => file.RelativePath.Equals("cache-test.txt", StringComparison.Ordinal));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ReportsDeterministicScanAndCacheProgress()
    {
        var root = Directory.CreateTempSubdirectory("modscope-progress-");
        try
        {
            CopyDirectory(FixtureRoot, root.FullName);
            var reader = new Mo2SnapshotReader();
            var firstProgress = new RecordingProgress();

            reader.Read(CreateSource(root.FullName), progress: firstProgress);

            var scanProgress = firstProgress.Values
                .Where(progress => progress.Phase == "scanning-mod-folders")
                .ToList();
            Assert.NotEmpty(scanProgress);
            Assert.Equal(0, scanProgress[0].Completed);
            Assert.Equal(scanProgress[0].Total, scanProgress[^1].Total);
            Assert.Equal(scanProgress[^1].Total, scanProgress[^1].Completed);
            Assert.Equal(
                scanProgress.Select(progress => progress.Completed),
                scanProgress.Select(progress => progress.Completed).OrderBy(value => value));
            Assert.Contains(firstProgress.Values, progress => progress.Phase == "building-index");
            Assert.Contains(firstProgress.Values, progress => progress.Phase == "projecting-profile");

            var cachedProgress = new RecordingProgress();
            reader.Read(CreateSource(root.FullName), progress: cachedProgress);

            Assert.DoesNotContain(
                cachedProgress.Values,
                progress => progress.Phase == "scanning-mod-folders");
            Assert.Contains(
                cachedProgress.Values,
                progress => progress.Phase == "reusing-static-knowledge");
            Assert.Contains(
                cachedProgress.Values,
                progress => progress.Phase == "projecting-profile");
        }
        finally
        {
            root.Delete(true);
        }
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
    public void NormalizedSnapshotDataAndIndexAreStableForTheSameInput()
    {
        var first = ReadFixture();
        var second = ReadFixture();

        var firstProfiles = first.ProfileEntries
            .Select(entry => string.Join("|", new[]
            {
                entry.RawLine,
                entry.SourceLineNumber.ToString(),
                entry.EnabledState.ToString(),
                entry.NormalizedModName ?? string.Empty,
                entry.Priority?.ToString() ?? string.Empty,
                string.Join(",", entry.Diagnostics.Select(diagnostic => diagnostic.Code))
            }))
            .ToArray();
        var secondProfiles = second.ProfileEntries
            .Select(entry => string.Join("|", new[]
            {
                entry.RawLine,
                entry.SourceLineNumber.ToString(),
                entry.EnabledState.ToString(),
                entry.NormalizedModName ?? string.Empty,
                entry.Priority?.ToString() ?? string.Empty,
                string.Join(",", entry.Diagnostics.Select(diagnostic => diagnostic.Code))
            }))
            .ToArray();
        Assert.Equal(firstProfiles, secondProfiles);

        var firstMods = first.Mods
            .OrderBy(mod => mod.ModKey, StringComparer.Ordinal)
            .Select(mod => string.Join("|", new[]
            {
                mod.DirectoryName,
                mod.ModKey,
                mod.ProfileState.ToString(),
                mod.EnabledState.ToString(),
                mod.Priority?.ToString() ?? string.Empty,
                mod.ResolvedDirectoryRelativePath ?? string.Empty,
                mod.ModInfo is null
                    ? string.Empty
                    : string.Join(":", new[]
                    {
                        mod.ModInfo.Name ?? string.Empty,
                        mod.ModInfo.DisplayName ?? string.Empty,
                        mod.ModInfo.Version ?? string.Empty
                    }),
                string.Join(";", mod.Files
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => string.Join(":", new[]
                    {
                        file.RelativePath,
                        file.Size.ToString(),
                        file.Sha256,
                        file.Source.RelativePath
                    }))),
                string.Join(";", mod.XmlFiles
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => string.Join(":", new[]
                    {
                        file.RelativePath,
                        file.ParseStatus.ToString(),
                        file.Source.RelativePath,
                        string.Join(",", file.PatchOperations
                            .OrderBy(operation => operation.ElementPath, StringComparer.Ordinal)
                            .Select(operation => string.Join("/", new[]
                            {
                                operation.ElementPath,
                                operation.RawOperationName,
                                operation.NormalizedKind?.ToString() ?? string.Empty
                            })))
                    })))
            }))
            .ToArray();
        var secondMods = second.Mods
            .OrderBy(mod => mod.ModKey, StringComparer.Ordinal)
            .Select(mod => string.Join("|", new[]
            {
                mod.DirectoryName,
                mod.ModKey,
                mod.ProfileState.ToString(),
                mod.EnabledState.ToString(),
                mod.Priority?.ToString() ?? string.Empty,
                mod.ResolvedDirectoryRelativePath ?? string.Empty,
                mod.ModInfo is null
                    ? string.Empty
                    : string.Join(":", new[]
                    {
                        mod.ModInfo.Name ?? string.Empty,
                        mod.ModInfo.DisplayName ?? string.Empty,
                        mod.ModInfo.Version ?? string.Empty
                    }),
                string.Join(";", mod.Files
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => string.Join(":", new[]
                    {
                        file.RelativePath,
                        file.Size.ToString(),
                        file.Sha256,
                        file.Source.RelativePath
                    }))),
                string.Join(";", mod.XmlFiles
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => string.Join(":", new[]
                    {
                        file.RelativePath,
                        file.ParseStatus.ToString(),
                        file.Source.RelativePath,
                        string.Join(",", file.PatchOperations
                            .OrderBy(operation => operation.ElementPath, StringComparer.Ordinal)
                            .Select(operation => string.Join("/", new[]
                            {
                                operation.ElementPath,
                                operation.RawOperationName,
                                operation.NormalizedKind?.ToString() ?? string.Empty
                            })))
                    })))
            }))
            .ToArray();
        Assert.Equal(firstMods, secondMods);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.Index.ForwardReferences, second.Index.ForwardReferences);
        Assert.Equal(first.Index.ReverseReferences, second.Index.ReverseReferences);
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

    private sealed class RecordingProgress : IProgress<LocalKnowledgeProgress>
    {
        public List<LocalKnowledgeProgress> Values { get; } = new();

        public void Report(LocalKnowledgeProgress value)
        {
            Values.Add(value);
        }
    }

    private sealed record FixtureFileState(string Sha256, DateTime LastWriteTimeUtc);
}
