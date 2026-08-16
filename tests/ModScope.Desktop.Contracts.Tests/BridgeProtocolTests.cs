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
              "contractVersion": 2,
              "requestId": "request-1",
              "command": "browser.navigate",
              "payload": {
                "url": "https://www.nexusmods.com/7daystodie/search/?gsearch=Alpha",
                "nexusSearchName": "Alpha Mod",
                "nexusSearchNames": ["Alpha Mod", "Alpha Directory"]
              }
            }
            """);

        var payload = BridgeProtocol.ReadPayload<NavigatePayload>(envelope.Payload);

        Assert.Equal(2, envelope.ContractVersion);
        Assert.Equal("request-1", envelope.RequestId);
        Assert.Equal("browser.navigate", envelope.Command);
        Assert.Equal("https://www.nexusmods.com/7daystodie/search/?gsearch=Alpha", payload.Url);
        Assert.Equal("Alpha Mod", payload.NexusSearchName);
        Assert.Equal(new[] { "Alpha Mod", "Alpha Directory" }, payload.NexusSearchNames);
    }

    [Fact]
    public void ParseLegacyNavigatePayloadLeavesNexusSearchNameNull()
    {
        var envelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "request-legacy",
              "command": "browser.navigate",
              "payload": {
                "url": "https://example.test/mod"
              }
            }
            """);

        var payload = BridgeProtocol.ReadPayload<NavigatePayload>(envelope.Payload);

        Assert.Equal("https://example.test/mod", payload.Url);
        Assert.Null(payload.NexusSearchName);
    }

    [Fact]
    public void SerializeNavigatePayloadUsesCamelCaseSearchName()
    {
        var json = JsonSerializer.Serialize(
            new NavigatePayload("https://example.test/search", "Alpha Mod", new[] { "Alpha Mod", "Alpha Directory" }),
            BridgeProtocol.JsonOptions);

        Assert.Contains("\"nexusSearchName\":\"Alpha Mod\"", json);
        Assert.Contains("\"nexusSearchNames\":[\"Alpha Mod\",\"Alpha Directory\"]", json);
    }

    [Fact]
    public void ParseCommandRejectsUnknownCommand()
    {
        var exception = Assert.Throws<BridgeProtocolException>(() =>
            BridgeProtocol.ParseCommand(
                """
                {
                  "contractVersion": 2,
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
                  "contractVersion": 3,
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

        Assert.Equal(2, document.RootElement.GetProperty("contractVersion").GetInt32());
        Assert.Equal("error", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal("request-2", document.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(
            "browser.url.invalid",
            document.RootElement.GetProperty("payload").GetProperty("code").GetString());
    }

    [Fact]
    public void SerializesModCandidateWebsiteInCamelCase()
    {
        var message = BridgeProtocol.SerializeMessage(
            "state",
            new KnowledgeUiState(
                null,
                new[]
                {
                    new ModCandidateUiState(
                        "Alpha Mod",
                        "Alpha Mod",
                        "Alpha Display",
                        "1.2.3",
                        "https://example.test/alpha",
                        "listed",
                        "enabled",
                        0,
                        new SourceReferenceUiState("modDirectory", "mods/Alpha Mod"),
                        null,
                        Array.Empty<DiagnosticUiState>())
                },
                Array.Empty<ProfileUiState>(),
                KnowledgeOperationUiState.Idle));

        Assert.Contains("\"website\":\"https://example.test/alpha\"", message);
        Assert.DoesNotContain("instanceRootPath", message);
    }

    [Fact]
    public void ParsesProfileSwitchAndLayoutVisibilityCommands()
    {
        var profileEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "profile-1",
              "command": "knowledge.switchProfile",
              "payload": { "profileName": "Alternate" }
            }
            """);
        var profilePayload = BridgeProtocol.ReadPayload<SwitchProfilePayload>(profileEnvelope.Payload);

        var layoutEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "layout-1",
              "command": "layout.setContextVisible",
              "payload": { "visible": false }
            }
            """);
        var layoutPayload = BridgeProtocol.ReadPayload<SetContextVisiblePayload>(layoutEnvelope.Payload);

        var modListEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "mod-list-layout-1",
              "command": "layout.setModListVisible",
              "payload": { "visible": false }
            }
            """);
        var modListPayload = BridgeProtocol.ReadPayload<SetModListVisiblePayload>(modListEnvelope.Payload);

        var toolbarEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "toolbar-layout-1",
              "command": "layout.setToolbarExpanded",
              "payload": { "expanded": true }
            }
            """);
        var toolbarPayload = BridgeProtocol.ReadPayload<SetToolbarExpandedPayload>(toolbarEnvelope.Payload);

        var moreEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "more-layout-1",
              "command": "layout.setMoreOpen",
              "payload": { "open": true }
            }
            """);
        var morePayload = BridgeProtocol.ReadMoreOpenPayload(moreEnvelope.Payload);

        var contextModeEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "context-mode-1",
              "command": "layout.setContextMode",
              "payload": { "mode": "analysis" }
            }
            """);
        var contextModePayload = BridgeProtocol.ReadContextModePayload(contextModeEnvelope.Payload);

        var modListModeEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "mod-list-mode-1",
              "command": "layout.setModListMode",
              "payload": { "mode": "deployment-edit" }
            }
            """);
        var modListModePayload = BridgeProtocol.ReadModListModePayload(modListModeEnvelope.Payload);

        Assert.Equal("Alternate", profilePayload.ProfileName);
        Assert.False(layoutPayload.Visible);
        Assert.False(modListPayload.Visible);
        Assert.True(toolbarPayload.Expanded);
        Assert.True(morePayload.Open);
        Assert.Equal("analysis", contextModePayload.Mode);
        Assert.Equal("deployment-edit", modListModePayload.Mode);
    }

    [Fact]
    public void RejectsUnknownLayoutModes()
    {
        var contextEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "context-mode-invalid",
              "command": "layout.setContextMode",
              "payload": { "mode": "maintenance" }
            }
            """);
        var modListEnvelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "mod-list-mode-invalid",
              "command": "layout.setModListMode",
              "payload": { "mode": "publish" }
            }
            """);

        Assert.Throws<BridgeProtocolException>(() => BridgeProtocol.ReadContextModePayload(contextEnvelope.Payload));
        Assert.Throws<BridgeProtocolException>(() => BridgeProtocol.ReadModListModePayload(modListEnvelope.Payload));
    }

    [Fact]
    public void SerializesToolbarExpandedPayloadInCamelCase()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new SetToolbarExpandedPayload(true),
            BridgeProtocol.JsonOptions);

        Assert.Equal("{\"expanded\":true}", json);
    }

    [Fact]
    public void SerializesMoreOpenPayloadInCamelCase()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new SetMoreOpenPayload(true),
            BridgeProtocol.JsonOptions);

        Assert.Equal("{\"open\":true}", json);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"open\":null}")]
    [InlineData("{\"open\":\"true\"}")]
    [InlineData("{\"open\":1}")]
    public void RejectsInvalidMoreOpenPayload(string payload)
    {
        var envelope = BridgeProtocol.ParseCommand(
            $$"""
            {
              "contractVersion": 2,
              "requestId": "more-layout-invalid",
              "command": "layout.setMoreOpen",
              "payload": {{payload}}
            }
            """);

        Assert.Throws<BridgeProtocolException>(
            () => BridgeProtocol.ReadMoreOpenPayload(envelope.Payload));
    }

    [Fact]
    public void ParsesFrontendReadyCommand()
    {
        var envelope = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "frontend-1",
              "command": "frontend.ready",
              "payload": {}
            }
            """);

        Assert.Equal("frontend.ready", envelope.Command);
        Assert.Equal(JsonValueKind.Object, envelope.Payload.ValueKind);
    }

    [Fact]
    public void ParsesSourceDiscoveryCommandsWithoutAbsolutePaths()
    {
        var discover = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "discover-1",
              "command": "knowledge.discoverSources",
              "payload": { "selectedRoots": ["C:\\MO2"] }
            }
            """);
        var select = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "select-1",
              "command": "knowledge.selectSource",
              "payload": { "candidateId": "mo2-candidate" }
            }
            """);
        var root = BridgeProtocol.ParseCommand(
            """
            {
              "contractVersion": 2,
              "requestId": "root-1",
              "command": "knowledge.selectRoot",
              "payload": {}
            }
            """);

        var selectedRoots = BridgeProtocol.ReadPayload<DiscoverSourcesPayload>(discover.Payload);
        var selectedSource = BridgeProtocol.ReadPayload<SelectSourcePayload>(select.Payload);

        Assert.Equal(new[] { "C:\\MO2" }, selectedRoots.SelectedRoots);
        Assert.Equal("mo2-candidate", selectedSource.CandidateId);
        Assert.Equal("knowledge.selectRoot", root.Command);
    }

    [Fact]
    public void SerializesKnowledgeOperationStateInCamelCase()
    {
        var message = BridgeProtocol.SerializeMessage(
            "state",
            new KnowledgeUiState(
                null,
                Array.Empty<ModCandidateUiState>(),
                Array.Empty<ProfileUiState>(),
                new KnowledgeOperationUiState(
                    "profile-switch",
                    true,
                    false,
                    "Alternate",
                    "scanning-mod-folders",
                    3,
                    89)));

        Assert.Contains(
            "\"operation\":{\"kind\":\"profile-switch\",\"isBusy\":true,\"isBackground\":false,\"targetProfileName\":\"Alternate\",\"phase\":\"scanning-mod-folders\",\"completed\":3,\"total\":89}",
            message);
    }

    [Fact]
    public void SerializesProfileLayoutAndBackgroundOperationInCamelCase()
    {
        var profile = System.Text.Json.JsonSerializer.Serialize(
            new ProfileUiState("default", "pending"),
            BridgeProtocol.JsonOptions);
        var layout = System.Text.Json.JsonSerializer.Serialize(
            new LayoutUiState(true, false),
            BridgeProtocol.JsonOptions);
        var operation = System.Text.Json.JsonSerializer.Serialize(
            new KnowledgeOperationUiState(
                "profile-preload",
                true,
                true,
                "alternate",
                "preloading-profile",
                1,
                2),
            BridgeProtocol.JsonOptions);

        Assert.Equal("{\"name\":\"default\",\"loadState\":\"pending\"}", profile);
        Assert.Equal("{\"contextVisible\":true,\"modListVisible\":false,\"contextMode\":\"context\",\"modListMode\":\"browse\",\"moreOpen\":false}", layout);
        Assert.Contains("\"isBackground\":true", operation);
    }

    [Fact]
    public void IdleKnowledgeOperationHasNoProgressValues()
    {
        Assert.Equal("idle", KnowledgeOperationUiState.Idle.Phase);
        Assert.False(KnowledgeOperationUiState.Idle.IsBusy);
        Assert.Null(KnowledgeOperationUiState.Idle.Completed);
        Assert.Null(KnowledgeOperationUiState.Idle.Total);
    }

    [Theory]
    [InlineData("analysis.selectBaseData")]
    [InlineData("analysis.selectRuntimeLogs")]
    [InlineData("analysis.analyzeConflicts")]
    [InlineData("analysis.compareRuntimeEvidence")]
    [InlineData("analysis.useFixture")]
    public void ParsesAnalysisCommands(string command)
    {
        var envelope = BridgeProtocol.ParseCommand("""
            {
              "contractVersion": 2,
              "requestId": "analysis-1",
              "command": "COMMAND",
              "payload": {}
            }
            """.Replace("COMMAND", command, StringComparison.Ordinal));

        Assert.Equal(command, envelope.Command);
    }

    [Fact]
    public void ParsesRuntimeComparisonVersionsInCamelCase()
    {
        var envelope = BridgeProtocol.ParseCommand("""
            {
              "contractVersion": 2,
              "requestId": "runtime-1",
              "command": "analysis.compareRuntimeEvidence",
              "payload": { "toolVersion": "1.0", "gameVersion": "7DTD-test" }
            }
            """);
        var payload = BridgeProtocol.ReadPayload<CompareRuntimeEvidencePayload>(envelope.Payload);

        Assert.Equal("1.0", payload.ToolVersion);
        Assert.Equal("7DTD-test", payload.GameVersion);
    }

    [Fact]
    public void SerializesAnalysisStateWithoutPathsOrRuntimeRawResults()
    {
        var observation = new RuntimeEvidenceObservationUiState(
            "First Mod",
            "items.xml",
            "/items/item[@name='A']/@value",
            "set",
            "Attribute Overrides",
            "Different",
            Array.Empty<DiagnosticUiState>());
        var runtimeEvidence = new RuntimeEvidenceUiState(
            "snapshot-1",
            "synthetic-instance",
            "default",
            "RuntimeOCD",
            null,
            null,
            DateTimeOffset.UnixEpoch,
            new[] { observation },
            Array.Empty<DiagnosticUiState>());
        var runtimeComparison = new RuntimeEvidenceComparisonUiState(
            "snapshot-1",
            "synthetic-instance",
            "default",
            runtimeEvidence,
            new[]
            {
                new RuntimeEvidenceComparisonItemUiState(
                    "items.xml",
                    "/items/item[@name='A']/@value",
                    "different",
                    "different",
                    "different",
                    new[] { observation },
                    Array.Empty<DiagnosticUiState>())
            },
            Array.Empty<DiagnosticUiState>());
        var state = new UiState(
            new BrowserUiState("about:blank", "", false, false),
            null,
            new SourceDiscoveryUiState(Array.Empty<SourceCandidateUiState>(), null),
            new KnowledgeUiState(
                null,
                Array.Empty<ModCandidateUiState>(),
                Array.Empty<ProfileUiState>(),
                KnowledgeOperationUiState.Idle),
            new IdentityUiState(
                "",
                null,
                "not-searched",
                Array.Empty<LocalModMatchUiState>(),
                null),
            null,
            null,
            new AnalysisUiState(
                new AnalysisInputUiState(true, true),
                null,
                runtimeComparison,
                AnalysisOperationUiState.Idle,
                Array.Empty<DiagnosticUiState>()),
            new DeploymentUiState(
                "idle",
                "",
                Array.Empty<DeploymentEntryUiState>(),
                null,
                false,
                false,
                Array.Empty<DeploymentModChangeUiState>(),
                Array.Empty<DeploymentJunctionChangeUiState>(),
                Array.Empty<DiagnosticUiState>()),
            new LayoutUiState(true, true),
            "analysis",
            Array.Empty<DiagnosticUiState>());

        var message = BridgeProtocol.SerializeMessage("state", state);

        Assert.Contains("\"analysis\"", message);
        Assert.DoesNotContain("baseDataPath", message);
        Assert.DoesNotContain("runtimeLogsPath", message);
        Assert.DoesNotContain("rawResult", message);
        Assert.DoesNotContain("runtime log body", message);
        Assert.DoesNotContain("C:\\\\private", message);
    }

    [Fact]
    public void SerializesPageObservationWithoutPageBody()
    {
        var observation = new PageObservationUiState(
            "https://example.test/mod",
            "Example Mod",
            DateTimeOffset.UnixEpoch,
            "WebView2",
            "succeeded",
            Array.Empty<DiagnosticUiState>());

        var message = BridgeProtocol.SerializeMessage("observation", observation);

        Assert.Contains("Example Mod", message);
        Assert.DoesNotContain("contentPreview", message);
        Assert.DoesNotContain("body", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializesXmlPatchOperationInspectorDto()
    {
        var source = new SourceReferenceUiState("modXml", "mods/First Mod/Config/changes.xml", 2, 4);
        var raw = new RawXmlObservationUiState(
            "/configs/set[1]",
            "set",
            Array.Empty<XmlAttributeObservationUiState>(),
            "one",
            source,
            false);
        var patch = new XmlPatchOperationUiState(
            "/configs/set[1]",
            "set",
            "set",
            raw,
            new[] { new XmlXPathCandidateUiState("/items/item[@name='A']/@value", "/configs/set[1]", source) },
            Array.Empty<XmlReferenceCandidateUiState>(),
            Array.Empty<XmlReferenceCandidateUiState>(),
            Array.Empty<XmlReferenceCandidateUiState>(),
            Array.Empty<XmlReferenceCandidateUiState>(),
            Array.Empty<DiagnosticUiState>(),
            source);

        var json = System.Text.Json.JsonSerializer.Serialize(patch, BridgeProtocol.JsonOptions);

        Assert.Contains("\"rawOperationName\":\"set\"", json);
        Assert.Contains("\"normalizedKind\":\"set\"", json);
        Assert.Contains("\"hasChildElements\":false", json);
        Assert.Contains("\"xPathCandidates\"", json);
    }

    [Fact]
    public void ParsesBrowserTabCommandsAndSerializesMetadataOnly()
    {
        foreach (var command in new[]
        {
            "browser.newTab",
            "browser.selectTab",
            "browser.closeTab",
            "browser.home",
            "browser.history",
            "browser.selectHistory"
        })
        {
            var payload = command switch
            {
                "browser.selectTab" or "browser.closeTab" => """{"tabId":"tab-1"}""",
                "browser.selectHistory" => """{"entryId":"history-1"}""",
                _ => "{}"
            };
            var envelope = BridgeProtocol.ParseCommand($$"""
                {
                  "contractVersion": 2,
                  "requestId": "request-1",
                  "command": "{{command}}",
                  "payload": {{payload}}
                }
                """);
            Assert.Equal(command, envelope.Command);
        }

        var browser = new BrowserUiState(
            "https://example.test/mod",
            "Example MOD",
            true,
            false,
            new[]
            {
                new BrowserTabUiState("tab-1", "Example MOD", "https://example.test/mod", true, false, true)
            },
            "tab-1",
            new[]
            {
                new BrowserHistoryEntryUiState(
                    "history-1",
                    "Example MOD",
                    "https://example.test/mod",
                    DateTimeOffset.Parse("2026-08-14T00:00:00Z"))
            });

        var json = System.Text.Json.JsonSerializer.Serialize(browser, BridgeProtocol.JsonOptions);

        Assert.Contains("\"tabs\"", json);
        Assert.Contains("\"activeTabId\":\"tab-1\"", json);
        Assert.Contains("\"history\"", json);
        Assert.DoesNotContain("C:\\\\Users\\\\wakad", json);
        Assert.DoesNotContain("raw runtime log", json);
    }
}
