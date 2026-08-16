using System.Text.Json;
using ModScope.Desktop.Contracts;
using Xunit;

namespace ModScope.Desktop.Contracts.Tests;

public sealed class VersionEvidenceContractTests
{
    [Fact]
    public void KeepsVersionEvidenceCommandsOnContractVersionTwoWithoutLocalFileContent()
    {
        var manifest = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "manifest-1",
              "command": "knowledge.selectEvidenceManifest",
              "payload": {}
            }
            """);
        var observation = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "web-version-1",
              "command": "knowledge.setWebVersionObservation",
              "payload": { "rawValue": "v1.2.3" }
            }
            """);
        var nexusObservation = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "nexus-version-1",
              "command": "knowledge.observeNexusFileVersion",
              "payload": {}
            }
            """);
        var load = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "load-1",
              "command": "knowledge.loadSource",
              "payload": { "candidateId": "mo2-candidate" }
            }
            """);

        Assert.Equal(2, manifest.ContractVersion);
        Assert.Equal(2, observation.ContractVersion);
        Assert.Equal(2, nexusObservation.ContractVersion);
        Assert.Equal(2, load.ContractVersion);
        Assert.Equal("knowledge.selectEvidenceManifest", manifest.Command);
        Assert.Equal("knowledge.observeNexusFileVersion", nexusObservation.Command);
        Assert.Equal("v1.2.3", BridgeProtocol.ReadPayload<SetWebVersionObservationPayload>(observation.Payload).RawValue);
        _ = BridgeProtocol.ReadPayload<ObserveNexusFileVersionPayload>(nexusObservation.Payload);
        Assert.Equal("mo2-candidate", BridgeProtocol.ReadPayload<LoadSourcePayload>(load.Payload).CandidateId);

        var serialized = JsonSerializer.Serialize(
            new
            {
                Manifest = new VersionEvidenceManifestUiState(
                    true,
                    "release-evidence.json",
                    "loaded",
                    Array.Empty<DiagnosticUiState>()),
                Load = BridgeProtocol.ReadPayload<LoadSourcePayload>(load.Payload)
            });
        Assert.DoesNotContain("manifestPath", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\\\", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-local-content", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", JsonSerializer.Serialize(BridgeProtocol.ReadPayload<ObserveNexusFileVersionPayload>(nexusObservation.Payload)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializesNexusModPageVersionScopeInTheExistingContractFields()
    {
        var observation = new VersionObservationUiState(
            "Quartz",
            "Release",
            "WebObservation",
            "v8.0.1",
            "8.0.1",
            "semver",
            new SourceReferenceUiState("webObservation", "web-session/nexus/mods/2409"),
            DateTimeOffset.UnixEpoch,
            Array.Empty<DiagnosticUiState>())
        {
            SourceSite = "Nexus",
            TargetUrl = "https://www.nexusmods.com/7daystodie/mods/2409",
            Evidence = "Nexus mod page visible Version label",
            ReleaseScopeKind = "NexusModPage",
            ReleaseScopeRawVersion = "v8.0.1",
            ReleaseScopeVersion = "8.0.1",
            ReleaseScopeUrl = "https://www.nexusmods.com/7daystodie/mods/2409",
            ReleaseScopeMatchedLine = "Version v8.0.1"
        };

        var json = JsonSerializer.Serialize(observation, BridgeProtocol.JsonOptions);

        Assert.Contains("\"releaseScopeKind\":\"NexusModPage\"", json);
        Assert.Contains("\"releaseScopeRawVersion\":\"v8.0.1\"", json);
        Assert.Contains("\"releaseScopeVersion\":\"8.0.1\"", json);
        Assert.Contains("\"releaseScopeMatchedLine\":\"Version v8.0.1\"", json);
    }
}
