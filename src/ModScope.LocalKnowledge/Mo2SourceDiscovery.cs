using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace ModScope.LocalKnowledge;

public sealed class Mo2SourceDiscovery : IMo2SourceDiscovery
{
    private const string SupportedGameName = "7 Days to Die";
    private const string InstanceRegistryPath = "Software\\Mod Organizer Team\\Mod Organizer";

    private readonly IMo2DiscoveryEnvironment _environment;

    public Mo2SourceDiscovery()
        : this(new WindowsMo2DiscoveryEnvironment())
    {
    }

    public Mo2SourceDiscovery(IMo2DiscoveryEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public static string? ReadConfiguredGamePath(string instanceRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceRootPath);

        try
        {
            var rootPath = Path.GetFullPath(instanceRootPath);
            var iniPath = Path.Combine(rootPath, "ModOrganizer.ini");
            if (!File.Exists(iniPath))
            {
                return null;
            }

            var diagnostics = new List<Diagnostic>();
            var ini = ParseIni(File.ReadAllText(iniPath), diagnostics);
            var baseDirectory = ResolvePath(
                ini.Get("Settings", "base_directory"),
                rootPath,
                rootPath,
                diagnostics,
                "base_directory") ?? rootPath;
            return ResolvePath(
                ini.Get("General", "gamePath"),
                rootPath,
                baseDirectory,
                diagnostics,
                "gamePath");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return null;
        }
    }

    public IReadOnlyList<Mo2SourceCandidate> Discover(
        Mo2SourceDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var roots = new Dictionary<string, RootProbe>(StringComparer.OrdinalIgnoreCase);
        AddRoots(
            roots,
            request.RememberedSource is null
                ? Array.Empty<string>()
                : new[] { request.RememberedSource.InstanceRootPath },
            Mo2SourceDiscoveryEvidenceKind.Remembered,
            EvidenceKind.Source,
            requireExisting: true);
        AddRoots(
            roots,
            request.SelectedRoots,
            Mo2SourceDiscoveryEvidenceKind.NativePicker,
            EvidenceKind.Source,
            requireExisting: false);

        foreach (var executablePath in _environment.GetRunningModOrganizerExecutablePaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = TryGetDirectoryName(executablePath);
            if (root is not null)
            {
                AddRoot(
                    roots,
                    root,
                    Mo2SourceDiscoveryEvidenceKind.RunningProcess,
                    EvidenceKind.Source,
                    requireExisting: false);
            }
        }

        var localAppDataPath = _environment.LocalAppDataPath;
        if (!string.IsNullOrWhiteSpace(localAppDataPath))
        {
            var globalInstancesPath = Path.Combine(localAppDataPath, "ModOrganizer");
            AddGlobalInstanceRoots(roots, globalInstancesPath, cancellationToken);

            var lastUsedInstanceName = _environment.GetLastUsedInstanceName();
            if (!string.IsNullOrWhiteSpace(lastUsedInstanceName))
            {
                AddRoot(
                    roots,
                    Path.Combine(globalInstancesPath, lastUsedInstanceName),
                    Mo2SourceDiscoveryEvidenceKind.GlobalInstance,
                    EvidenceKind.Inference,
                    requireExisting: true);
            }
        }

        var candidates = new List<Mo2SourceCandidate>();
        foreach (var probe in roots.Values.OrderBy(item => item.RootPath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rememberedProfile = request.RememberedSource is not null
                && string.Equals(
                    TryNormalizePath(request.RememberedSource.InstanceRootPath),
                    probe.RootPath,
                    StringComparison.OrdinalIgnoreCase)
                ? request.RememberedSource.ProfileName
                : null;
            candidates.AddRange(BuildCandidates(probe, cancellationToken, rememberedProfile));
        }

        return candidates
            .OrderBy(candidate => ReadinessRank(candidate.Readiness))
            .ThenByDescending(candidate => candidate.Evidence.Any(evidence => evidence.EvidenceKind == EvidenceKind.Source))
            .ThenBy(candidate => candidate.Source.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Source.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private static void AddGlobalInstanceRoots(
        IDictionary<string, RootProbe> roots,
        string globalInstancesPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(globalInstancesPath) || IsReparsePoint(globalInstancesPath))
        {
            return;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(globalInstancesPath, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsReparsePoint(directory)
                    || !File.Exists(Path.Combine(directory, "ModOrganizer.ini")))
                {
                    continue;
                }

                AddRoot(
                    roots,
                    directory,
                    Mo2SourceDiscoveryEvidenceKind.GlobalInstance,
                    EvidenceKind.Inference,
                    requireExisting: true);
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static void AddRoots(
        IDictionary<string, RootProbe> roots,
        IEnumerable<string> values,
        Mo2SourceDiscoveryEvidenceKind discoveryKind,
        EvidenceKind evidenceKind,
        bool requireExisting)
    {
        foreach (var value in values)
        {
            AddRoot(roots, value, discoveryKind, evidenceKind, requireExisting);
        }
    }

    private static void AddRoot(
        IDictionary<string, RootProbe> roots,
        string? value,
        Mo2SourceDiscoveryEvidenceKind discoveryKind,
        EvidenceKind evidenceKind,
        bool requireExisting)
    {
        var rootPath = TryNormalizePath(value);
        if (rootPath is null || (requireExisting && !Directory.Exists(rootPath)))
        {
            return;
        }

        if (!roots.TryGetValue(rootPath, out var probe))
        {
            probe = new RootProbe(rootPath);
            roots.Add(rootPath, probe);
        }

        probe.Evidence.Add(new Mo2SourceDiscoveryEvidence(discoveryKind, evidenceKind));
    }

    private static IReadOnlyList<Mo2SourceCandidate> BuildCandidates(
        RootProbe probe,
        CancellationToken cancellationToken,
        string? rememberedProfileName)
    {
        var rootPath = probe.RootPath;
        var instanceName = GetDirectoryName(rootPath);
        var evidence = probe.Evidence
            .Distinct()
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.EvidenceKind)
            .ToList()
            .AsReadOnly();

        if (!Directory.Exists(rootPath))
        {
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    string.Empty,
                    string.Empty,
                    CreateDefaultSource(instanceName, rootPath, string.Empty),
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    new[]
                    {
                        new Diagnostic(
                            "mo2.source.root.missing",
                            DiagnosticSeverity.Warning,
                            $"The remembered MO2 root '{instanceName}' no longer exists.")
                    })
            };
        }

        if (IsReparsePoint(rootPath))
        {
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    string.Empty,
                    string.Empty,
                    CreateDefaultSource(instanceName, rootPath, string.Empty),
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    new[]
                    {
                        new Diagnostic(
                            "mo2.source.root.reparse",
                            DiagnosticSeverity.Warning,
                            $"The MO2 root '{instanceName}' is a reparse point.")
                    })
            };
        }

        var iniPath = Path.Combine(rootPath, "ModOrganizer.ini");
        if (!File.Exists(iniPath))
        {
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    string.Empty,
                    string.Empty,
                    CreateDefaultSource(instanceName, rootPath, string.Empty),
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    new[]
                    {
                        new Diagnostic(
                            "mo2.source.ini.missing",
                            DiagnosticSeverity.Warning,
                            "The candidate does not contain ModOrganizer.ini.",
                            new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"))
                    })
            };
        }

        var diagnostics = new List<Diagnostic>();
        IniDocument ini;
        try
        {
            var decoded = ParsingUtilities.DecodeText(File.ReadAllBytes(iniPath));
            if (decoded.HadDecodingError)
            {
                diagnostics.Add(new Diagnostic(
                    "mo2.ini.encoding.invalid",
                    DiagnosticSeverity.Error,
                    "ModOrganizer.ini contains bytes that are not valid for the detected encoding.",
                    new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini")));
            }

            ini = ParseIni(decoded.Text, diagnostics);

            if (ini.IsMalformed || diagnostics.Any(diagnostic => diagnostic.Code == "mo2.ini.encoding.invalid"))
            {
                return new[]
                {
                    CreateCandidate(
                        rootPath,
                        instanceName,
                        string.Empty,
                        string.Empty,
                        CreateDefaultSource(instanceName, rootPath, string.Empty),
                        Mo2SourceCandidateReadiness.Invalid,
                        evidence,
                        diagnostics)
                };
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.ini.read.failed",
                DiagnosticSeverity.Error,
                $"ModOrganizer.ini could not be read: {exception.Message}",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini")));
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    string.Empty,
                    string.Empty,
                    CreateDefaultSource(instanceName, rootPath, string.Empty),
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    diagnostics)
            };
        }
        catch (IOException exception)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.ini.read.failed",
                DiagnosticSeverity.Error,
                $"ModOrganizer.ini could not be read: {exception.Message}",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini")));
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    string.Empty,
                    string.Empty,
                    CreateDefaultSource(instanceName, rootPath, string.Empty),
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    diagnostics)
            };
        }

        var gameName = ini.Get("General", "gameName") ?? string.Empty;
        var selectedProfile = ini.Get("General", "selected_profile") ?? string.Empty;
        var baseDirectory = ResolvePath(ini.Get("Settings", "base_directory"), rootPath, rootPath, diagnostics, "base_directory");
        var gamePath = ResolvePath(ini.Get("General", "gamePath"), rootPath, baseDirectory ?? rootPath, diagnostics, "gamePath");
        var modsPath = ResolvePath(ini.Get("Settings", "mod_directory"), rootPath, baseDirectory ?? rootPath, diagnostics, "mod_directory")
            ?? Path.Combine(rootPath, "mods");
        var profilesPath = ResolvePath(ini.Get("Settings", "profiles_directory"), rootPath, baseDirectory ?? rootPath, diagnostics, "profiles_directory")
            ?? Path.Combine(rootPath, "profiles");

        var gameSupported = string.Equals(gameName, SupportedGameName, StringComparison.OrdinalIgnoreCase);
        if (!gameSupported)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.game.unsupported",
                DiagnosticSeverity.Warning,
                string.IsNullOrWhiteSpace(gameName)
                    ? "The MO2 candidate does not declare a game name."
                    : $"The MO2 candidate declares unsupported game '{gameName}'.",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                gameName));

            var unsupportedSource = CreateDefaultSource(
                instanceName,
                rootPath,
                string.IsNullOrWhiteSpace(selectedProfile) ? string.Empty : selectedProfile);
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    gameName,
                    unsupportedSource.ProfileName,
                    unsupportedSource,
                    Mo2SourceCandidateReadiness.UnsupportedGame,
                    evidence,
                    diagnostics)
            };
        }

        var modsReady = ValidateDirectory(modsPath, "mod_directory", diagnostics);
        var profilesReady = ValidateDirectory(profilesPath, "profiles_directory", diagnostics);
        if (!modsReady || !profilesReady)
        {
            var invalidSource = CreateDefaultSource(instanceName, rootPath, string.Empty) with
            {
                ProfilePath = profilesPath,
                ModsPath = modsPath,
                ProfilesPath = profilesPath
            };
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    gameName,
                    string.Empty,
                    invalidSource,
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    diagnostics)
            };
        }

        var profileNames = EnumerateProfileNames(profilesPath, diagnostics, cancellationToken);
        var validSelectedProfile = profileNames.FirstOrDefault(profile =>
            string.Equals(profile, selectedProfile, StringComparison.OrdinalIgnoreCase));
        var validRememberedProfile = profileNames.FirstOrDefault(profile =>
            string.Equals(profile, rememberedProfileName, StringComparison.OrdinalIgnoreCase));
        var selectedProfiles = new List<string>();

        if (validRememberedProfile is not null)
        {
            selectedProfiles.Add(validRememberedProfile);
        }
        else if (validSelectedProfile is not null)
        {
            selectedProfiles.Add(validSelectedProfile);
        }
        else if (profileNames.Any(profile => string.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase)))
        {
            selectedProfiles.Add(profileNames.First(profile =>
                string.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(selectedProfile))
            {
                diagnostics.Add(new Diagnostic(
                    "mo2.profile.selected_missing",
                    DiagnosticSeverity.Warning,
                    $"The selected profile '{selectedProfile}' does not exist. The Default profile was selected.",
                    new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                    selectedProfile));
            }
        }
        else if (profileNames.Count == 1)
        {
            selectedProfiles.Add(profileNames[0]);
            if (!string.IsNullOrWhiteSpace(selectedProfile))
            {
                diagnostics.Add(new Diagnostic(
                    "mo2.profile.selected_missing",
                    DiagnosticSeverity.Warning,
                    $"The selected profile '{selectedProfile}' does not exist. The only available profile was selected.",
                    new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                    selectedProfile));
            }
        }
        else
        {
            selectedProfiles.AddRange(profileNames);
            if (!string.IsNullOrWhiteSpace(selectedProfile))
            {
                diagnostics.Add(new Diagnostic(
                    "mo2.profile.selection_required",
                    DiagnosticSeverity.Warning,
                    $"The selected profile '{selectedProfile}' does not exist. Choose one of the available profiles.",
                    new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                    selectedProfile));
            }
        }

        if (selectedProfiles.Count == 0)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.profile.missing",
                DiagnosticSeverity.Warning,
                "The MO2 candidate has no profile containing modlist.txt.",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini")));

            var source = CreateDefaultSource(instanceName, rootPath, string.Empty) with
            {
                ProfilePath = profilesPath,
                ModsPath = modsPath,
                ProfilesPath = profilesPath
            };
            return new[]
            {
                CreateCandidate(
                    rootPath,
                    instanceName,
                    gameName,
                    string.Empty,
                    source,
                    Mo2SourceCandidateReadiness.Invalid,
                    evidence,
                    diagnostics)
            };
        }

        return selectedProfiles
            .Select(profileName =>
            {
                var profilePath = Path.Combine(profilesPath, profileName);
            var source = new Mo2SourceDefinition(
                    instanceName,
                    profileName,
                    rootPath,
                    profilePath,
                    modsPath)
                {
                    ProfilesPath = profilesPath,
                    GamePath = gamePath
                };
                return CreateCandidate(
                    rootPath,
                    instanceName,
                    gameName,
                    profileName,
                    source,
                    Mo2SourceCandidateReadiness.Ready,
                    evidence,
                    diagnostics);
            })
            .OrderBy(candidate => candidate.Source.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static IniDocument ParseIni(string text, ICollection<Diagnostic> diagnostics)
    {
        var values = new Dictionary<(string Section, string Key), string>();
        var section = string.Empty;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var malformed = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0 || section.Length == 0)
            {
                malformed = true;
                diagnostics.Add(new Diagnostic(
                    "mo2.ini.malformed",
                    DiagnosticSeverity.Error,
                    "ModOrganizer.ini contains a line that is not a valid section or key-value entry.",
                    new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini", index + 1),
                    line));
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            values[(section, key)] = value;
        }

        return new IniDocument(values, malformed);
    }

    private static IReadOnlyList<string> EnumerateProfileNames(
        string profilesPath,
        ICollection<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            return Directory.EnumerateDirectories(profilesPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !IsReparsePoint(path))
                .Where(path => File.Exists(Path.Combine(path, "modlist.txt")))
                .Select(GetDirectoryName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.profile.enumeration.failed",
                DiagnosticSeverity.Error,
                $"MO2 profiles could not be enumerated: {exception.Message}",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini")));
        }
        catch (IOException exception)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.profile.enumeration.failed",
                DiagnosticSeverity.Error,
                $"MO2 profiles could not be enumerated: {exception.Message}",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Array.Empty<string>();
    }

    private static bool ValidateDirectory(
        string path,
        string settingName,
        ICollection<Diagnostic> diagnostics)
    {
        if (!Directory.Exists(path))
        {
            diagnostics.Add(new Diagnostic(
                "mo2.path.missing",
                DiagnosticSeverity.Warning,
                $"The configured MO2 {settingName} directory does not exist.",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                path));
            return false;
        }

        if (IsReparsePoint(path))
        {
            diagnostics.Add(new Diagnostic(
                "mo2.path.reparse",
                DiagnosticSeverity.Warning,
                $"The configured MO2 {settingName} directory is a reparse point.",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                path));
            return false;
        }

        return true;
    }

    private static string? ResolvePath(
        string? rawValue,
        string instanceRoot,
        string baseDirectory,
        ICollection<Diagnostic> diagnostics,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = DecodeMo2Value(rawValue);
        value = value.Replace("%BASE_DIR%", baseDirectory, StringComparison.OrdinalIgnoreCase);
        value = Environment.ExpandEnvironmentVariables(value);

        try
        {
            return Path.GetFullPath(Path.IsPathRooted(value)
                ? value
                : Path.Combine(instanceRoot, value));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            diagnostics.Add(new Diagnostic(
                "mo2.path.invalid",
                DiagnosticSeverity.Error,
                string.Equals(settingName, "gamePath", StringComparison.Ordinal)
                    ? "The configured MO2 game path is invalid."
                    : $"The configured MO2 {settingName} path is invalid: {exception.Message}",
                new SourceReference(SourceReferenceKind.InstanceFile, "ModOrganizer.ini"),
                string.Equals(settingName, "gamePath", StringComparison.Ordinal)
                    ? null
                    : rawValue));
            return null;
        }
    }

    private static string DecodeMo2Value(string value)
    {
        var decoded = value.Trim();
        if (decoded.StartsWith("@ByteArray(", StringComparison.Ordinal)
            && decoded.EndsWith(')'))
        {
            decoded = decoded[11..^1];
        }

        if (decoded.Length >= 2 && decoded[0] == '"' && decoded[^1] == '"')
        {
            decoded = decoded[1..^1];
        }

        return decoded.Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static Mo2SourceCandidate CreateCandidate(
        string rootPath,
        string instanceName,
        string gameName,
        string profileName,
        Mo2SourceDefinition source,
        Mo2SourceCandidateReadiness readiness,
        IReadOnlyList<Mo2SourceDiscoveryEvidence> evidence,
        IEnumerable<Diagnostic> diagnostics)
    {
        return new Mo2SourceCandidate(
            CreateCandidateId(rootPath, profileName),
            gameName,
            source,
            readiness,
            evidence,
            diagnostics.ToList().AsReadOnly());
    }

    private static Mo2SourceDefinition CreateDefaultSource(
        string instanceName,
        string rootPath,
        string profileName)
    {
        return new Mo2SourceDefinition(
            instanceName,
            profileName,
            rootPath,
            Path.Combine(rootPath, "profiles", profileName),
            Path.Combine(rootPath, "mods"));
    }

    private static string CreateCandidateId(string rootPath, string profileName)
    {
        var canonical = $"{rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant()}\0{profileName.ToUpperInvariant()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return $"mo2-{hash}";
    }

    private static int ReadinessRank(Mo2SourceCandidateReadiness readiness)
    {
        return readiness switch
        {
            Mo2SourceCandidateReadiness.Ready => 0,
            Mo2SourceCandidateReadiness.ProfileSelectionRequired => 1,
            Mo2SourceCandidateReadiness.UnsupportedGame => 2,
            _ => 3
        };
    }

    private static string? TryGetDirectoryName(string? path)
    {
        var normalized = TryNormalizePath(path);
        return normalized is null ? null : Path.GetDirectoryName(normalized);
    }

    private static string GetDirectoryName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed) ?? trimmed;
    }

    private static string? TryNormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private sealed class RootProbe
    {
        public RootProbe(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public List<Mo2SourceDiscoveryEvidence> Evidence { get; } = new();
    }

    private sealed record IniDocument(
        IReadOnlyDictionary<(string Section, string Key), string> Values,
        bool IsMalformed)
    {
        public string? Get(string section, string key)
        {
            return Values.TryGetValue((section, key), out var value) ? DecodeMo2Value(value) : null;
        }
    }
}

public sealed class WindowsMo2DiscoveryEnvironment : IMo2DiscoveryEnvironment
{
    public string? LocalAppDataPath => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public IReadOnlyList<string> GetRunningModOrganizerExecutablePaths()
    {
        var paths = new List<string>();
        foreach (var process in Process.GetProcessesByName("ModOrganizer"))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(process.MainModule?.FileName))
                {
                    paths.Add(process.MainModule.FileName);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public string? GetLastUsedInstanceName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Mod Organizer Team\\Mod Organizer");
            if (key is null)
            {
                return null;
            }

            var values = new List<object?> { key.GetValue(null) };
            foreach (var name in key.GetValueNames())
            {
                values.Add(key.GetValue(name));
            }
            return values
                .OfType<string>()
                .Select(value => value.Trim())
                .FirstOrDefault(value => value.Length > 0);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public sealed class JsonMo2SourcePreferenceStore : IMo2SourcePreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonMo2SourcePreferenceStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModScope",
            "mo2-source.json"))
    {
    }

    public JsonMo2SourcePreferenceStore(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public Mo2SourcePreference? Read()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Mo2SourcePreference>(
                File.ReadAllText(_filePath),
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(Mo2SourcePreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The MO2 preference path must include a directory.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(preference, JsonOptions));
    }
}
