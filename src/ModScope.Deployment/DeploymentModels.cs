using ModScope.LocalKnowledge;

namespace ModScope.Deployment;

public enum DeploymentResultStatus
{
    Applied,
    Blocked,
    RecoveryRequired
}

public sealed record DeploymentEntryDraft(
    string ModKey,
    bool Enabled,
    int Order);

public sealed record DeploymentDraft(
    string ProfileName,
    IReadOnlyList<DeploymentEntryDraft> Entries);

public sealed record DeploymentDiagnostic(
    string Code,
    string Message,
    bool IsBlocking,
    string? TargetName = null);

public sealed record DeploymentModChange(
    string ModKey,
    bool BeforeEnabled,
    bool AfterEnabled,
    int BeforeOrder,
    int AfterOrder);

public sealed record DeploymentJunctionChange(
    string Action,
    string TargetName,
    string LinkPath,
    string? TargetPath,
    string? PreviousTargetPath,
    string? ModKey);

public sealed record ManagedJunctionState(
    string GameRootPath,
    string LinkPath,
    string TargetPath,
    string TargetName,
    string ModKey,
    string ProfileName);

public sealed record DeploymentManifest(
    IReadOnlyList<ManagedJunctionState> Junctions)
{
    public static DeploymentManifest Empty { get; } = new(Array.Empty<ManagedJunctionState>());
}

public sealed record DeploymentPlan(
    string PlanId,
    DeploymentDraft Draft,
    Mo2SourceDefinition Source,
    string ModListSha256,
    string SourceFingerprint,
    string GameFingerprint,
    bool ModListChanged,
    IReadOnlyList<DeploymentModChange> ModChanges,
    IReadOnlyList<DeploymentJunctionChange> JunctionChanges,
    IReadOnlyList<DeploymentDiagnostic> Diagnostics,
    DateTimeOffset CreatedAtUtc)
{
    public IReadOnlyList<ManagedJunctionState> NextManagedJunctions { get; init; } =
        Array.Empty<ManagedJunctionState>();

    public bool CanApply => Diagnostics.All(diagnostic => !diagnostic.IsBlocking);
}

public sealed record DeploymentResult(
    DeploymentResultStatus Status,
    string? PlanId,
    string Message,
    IReadOnlyList<DeploymentDiagnostic> Diagnostics);

public sealed record JunctionInspection(
    bool Exists,
    bool IsDirectory,
    bool IsReparsePoint,
    string? TargetPath);

public interface IJunctionOperator
{
    JunctionInspection Inspect(string path);

    void Create(string linkPath, string targetPath);

    void Remove(string linkPath);
}

public interface IProcessGate
{
    IReadOnlyList<string> GetBlockingProcesses();
}

public interface IDeploymentStateStore
{
    DeploymentManifest Read();

    void Write(DeploymentManifest manifest);
}

public interface IModDeploymentService
{
    DeploymentPlan Preview(
        Mo2SourceDefinition source,
        DeploymentDraft draft,
        CancellationToken cancellationToken = default);

    DeploymentResult Apply(
        DeploymentPlan plan,
        CancellationToken cancellationToken = default);
}
