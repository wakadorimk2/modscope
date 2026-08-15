using System.Text.Json;
using ModScope.Desktop.Contracts;
using Xunit;

namespace ModScope.Desktop.Contracts.Tests;

public sealed class InspectorConclusionContractTests
{
    [Fact]
    public void SerializesTheConclusionStateWithoutChangingContractVersion()
    {
        var state = new InspectorUiState(
            "mod-key",
            "SCore",
            "ready",
            "enabled",
            1,
            null,
            Array.Empty<ModFileUiState>(),
            Array.Empty<XmlFileUiState>(),
            Array.Empty<DiagnosticUiState>(),
            new SourceReferenceUiState("modDirectory", "mods/SCore"))
        {
            Conclusion = new InspectorConclusionUiState(
                "3.1.9.1528",
                "3.1.25.1615",
                "updateAvailable",
                "The observed release is newer.",
                "unknown",
                "No game compatibility evidence was observed.",
                "ambiguous",
                "Medium",
                "GitHub release list first visible release from GitHub.",
            new SourceReferenceUiState("modFile", "mods/SCore/ModInfo.xml"),
            new SourceReferenceUiState("webObservation", "web-session/github/Sperell/Sperell.Mods/releases"),
            "GitHub",
            "https://github.com/Sperell/Sperell.Mods/releases",
            DateTimeOffset.Parse("2026-08-15T00:00:00Z"))
            {
                Summary = "Newer SCore release found",
                CompatibilityTarget = "7DTD v3.1.0 (b14)",
                CompatibilityRelation = "GameVersion",
                CompatibilityEvidence = "Game Version: v3.1.0 (b14)",
                CompatibilitySourceSite = "GitHub",
                CompatibilityTargetUrl = "https://github.com/Sperell/Sperell.Mods/releases",
                CompatibilityObservedAtUtc = DateTimeOffset.Parse("2026-08-15T00:00:00Z"),
                CompatibilityDiagnostics = new[]
                {
                    new DiagnosticUiState(
                        "web.compatibility.conflict",
                        "Warning",
                        "Conflicting Web compatibility observations were preserved.")
                }
            }
        };

        var serialized = JsonSerializer.Serialize(state);

        Assert.Contains("LatestObservedVersion", serialized, StringComparison.Ordinal);
        Assert.Contains("updateAvailable", serialized, StringComparison.Ordinal);
        Assert.Contains("Medium", serialized, StringComparison.Ordinal);
        Assert.Contains("CompatibilityTarget", serialized, StringComparison.Ordinal);
        Assert.Contains("3.1.0 (b14)", serialized, StringComparison.Ordinal);
        Assert.Contains("web.compatibility.conflict", serialized, StringComparison.Ordinal);
    }
}
