using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModScope.LocalKnowledge;

namespace ModScope.Deployment;

public sealed class ModDeploymentService : IModDeploymentService
{
    private readonly Mo2SnapshotReader _snapshotReader;
    private readonly IJunctionOperator _junctionOperator;
    private readonly IProcessGate _processGate;
    private readonly IDeploymentStateStore _stateStore;

    public ModDeploymentService(
        Mo2SnapshotReader? snapshotReader = null,
        IJunctionOperator? junctionOperator = null,
        IProcessGate? processGate = null,
        IDeploymentStateStore? stateStore = null)
    {
        _snapshotReader = snapshotReader ?? new Mo2SnapshotReader();
        _junctionOperator = junctionOperator ?? new WindowsJunctionOperator();
        _processGate = processGate ?? new WindowsProcessGate();
        _stateStore = stateStore ?? new FileDeploymentStateStore();
    }

    public DeploymentPlan Preview(
        Mo2SourceDefinition source,
        DeploymentDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(draft);

        var diagnostics = new List<DeploymentDiagnostic>();
        var normalizedSource = source with
        {
            InstanceName = source.InstanceName.Trim(),
            ProfileName = source.ProfileName.Trim(),
            InstanceRootPath = Path.GetFullPath(source.InstanceRootPath),
            ProfilePath = Path.GetFullPath(source.ProfilePath),
            ModsPath = Path.GetFullPath(source.ModsPath),
            GamePath = string.IsNullOrWhiteSpace(source.GamePath)
                ? null
                : Path.GetFullPath(source.GamePath)
        };

        if (!string.Equals(
                normalizedSource.ProfileName,
                draft.ProfileName.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Blocked(
                "deployment.profile.mismatch",
                "The deployment draft does not target the loaded MO2 profile."));
        }

        foreach (var process in _processGate.GetBlockingProcesses())
        {
            diagnostics.Add(Blocked(
                "deployment.process.running",
                $"Close the running process before applying the deployment: {process}."));
        }

        var modListPath = Path.Combine(normalizedSource.ProfilePath, "modlist.txt");
        ModlistDocument? document = null;
        try
        {
            document = ModlistDocument.Read(modListPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(Blocked(
                "deployment.modlist.read.failed",
                $"The profile modlist could not be read ({exception.GetType().Name})."));
        }

        LocalModSnapshot? snapshot = null;
        try
        {
            snapshot = _snapshotReader.Read(normalizedSource, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException)
        {
            diagnostics.Add(Blocked(
                "deployment.source.read.failed",
                $"The MO2 source could not be read ({exception.GetType().Name})."));
        }

        string? gameRoot = null;
        string? gameModsPath = null;
        if (string.IsNullOrWhiteSpace(normalizedSource.GamePath))
        {
            diagnostics.Add(Blocked(
                "deployment.game.path.missing",
                "MO2 does not provide a valid Steam game root."));
        }
        else
        {
            gameRoot = ValidateGameRoot(normalizedSource.GamePath, diagnostics);
            if (gameRoot is not null)
            {
                gameModsPath = Path.Combine(gameRoot, "Mods");
                if (File.Exists(gameModsPath))
                {
                    diagnostics.Add(Blocked(
                        "deployment.game.mods.file",
                        "The Steam game Mods path is a file. ModScope will not replace it."));
                    gameModsPath = null;
                }
                else if (Directory.Exists(gameModsPath) && IsReparsePoint(gameModsPath))
                {
                    diagnostics.Add(Blocked(
                        "deployment.game.mods.reparse",
                        "The Steam game Mods directory is a reparse point. ModScope will not use it."));
                    gameModsPath = null;
                }
            }
        }

        DeploymentManifest manifest = DeploymentManifest.Empty;
        try
        {
            manifest = _stateStore.Read();
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            diagnostics.Add(Blocked(
                "deployment.state.invalid",
                $"The ModScope deployment manifest could not be read ({exception.GetType().Name})."));
        }

        var modChanges = Array.Empty<DeploymentModChange>();
        var modListChanged = false;
        if (document is not null)
        {
            try
            {
                modChanges = BuildModChanges(document, draft);
                modListChanged = modChanges.Length > 0;
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add(Blocked("deployment.modlist.draft.invalid", exception.Message));
            }
        }

        var desiredJunctions = new List<DesiredJunction>();
        if (snapshot is not null && gameModsPath is not null)
        {
            desiredJunctions = ResolveDesiredJunctions(
                normalizedSource,
                snapshot,
                draft,
                diagnostics,
                cancellationToken);
        }

        var junctionChanges = new List<DeploymentJunctionChange>();
        var nextManagedJunctions = manifest.Junctions;
        if (gameRoot is not null && gameModsPath is not null)
        {
            var junctionPlan = BuildJunctionPlan(
                gameRoot,
                gameModsPath,
                normalizedSource,
                draft,
                desiredJunctions,
                manifest,
                diagnostics);
            junctionChanges.AddRange(junctionPlan.Changes);
            nextManagedJunctions = junctionPlan.NextManagedJunctions;
        }

        var sourceFingerprint = snapshot is null
            ? "unavailable"
            : BuildSourceFingerprint(snapshot);
        var gameFingerprint = BuildGameFingerprint(gameModsPath, manifest);
        var modListSha256 = document?.Sha256 ?? "unavailable";

        return new DeploymentPlan(
            Guid.NewGuid().ToString("N"),
            new DeploymentDraft(
                draft.ProfileName.Trim(),
                draft.Entries.OrderBy(entry => entry.Order).ToList().AsReadOnly()),
            normalizedSource,
            modListSha256,
            sourceFingerprint,
            gameFingerprint,
            modListChanged,
            modChanges,
            junctionChanges.AsReadOnly(),
            diagnostics.AsReadOnly(),
            DateTimeOffset.UtcNow)
        {
            NextManagedJunctions = nextManagedJunctions
        };
    }

    public DeploymentResult Apply(
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.CanApply)
        {
            return BlockedResult(plan, "The deployment plan contains blocking diagnostics.");
        }

        var current = Preview(plan.Source, plan.Draft, cancellationToken);
        if (!current.CanApply)
        {
            return BlockedResult(plan, "The deployment plan is no longer applicable.", current.Diagnostics);
        }

        if (!string.Equals(plan.ModListSha256, current.ModListSha256, StringComparison.Ordinal)
            || !string.Equals(plan.SourceFingerprint, current.SourceFingerprint, StringComparison.Ordinal)
            || !string.Equals(plan.GameFingerprint, current.GameFingerprint, StringComparison.Ordinal))
        {
            return BlockedResult(
                plan,
                "The deployment plan is stale. Preview the current disk state again.",
                new[]
                {
                    Blocked(
                        "deployment.plan.stale",
                        "The profile, MO2 MOD source, or Steam game Mods directory changed after preview.")
                });
        }

        var oldManifest = _stateStore.Read();
        var modListPath = Path.Combine(plan.Source.ProfilePath, "modlist.txt");
        var gameRoot = plan.Source.GamePath!;
        var gameModsPath = Path.Combine(gameRoot, "Mods");
        var backupPath = string.Empty;
        var tempPath = string.Empty;
        var modListChanged = false;
        var appliedChanges = new List<DeploymentJunctionChange>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = ModlistDocument.Read(modListPath);
            var renderedModlist = document.Rewrite(plan.Draft.Entries);
            modListChanged = !renderedModlist.AsSpan().SequenceEqual(File.ReadAllBytes(modListPath));

            backupPath = CreateBackupPath(modListPath);
            File.Copy(modListPath, backupPath, overwrite: false);

            if (plan.JunctionChanges.Any(change => change.Action == "create")
                && !Directory.Exists(gameModsPath))
            {
                Directory.CreateDirectory(gameModsPath);
            }

            foreach (var change in plan.JunctionChanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (change.Action)
                {
                    case "create":
                        _junctionOperator.Create(change.LinkPath, change.TargetPath!);
                        appliedChanges.Add(change);
                        break;
                    case "remove":
                        _junctionOperator.Remove(change.LinkPath);
                        appliedChanges.Add(change);
                        break;
                }
            }

            VerifyDesiredJunctions(plan, gameModsPath);

            if (modListChanged)
            {
                tempPath = modListPath + ".modscope-" + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(tempPath, renderedModlist);
                File.Move(tempPath, modListPath, overwrite: true);
                tempPath = string.Empty;
                var afterWrite = File.ReadAllBytes(modListPath);
                if (!afterWrite.AsSpan().SequenceEqual(renderedModlist))
                {
                    throw new IOException("The profile modlist verification failed after replacement.");
                }
            }

            _snapshotReader.Read(plan.Source, cancellationToken);
            _stateStore.Write(new DeploymentManifest(plan.NextManagedJunctions));

            return new DeploymentResult(
                DeploymentResultStatus.Applied,
                plan.PlanId,
                "The profile and Steam game MOD deployment were applied and verified.",
                Array.Empty<DeploymentDiagnostic>());
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or OperationCanceledException)
        {
            var rollbackDiagnostics = Rollback(
                modListPath,
                backupPath,
                gameModsPath,
                appliedChanges,
                oldManifest,
                cancellationToken);
            var diagnostics = new List<DeploymentDiagnostic>
            {
                Blocked("deployment.apply.failed", "The deployment failed. Rollback was attempted.")
            };
            diagnostics.AddRange(rollbackDiagnostics);
            return new DeploymentResult(
                rollbackDiagnostics.Any(diagnostic => diagnostic.IsBlocking)
                    ? DeploymentResultStatus.RecoveryRequired
                    : DeploymentResultStatus.Blocked,
                plan.PlanId,
                rollbackDiagnostics.Any(diagnostic => diagnostic.IsBlocking)
                    ? "The deployment failed and recovery requires attention."
                    : "The deployment failed and was rolled back.",
                diagnostics.AsReadOnly());
        }
        finally
        {
            if (tempPath.Length > 0 && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private DeploymentJunctionPlan BuildJunctionPlan(
        string gameRoot,
        string gameModsPath,
        Mo2SourceDefinition source,
        DeploymentDraft draft,
        IReadOnlyList<DesiredJunction> desiredJunctions,
        DeploymentManifest manifest,
        ICollection<DeploymentDiagnostic> diagnostics)
    {
        var changes = new List<DeploymentJunctionChange>();
        var currentManaged = manifest.Junctions
            .Where(junction => PathsEqual(junction.GameRootPath, gameRoot))
            .ToList();
        var currentManagedByLink = currentManaged
            .GroupBy(junction => junction.LinkPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var next = manifest.Junctions
            .Where(junction => !PathsEqual(junction.GameRootPath, gameRoot))
            .ToList();

        foreach (var desired in desiredJunctions)
        {
            var inspection = _junctionOperator.Inspect(desired.LinkPath);
            currentManagedByLink.TryGetValue(desired.LinkPath, out var managed);
            if (!inspection.Exists)
            {
                changes.Add(new DeploymentJunctionChange(
                    "create",
                    desired.TargetName,
                    desired.LinkPath,
                    desired.TargetPath,
                    null,
                    desired.ModKey));
            }
            else if (!inspection.IsDirectory || !inspection.IsReparsePoint)
            {
                diagnostics.Add(Blocked(
                    "deployment.junction.collision",
                    $"The game Mods target '{desired.TargetName}' is an existing real folder or file.",
                    desired.TargetName));
            }
            else if (inspection.TargetPath is null
                || !PathsEqual(inspection.TargetPath, desired.TargetPath))
            {
                diagnostics.Add(Blocked(
                    "deployment.junction.foreign",
                    $"The game Mods target '{desired.TargetName}' is an existing junction with another target.",
                    desired.TargetName));
            }
            else
            {
                changes.Add(new DeploymentJunctionChange(
                    managed is null ? "adopt" : "keep",
                    desired.TargetName,
                    desired.LinkPath,
                    desired.TargetPath,
                    inspection.TargetPath,
                    desired.ModKey));
            }

            next.Add(new ManagedJunctionState(
                gameRoot,
                desired.LinkPath,
                desired.TargetPath,
                desired.TargetName,
                desired.ModKey,
                draft.ProfileName));
        }

        foreach (var managed in currentManaged)
        {
            if (desiredJunctions.Any(desired => PathsEqual(desired.LinkPath, managed.LinkPath)))
            {
                continue;
            }

            if (!IsWithinDirectory(gameModsPath, managed.LinkPath))
            {
                diagnostics.Add(Blocked(
                    "deployment.manifest.path.invalid",
                    $"The managed junction '{managed.TargetName}' is outside the selected game Mods directory.",
                    managed.TargetName));
                continue;
            }

            var inspection = _junctionOperator.Inspect(managed.LinkPath);
            if (!inspection.Exists)
            {
                continue;
            }

            if (!inspection.IsDirectory
                || !inspection.IsReparsePoint
                || inspection.TargetPath is null
                || !PathsEqual(inspection.TargetPath, managed.TargetPath))
            {
                diagnostics.Add(Blocked(
                    "deployment.managed.junction.changed",
                    $"The managed junction '{managed.TargetName}' no longer points to its recorded target.",
                    managed.TargetName));
                continue;
            }

            changes.Add(new DeploymentJunctionChange(
                "remove",
                managed.TargetName,
                managed.LinkPath,
                null,
                managed.TargetPath,
                managed.ModKey));
        }

        return new DeploymentJunctionPlan(
            changes.AsReadOnly(),
            next.AsReadOnly());
    }

    private static DeploymentModChange[] BuildModChanges(
        ModlistDocument document,
        DeploymentDraft draft)
    {
        var slots = document.EditableLines;
        var entries = draft.Entries.OrderBy(entry => entry.Order).ToList();
        var duplicateSlots = slots
            .GroupBy(slot => slot.ModKey!, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSlots is not null)
        {
            throw new InvalidOperationException(
                $"The profile modlist contains the MOD '{duplicateSlots.Key}' more than once.");
        }

        var duplicate = entries
            .GroupBy(entry => entry.ModKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"The deployment draft contains the MOD '{duplicate.Key}' more than once.");
        }

        var slotKeys = slots.Select(slot => slot.ModKey!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var draftKeys = entries.Select(entry => entry.ModKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!slotKeys.SetEquals(draftKeys))
        {
            throw new InvalidOperationException(
                "The deployment draft does not match the editable MOD entries in modlist.txt.");
        }

        var before = slots
            .Reverse()
            .Select((slot, index) => new { ModKey = slot.ModKey!, Enabled = slot.IsEnabled == true, Order = index })
            .ToDictionary(value => value.ModKey, StringComparer.OrdinalIgnoreCase);
        var changes = new List<DeploymentModChange>();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var original = before[entry.ModKey];
            if (original.Enabled != entry.Enabled || original.Order != index)
            {
                changes.Add(new DeploymentModChange(
                    entry.ModKey,
                    original.Enabled,
                    entry.Enabled,
                    original.Order,
                    index));
            }
        }

        return changes.ToArray();
    }

    private static List<DesiredJunction> ResolveDesiredJunctions(
        Mo2SourceDefinition source,
        LocalModSnapshot snapshot,
        DeploymentDraft draft,
        ICollection<DeploymentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var recordsByOuter = snapshot.Mods
            .Where(record => record.Mo2OuterDirectoryName is not null)
            .GroupBy(record => record.Mo2OuterDirectoryName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var result = new List<DesiredJunction>();
        var targetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in draft.Entries.Where(entry => entry.Enabled).OrderBy(entry => entry.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!recordsByOuter.TryGetValue(entry.ModKey, out var records))
            {
                diagnostics.Add(Blocked(
                    "deployment.mod.unresolved",
                    $"The enabled MOD '{entry.ModKey}' has no resolved MO2 MOD root.",
                    entry.ModKey));
                continue;
            }

            foreach (var record in records)
            {
                var resolution = record.RootResolution;
                if (resolution is null)
                {
                    diagnostics.Add(Blocked(
                        "deployment.mod.root.missing",
                        $"The enabled MOD '{entry.ModKey}' has no supported ModInfo.xml root.",
                        entry.ModKey));
                    continue;
                }

                var targetPath = Path.GetFullPath(Path.Combine(
                    source.ModsPath,
                    resolution.InnerDirectoryRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinDirectory(source.ModsPath, targetPath))
                {
                    diagnostics.Add(Blocked(
                        "deployment.mod.path.escape",
                        $"The resolved MOD root for '{entry.ModKey}' is outside the selected MO2 mods directory.",
                        entry.ModKey));
                    continue;
                }

                if (!Directory.Exists(targetPath))
                {
                    diagnostics.Add(Blocked(
                        "deployment.mod.root.missing",
                        $"The resolved MOD root for '{entry.ModKey}' does not exist.",
                        entry.ModKey));
                    continue;
                }

                if (IsReparsePoint(targetPath))
                {
                    diagnostics.Add(Blocked(
                        "deployment.mod.root.reparse",
                        $"The resolved MOD root for '{entry.ModKey}' is a reparse point.",
                        entry.ModKey));
                    continue;
                }

                var targetName = new DirectoryInfo(targetPath).Name;
                if (!targetNames.Add(targetName))
                {
                    diagnostics.Add(Blocked(
                        "deployment.junction.duplicate_target",
                        $"Multiple enabled MOD roots use the game Mods target '{targetName}'.",
                        targetName));
                    continue;
                }

                result.Add(new DesiredJunction(
                    entry.ModKey,
                    targetName,
                    targetPath,
                    Path.Combine(source.GamePath!, "Mods", targetName)));
            }
        }

        return result;
    }

    private static string? ValidateGameRoot(
        string gamePath,
        ICollection<DeploymentDiagnostic> diagnostics)
    {
        var gameRoot = Path.GetFullPath(gamePath);
        if (!Directory.Exists(gameRoot))
        {
            diagnostics.Add(Blocked(
                "deployment.game.path.missing",
                "The configured Steam game root does not exist."));
            return null;
        }

        if (IsReparsePoint(gameRoot))
        {
            diagnostics.Add(Blocked(
                "deployment.game.path.reparse",
                "The configured Steam game root is a reparse point."));
            return null;
        }

        var executable = Path.Combine(gameRoot, "7DaysToDie.exe");
        if (!File.Exists(executable))
        {
            diagnostics.Add(Blocked(
                "deployment.game.executable.missing",
                "The configured Steam game root does not contain 7DaysToDie.exe."));
            return null;
        }

        return gameRoot;
    }

    private void VerifyDesiredJunctions(DeploymentPlan plan, string gameModsPath)
    {
        foreach (var change in plan.JunctionChanges
                     .Where(change => change.Action is "create" or "keep" or "adopt"))
        {
            var inspection = _junctionOperator.Inspect(change.LinkPath);
            if (!inspection.Exists
                || !inspection.IsDirectory
                || !inspection.IsReparsePoint
                || inspection.TargetPath is null
                || change.TargetPath is null
                || !PathsEqual(inspection.TargetPath, change.TargetPath))
            {
                throw new IOException(
                    $"The game Mods junction '{change.TargetName}' could not be verified.");
            }
        }

        if (Directory.Exists(gameModsPath) && IsReparsePoint(gameModsPath))
        {
            throw new IOException("The game Mods directory became a reparse point during apply.");
        }
    }

    private IReadOnlyList<DeploymentDiagnostic> Rollback(
        string modListPath,
        string backupPath,
        string gameModsPath,
        IReadOnlyList<DeploymentJunctionChange> appliedChanges,
        DeploymentManifest oldManifest,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<DeploymentDiagnostic>();
        try
        {
            if (backupPath.Length > 0 && File.Exists(backupPath))
            {
                File.Copy(backupPath, modListPath, overwrite: true);
            }

            foreach (var change in appliedChanges.Reverse())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inspection = _junctionOperator.Inspect(change.LinkPath);
                if (change.Action == "create")
                {
                    if (!inspection.Exists)
                    {
                        continue;
                    }

                    if (!inspection.IsReparsePoint
                        || inspection.TargetPath is null
                        || change.TargetPath is null
                        || !PathsEqual(inspection.TargetPath, change.TargetPath))
                    {
                        diagnostics.Add(Blocked(
                            "deployment.rollback.junction.changed",
                            $"The created junction '{change.TargetName}' changed before rollback.",
                            change.TargetName));
                        continue;
                    }

                    _junctionOperator.Remove(change.LinkPath);
                }
                else if (change.Action == "remove")
                {
                    if (inspection.Exists)
                    {
                        if (!inspection.IsReparsePoint
                            || inspection.TargetPath is null
                            || change.PreviousTargetPath is null
                            || !PathsEqual(inspection.TargetPath, change.PreviousTargetPath))
                        {
                            diagnostics.Add(Blocked(
                                "deployment.rollback.junction.collision",
                                $"The removed junction '{change.TargetName}' cannot be restored safely.",
                                change.TargetName));
                        }

                        continue;
                    }

                    if (change.PreviousTargetPath is null || !Directory.Exists(change.PreviousTargetPath))
                    {
                        diagnostics.Add(Blocked(
                            "deployment.rollback.target.missing",
                            $"The previous target for '{change.TargetName}' no longer exists.",
                            change.TargetName));
                        continue;
                    }

                    Directory.CreateDirectory(gameModsPath);
                    _junctionOperator.Create(change.LinkPath, change.PreviousTargetPath);
                }
            }

            _stateStore.Write(oldManifest);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or OperationCanceledException)
        {
            diagnostics.Add(Blocked(
                "deployment.rollback.failed",
                "Rollback could not finish. Manual recovery is required."));
        }

        return diagnostics.AsReadOnly();
    }

    private static DeploymentResult BlockedResult(
        DeploymentPlan plan,
        string message,
        IReadOnlyList<DeploymentDiagnostic>? diagnostics = null)
    {
        return new DeploymentResult(
            DeploymentResultStatus.Blocked,
            plan.PlanId,
            message,
            diagnostics ?? plan.Diagnostics);
    }

    private static DeploymentDiagnostic Blocked(
        string code,
        string message,
        string? targetName = null)
    {
        return new DeploymentDiagnostic(code, message, true, targetName);
    }

    private static string BuildSourceFingerprint(LocalModSnapshot snapshot)
    {
        var values = new List<string> { snapshot.InputManifest.ProfileModListSha256 };
        values.AddRange(snapshot.InputManifest.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => $"{file.RelativePath}|{file.Size}|{file.Sha256}"));
        values.AddRange(snapshot.Mods
            .OrderBy(mod => mod.Mo2OuterDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.ModKey, StringComparer.Ordinal)
            .Select(mod => $"{mod.Mo2OuterDirectoryName}|{mod.ModKey}|{mod.RootResolution?.InnerDirectoryRelativePath}"));
        return HashText(values);
    }

    private string BuildGameFingerprint(string? gameModsPath, DeploymentManifest manifest)
    {
        var values = new List<string>();
        if (gameModsPath is null || !Directory.Exists(gameModsPath))
        {
            values.Add("missing");
        }
        else
        {
            values.AddRange(Directory.EnumerateFileSystemEntries(gameModsPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                {
                    var inspection = _junctionOperator.Inspect(path);
                    return $"{Path.GetFileName(path)}|{inspection.IsDirectory}|{inspection.IsReparsePoint}|{inspection.TargetPath}";
                }));
        }

        values.AddRange(manifest.Junctions
            .OrderBy(junction => junction.LinkPath, StringComparer.OrdinalIgnoreCase)
            .Select(junction => $"managed|{junction.GameRootPath}|{junction.LinkPath}|{junction.TargetPath}"));
        return HashText(values);
    }

    private static string CreateBackupPath(string modListPath)
    {
        var directory = Path.GetDirectoryName(modListPath)
            ?? throw new InvalidOperationException("The profile modlist has no parent directory.");
        var baseName = Path.GetFileName(modListPath);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var suffix = attempt == 0 ? string.Empty : $"-{attempt}";
            var candidate = Path.Combine(directory, $"{baseName}.bak-{stamp}{suffix}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("A unique modlist backup path could not be created.");
    }

    private static string HashText(IEnumerable<string> values)
    {
        var text = string.Join("\n", values);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static bool IsWithinDirectory(string parent, string child)
    {
        var normalizedParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(child);
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private sealed record DesiredJunction(
        string ModKey,
        string TargetName,
        string TargetPath,
        string LinkPath);

    private sealed record DeploymentJunctionPlan(
        IReadOnlyList<DeploymentJunctionChange> Changes,
        IReadOnlyList<ManagedJunctionState> NextManagedJunctions);
}

public sealed class FileDeploymentStateStore : IDeploymentStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;

    public FileDeploymentStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModScope",
            "deployment-state.json");
    }

    public DeploymentManifest Read()
    {
        if (!File.Exists(_path))
        {
            return DeploymentManifest.Empty;
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<DeploymentManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("The deployment manifest is empty.");
    }

    public void Write(DeploymentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The deployment manifest has no parent directory.");
        Directory.CreateDirectory(directory);
        var tempPath = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(manifest, JsonOptions));
            File.Move(tempPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

public sealed class WindowsJunctionOperator : IJunctionOperator
{
    private const string PowerShellCommand =
        @"$ErrorActionPreference = 'Stop'; New-Item -ItemType Junction -LiteralPath $env:MODSCOPE_JUNCTION_LINK -Target $env:MODSCOPE_JUNCTION_TARGET | Out-Null";

    public JunctionInspection Inspect(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            var isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            string? targetPath = null;
            if (isReparsePoint)
            {
                try
                {
                    targetPath = new DirectoryInfo(path).ResolveLinkTarget(false)?.FullName;
                }
                catch (IOException)
                {
                    targetPath = null;
                }
                catch (UnauthorizedAccessException)
                {
                    targetPath = null;
                }
            }

            return new JunctionInspection(true, isDirectory, isReparsePoint, targetPath);
        }
        catch (FileNotFoundException)
        {
            return new JunctionInspection(false, false, false, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new JunctionInspection(false, false, false, null);
        }
    }

    public void Create(string linkPath, string targetPath)
    {
        if (Inspect(linkPath).Exists)
        {
            throw new IOException($"The junction path already exists: {linkPath}.");
        }

        if (!Directory.Exists(targetPath))
        {
            throw new DirectoryNotFoundException($"The junction target does not exist: {targetPath}.");
        }

        var parent = Path.GetDirectoryName(linkPath)
            ?? throw new InvalidOperationException("The junction link has no parent directory.");
        Directory.CreateDirectory(parent);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            Arguments = "-NoLogo -NoProfile -NonInteractive -Command \"" + PowerShellCommand + "\""
        };
        startInfo.Environment["MODSCOPE_JUNCTION_LINK"] = linkPath;
        startInfo.Environment["MODSCOPE_JUNCTION_TARGET"] = targetPath;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("PowerShell could not start for junction creation.");
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("PowerShell did not finish junction creation.");
        }

        var error = process.StandardError.ReadToEnd().Trim();
        if (process.ExitCode != 0)
        {
            throw new IOException(
                string.IsNullOrWhiteSpace(error)
                    ? "PowerShell failed to create the junction."
                    : $"PowerShell failed to create the junction: {error}");
        }
    }

    public void Remove(string linkPath)
    {
        var inspection = Inspect(linkPath);
        if (!inspection.Exists)
        {
            return;
        }

        if (!inspection.IsDirectory || !inspection.IsReparsePoint)
        {
            throw new IOException($"The path is not a removable directory junction: {linkPath}.");
        }

        Directory.Delete(linkPath, recursive: false);
    }
}

public sealed class WindowsProcessGate : IProcessGate
{
    private static readonly string[] ProcessNames = { "ModOrganizer", "7DaysToDie" };

    public IReadOnlyList<string> GetBlockingProcesses()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var processName in ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        result.Add(process.ProcessName);
                    }
                }
                catch (InvalidOperationException)
                {
                    result.Add(processName);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return result.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }
}
