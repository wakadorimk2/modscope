using ModScope.Deployment;
using ModScope.Desktop.Contracts;
using ModScope.LocalKnowledge;
using ModScope.Query;

namespace ModScope.Desktop;

internal static class DesktopStateMapper
{
    public static UiState Map(
        BrowserUiState browser,
        PageObservation? observation,
        SourceDiscoveryReadModel? sourceDiscovery,
        string? selectedSourceCandidateId,
        KnowledgeSessionReadModel? session,
        IReadOnlyList<ModCandidateSummary> candidates,
        IReadOnlyList<ProfileSummaryReadModel> profiles,
        IdentityUiState identity,
        LocalContextReadModel? localContext,
        InspectorReadModel? inspector,
        AnalysisUiState analysis,
        LayoutUiState layout,
        string statusMessage,
        KnowledgeOperationUiState operation,
        IReadOnlyDictionary<string, string>? profileLoadStates = null,
        IReadOnlyList<ProfileEditEntryReadModel>? profileEditEntries = null,
        DeploymentPlan? deploymentPlan = null,
        string deploymentStatus = "idle",
        bool canLaunch = false,
        IReadOnlyList<DeploymentDiagnostic>? deploymentDiagnostics = null,
        VersionObservationReadModel? sessionWebVersionObservation = null,
        IReadOnlyList<CompatibilityObservationReadModel>? sessionWebCompatibilityObservations = null,
        IReadOnlyList<DiagnosticReadModel>? sessionWebCompatibilityDiagnostics = null)
    {
        return new UiState(
            browser,
            observation is null ? null : PageObservation(observation),
            sourceDiscovery is null
                ? new SourceDiscoveryUiState(Array.Empty<SourceCandidateUiState>(), selectedSourceCandidateId)
                : SourceDiscovery(sourceDiscovery, selectedSourceCandidateId),
            new KnowledgeUiState(
                session is null ? null : KnowledgeSession(session),
                candidates.Select(Candidate).ToList().AsReadOnly(),
                profiles
                    .Select(profile => Profile(profile, profileLoadStates))
                    .ToList()
                    .AsReadOnly(),
                operation),
            identity,
            localContext is null ? null : LocalContext(localContext),
            inspector is null
                ? null
                : Inspector(
                    inspector,
                    identity,
                    localContext,
                    sessionWebVersionObservation,
                    sessionWebCompatibilityObservations ?? Array.Empty<CompatibilityObservationReadModel>(),
                    sessionWebCompatibilityDiagnostics ?? Array.Empty<DiagnosticReadModel>()),
            analysis,
            Deployment(
                session?.ProfileName ?? string.Empty,
                profileEditEntries ?? Array.Empty<ProfileEditEntryReadModel>(),
                deploymentPlan,
                deploymentStatus,
                canLaunch,
                deploymentDiagnostics ?? Array.Empty<DeploymentDiagnostic>()),
            layout,
            statusMessage,
            session is null ? Array.Empty<DiagnosticUiState>() : Diagnostics(session.Diagnostics));
    }

    public static AnalysisUiState MapAnalysis(
        ConflictAnalysisReadModel? conflict,
        RuntimeEvidenceComparisonReadModel? runtimeComparison,
        bool baseDataReady,
        bool runtimeLogsReady,
        string baseDataStatus,
        AnalysisOperationUiState operation,
        IReadOnlyList<DiagnosticUiState> diagnostics)
    {
        return new AnalysisUiState(
            new AnalysisInputUiState(baseDataReady, runtimeLogsReady, baseDataStatus),
            conflict is null ? null : ConflictAnalysis(conflict),
            runtimeComparison is null ? null : RuntimeEvidenceComparison(runtimeComparison),
            operation,
            diagnostics);
    }

    internal static LocalModMatchUiState LocalModMatch(LocalModMatchReadModel value)
    {
        return new LocalModMatchUiState(
            value.ModKey,
            value.DirectoryName,
            value.DisplayName,
            EnumText(value.ProfileState),
            EnumText(value.EnabledState),
            EnumText(value.MatchKind),
            EnumText(value.Strength),
            value.Evidence,
            value.AutoConfirmEligible);
    }

    private static SourceDiscoveryUiState SourceDiscovery(
        SourceDiscoveryReadModel value,
        string? selectedCandidateId)
    {
        return new SourceDiscoveryUiState(
            value.Candidates.Select(SourceCandidate).ToList().AsReadOnly(),
            selectedCandidateId);
    }

    private static SourceCandidateUiState SourceCandidate(Mo2SourceCandidateReadModel value)
    {
        return new SourceCandidateUiState(
            value.CandidateId,
            value.InstanceName,
            value.GameName,
            value.ProfileName,
            value.Readiness,
            value.IsReady,
            value.GameTargetReady,
            value.Evidence,
            Diagnostics(value.Diagnostics));
    }

    private static DeploymentUiState Deployment(
        string profileName,
        IReadOnlyList<ProfileEditEntryReadModel> profileEntries,
        DeploymentPlan? plan,
        string status,
        bool canLaunch,
        IReadOnlyList<DeploymentDiagnostic> extraDiagnostics)
    {
        var entries = profileEntries
            .Select(entry => new DeploymentEntryUiState(
                entry.EntryId,
                entry.ModKey,
                string.Equals(entry.EnabledState, nameof(ModEnabledState.Enabled), StringComparison.OrdinalIgnoreCase),
                entry.Priority,
                entry.IsSeparator,
                !entry.IsSeparator))
            .ToList()
            .AsReadOnly();
        var diagnostics = extraDiagnostics
            .Select(ToDeploymentDiagnostic)
            .ToList();
        if (plan is null)
        {
            return new DeploymentUiState(
                status,
                profileName,
                entries,
                null,
                false,
                canLaunch,
                Array.Empty<DeploymentModChangeUiState>(),
                Array.Empty<DeploymentJunctionChangeUiState>(),
                diagnostics.AsReadOnly());
        }

        diagnostics.AddRange(plan.Diagnostics.Select(ToDeploymentDiagnostic));
        var mappedStatus = status is "idle" or "preview-ready" or "blocked"
            ? (plan.CanApply ? "preview-ready" : "blocked")
            : status;
        return new DeploymentUiState(
            mappedStatus,
            profileName,
            entries,
            plan.PlanId,
            plan.CanApply,
            canLaunch,
            plan.ModChanges
                .Select(change => new DeploymentModChangeUiState(
                    change.ModKey,
                    change.BeforeEnabled,
                    change.AfterEnabled,
                    change.BeforeOrder,
                    change.AfterOrder))
                .ToList()
                .AsReadOnly(),
            plan.JunctionChanges
                .Select(change => new DeploymentJunctionChangeUiState(
                    change.Action,
                    change.TargetName))
                .ToList()
                .AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    private static DiagnosticUiState ToDeploymentDiagnostic(DeploymentDiagnostic diagnostic)
    {
        return new DiagnosticUiState(
            diagnostic.Code,
            diagnostic.IsBlocking ? "error" : "warning",
            diagnostic.Message,
            null,
            diagnostic.TargetName);
    }

    private static PageObservationUiState PageObservation(PageObservation value)
    {
        return new PageObservationUiState(
            value.Url.ToString(),
            value.Title,
            value.ObservedAtUtc,
            value.Source,
            EnumText(value.ExtractionStatus),
            Diagnostics(value.Diagnostics));
    }

    private static KnowledgeSessionUiState KnowledgeSession(KnowledgeSessionReadModel value)
    {
        return new KnowledgeSessionUiState(
            value.SnapshotId,
            value.InstanceName,
            value.ProfileName,
            value.CreatedAtUtc,
            value.ParserVersion,
            value.SchemaVersion,
            Diagnostics(value.Diagnostics))
        {
            VersionEvidenceManifest = value.VersionEvidenceManifest is null
                ? null
                : new VersionEvidenceManifestUiState(
                    value.VersionEvidenceManifest.IsLoaded,
                    value.VersionEvidenceManifest.DisplayName,
                    value.VersionEvidenceManifest.Status,
                    Diagnostics(value.VersionEvidenceManifest.Diagnostics))
        };
    }

    private static ModCandidateUiState Candidate(ModCandidateSummary value)
    {
        return new ModCandidateUiState(
            value.ModKey,
            value.DirectoryName,
            value.DisplayName,
            value.Version,
            value.Website,
            EnumText(value.ProfileState),
            EnumText(value.EnabledState),
            value.Priority,
            Source(value.Source),
            value.PriorityEvidence is null ? null : Evidence(value.PriorityEvidence),
            Diagnostics(value.Diagnostics),
            Role(value.Role))
        {
            PackageRelation = value.PackageRelation is null ? null : PackageRelation(value.PackageRelation)
        };
    }

    private static ModRoleUiState Role(ModRoleReadModel value)
    {
        return new ModRoleUiState(
            EnumText(value.Role),
            EnumText(value.Assessment),
            value.Reason,
            value.Evidence
                .Select(evidence => new ModRoleEvidenceUiState(
                    EnumText(evidence.Kind),
                    evidence.Detail,
                    Source(evidence.Source)))
                .ToList()
                .AsReadOnly());
    }

    private static ProfileUiState Profile(
        ProfileSummaryReadModel value,
        IReadOnlyDictionary<string, string>? profileLoadStates)
    {
        var loadState = profileLoadStates is not null
            && profileLoadStates.TryGetValue(value.ProfileName, out var state)
            ? state
            : "ready";
        return new ProfileUiState(value.ProfileName, loadState);
    }

    private static LocalContextUiState LocalContext(LocalContextReadModel value)
    {
        return new LocalContextUiState(
            value.CandidateIdentity,
            EnumText(value.Status),
            value.InstanceName,
            value.ProfileName,
            value.LocalModKey,
            value.DirectoryName,
            EnumText(value.EnabledState),
            value.Priority,
            value.KnownVersion,
            value.Evidence.Select(Evidence).ToList().AsReadOnly(),
            value.Uncertainties,
            Diagnostics(value.Diagnostics));
    }

    private static InspectorUiState Inspector(
        InspectorReadModel value,
        IdentityUiState identity,
        LocalContextReadModel? localContext,
        VersionObservationReadModel? sessionWebVersionObservation,
        IReadOnlyList<CompatibilityObservationReadModel> sessionWebCompatibilityObservations,
        IReadOnlyList<DiagnosticReadModel> sessionWebCompatibilityDiagnostics)
    {
        var packageRelation = value.PackageRelation is null
            ? null
            : PackageRelation(value.PackageRelation, value.ModKey, sessionWebVersionObservation);
        var applicableCompatibilityObservations = sessionWebCompatibilityObservations
            .Where(observation => string.Equals(observation.OwnerKey, value.ModKey, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
        var applicableCompatibilityDiagnostics = sessionWebCompatibilityDiagnostics
            .ToList()
            .AsReadOnly();
        return new InspectorUiState(
            value.ModKey,
            value.DirectoryName,
            EnumText(value.ProfileState),
            EnumText(value.EnabledState),
            value.Priority,
            value.ModInfo is null ? null : ModInfo(value.ModInfo),
            value.Files.Select(File).ToList().AsReadOnly(),
            value.XmlFiles.Select(XmlFile).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source))
        {
            PackageRelation = packageRelation,
            CompatibilityObservations = applicableCompatibilityObservations
                .Select(CompatibilityObservation)
                .ToList()
                .AsReadOnly(),
            CompatibilityDiagnostics = Diagnostics(applicableCompatibilityDiagnostics),
            Conclusion = Conclusion(
                value,
                packageRelation,
                identity,
                localContext,
                applicableCompatibilityObservations,
                applicableCompatibilityDiagnostics)
        };
    }

    private static ModInfoUiState ModInfo(ModInfoReadModel value)
    {
        return new ModInfoUiState(
            value.RelativePath,
            EnumText(value.ParseStatus),
            value.Name,
            value.DisplayName,
            value.Version,
            value.Description,
            value.Author,
            value.Website,
            value.UnknownObservations.Select(RawObservation).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source));
    }

    private static ModFileUiState File(ModFileReadModel value)
    {
        return new ModFileUiState(
            value.RelativePath,
            value.Size,
            value.Sha256,
            Source(value.Source),
            EnumText(value.EvidenceKind));
    }

    private static XmlFileUiState XmlFile(XmlFileReadModel value)
    {
        return new XmlFileUiState(
            value.RelativePath,
            EnumText(value.ParseStatus),
            value.EncodingName,
            value.RootElementName,
            value.ElementCount,
            value.AttributeCount,
            value.XPathCandidates.Select(XPath).ToList().AsReadOnly(),
            value.RawObservations.Select(RawObservation).ToList().AsReadOnly(),
            value.PatchOperations.Select(PatchOperation).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source));
    }

    private static XmlXPathCandidateUiState XPath(XmlXPathCandidateReadModel value)
    {
        return new XmlXPathCandidateUiState(
            value.RawValue,
            value.ElementPath,
            Source(value.Source));
    }

    private static RawXmlObservationUiState RawObservation(RawXmlObservationReadModel value)
    {
        return new RawXmlObservationUiState(
            value.ElementPath,
            value.ElementName,
            value.Attributes
                .Select(attribute => new XmlAttributeObservationUiState(attribute.Name, attribute.Value))
                .ToList()
                .AsReadOnly(),
            value.InnerText,
            Source(value.Source),
            value.HasChildElements);
    }

    private static XmlReferenceCandidateUiState ReferenceCandidate(
        XmlReferenceCandidateReadModel value)
    {
        return new XmlReferenceCandidateUiState(
            value.RawValue,
            value.NormalizedValue,
            value.ElementPath,
            EnumText(value.EvidenceKind),
            Source(value.Source));
    }

    private static XmlPatchOperationUiState PatchOperation(XmlPatchOperationReadModel value)
    {
        return new XmlPatchOperationUiState(
            value.ElementPath,
            value.RawOperationName,
            value.NormalizedKind is null ? null : EnumText(value.NormalizedKind.Value),
            RawObservation(value.RawObservation),
            value.XPathCandidates.Select(XPath).ToList().AsReadOnly(),
            value.TargetXmlCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            value.EntityCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            value.PropertyCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            value.AttributeCandidates.Select(ReferenceCandidate).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            Source(value.Source));
    }

    private static PackageRelationUiState PackageRelation(
        PackageRelationReadModel value,
        string? modKey = null,
        VersionObservationReadModel? sessionWebVersionObservation = null)
    {
        var applicableSessionObservation = sessionWebVersionObservation is not null
            && string.Equals(sessionWebVersionObservation.OwnerKey, modKey, StringComparison.OrdinalIgnoreCase)
            ? new[] { sessionWebVersionObservation }
            : Array.Empty<VersionObservationReadModel>();
        var observations = value.VersionObservations
            .Concat(applicableSessionObservation)
            .ToList()
            .AsReadOnly();
        return new PackageRelationUiState(
            value.PackageDirectoryName,
            value.ModletCount,
            value.SharedAcrossModlets,
            EnumText(value.IdentityState),
            value.IdentityReason,
            value.MetadataStatus,
            value.PackageModId,
            value.PackageFileId,
            value.PackageVersion,
            Source(value.PackageSource),
            value.SourceArtifacts.Select(artifact => new SourceArtifactUiState(
                    artifact.ArtifactId,
                    artifact.Kind,
                    artifact.Name,
                    artifact.ModId,
                    artifact.FileId,
                    artifact.SourceUrl,
                    Source(artifact.Source)))
                .ToList()
                .AsReadOnly(),
            observations.Select(VersionObservation).ToList().AsReadOnly(),
            VersionComparison(value, observations),
            Diagnostics(value.Diagnostics));
    }

    private static VersionObservationUiState VersionObservation(VersionObservationReadModel value)
    {
        return new VersionObservationUiState(
            value.OwnerKey,
            value.Role,
            value.SourceKind,
            value.RawValue,
            value.NormalizedValue,
            EnumText(value.Scheme),
            Source(value.Source),
            value.ObservedAtUtc,
            Diagnostics(value.Diagnostics))
        {
            SourceSite = value.SourceSite,
            TargetUrl = value.TargetUrl,
            Evidence = value.Evidence,
            ReleaseScopeKind = value.ReleaseScopeKind,
            ReleaseScopeRawVersion = value.ReleaseScopeRawVersion,
            ReleaseScopeVersion = value.ReleaseScopeVersion,
            ReleaseScopeUrl = value.ReleaseScopeUrl,
            ReleaseScopeMatchedLine = value.ReleaseScopeMatchedLine
        };
    }

    private static CompatibilityObservationUiState CompatibilityObservation(
        CompatibilityObservationReadModel value)
    {
        return new CompatibilityObservationUiState(
            value.OwnerKey,
            value.Relation,
            value.GameContext,
            value.RawValue,
            value.NormalizedValue,
            value.Build,
            value.MatchedLine,
            Source(value.Source),
            value.ObservedAtUtc,
            Diagnostics(value.Diagnostics))
        {
            SourceSite = value.SourceSite,
            TargetUrl = value.TargetUrl,
            ReleaseScopeKind = value.ReleaseScopeKind,
            ReleaseScopeRawVersion = value.ReleaseScopeRawVersion,
            ReleaseScopeVersion = value.ReleaseScopeVersion,
            ReleaseScopeUrl = value.ReleaseScopeUrl,
            ReleaseScopeMatchedLine = value.ReleaseScopeMatchedLine
        };
    }

    private static InspectorConclusionUiState Conclusion(
        InspectorReadModel inspector,
        PackageRelationUiState? packageRelation,
        IdentityUiState identity,
        LocalContextReadModel? localContext,
        IReadOnlyList<CompatibilityObservationReadModel> compatibilityObservations,
        IReadOnlyList<DiagnosticReadModel> compatibilityDiagnostics)
    {
        var identityState = packageRelation?.IdentityState ?? "unresolved";
        var identityConfidence = identityState switch
        {
            "exact" => "High",
            "ambiguous" => "Medium",
            "conflicting" => "Low",
            _ => "Unknown"
        };
        var observations = packageRelation?.VersionObservations
            ?? Array.Empty<VersionObservationUiState>();
        var latest = observations
            .Where(observation => string.Equals(observation.SourceKind, "WebObservation", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(observation => observation.ObservedAtUtc)
            .FirstOrDefault();
        var installedVersion = inspector.ModInfo?.Version;
        var installedSource = string.IsNullOrWhiteSpace(installedVersion)
            ? null
            : Source(inspector.ModInfo!.Source);
        var latestVersion = latest?.RawValue;
        var releaseAssociation = DetermineReleaseAssociation(
            inspector,
            identity,
            localContext,
            latest,
            packageRelation);
        var versionStatus = "notAssessed";
        var versionReason = "No GitHub Releases or Nexus Files version was observed in this session.";

        if (packageRelation is null)
        {
            versionReason = "Package identity and version evidence are not available.";
        }
        else if (!releaseAssociation.IsConfirmed)
        {
            versionReason = releaseAssociation.Reason;
        }
        else if (latest is not null && latest.RawValue is null)
        {
            var unsupported = latest.Diagnostics.Any(diagnostic =>
                string.Equals(diagnostic.Code, "web.version.unsupported-format", StringComparison.OrdinalIgnoreCase));
            versionStatus = unsupported ? "notComparable" : "notAssessed";
            versionReason = unsupported
                ? "The source exposed a version value, but its scheme is not comparable."
                : latest.Evidence ?? "The source did not expose one confirmed latest version.";
        }
        else if (latest is null || string.IsNullOrWhiteSpace(latest.RawValue))
        {
            versionReason = "No confirmed latest version is available from the current release surface.";
        }
        else if (string.IsNullOrWhiteSpace(installedVersion))
        {
            versionReason = "Installed ModInfo.xml version is not available.";
        }
        else
        {
            var installedNormalized = ModScope.LocalKnowledge.VersionNormalizer.Normalize(
                installedVersion,
                out var installedScheme);
            var latestNormalized = latest.NormalizedValue ?? latest.RawValue;
            var latestScheme = latest.Scheme switch
            {
                "semver" => ModScope.LocalKnowledge.VersionScheme.Semver,
                "numericDotted" => ModScope.LocalKnowledge.VersionScheme.NumericDotted,
                _ => ModScope.LocalKnowledge.VersionScheme.Unknown
            };
            if (installedNormalized is null
                || latestNormalized is null
                || installedScheme != latestScheme
                || installedScheme is not (ModScope.LocalKnowledge.VersionScheme.Semver or ModScope.LocalKnowledge.VersionScheme.NumericDotted)
                || !ModScope.LocalKnowledge.VersionComparator.TryCompareNormalized(
                    installedNormalized,
                    latestNormalized,
                    installedScheme,
                    out var comparison))
            {
                versionStatus = "notComparable";
                versionReason = "Installed and latest versions do not use one supported comparable scheme.";
            }
            else
            {
                versionStatus = comparison switch
                {
                    < 0 => "updateAvailable",
                    0 => "upToDate",
                    _ => "installedNewer"
                };
                versionReason = comparison switch
                {
                    < 0 => "The observed release is newer than the installed ModInfo.xml version.",
                    0 => "The installed ModInfo.xml version matches the observed release.",
                    _ => "The installed ModInfo.xml version is newer than the observed release."
                };
            }
        }

        var compatibility = BuildCompatibilityConclusion(
            compatibilityObservations,
            compatibilityDiagnostics,
            latest);
        var summary = versionStatus switch
        {
            "updateAvailable" => "Update available",
            "upToDate" => "Up to date",
            "installedNewer" => "Installed newer",
            _ => "Release comparison not assessed"
        };
        var why = latest is null
            ? versionReason
            : latest.RawValue is null
                ? latest.Evidence ?? versionReason
                : $"{latest.Evidence ?? "Release version observed"} from {latest.SourceSite ?? "the current Web page"}.";
        return new InspectorConclusionUiState(
            installedVersion,
            latestVersion,
            versionStatus,
            versionReason,
            compatibility.Status,
            compatibility.Reason,
            identityState,
            identityConfidence,
            why,
            installedSource,
            latest?.Source,
            latest?.SourceSite,
            latest?.TargetUrl,
            latest?.ObservedAtUtc)
        {
            Summary = summary,
            CompatibilityTarget = compatibility.Target,
            CompatibilityRelation = compatibility.Relation,
            CompatibilityEvidence = compatibility.Evidence,
            CompatibilityCondition = compatibility.Condition,
            CompatibilitySource = compatibility.Source,
            CompatibilitySourceSite = compatibility.SourceSite,
            CompatibilityTargetUrl = compatibility.TargetUrl,
            CompatibilityObservedAtUtc = compatibility.ObservedAtUtc,
            CompatibilityDiagnostics = compatibility.Diagnostics,
            ReleaseAssociationStatus = releaseAssociation.Status,
            ReleaseAssociationReason = releaseAssociation.Reason,
            ReleaseAssociationEvidence = releaseAssociation.Evidence,
            SelectedLatestReleaseScopeKind = latest?.ReleaseScopeKind,
            SelectedLatestReleaseScopeRawVersion = latest?.ReleaseScopeRawVersion,
            SelectedLatestReleaseScopeVersion = latest?.ReleaseScopeVersion,
            SelectedLatestReleaseUrl = latest?.ReleaseScopeUrl,
            SelectedLatestReleaseScopeLine = latest?.ReleaseScopeMatchedLine
        };
    }

    private static ReleaseAssociationResult DetermineReleaseAssociation(
        InspectorReadModel inspector,
        IdentityUiState identity,
        LocalContextReadModel? localContext,
        VersionObservationUiState? latest,
        PackageRelationUiState? packageRelation)
    {
        if (identity.RecognitionStatus is not ("auto-confirmed" or "manual-confirmed"))
        {
            return ReleaseAssociationResult.NotAssessed("Page identity is not confirmed.");
        }

        if (string.IsNullOrWhiteSpace(identity.SelectedLocalModKey)
            || !string.Equals(identity.SelectedLocalModKey, inspector.ModKey, StringComparison.OrdinalIgnoreCase))
        {
            return ReleaseAssociationResult.NotAssessed("The selected local MOD does not match the Inspector MOD.");
        }

        if (localContext is null
            || !string.Equals(localContext.Status.ToString(), "Installed", StringComparison.OrdinalIgnoreCase))
        {
            return ReleaseAssociationResult.NotAssessed("The current local context is not installed.");
        }

        if (packageRelation is null)
        {
            return ReleaseAssociationResult.NotAssessed("Package relation evidence is not available.");
        }

        if (latest is null
            || !string.Equals(latest.SourceKind, "WebObservation", StringComparison.OrdinalIgnoreCase)
            || (!string.Equals(latest.SourceSite, "GitHub", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(latest.SourceSite, "Nexus", StringComparison.OrdinalIgnoreCase)))
        {
            return ReleaseAssociationResult.NotAssessed("An automatic Web release observation is not available.");
        }

        if (!IsReleaseScope(latest))
        {
            return ReleaseAssociationResult.NotAssessed("The latest Web release scope is unresolved.");
        }

        var evidence = string.IsNullOrWhiteSpace(latest.ReleaseScopeMatchedLine)
            ? $"{latest.ReleaseScopeKind} {latest.ReleaseScopeVersion}"
            : $"{latest.ReleaseScopeKind} {latest.ReleaseScopeVersion} · {latest.ReleaseScopeMatchedLine}";
        return new ReleaseAssociationResult(
            true,
            "confirmed",
            $"Confirmed for {inspector.ModKey}",
            evidence);
    }

    private static CompatibilityConclusionResult BuildCompatibilityConclusion(
        IReadOnlyList<CompatibilityObservationReadModel> observations,
        IReadOnlyList<DiagnosticReadModel> diagnostics,
        VersionObservationUiState? latest)
    {
        var scopedObservations = SelectCompatibilityScope(
            observations,
            latest);
        var positive = scopedObservations
            .Where(observation => IsPositiveCompatibilityRelation(observation.Relation))
            .Where(observation => string.Equals(observation.GameContext, "7DTD", StringComparison.OrdinalIgnoreCase))
            .Where(observation => !string.IsNullOrWhiteSpace(observation.NormalizedValue))
            .ToList();
        var targetGroups = positive
            .GroupBy(
                observation => string.Join(
                    "|",
                    observation.GameContext,
                    observation.NormalizedValue,
                    observation.Build ?? string.Empty),
                StringComparer.OrdinalIgnoreCase)
            .ToList();
        var conditions = scopedObservations
            .Where(observation => string.Equals(
                observation.Relation,
                nameof(WebCompatibilityRelation.RequiresGameVersion),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var uiDiagnostics = diagnostics.Select(Diagnostic).ToList();

        if (targetGroups.Count == 1)
        {
            var selected = targetGroups[0].First();
            return new CompatibilityConclusionResult(
                "observed",
                "One explicit 7DTD compatibility target was observed. This is Web evidence, not a runtime compatibility guarantee.",
                FormatCompatibilityTarget(selected),
                selected.Relation,
                selected.MatchedLine,
                conditions.Count == 0 ? null : string.Join(" · ", conditions.Select(condition => condition.MatchedLine)),
                Source(selected.Source),
                selected.SourceSite,
                selected.TargetUrl,
                selected.ObservedAtUtc,
                uiDiagnostics.AsReadOnly(),
                selected.ReleaseScopeKind,
                selected.ReleaseScopeRawVersion,
                selected.ReleaseScopeVersion,
                selected.ReleaseScopeUrl,
                selected.ReleaseScopeMatchedLine);
        }

        if (targetGroups.Count > 1)
        {
            var firstConflictObservation = positive[0];
            uiDiagnostics.Add(new DiagnosticUiState(
                "web.compatibility.conflict",
                "Warning",
                "Conflicting Web compatibility observations were preserved. No winner was selected."));
            return new CompatibilityConclusionResult(
                "unknown",
                "Conflicting Web compatibility observations",
                null,
                null,
                null,
                conditions.Count == 0 ? null : string.Join(" · ", conditions.Select(condition => condition.MatchedLine)),
                Source(firstConflictObservation.Source),
                firstConflictObservation.SourceSite,
                firstConflictObservation.TargetUrl,
                firstConflictObservation.ObservedAtUtc,
                uiDiagnostics.AsReadOnly(),
                firstConflictObservation.ReleaseScopeKind,
                firstConflictObservation.ReleaseScopeRawVersion,
                firstConflictObservation.ReleaseScopeVersion,
                firstConflictObservation.ReleaseScopeUrl,
                firstConflictObservation.ReleaseScopeMatchedLine);
        }

        if (conditions.Count > 0)
        {
            var condition = conditions[0];
            return new CompatibilityConclusionResult(
                "unknown",
                "Only a game-version requirement was observed; compatibility was not asserted.",
                null,
                null,
                null,
                string.Join(" · ", conditions.Select(condition => condition.MatchedLine)),
                Source(condition.Source),
                condition.SourceSite,
                condition.TargetUrl,
                condition.ObservedAtUtc,
                uiDiagnostics.AsReadOnly(),
                condition.ReleaseScopeKind,
                condition.ReleaseScopeRawVersion,
                condition.ReleaseScopeVersion,
                condition.ReleaseScopeUrl,
                condition.ReleaseScopeMatchedLine);
        }

        var reason = observations.Count > 0 && scopedObservations.Count == 0
            ? "No compatibility evidence was observed in the selected latest release scope. Earlier scope evidence remains in history."
            : observations.Count > 0
                ? "No supported 7DTD compatibility target was observed. Raw Web evidence remains available below."
            : "No Web compatibility evidence was observed.";
        var firstObservation = scopedObservations.FirstOrDefault();
        firstObservation ??= observations.FirstOrDefault();
        return new CompatibilityConclusionResult(
            "unknown",
            reason,
            null,
            null,
            null,
            null,
            firstObservation is null ? null : Source(firstObservation.Source),
            firstObservation?.SourceSite,
            firstObservation?.TargetUrl,
            firstObservation?.ObservedAtUtc,
            uiDiagnostics.AsReadOnly(),
            firstObservation?.ReleaseScopeKind,
            firstObservation?.ReleaseScopeRawVersion,
            firstObservation?.ReleaseScopeVersion,
            firstObservation?.ReleaseScopeUrl,
            firstObservation?.ReleaseScopeMatchedLine);
    }

    private static IReadOnlyList<CompatibilityObservationReadModel> SelectCompatibilityScope(
        IReadOnlyList<CompatibilityObservationReadModel> observations,
        VersionObservationUiState? latest)
    {
        var latestScope = IsReleaseScope(latest)
            ? new CompatibilityScopeKey(
                latest!.ReleaseScopeKind!,
                latest.ReleaseScopeVersion!,
                latest.ReleaseScopeUrl)
            : null;

        if (latestScope is not null)
        {
            return observations
                .Where(observation => latestScope.Matches(observation))
                .ToList()
                .AsReadOnly();
        }

        var pageScope = observations
            .FirstOrDefault(observation =>
                string.Equals(observation.ReleaseScopeKind, "Page", StringComparison.OrdinalIgnoreCase));
        if (pageScope is null)
        {
            return Array.Empty<CompatibilityObservationReadModel>();
        }

        return observations
            .Where(observation =>
                string.Equals(observation.ReleaseScopeKind, "Page", StringComparison.OrdinalIgnoreCase)
                && string.Equals(observation.ReleaseScopeUrl, pageScope.ReleaseScopeUrl, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    private static bool IsReleaseScope(VersionObservationUiState? observation)
    {
        return observation is not null
            && (string.Equals(observation.ReleaseScopeKind, "GitHubRelease", StringComparison.OrdinalIgnoreCase)
                || string.Equals(observation.ReleaseScopeKind, "NexusFile", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(observation.ReleaseScopeVersion)
            && !string.IsNullOrWhiteSpace(observation.ReleaseScopeUrl);
    }

    private static bool IsReleaseScope(CompatibilityObservationReadModel observation)
    {
        return (string.Equals(observation.ReleaseScopeKind, "GitHubRelease", StringComparison.OrdinalIgnoreCase)
                || string.Equals(observation.ReleaseScopeKind, "NexusFile", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(observation.ReleaseScopeVersion)
            && !string.IsNullOrWhiteSpace(observation.ReleaseScopeUrl);
    }

    private static bool IsPositiveCompatibilityRelation(string relation)
    {
        return relation.Equals(nameof(WebCompatibilityRelation.GameVersion), StringComparison.OrdinalIgnoreCase)
            || relation.Equals(nameof(WebCompatibilityRelation.SupportedGameVersion), StringComparison.OrdinalIgnoreCase)
            || relation.Equals(nameof(WebCompatibilityRelation.SupportedFor), StringComparison.OrdinalIgnoreCase)
            || relation.Equals(nameof(WebCompatibilityRelation.CompatibleWith), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatCompatibilityTarget(CompatibilityObservationReadModel observation)
    {
        var target = $"{observation.GameContext} v{observation.NormalizedValue}";
        return string.IsNullOrWhiteSpace(observation.Build)
            ? target
            : $"{target} ({observation.Build})";
    }

    private sealed record CompatibilityConclusionResult(
        string Status,
        string Reason,
        string? Target,
        string? Relation,
        string? Evidence,
        string? Condition,
        SourceReferenceUiState? Source,
        string? SourceSite,
        string? TargetUrl,
        DateTimeOffset? ObservedAtUtc,
        IReadOnlyList<DiagnosticUiState> Diagnostics,
        string? ReleaseScopeKind,
        string? ReleaseScopeRawVersion,
        string? ReleaseScopeVersion,
        string? ReleaseScopeUrl,
        string? ReleaseScopeMatchedLine);

    private sealed record ReleaseAssociationResult(
        bool IsConfirmed,
        string Status,
        string Reason,
        string? Evidence)
    {
        public static ReleaseAssociationResult NotAssessed(string reason)
        {
            return new ReleaseAssociationResult(false, "not-assessed", reason, null);
        }
    }

    private sealed record CompatibilityScopeKey(
        string Kind,
        string Version,
        string? Url)
    {
        public bool Matches(CompatibilityObservationReadModel observation)
        {
            return string.Equals(observation.ReleaseScopeKind, Kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(observation.ReleaseScopeVersion, Version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(observation.ReleaseScopeUrl, Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static VersionComparisonUiState VersionComparison(
        PackageRelationReadModel package,
        IReadOnlyList<VersionObservationReadModel> observations)
    {
        var status = package.Comparison.Status;
        var reason = package.Comparison.Reason;
        if (observations.Count != package.VersionObservations.Count)
        {
            if (package.IdentityState != QueryIdentityResolutionState.Exact)
            {
                status = QueryVersionComparisonStatus.NotAssessed;
                reason = "Identity is not exact, so the session Web observation was not assessed.";
            }
            else
            {
                var comparable = observations
                    .Where(observation => !string.IsNullOrWhiteSpace(observation.NormalizedValue))
                    .ToList();
                var schemes = comparable.Select(observation => observation.Scheme).Distinct().ToList();
                if (comparable.Count < 2)
                {
                    status = QueryVersionComparisonStatus.NotComparable;
                    reason = "At least two version observations are required.";
                }
                else if (schemes.Count != 1
                    || schemes[0] is not (QueryVersionScheme.Semver or QueryVersionScheme.NumericDotted))
                {
                    status = QueryVersionComparisonStatus.NotComparable;
                    reason = "The observations do not use one supported version scheme.";
                }
                else
                {
                    var first = comparable[0].NormalizedValue;
                    var equal = comparable.All(observation =>
                        string.Equals(observation.NormalizedValue, first, StringComparison.OrdinalIgnoreCase));
                    status = equal
                        ? QueryVersionComparisonStatus.Equal
                        : QueryVersionComparisonStatus.Mismatch;
                    reason = equal
                        ? "All supported observations have the same normalized value."
                        : "Supported observations have different normalized values.";
                }
            }
        }

        return new VersionComparisonUiState(
            EnumText(status),
            reason,
            observations.Select(VersionObservation).ToList().AsReadOnly());
    }

    private static BaseDataFileUiState BaseDataFile(BaseDataFileReadModel value)
    {
        return new BaseDataFileUiState(
            value.TargetXml,
            value.Size,
            value.Sha256,
            value.ParseStatus is null ? null : EnumText(value.ParseStatus.Value),
            Source(value.Source),
            Diagnostics(value.Diagnostics));
    }

    private static SemanticConflictOperationUiState SemanticConflictOperation(
        SemanticConflictOperationReadModel value)
    {
        return new SemanticConflictOperationUiState(
            value.OperationKey,
            value.ModKey,
            value.Priority,
            value.XmlFileRelativePath,
            value.ElementPath,
            value.RawOperationName,
            value.NormalizedKind is null ? null : EnumText(value.NormalizedKind.Value),
            value.TargetXml,
            value.XPath,
            value.AttributeName,
            value.Value,
            Source(value.Source),
            value.Evidence.Select(Evidence).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics),
            value.HasChildElements);
    }

    private static EffectiveChangeUiState EffectiveChange(EffectiveChangeReadModel value)
    {
        return new EffectiveChangeUiState(
            value.MatchPath,
            value.AttributeName,
            value.BeforeValue,
            value.AfterValue,
            value.ExistedBefore,
            value.ExistsAfter,
            Source(value.Source));
    }

    private static SemanticConflictGroupUiState SemanticConflictGroup(
        SemanticConflictGroupReadModel value)
    {
        return new SemanticConflictGroupUiState(
            value.TargetXml,
            value.XPath,
            EnumText(value.Assessment),
            EnumText(value.Confidence),
            EnumText(value.EffectiveStatus),
            value.Operations.Select(SemanticConflictOperation).ToList().AsReadOnly(),
            value.EffectiveChanges.Select(EffectiveChange).ToList().AsReadOnly(),
            value.Evidence.Select(Evidence).ToList().AsReadOnly(),
            value.Uncertainties,
            Diagnostics(value.Diagnostics));
    }

    private static ConflictAnalysisUiState ConflictAnalysis(ConflictAnalysisReadModel value)
    {
        return new ConflictAnalysisUiState(
            value.SnapshotId,
            value.InstanceName,
            value.ProfileName,
            value.BaseDataFiles.Select(BaseDataFile).ToList().AsReadOnly(),
            value.OperationGroups.Select(SemanticConflictGroup).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics));
    }

    private static RuntimeEvidenceObservationUiState RuntimeEvidenceObservation(
        RuntimeEvidenceObservationReadModel value)
    {
        return new RuntimeEvidenceObservationUiState(
            value.ModKey,
            value.TargetXml,
            value.XPath,
            value.ObservedOperation,
            value.ObservedCategory,
            value.NormalizedAssessment is null
                ? null
                : EnumText(value.NormalizedAssessment.Value),
            Diagnostics(value.Diagnostics));
    }

    private static RuntimeEvidenceUiState RuntimeEvidence(RuntimeEvidenceReadModel value)
    {
        return new RuntimeEvidenceUiState(
            value.SnapshotId,
            value.InstanceName,
            value.ProfileName,
            value.EvidenceSource,
            value.ToolVersion,
            value.GameVersion,
            value.CaptureTimeUtc,
            value.Results.Select(RuntimeEvidenceObservation).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics));
    }

    private static RuntimeEvidenceComparisonItemUiState RuntimeEvidenceComparisonItem(
        RuntimeEvidenceComparisonItemReadModel value)
    {
        return new RuntimeEvidenceComparisonItemUiState(
            value.TargetXml,
            value.XPath,
            EnumText(value.Status),
            value.StaticAssessment is null ? null : EnumText(value.StaticAssessment.Value),
            value.RuntimeAssessment is null ? null : EnumText(value.RuntimeAssessment.Value),
            value.Observations.Select(RuntimeEvidenceObservation).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics));
    }

    private static RuntimeEvidenceComparisonUiState RuntimeEvidenceComparison(
        RuntimeEvidenceComparisonReadModel value)
    {
        return new RuntimeEvidenceComparisonUiState(
            value.SnapshotId,
            value.InstanceName,
            value.ProfileName,
            RuntimeEvidence(value.RuntimeEvidence),
            value.Results.Select(RuntimeEvidenceComparisonItem).ToList().AsReadOnly(),
            Diagnostics(value.Diagnostics));
    }

    private static EvidenceReferenceUiState Evidence(EvidenceReferenceReadModel value)
    {
        return new EvidenceReferenceUiState(EnumText(value.Kind), Source(value.Source));
    }

    private static DiagnosticUiState Diagnostic(DiagnosticReadModel value)
    {
        return new DiagnosticUiState(
            value.Code,
            EnumText(value.Severity),
            value.Message,
            value.Source is null ? null : Source(value.Source),
            value.RawValue);
    }

    private static IReadOnlyList<DiagnosticUiState> Diagnostics(
        IReadOnlyList<DiagnosticReadModel> values)
    {
        return values.Select(Diagnostic).ToList().AsReadOnly();
    }

    private static SourceReferenceUiState Source(SourceReferenceReadModel value)
    {
        return new SourceReferenceUiState(
            EnumText(value.Kind),
            value.RelativePath,
            value.LineNumber,
            value.ColumnNumber);
    }

    private static string EnumText<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0
            ? string.Empty
            : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
