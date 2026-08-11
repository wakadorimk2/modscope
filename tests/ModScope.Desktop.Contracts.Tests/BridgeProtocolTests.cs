using System.Text.Json;
using ModScope.Desktop.Contracts;
using Xunit;

namespace ModScope.Desktop.Contracts.Tests;

public sealed class BridgeProtocolTests
{
    [Fact]
    public void ParseCommandReadsCamelCaseEnvelope()
    {
        var envelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 1,
              "requestId": "request-1",
              "command": "browser.navigate",
              "payload": {
                "url": "https://example.test/mod"
              }
            }
            """);

        var payload = BridgeProtocol.ReadPayload<NavigatePayload>(envelope.Payload);

        Assert.Equal(1, envelope.ContractVersion);
        Assert.Equal("request-1", envelope.RequestId);
        Assert.Equal("browser.navigate", envelope.Command);
        Assert.Equal("https://example.test/mod", payload.Url);
    }

    [Fact]
    public void ParseCommandRejectsUnknownCommand()
    {
        var exception = Assert.Throws<BridgeProtocolException>(() =>
            BridgeProtocol.ParseCommand(
                """
                {
                  "contractVersion": 1,
                  "requestId": "request-1",
                  "command": "local.write",
                  "payload": {}
                }
                """));

        Assert.Contains("Unknown bridge command", exception.Message);
    }

    [Fact]
    public void ParseCommandRejectsUnsupportedVersion()
    {
        var exception = Assert.Throws<BridgeProtocolException>(() =>
            BridgeProtocol.ParseCommand(
                """
                {
                  "contractVersion": 2,
                  "requestId": "request-1",
                  "command": "browser.reload",
                  "payload": {}
                }
                """));

        Assert.Contains("Unsupported bridge contract version", exception.Message);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,unsafe")]
    [InlineData("https://appassets.modscope/index.html")]
    public void BrowserUriValidationRejectsUnsupportedNavigation(string value)
    {
        Assert.False(BridgeProtocol.TryGetSupportedBrowserUri(value, out _));
    }

    [Fact]
    public void SerializeMessageUsesContractVersionAndCamelCase()
    {
        var json = BridgeProtocol.SerializeMessage(
            "error",
            new BridgeErrorPayload("browser.url.invalid", "Invalid URL."),
            "request-2");
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("contractVersion").GetInt32());
        Assert.Equal("error", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal("request-2", document.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(
            "browser.url.invalid",
            document.RootElement.GetProperty("payload").GetProperty("code").GetString());
    }

    [Fact]
    public void ParsesProfileSwitchAndContextVisibilityCommands()
    {
        var profileEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 1,
              "requestId": "profile-1",
              "command": "knowledge.switchProfile",
              "payload": { "profileName": "Alternate" }
            }
            """);
        var profilePayload = BridgeProtocol.ReadPayload<SwitchProfilePayload>(profileEnvelope.Payload);

        var layoutEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 1,
              "requestId": "layout-1",
              "command": "layout.setContextVisible",
              "payload": { "visible": false }
            }
            """);
        var layoutPayload = BridgeProtocol.ReadPayload<SetContextVisiblePayload>(layoutEnvelope.Payload);

        Assert.Equal("Alternate", profilePayload.ProfileName);
        Assert.False(layoutPayload.Visible);
    }
}
