using System.Text;

namespace ModScope.LocalKnowledge;

public sealed class Mo2SnapshotReader : IMo2SnapshotReader
{
    private static readonly object StaticCatalogCacheGate = new();
    private static readonly Dictionary<string, StaticModCatalog> StaticCatalogCache =
        new(StringComparer.OrdinalIgnoreCase);

    public LocalModSnapshot Read(
        Mo2SourceDefinition source,
        CancellationToken cancellationToken = default,
        IProgress<LocalKnowledgeProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var paths = ValidateSource(source);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new LocalKnowledgeProgress("reading-profile"));

        var modListPath = Path.Combine(paths.ProfilePath, "modlist.txt");
        if (!File.Exists(modListPath))
        {
            throw new FileNotFoundException("The explicit MO2 profile does not contain modlist.txt.", modListPath);
        }

        var modListBytes = File.ReadAllBytes(modListPath);
        var modListHash = ParsingUtilities.Sha256Hex(modListBytes);
        var decodedModList = ParsingUtilities.DecodeText(modListBytes);
        var diagnostics = new List<Diagnostic>();

        if (decodedModList.HadDecodingError)
        {
            diagnostics.Add(new Diagnostic(
                "profile.encoding.invalid",
                DiagnosticSeverity.Error,
                "The profile modlist contains bytes that are not valid for the detected encoding.",
                new SourceReference(SourceReferenceKind.ProfileFile, "profile/modlist.txt")));
        }

        var profileEntries = ParseProfileEntries(decodedModList.Text);
        diagnostics.AddRange(profileEntries.SelectMany(entry => entry.Diagnostics));

        var staticCatalog = GetStaticCatalog(paths, cancellationToken, progress);
        diagnostics.AddRange(staticCatalog.Diagnostics);
        progress?.Report(new LocalKnowledgeProgress("projecting-profile"));
        var records = ProjectRecords(staticCatalog, profileEntries, diagnostics, cancellationToken);

        var manifestFiles = new List<InputManifestFile>
        {
            new("profile/modlist.txt", modListBytes.LongLength, modListHash)
        };

        foreach (var file in staticCatalog.Inventory
                     .OrderBy(item => item.Source.RelativePath, StringComparer.Ordinal)
                     .ThenBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            manifestFiles.Add(new InputManifestFile(
                file.Source.RelativePath,
                file.Size,
                file.Sha256));
        }

        var manifest = new InputManifest(
            modListHash,
            CollectionHelpers.ReadOnly(manifestFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal)),
            ParserMetadata.ParserVersion,
            ParserMetadata.SchemaVersion);

        var snapshotId = CreateSnapshotId(source, manifest);
        return new LocalModSnapshot(
            snapshotId,
            source.InstanceName,
            source.ProfileName,
            DateTimeOffset.UtcNow,
            ParserMetadata.ParserVersion,
            ParserMetadata.SchemaVersion,
            CollectionHelpers.ReadOnly(profileEntries),
            CollectionHelpers.ReadOnly(records),
            manifest,
            CollectionHelpers.ReadOnly(diagnostics))
        {
            Index = staticCatalog.Index
        };
    }

    private static StaticModCatalog GetStaticCatalog(
        ValidatedSourcePaths paths,
        CancellationToken cancellationToken,
        IProgress<LocalKnowledgeProgress>? progress)
    {
        var cacheKey = BuildStaticCatalogCacheKey(paths.ModsPath);
        progress?.Report(new LocalKnowledgeProgress("checking-cache"));

        lock (StaticCatalogCacheGate)
        {
            if (StaticCatalogCache.TryGetValue(cacheKey, out var cached)
                && IsStaticCatalogCurrent(cached, paths.ModsPath, cancellationToken))
            {
                progress?.Report(new LocalKnowledgeProgress("reusing-static-knowledge"));
                return cached;
            }

            var rebuilt = BuildStaticCatalog(paths, cancellationToken, progress);
            StaticCatalogCache[cacheKey] = rebuilt;
            return rebuilt;
        }
    }

    private static StaticModCatalog BuildStaticCatalog(
        ValidatedSourcePaths paths,
        CancellationToken cancellationToken,
        IProgress<LocalKnowledgeProgress>? progress)
    {
        var diagnostics = new List<Diagnostic>();
        var directories = EnumerateModDirectories(paths.ModsPath, diagnostics)
            .GroupBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(directory => directory.Name, StringComparer.Ordinal)
            .ToList();
        var results = new ModDirectoryReadResult[directories.Count];
        progress?.Report(new LocalKnowledgeProgress(
            "scanning-mod-folders",
            0,
            directories.Count));
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 2
        };
        var progressGate = new object();
        var completed = 0;

        Parallel.For(
            0,
            directories.Count,
            options,
            index =>
            {
                results[index] = BuildModRecordsForDirectory(
                    directories[index],
                    cancellationToken);
                lock (progressGate)
                {
                    completed += 1;
                    progress?.Report(new LocalKnowledgeProgress(
                        "scanning-mod-folders",
                        completed,
                        directories.Count));
                }
            });

        var records = results
            .SelectMany(result => result.Records)
            .ToList()
            .AsReadOnly();
        var inventory = results
            .SelectMany(result => result.Inventory)
            .ToList()
            .AsReadOnly();
        diagnostics.AddRange(results.SelectMany(result => result.Diagnostics));
        IReadOnlyList<MetadataFingerprintEntry> metadataFingerprint;
        try
        {
            metadataFingerprint = CaptureMetadataFingerprint(paths.ModsPath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic(
                "mods.metadata.enumeration_failed",
                DiagnosticSeverity.Warning,
                $"The MOD metadata fingerprint could not be captured: {exception.Message}",
                new SourceReference(SourceReferenceKind.ModDirectory, "mods")));
            metadataFingerprint = Array.Empty<MetadataFingerprintEntry>();
        }

        progress?.Report(new LocalKnowledgeProgress("building-index"));

        return new StaticModCatalog(
            directories.Select(directory => directory.Name).ToList().AsReadOnly(),
            records,
            inventory,
            diagnostics.AsReadOnly(),
            metadataFingerprint,
            LocalKnowledgeIndexBuilder.Build(records));
    }

    private static IReadOnlyList<LocalModRecord> ProjectRecords(
        StaticModCatalog catalog,
        IReadOnlyList<ProfileModEntry> profileEntries,
        List<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var directoryNames = new HashSet<string>(catalog.DirectoryNames, StringComparer.OrdinalIgnoreCase);
        var recordsByOuterDirectory = catalog.Records
            .Where(record => record.Mo2OuterDirectoryName is not null)
            .GroupBy(record => record.Mo2OuterDirectoryName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var firstProfileEntryByName = profileEntries
            .Where(entry => entry.NormalizedModName is not null)
            .GroupBy(entry => entry.NormalizedModName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var records = new List<LocalModRecord>();
        var recordedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profileEntry in firstProfileEntryByName.Values.OrderBy(entry => entry.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestedName = profileEntry.NormalizedModName!;
            if (!directoryNames.Contains(requestedName))
            {
                var unresolvedDiagnostic = new Diagnostic(
                    "mod.unresolved",
                    DiagnosticSeverity.Warning,
                    $"The profile entry '{requestedName}' has no matching directory under the explicit mods path.",
                    profileEntry.Source,
                    requestedName);
                diagnostics.Add(unresolvedDiagnostic);
                records.Add(new LocalModRecord(
                    requestedName,
                    requestedName,
                    ModProfileState.Unresolved,
                    profileEntry.EnabledState,
                    profileEntry.Priority,
                    null,
                    null,
                    Array.Empty<ModFileRecord>(),
                    Array.Empty<XmlFileReference>(),
                    new[] { unresolvedDiagnostic },
                    profileEntry.Source));
                continue;
            }

            recordedDirectoryNames.Add(requestedName);
            if (!recordsByOuterDirectory.TryGetValue(requestedName, out var staticRecords))
            {
                continue;
            }

            records.AddRange(staticRecords.Select(record => record with
            {
                ProfileState = ModProfileState.Listed,
                EnabledState = profileEntry.EnabledState,
                Priority = profileEntry.Priority
            }));
        }

        records.AddRange(catalog.Records
            .Where(record => record.Mo2OuterDirectoryName is not null
                && !recordedDirectoryNames.Contains(record.Mo2OuterDirectoryName))
            .OrderBy(record => record.Mo2OuterDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.ModKey, StringComparer.Ordinal));

        return records.AsReadOnly();
    }

    private static bool IsStaticCatalogCurrent(
        StaticModCatalog catalog,
        string modsPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = CaptureMetadataFingerprint(modsPath, cancellationToken);
            return current.SequenceEqual(catalog.MetadataFingerprint);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IReadOnlyList<MetadataFingerprintEntry> CaptureMetadataFingerprint(
        string modsPath,
        CancellationToken cancellationToken)
    {
        var root = new DirectoryInfo(modsPath);
        var entries = new List<MetadataFingerprintEntry>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            var childDirectories = directory
                .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ToList();
            var childFiles = directory
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ToList();

            foreach (var childDirectory in childDirectories)
            {
                var isReparsePoint = childDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint);
                entries.Add(CreateMetadataFingerprintEntry(root, childDirectory, isReparsePoint));
                if (!isReparsePoint)
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (var file in childFiles)
            {
                var isReparsePoint = file.Attributes.HasFlag(FileAttributes.ReparsePoint);
                entries.Add(CreateMetadataFingerprintEntry(root, file, isReparsePoint));
            }
        }

        return entries
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ThenBy(entry => entry.IsDirectory)
            .ToList()
            .AsReadOnly();
    }

    private static MetadataFingerprintEntry CreateMetadataFingerprintEntry(
        DirectoryInfo root,
        FileSystemInfo entry,
        bool isReparsePoint)
    {
        var relativePath = ParsingUtilities.NormalizeRelativePath(
            Path.GetRelativePath(root.FullName, entry.FullName));
        return new MetadataFingerprintEntry(
            relativePath,
            entry is DirectoryInfo,
            entry is FileInfo file ? file.Length : 0,
            entry.LastWriteTimeUtc.Ticks,
            isReparsePoint);
    }

    private static string BuildStaticCatalogCacheKey(string modsPath)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modsPath));
        return string.Join(
            "\n",
            normalizedPath,
            ParserMetadata.ParserVersion,
            ParserMetadata.SchemaVersion.ToString());
    }

    private static IReadOnlyList<ProfileModEntry> ParseProfileEntries(string text)
    {
        var entries = new List<ProfileModEntry>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var priority = 0;
        var lines = ParsingUtilities.SplitLines(text);

        for (var index = 0; index < lines.Count; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            var source = new SourceReference(
                SourceReferenceKind.ProfileFile,
                "profile/modlist.txt",
                index + 1,
                1);
            var entryDiagnostics = new List<Diagnostic>();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                entries.Add(new ProfileModEntry(
                    rawLine,
                    index + 1,
                    ModEnabledState.Unknown,
                    null,
                    null,
                    source,
                    entryDiagnostics.AsReadOnly()));
                continue;
            }

            var state = trimmed[0] switch
            {
                '+' => ModEnabledState.Enabled,
                '-' => ModEnabledState.Disabled,
                _ => ModEnabledState.Unknown
            };

            if (state == ModEnabledState.Unknown)
            {
                entryDiagnostics.Add(new Diagnostic(
                    "profile.line.unrecognized",
                    DiagnosticSeverity.Warning,
                    "The profile line is not a supported enabled or disabled MOD entry.",
                    source,
                    rawLine));
                entries.Add(new ProfileModEntry(
                    rawLine,
                    index + 1,
                    state,
                    null,
                    null,
                    source,
                    entryDiagnostics.AsReadOnly()));
                continue;
            }

            var normalizedName = trimmed[1..].Trim();
            if (normalizedName.Length == 0)
            {
                entryDiagnostics.Add(new Diagnostic(
                    "profile.line.empty_name",
                    DiagnosticSeverity.Warning,
                    "The profile entry has an enabled state but no MOD name.",
                    source,
                    rawLine));
                entries.Add(new ProfileModEntry(
                    rawLine,
                    index + 1,
                    state,
                    null,
                    null,
                    source,
                    entryDiagnostics.AsReadOnly()));
                continue;
            }

            var currentPriority = priority++;
            if (!seenNames.Add(normalizedName))
            {
                entryDiagnostics.Add(new Diagnostic(
                    "profile.mod.duplicate",
                    DiagnosticSeverity.Warning,
                    $"The MOD '{normalizedName}' appears more than once in the profile.",
                    source,
                    normalizedName));
            }

            entries.Add(new ProfileModEntry(
                rawLine,
                index + 1,
                state,
                normalizedName,
                currentPriority,
                source,
                entryDiagnostics.AsReadOnly()));
        }

        return entries.AsReadOnly();
    }

    public IReadOnlyList<Mo2ProfileDefinition> ListProfiles(
        Mo2SourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var paths = ValidateSource(source);
        cancellationToken.ThrowIfCancellationRequested();

        var profilesPath = Path.Combine(paths.InstanceRootPath, "profiles");
        var profiles = new List<Mo2ProfileDefinition>();

        var profilesDirectory = new DirectoryInfo(profilesPath);
        if (profilesDirectory.Exists
            && !profilesDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            foreach (var directory in profilesDirectory
                .EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(directory.FullName, "modlist.txt")))
                {
                    profiles.Add(new Mo2ProfileDefinition(directory.Name, directory.FullName));
                }
            }
        }

        if (profiles.Count == 0
            || !profiles.Any(profile =>
                string.Equals(profile.Name, source.ProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            profiles.Add(new Mo2ProfileDefinition(source.ProfileName, paths.ProfilePath));
        }

        return CollectionHelpers.ReadOnly(
            profiles
                .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<DirectoryInfo> EnumerateModDirectories(
        string modsPath,
        List<Diagnostic> diagnostics)
    {
        try
        {
            var directories = new List<DirectoryInfo>();
            foreach (var directory in new DirectoryInfo(modsPath).EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    diagnostics.Add(new Diagnostic(
                        "mods.directory.reparse_skipped",
                        DiagnosticSeverity.Warning,
                        $"The reparse-point directory '{directory.Name}' was not traversed.",
                        new SourceReference(
                            SourceReferenceKind.ModDirectory,
                            ParsingUtilities.BuildSourcePath("mods", directory.Name))));
                    continue;
                }

                directories.Add(directory);
            }

            foreach (var duplicateGroup in directories
                .GroupBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                diagnostics.Add(new Diagnostic(
                    "mods.directory.duplicate_case_insensitive",
                    DiagnosticSeverity.Warning,
                    $"Multiple MOD directories match the name '{duplicateGroup.Key}' without case sensitivity.",
                    new SourceReference(
                        SourceReferenceKind.ModDirectory,
                        ParsingUtilities.BuildSourcePath("mods", duplicateGroup.Key)),
                    duplicateGroup.Key));
            }

            return directories
                .OrderBy(directory => directory.Name, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic(
                "mods.directory.enumeration_failed",
                DiagnosticSeverity.Error,
                $"The explicit mods directory could not be enumerated: {exception.Message}",
                new SourceReference(SourceReferenceKind.ModDirectory, "mods")));
            return Array.Empty<DirectoryInfo>();
        }
    }

    private static ModDirectoryReadResult BuildModRecordsForDirectory(
        DirectoryInfo outerDirectory,
        CancellationToken cancellationToken)
    {
        var discovery = DiscoverModRoots(outerDirectory, cancellationToken);
        var diagnostics = discovery.Diagnostics.ToList();
        var records = new List<LocalModRecord>();
        var inventory = new List<FileInventoryItem>();

        if (discovery.AcceptedRoots.Count == 0)
        {
            var outerSource = BuildModDirectorySource(outerDirectory.Name);
            var outerScan = ScanFiles(outerDirectory, outerSource.RelativePath, cancellationToken);
            inventory.AddRange(outerScan.Files);
            diagnostics.AddRange(outerScan.Diagnostics);
            diagnostics.Add(new Diagnostic(
                "mod.root.not_found",
                DiagnosticSeverity.Warning,
                $"The MO2 outer folder '{outerDirectory.Name}' has no ModInfo.xml at depth 0 or 1.",
                outerSource,
                outerDirectory.Name));

            return new ModDirectoryReadResult(
                Array.Empty<LocalModRecord>(),
                inventory.AsReadOnly(),
                diagnostics.AsReadOnly());
        }

        foreach (var root in discovery.AcceptedRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = BuildModRecord(
                outerDirectory,
                root,
                cancellationToken);
            records.Add(parsed.Record);
            inventory.AddRange(parsed.Inventory);
            diagnostics.AddRange(parsed.Record.Diagnostics);
        }

        return new ModDirectoryReadResult(
            records.AsReadOnly(),
            inventory.AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    private static ModRootDiscoveryResult DiscoverModRoots(
        DirectoryInfo outerDirectory,
        CancellationToken cancellationToken)
    {
        var outerSource = BuildModDirectorySource(outerDirectory.Name);
        var discoveryDiagnostics = new List<Diagnostic>();
        var modInfoFiles = EnumerateModInfoFiles(
            outerDirectory,
            outerSource,
            discoveryDiagnostics,
            cancellationToken);
        var direct = modInfoFiles.FirstOrDefault(file =>
            ParsingUtilities.NormalizeRelativePath(Path.GetRelativePath(outerDirectory.FullName, file.FullName))
                .Equals("ModInfo.xml", StringComparison.OrdinalIgnoreCase));
        var accepted = new List<ModRootCandidate>();
        var diagnostics = new List<Diagnostic>(discoveryDiagnostics);

        if (direct is not null)
        {
            accepted.Add(CreateModRootCandidate(
                outerDirectory,
                outerDirectory,
                EvidenceKind.Source,
                outerSource));
        }
        else
        {
            foreach (var file in modInfoFiles
                         .Where(file => GetDirectoryDepth(outerDirectory, file.Directory) == 1)
                         .GroupBy(file => file.Directory!.FullName, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.OrderBy(file => file.FullName, StringComparer.Ordinal).First())
                         .OrderBy(file => file.Directory!.Name, StringComparer.Ordinal))
            {
                accepted.Add(CreateModRootCandidate(
                    outerDirectory,
                    file.Directory!,
                    EvidenceKind.Inference,
                    outerSource));
            }
        }

        foreach (var file in modInfoFiles
                     .Where(file => GetDirectoryDepth(outerDirectory, file.Directory) >= 2)
                     .OrderBy(file => file.FullName, StringComparer.Ordinal))
        {
            var relativePath = ParsingUtilities.NormalizeRelativePath(
                Path.GetRelativePath(outerDirectory.FullName, file.FullName));
            diagnostics.Add(new Diagnostic(
                "mod.root.depth_exceeded",
                DiagnosticSeverity.Warning,
                $"The ModInfo.xml candidate '{relativePath}' is deeper than the supported root depth.",
                outerSource,
                relativePath));
        }

        return new ModRootDiscoveryResult(
            accepted.AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    private static IReadOnlyList<FileInfo> EnumerateModInfoFiles(
        DirectoryInfo outerDirectory,
        SourceReference outerSource,
        List<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var files = new List<FileInfo>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(outerDirectory);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<DirectoryInfo> childDirectories;
            IEnumerable<FileInfo> childFiles;
            try
            {
                childDirectories = directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                childFiles = directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic(
                    "mod.root.discovery.read_failed",
                    DiagnosticSeverity.Warning,
                    $"The directory '{directory.Name}' could not be inspected for ModInfo.xml: {exception.Message}",
                    outerSource,
                    directory.Name));
                continue;
            }

            files.AddRange(childFiles
                .Where(file => file.Name.Equals("ModInfo.xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.FullName, StringComparer.Ordinal));

            foreach (var childDirectory in childDirectories
                         .OrderBy(item => item.Name, StringComparer.Ordinal)
                         .Reverse())
            {
                if (childDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    diagnostics.Add(new Diagnostic(
                        "mod.root.discovery.reparse_skipped",
                        DiagnosticSeverity.Warning,
                        $"The reparse-point directory '{childDirectory.Name}' was not inspected for ModInfo.xml.",
                        outerSource,
                        childDirectory.Name));
                    continue;
                }

                pending.Push(childDirectory);
            }
        }

        return files
            .OrderBy(file => file.FullName, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private static ModRootCandidate CreateModRootCandidate(
        DirectoryInfo outerDirectory,
        DirectoryInfo innerDirectory,
        EvidenceKind evidenceKind,
        SourceReference outerSource)
    {
        var innerRelativePath = innerDirectory.FullName.Equals(
            outerDirectory.FullName,
            StringComparison.OrdinalIgnoreCase)
            ? outerDirectory.Name
            : ParsingUtilities.BuildSourcePath(
                outerDirectory.Name,
                ParsingUtilities.NormalizeRelativePath(
                    Path.GetRelativePath(outerDirectory.FullName, innerDirectory.FullName)));
        var innerSource = new SourceReference(
            SourceReferenceKind.ModDirectory,
            ParsingUtilities.BuildSourcePath("mods", innerRelativePath));

        return new ModRootCandidate(
            innerDirectory,
            outerDirectory.Name,
            innerRelativePath,
            evidenceKind,
            outerSource,
            innerSource);
    }

    private static int GetDirectoryDepth(DirectoryInfo root, DirectoryInfo? directory)
    {
        if (directory is null)
        {
            return int.MaxValue;
        }

        var relativePath = ParsingUtilities.NormalizeRelativePath(
            Path.GetRelativePath(root.FullName, directory.FullName));
        return relativePath == "." || string.IsNullOrEmpty(relativePath)
            ? 0
            : relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static (LocalModRecord Record, IReadOnlyList<FileInventoryItem> Inventory) BuildModRecord(
        DirectoryInfo outerDirectory,
        ModRootCandidate root,
        CancellationToken cancellationToken)
    {
        var scan = ScanFiles(root.Directory, root.InnerSource.RelativePath, cancellationToken);
        var fileRecords = scan.Files
            .Select(file => new ModFileRecord(
                file.RelativePath,
                file.Size,
                file.Sha256,
                file.Source,
                new EvidenceReference(EvidenceKind.Source, file.Source)))
            .ToList()
            .AsReadOnly();
        var parsed = SevenDaysToDieParsing.Parse(
            root.InnerDirectoryRelativePath,
            scan.Files,
            scan.XmlContents);
        var diagnostics = scan.Diagnostics.Concat(parsed.Diagnostics).ToList();
        var rootResolution = new ModRootResolution(
            root.OuterDirectoryRelativePath,
            root.InnerDirectoryRelativePath,
            root.EvidenceKind,
            root.OuterSource,
            root.InnerSource);

        return (
            new LocalModRecord(
                root.Directory.Name,
                root.InnerDirectoryRelativePath,
                ModProfileState.Unlisted,
                ModEnabledState.Unknown,
                null,
                root.InnerDirectoryRelativePath,
                parsed.ModInfo,
                fileRecords,
                parsed.XmlFiles,
                diagnostics.AsReadOnly(),
                root.InnerSource)
            {
                Mo2OuterDirectoryName = outerDirectory.Name,
                Mo2OuterSource = root.OuterSource,
                RootResolution = rootResolution
            },
            scan.Files);
    }

    private static FileInventoryScanResult ScanFiles(
        DirectoryInfo root,
        string sourceDirectoryPath,
        CancellationToken cancellationToken)
    {
        var files = new List<FileInventoryItem>();
        var diagnostics = new List<Diagnostic>();
        var xmlContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<DirectoryInfo> childDirectories;
            IEnumerable<FileInfo> childFiles;

            try
            {
                childDirectories = directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                childFiles = directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic(
                    "mod.directory.read_failed",
                    DiagnosticSeverity.Error,
                    $"The MOD directory '{directory.Name}' could not be read: {exception.Message}",
                    new SourceReference(
                        SourceReferenceKind.ModDirectory,
                        sourceDirectoryPath)));
                continue;
            }

            foreach (var childDirectory in childDirectories.OrderBy(item => item.Name, StringComparer.Ordinal).Reverse())
            {
                if (childDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    diagnostics.Add(new Diagnostic(
                        "mod.directory.reparse_skipped",
                        DiagnosticSeverity.Warning,
                        $"The reparse-point directory '{childDirectory.Name}' was not traversed.",
                        new SourceReference(
                            SourceReferenceKind.ModDirectory,
                            sourceDirectoryPath)));
                    continue;
                }

                pending.Push(childDirectory);
            }

            foreach (var file in childFiles.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    diagnostics.Add(new Diagnostic(
                        "mod.file.reparse_skipped",
                        DiagnosticSeverity.Warning,
                        $"The reparse-point file '{file.Name}' was not read.",
                        new SourceReference(
                            SourceReferenceKind.ModFile,
                            sourceDirectoryPath)));
                    continue;
                }

                var relativePath = ParsingUtilities.NormalizeRelativePath(Path.GetRelativePath(root.FullName, file.FullName));
                var source = new SourceReference(
                    SourceReferenceKind.ModFile,
                    ParsingUtilities.BuildSourcePath(sourceDirectoryPath, relativePath));

                try
                {
                    byte[]? xmlBytes = null;
                    var sha256 = IsXmlInputFile(relativePath)
                        ? ReadXmlBytesAndHash(file.FullName, out xmlBytes)
                        : ParsingUtilities.Sha256File(file.FullName);
                    if (xmlBytes is not null)
                    {
                        xmlContents[file.FullName] = xmlBytes;
                    }

                    files.Add(new FileInventoryItem(
                        file.FullName,
                        relativePath,
                        file.Length,
                        sha256,
                        source));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(new Diagnostic(
                        "mod.file.read_failed",
                        DiagnosticSeverity.Error,
                        $"The MOD file '{relativePath}' could not be read: {exception.Message}",
                        source));
                }
            }
        }

        return new FileInventoryScanResult(
            files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList().AsReadOnly(),
            diagnostics.AsReadOnly(),
            xmlContents);
    }

    private static bool IsXmlInputFile(string relativePath)
    {
        return relativePath.Equals("ModInfo.xml", StringComparison.OrdinalIgnoreCase)
            || (relativePath.StartsWith("Config/", StringComparison.OrdinalIgnoreCase)
                && relativePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadXmlBytesAndHash(string fullPath, out byte[] bytes)
    {
        bytes = File.ReadAllBytes(fullPath);
        return ParsingUtilities.Sha256Hex(bytes);
    }

    private static string CreateSnapshotId(
        Mo2SourceDefinition source,
        InputManifest manifest)
    {
        var canonical = new StringBuilder()
            .Append(ParserMetadata.ParserVersion)
            .Append('\n')
            .Append(ParserMetadata.SchemaVersion)
            .Append('\n')
            .Append(source.InstanceName)
            .Append('\n')
            .Append(source.ProfileName)
            .Append('\n')
            .Append(manifest.ProfileModListSha256)
            .Append('\n');

        foreach (var file in manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            canonical.Append(file.RelativePath)
                .Append('\t')
                .Append(file.Size)
                .Append('\t')
                .Append(file.Sha256)
                .Append('\n');
        }

        return $"sha256:{ParsingUtilities.Sha256Hex(Encoding.UTF8.GetBytes(canonical.ToString()))}";
    }

    private static SourceReference BuildModDirectorySource(string directoryRelativePath)
    {
        return new SourceReference(
            SourceReferenceKind.ModDirectory,
            ParsingUtilities.BuildSourcePath("mods", directoryRelativePath));
    }

    private static ValidatedSourcePaths ValidateSource(Mo2SourceDefinition source)
    {
        var instanceRoot = GetAbsolutePath(source.InstanceRootPath, nameof(source.InstanceRootPath));
        var profilePath = GetAbsolutePath(source.ProfilePath, nameof(source.ProfilePath));
        var modsPath = GetAbsolutePath(source.ModsPath, nameof(source.ModsPath));

        if (!Directory.Exists(instanceRoot))
        {
            throw new DirectoryNotFoundException($"The explicit MO2 instance root does not exist: {instanceRoot}");
        }

        if (IsReparsePoint(instanceRoot))
        {
            throw new IOException($"The explicit MO2 instance root is a reparse point: {instanceRoot}");
        }

        if (!Directory.Exists(profilePath))
        {
            throw new DirectoryNotFoundException($"The explicit MO2 profile path does not exist: {profilePath}");
        }

        if (IsReparsePoint(profilePath))
        {
            throw new IOException($"The explicit MO2 profile path is a reparse point: {profilePath}");
        }

        if (!Directory.Exists(modsPath))
        {
            throw new DirectoryNotFoundException($"The explicit MO2 mods path does not exist: {modsPath}");
        }

        if (IsReparsePoint(modsPath))
        {
            throw new IOException($"The explicit MO2 mods path is a reparse point: {modsPath}");
        }

        return new ValidatedSourcePaths(instanceRoot, profilePath, modsPath);
    }

    private static bool IsReparsePoint(string path)
    {
        return new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static string GetAbsolutePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The source path must be an absolute path.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private sealed record ValidatedSourcePaths(
        string InstanceRootPath,
        string ProfilePath,
        string ModsPath);

    private sealed record FileInventoryScanResult(
        IReadOnlyList<FileInventoryItem> Files,
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, byte[]> XmlContents);

    private sealed record ModRootCandidate(
        DirectoryInfo Directory,
        string OuterDirectoryRelativePath,
        string InnerDirectoryRelativePath,
        EvidenceKind EvidenceKind,
        SourceReference OuterSource,
        SourceReference InnerSource);

    private sealed record ModRootDiscoveryResult(
        IReadOnlyList<ModRootCandidate> AcceptedRoots,
        IReadOnlyList<Diagnostic> Diagnostics);

    private sealed record ModDirectoryReadResult(
        IReadOnlyList<LocalModRecord> Records,
        IReadOnlyList<FileInventoryItem> Inventory,
        IReadOnlyList<Diagnostic> Diagnostics);

    private sealed record StaticModCatalog(
        IReadOnlyList<string> DirectoryNames,
        IReadOnlyList<LocalModRecord> Records,
        IReadOnlyList<FileInventoryItem> Inventory,
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<MetadataFingerprintEntry> MetadataFingerprint,
        LocalKnowledgeIndex Index);

    private sealed record MetadataFingerprintEntry(
        string RelativePath,
        bool IsDirectory,
        long Size,
        long LastWriteTimeUtcTicks,
        bool IsReparsePoint);
}
