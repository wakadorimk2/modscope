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
        Assert.Equal(2, load.ContractVersion);
        Assert.Equal("knowledge.selectEvidenceManifest", manifest.Command);
        Assert.Equal("v1.2.3", BridgeProtocol.ReadPayload<SetWebVersionObservationPayload>(observation.Payload).RawValue);
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
    }
}
