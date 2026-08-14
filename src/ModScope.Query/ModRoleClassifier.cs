using ModScope.LocalKnowledge;

namespace ModScope.Query;

public static class ModRoleClassifier
{
    private static readonly string[] FoundationMarkers =
    {
        "api",
        "base",
        "core",
        "framework",
        "foundation",
        "library",
        "support"
    };

    private static readonly string[] CompatibilityMarkers =
    {
        "compat",
        "compatibility",
        "fix",
        "patch"
    };

    public static IReadOnlyDictionary<string, ModRoleReadModel> Classify(LocalModSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var modFiles = snapshot.Mods
            .SelectMany(mod => mod.XmlFiles.Select(xml => (
                Mod: mod,
                Xml: xml,
                FullPath: BuildModXmlPath(mod, xml.RelativePath))))
            .ToList();
        var knownXmlPaths = modFiles
            .ToDictionary(item => item.FullPath, item => item.Mod, StringComparer.OrdinalIgnoreCase);
        var targetEvidenceByModKey = BuildTargetEvidence(modFiles, knownXmlPaths);
        var result = new Dictionary<string, ModRoleReadModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in snapshot.Mods)
        {
            result[mod.ModKey] = ClassifyMod(
                mod,
                knownXmlPaths,
                targetEvidenceByModKey.TryGetValue(mod.ModKey, out var targetEvidence)
                    ? targetEvidence.AsReadOnly()
                    : Array.Empty<ModRoleEvidenceReadModel>());
        }

        return result;
    }

    private static ModRoleReadModel ClassifyMod(
        LocalModRecord mod,
        IReadOnlyDictionary<string, LocalModRecord> knownXmlPaths,
        IReadOnlyList<ModRoleEvidenceReadModel> targetEvidence)
    {
        var evidence = new List<ModRoleEvidenceReadModel>();
        var metadataText = string.Join(
            ' ',
            mod.ModInfo?.Name,
            mod.ModInfo?.DisplayName,
            mod.ModInfo?.Description,
            mod.DirectoryName)
            .ToLowerInvariant();

        var directCompatibility = mod.XmlFiles
            .SelectMany(xml => xml.PatchOperations)
            .SelectMany(operation => operation.TargetXmlCandidates)
            .Select(candidate => (Candidate: candidate, Target: ResolveLocalXml(candidate.NormalizedValue, knownXmlPaths)))
            .FirstOrDefault(item => item.Target is not null && !ReferenceEquals(item.Target, mod));

        if (directCompatibility.Target is not null)
        {
            evidence.Add(new ModRoleEvidenceReadModel(
                QueryEvidenceKind.StaticEvidence,
                $"Targets local MOD XML '{directCompatibility.Target.DirectoryName}'.",
                QueryProjection.Source(directCompatibility.Candidate.Source)));
            return new ModRoleReadModel(
                QueryModRole.Compatibility,
                QueryModRoleAssessment.Verified,
                "A patch operation targets another readable local MOD.",
                evidence.AsReadOnly());
        }

        var foundationMarker = FoundationMarkers.FirstOrDefault(metadataText.Contains);
        if (foundationMarker is not null && mod.ModInfo is not null)
        {
            evidence.Add(new ModRoleEvidenceReadModel(
                QueryEvidenceKind.Inference,
                $"Static MOD metadata contains foundation marker '{foundationMarker}'.",
                QueryProjection.Source(mod.ModInfo.Source)));
            return new ModRoleReadModel(
                QueryModRole.Foundation,
                QueryModRoleAssessment.Inferred,
                "Static metadata suggests a broad support role. This does not prove a dependency.",
                evidence.AsReadOnly());
        }

        if (targetEvidence.Count > 0)
        {
            return new ModRoleReadModel(
                QueryModRole.Foundation,
                QueryModRoleAssessment.Inferred,
                "Another readable local MOD targets this MOD XML. This suggests a base role but does not prove a dependency.",
                targetEvidence);
        }

        var compatibilityMarker = CompatibilityMarkers.FirstOrDefault(metadataText.Contains);
        if (compatibilityMarker is not null && mod.ModInfo is not null)
        {
            evidence.Add(new ModRoleEvidenceReadModel(
                QueryEvidenceKind.Inference,
                $"Static MOD metadata contains compatibility marker '{compatibilityMarker}'.",
                QueryProjection.Source(mod.ModInfo.Source)));
            return new ModRoleReadModel(
                QueryModRole.Compatibility,
                QueryModRoleAssessment.Inferred,
                "Static metadata suggests a compatibility role.",
                evidence.AsReadOnly());
        }

        var contentOperation = mod.XmlFiles
            .SelectMany(xml => xml.PatchOperations)
            .FirstOrDefault(operation => operation.NormalizedKind is
                XmlPatchOperationKind.Append or
                XmlPatchOperationKind.Prepend or
                XmlPatchOperationKind.InsertBefore or
                XmlPatchOperationKind.InsertAfter
                || operation.EntityCandidates.Count > 0);

        if (contentOperation is not null)
        {
            evidence.Add(new ModRoleEvidenceReadModel(
                QueryEvidenceKind.Inference,
                "Static XML patch operations add or insert content.",
                QueryProjection.Source(contentOperation.Source)));
            return new ModRoleReadModel(
                QueryModRole.Content,
                QueryModRoleAssessment.Inferred,
                "Static XML evidence suggests concrete content.",
                evidence.AsReadOnly());
        }

        if (mod.XmlFiles.SelectMany(xml => xml.PatchOperations).Any())
        {
            var operation = mod.XmlFiles
                .SelectMany(xml => xml.PatchOperations)
                .First();
            evidence.Add(new ModRoleEvidenceReadModel(
                QueryEvidenceKind.StaticEvidence,
                "The MOD contains readable XML patch operations, but their presentation role is not specific.",
                QueryProjection.Source(operation.Source)));
            return new ModRoleReadModel(
                QueryModRole.Unknown,
                QueryModRoleAssessment.Unknown,
                "Static evidence exists, but it does not identify a presentation role.",
                evidence.AsReadOnly());
        }

        return new ModRoleReadModel(
            QueryModRole.Unknown,
            QueryModRoleAssessment.Unknown,
            "No static role evidence was found.",
            Array.Empty<ModRoleEvidenceReadModel>());
    }

    private static Dictionary<string, List<ModRoleEvidenceReadModel>> BuildTargetEvidence(
        IReadOnlyList<(LocalModRecord Mod, XmlFileReference Xml, string FullPath)> modFiles,
        IReadOnlyDictionary<string, LocalModRecord> knownXmlPaths)
    {
        var result = new Dictionary<string, List<ModRoleEvidenceReadModel>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in modFiles)
        {
            foreach (var operation in item.Xml.PatchOperations)
            {
                foreach (var candidate in operation.TargetXmlCandidates)
                {
                    var target = ResolveLocalXml(candidate.NormalizedValue, knownXmlPaths);
                    if (target is null || string.Equals(target.ModKey, item.Mod.ModKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!result.TryGetValue(target.ModKey, out var evidence))
                    {
                        evidence = new List<ModRoleEvidenceReadModel>();
                        result[target.ModKey] = evidence;
                    }

                    evidence.Add(new ModRoleEvidenceReadModel(
                        QueryEvidenceKind.StaticEvidence,
                        $"Readable MOD '{item.Mod.DirectoryName}' targets this MOD XML.",
                        QueryProjection.Source(candidate.Source)));
                }
            }
        }

        return result;
    }

    private static LocalModRecord? ResolveLocalXml(
        string? value,
        IReadOnlyDictionary<string, LocalModRecord> knownXmlPaths)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/').TrimStart('/');
        if (knownXmlPaths.TryGetValue(normalized, out var exact))
        {
            return exact;
        }

        var suffix = "/" + normalized;
        return knownXmlPaths
            .Where(item => item.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Value)
            .FirstOrDefault();
    }

    private static string BuildModXmlPath(LocalModRecord mod, string relativePath)
    {
        var directory = mod.ResolvedDirectoryRelativePath ?? mod.DirectoryName;
        return $"mods/{directory}/{relativePath}".Replace('\\', '/');
    }
}
