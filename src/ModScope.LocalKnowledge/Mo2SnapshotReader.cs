using System.Text;

namespace ModScope.LocalKnowledge;

public sealed class Mo2SnapshotReader : IMo2SnapshotReader
{
    public LocalModSnapshot Read(
        Mo2SourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var paths = ValidateSource(source);
        cancellationToken.ThrowIfCancellationRequested();

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

        var directories = EnumerateModDirectories(paths.ModsPath, diagnostics);
        var directoryLookup = directories
            .GroupBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var firstProfileEntryByName = profileEntries
            .Where(entry => entry.NormalizedModName is not null)
            .GroupBy(entry => entry.NormalizedModName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var records = new List<LocalModRecord>();
        var inventories = new List<(string DirectoryName, IReadOnlyList<FileInventoryItem> Files)>();
        var recordedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profileEntry in firstProfileEntryByName.Values.OrderBy(entry => entry.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestedName = profileEntry.NormalizedModName!;
            if (!directoryLookup.TryGetValue(requestedName, out var directory))
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

            recordedDirectoryNames.Add(directory.Name);
            var parsed = BuildModRecord(
                directory,
                ModProfileState.Listed,
                profileEntry.EnabledState,
                profileEntry.Priority,
                cancellationToken);
            records.Add(parsed.Record);
            inventories.Add((directory.Name, parsed.Inventory));
            diagnostics.AddRange(parsed.Record.Diagnostics);
        }

        foreach (var directory in directoryLookup.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recordedDirectoryNames.Contains(directory.Name))
            {
                continue;
            }

            var parsed = BuildModRecord(
                directory,
                ModProfileState.Unlisted,
                ModEnabledState.Unknown,
                null,
                cancellationToken);
            records.Add(parsed.Record);
            inventories.Add((directory.Name, parsed.Inventory));
            diagnostics.AddRange(parsed.Record.Diagnostics);
        }

        var manifestFiles = new List<InputManifestFile>
        {
            new("profile/modlist.txt", modListBytes.LongLength, modListHash)
        };

        foreach (var inventory in inventories.OrderBy(item => item.DirectoryName, StringComparer.Ordinal))
        {
            foreach (var file in inventory.Files.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            {
                manifestFiles.Add(new InputManifestFile(
                    ParsingUtilities.BuildSourcePath(
                        ParsingUtilities.BuildSourcePath("mods", inventory.DirectoryName),
                        file.RelativePath),
                    file.Size,
                    file.Sha256));
            }
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
            CollectionHelpers.ReadOnly(diagnostics));
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

    private static (LocalModRecord Record, IReadOnlyList<FileInventoryItem> Inventory) BuildModRecord(
        DirectoryInfo directory,
        ModProfileState profileState,
        ModEnabledState enabledState,
        int? priority,
        CancellationToken cancellationToken)
    {
        var modSource = new SourceReference(
            SourceReferenceKind.ModDirectory,
            ParsingUtilities.BuildSourcePath("mods", directory.Name));
        var scan = ScanFiles(directory, cancellationToken);
        var fileRecords = scan.Files
            .Select(file => new ModFileRecord(
                file.RelativePath,
                file.Size,
                file.Sha256,
                file.Source,
                new EvidenceReference(EvidenceKind.Source, file.Source)))
            .ToList()
            .AsReadOnly();
        var parsed = SevenDaysToDieParsing.Parse(directory.Name, scan.Files);
        var diagnostics = scan.Diagnostics.Concat(parsed.Diagnostics).ToList();

        return (
            new LocalModRecord(
                directory.Name,
                directory.Name,
                profileState,
                enabledState,
                priority,
                directory.Name,
                parsed.ModInfo,
                fileRecords,
                parsed.XmlFiles,
                diagnostics.AsReadOnly(),
                modSource),
            scan.Files);
    }

    private static FileInventoryScanResult ScanFiles(
        DirectoryInfo root,
        CancellationToken cancellationToken)
    {
        var files = new List<FileInventoryItem>();
        var diagnostics = new List<Diagnostic>();
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
                        ParsingUtilities.BuildSourcePath("mods", root.Name))));
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
                            ParsingUtilities.BuildSourcePath("mods", root.Name))));
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
                            ParsingUtilities.BuildSourcePath("mods", root.Name))));
                    continue;
                }

                var relativePath = ParsingUtilities.NormalizeRelativePath(Path.GetRelativePath(root.FullName, file.FullName));
                var source = new SourceReference(
                    SourceReferenceKind.ModFile,
                    ParsingUtilities.BuildSourcePath(
                        ParsingUtilities.BuildSourcePath("mods", root.Name),
                        relativePath));

                try
                {
                    files.Add(new FileInventoryItem(
                        file.FullName,
                        relativePath,
                        file.Length,
                        ParsingUtilities.Sha256File(file.FullName),
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
            diagnostics.AsReadOnly());
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

    private static ValidatedSourcePaths ValidateSource(Mo2SourceDefinition source)
    {
        var instanceRoot = GetAbsolutePath(source.InstanceRootPath, nameof(source.InstanceRootPath));
        var profilePath = GetAbsolutePath(source.ProfilePath, nameof(source.ProfilePath));
        var modsPath = GetAbsolutePath(source.ModsPath, nameof(source.ModsPath));

        if (!Directory.Exists(instanceRoot))
        {
            throw new DirectoryNotFoundException($"The explicit MO2 instance root does not exist: {instanceRoot}");
        }

        if (!Directory.Exists(profilePath))
        {
            throw new DirectoryNotFoundException($"The explicit MO2 profile path does not exist: {profilePath}");
        }

        if (!Directory.Exists(modsPath))
        {
            throw new DirectoryNotFoundException($"The explicit MO2 mods path does not exist: {modsPath}");
        }

        if (!ParsingUtilities.IsWithin(instanceRoot, profilePath)
            || !ParsingUtilities.IsWithin(instanceRoot, modsPath))
        {
            throw new ArgumentException(
                "The explicit profile and mods paths must remain within the explicit MO2 instance root.",
                nameof(source));
        }

        return new ValidatedSourcePaths(instanceRoot, profilePath, modsPath);
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
        IReadOnlyList<Diagnostic> Diagnostics);
}
