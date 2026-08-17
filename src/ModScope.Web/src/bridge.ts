import {
  initialState,
  type DiagnosticUiState,
  type BrowserHistoryEntryUiState,
  type HostMessage,
  type ModCandidateUiState,
  type WorkspaceActionAvailabilityUiState,
  type WorkspaceActionsUiState,
  type UiState
} from './contracts';

type WebViewPort = {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: MessageEvent) => void): void;
  removeEventListener(type: 'message', listener: (event: MessageEvent) => void): void;
};

export type Bridge = {
  isDesktopHost: boolean;
  connect(): () => void;
  send(command: string, payload?: unknown): void;
};

function getWebViewPort(): WebViewPort | null {
  const windowWithWebView = window as Window & {
    chrome?: { webview?: WebViewPort };
  };
  return windowWithWebView.chrome?.webview ?? null;
}

function cloneState(state: UiState): UiState {
  return JSON.parse(JSON.stringify(state)) as UiState;
}

function mockCandidatesForProfile(profileName: string): ModCandidateUiState[] {
  if (profileName === 'alternate') {
    return [
      {
        modKey: 'Gamma Mod',
        directoryName: 'Gamma Mod',
        displayName: 'Gamma Mod',
        version: '2.0.0',
        website: null,
        profileState: 'listed',
        enabledState: 'enabled',
        priority: 0,
        source: { kind: 'modDirectory', relativePath: 'mods/Gamma Mod' },
        priorityEvidence: null,
        diagnostics: [],
        role: {
          role: 'Foundation',
          assessment: 'Inferred',
          reason: 'Static metadata suggests a broad support role. Dependency is not asserted.',
          evidence: [{ kind: 'modInfo', detail: 'ModInfo name contains a foundation marker.', source: { kind: 'modInfo', relativePath: 'mods/Gamma Mod/ModInfo.xml' } }]
        }
      }
    ];
  }

  return [
    {
      modKey: 'Alpha Mod',
      directoryName: 'Alpha Mod',
      displayName: 'Alpha Mod',
      version: '1.2.3',
      website: 'https://example.test/alpha',
      profileState: 'listed',
      enabledState: 'enabled',
      priority: 0,
      source: { kind: 'modDirectory', relativePath: 'mods/Alpha Mod' },
      priorityEvidence: null,
      diagnostics: [],
      role: {
        role: 'Foundation',
        assessment: 'Inferred',
        reason: 'Static metadata suggests a broad support role. Dependency is not asserted.',
        evidence: [{ kind: 'modInfo', detail: 'ModInfo name contains a foundation marker.', source: { kind: 'modInfo', relativePath: 'mods/Alpha Mod/ModInfo.xml' } }]
      }
    },
    {
      modKey: 'Beta Mod',
      directoryName: 'Beta Mod',
      displayName: 'Beta Mod',
      version: null,
      website: null,
      profileState: 'listed',
      enabledState: 'disabled',
      priority: 1,
      source: { kind: 'modDirectory', relativePath: 'mods/Beta Mod' },
      priorityEvidence: null,
      diagnostics: [],
      role: {
        role: 'Content',
        assessment: 'Inferred',
        reason: 'Static XML patch evidence indicates content changes.',
        evidence: [{ kind: 'xmlPatchOperation', detail: 'Config/changes.xml contains a patch operation.', source: { kind: 'modXml', relativePath: 'mods/Beta Mod/Config/changes.xml' } }]
      }
    },
    {
      modKey: 'Missing Mod',
      directoryName: 'Missing Mod',
      displayName: 'Missing Mod',
      version: null,
      website: null,
      profileState: 'unresolved',
      enabledState: 'enabled',
      priority: 2,
      source: { kind: 'profileFile', relativePath: 'profile/modlist.txt' },
      priorityEvidence: null,
      diagnostics: [{
        code: 'mod.unresolved',
        severity: 'warning',
        message: 'The profile entry has no matching MOD directory.',
        source: { kind: 'profileFile', relativePath: 'profile/modlist.txt' },
        rawValue: 'Missing Mod'
      }],
      role: {
        role: 'Unknown',
        assessment: 'Unknown',
        reason: 'No readable MOD evidence is available.',
        evidence: []
      }
    },
    {
      modKey: 'Unlisted Mod',
      directoryName: 'Unlisted Mod',
      displayName: 'Unlisted Mod',
      version: '0.9.0',
      website: null,
      profileState: 'unlisted',
      enabledState: 'unknown',
      priority: null,
      source: { kind: 'modDirectory', relativePath: 'mods/Unlisted Mod' },
      priorityEvidence: null,
      diagnostics: [],
      role: {
        role: 'Unknown',
        assessment: 'Unknown',
        reason: 'No static role evidence is available.',
        evidence: []
      }
    }
  ];
}

function mockDeploymentEntriesForProfile(profileName: string) {
  return mockCandidatesForProfile(profileName)
    .filter((candidate) => candidate.profileState !== 'unlisted')
    .sort((left, right) => (left.priority ?? Number.MAX_SAFE_INTEGER) - (right.priority ?? Number.MAX_SAFE_INTEGER))
    .map((candidate, index) => ({
      entryId: 'mock-line-' + String(index + 1),
      modKey: candidate.modKey,
      enabled: candidate.enabledState === 'enabled',
      priority: candidate.priority,
      isSeparator: false,
      isEditable: true
    }));
}

function mockActionAvailability(isEnabled: boolean, disabledReason: string | null = null): WorkspaceActionAvailabilityUiState {
  return { isEnabled, disabledReason };
}

function mockWorkspaceActions(hasSession: boolean, profileRowCount: number): WorkspaceActionsUiState {
  const analysisReason = hasSession ? null : 'Load a local profile first.';
  const editProfileReason = !hasSession
    ? 'Load a local profile first.'
    : profileRowCount === 0
      ? 'No profile entries are available.'
      : null;
  return {
    history: mockActionAvailability(true),
    contextMode: mockActionAvailability(true),
    settingsMode: mockActionAvailability(true),
    debugMode: mockActionAvailability(true),
    analysisMode: mockActionAvailability(hasSession, analysisReason),
    browseModList: mockActionAvailability(true),
    editProfile: mockActionAvailability(editProfileReason === null, editProfileReason),
    toggleModList: mockActionAvailability(true),
    toggleContext: mockActionAvailability(true)
  };
}

function mockAnalysisForFixture(): UiState['analysis'] {
  const source = (kind: string, relativePath: string, lineNumber?: number) => ({
    kind,
    relativePath,
    lineNumber: lineNumber ?? null,
    columnNumber: null
  });
  const emptyDiagnostics: DiagnosticUiState[] = [];
  const operation = (
    operationKey: string,
    modKey: string,
    priority: number,
    value: string,
    lineNumber: number
  ) => ({
    operationKey,
    modKey,
    priority,
    xmlFileRelativePath: 'Config/changes.xml',
    elementPath: `/configs/set[${priority + 1}]`,
    rawOperationName: 'set',
    normalizedKind: 'set',
    targetXml: 'items.xml',
    xPath: "/items/item[@name='A']/@value",
    attributeName: null,
    value,
    source: source('modXml', `mods/${modKey}/Config/changes.xml`, lineNumber),
    evidence: [{ kind: 'staticXml', source: source('modXml', `mods/${modKey}/Config/changes.xml`, lineNumber) }],
    diagnostics: emptyDiagnostics,
    hasChildElements: false
  });
  const staticGroup = {
    targetXml: 'items.xml',
    xPath: "/items/item[@name='A']/@value",
    assessment: 'different',
    confidence: 'high',
    effectiveStatus: 'different',
    operations: [
      operation('alpha-a-value', 'Alpha Mod', 0, 'one', 2),
      operation('beta-a-value', 'Beta Mod', 1, 'two', 2)
    ],
    effectiveChanges: [{
      matchPath: "/items/item[@name='A']",
      attributeName: 'value',
      beforeValue: 'base',
      afterValue: 'two',
      existedBefore: true,
      existsAfter: true,
      source: source('baseData', 'base/Data/Config/items.xml', 3)
    }],
    evidence: [{ kind: 'baseData', source: source('baseData', 'base/Data/Config/items.xml', 3) }],
    uncertainties: ['Runtime evidence is separate from static XML evidence.'],
    diagnostics: emptyDiagnostics
  };
  const runtimeObservation = (modKey: string, assessment: string, lineNumber: number) => ({
    modKey,
    targetXml: 'items.xml',
    xPath: "/items/item[@name='A']/@value",
    observedOperation: 'set',
    observedCategory: 'Attribute Overrides',
    normalizedAssessment: assessment,
    diagnostics: emptyDiagnostics
  });

  return {
    inputs: { baseDataReady: true, runtimeLogsReady: true, baseDataStatus: 'inferred' },
    conflict: {
      snapshotId: 'mock:snapshot',
      instanceName: 'synthetic-instance',
      profileName: 'default',
      baseFiles: [{
        targetXml: 'items.xml',
        size: 256,
        sha256: 'synthetic-sha256',
        parseStatus: 'succeeded',
        source: source('baseData', 'base/Data/Config/items.xml'),
        diagnostics: emptyDiagnostics
      }],
      groups: [staticGroup],
      diagnostics: emptyDiagnostics
    },
    runtimeComparison: {
      snapshotId: 'mock:snapshot',
      instanceName: 'synthetic-instance',
      profileName: 'default',
      runtimeEvidence: {
        snapshotId: 'mock:snapshot',
        instanceName: 'synthetic-instance',
        profileName: 'default',
        toolName: 'RuntimeOCD',
        toolVersion: null,
        gameVersion: null,
        capturedAtUtc: new Date().toISOString(),
        observations: [
          runtimeObservation('Alpha Mod', 'different', 2),
          runtimeObservation('Beta Mod', 'different', 4)
        ],
        diagnostics: [{
          code: 'runtime.ocd.version.unknown',
          severity: 'info',
          message: 'Tool version is unknown.',
          source: source('runtimeLog', 'ConflictDetector_(AO)_Attribute_Overrides/phase6-synthetic.txt')
        }]
      },
      items: [{
        targetXml: 'items.xml',
        xPath: "/items/item[@name='A']/@value",
        status: 'different',
        staticAssessment: 'different',
        runtimeAssessment: 'different',
        observations: [runtimeObservation('Alpha Mod', 'different', 2)],
        diagnostics: emptyDiagnostics
      }],
      diagnostics: emptyDiagnostics
    },
    operation: { kind: 'idle', isBusy: false },
    diagnostics: []
  };
}

const mockSensitiveQueryKeys = new Set([
  'access_token',
  'auth',
  'authorization',
  'client_secret',
  'code',
  'id_token',
  'nonce',
  'redirect_uri',
  'return_to',
  'session',
  'session_id',
  'state',
  'token'
]);
const mockAuthenticationPathSegments = new Set(['auth', 'authorize', 'callback', 'login', 'oauth', 'signin']);

function normalizeMockHistoryUrl(url: string): string | null {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') return null;
    if (parsed.hostname.toLowerCase() === 'users.nexusmods.com') return null;
    if (parsed.pathname.split('/').filter(Boolean).some((segment) => mockAuthenticationPathSegments.has(segment.toLowerCase()))) return null;
    let hasSensitiveQueryKey = false;
    parsed.searchParams.forEach((_value, key) => {
      if (mockSensitiveQueryKeys.has(key.toLowerCase())) hasSensitiveQueryKey = true;
    });
    if (hasSensitiveQueryKey) return null;
    parsed.search = '';
    parsed.hash = '';
    return parsed.toString();
  } catch {
    return null;
  }
}

function mockInternalPageFor(url: string, title: string): string | null {
  if (url === 'about:history') return 'history';
  if (url === 'about:deployment-preview') return 'deployment-preview';
  if (url === 'about:blank' && title === 'ModScope Home') return 'home';
  return null;
}

function clearMockObservation(next: UiState): void {
  next.observation = null;
  next.identity = cloneState(initialState).identity;
  next.localContext = null;
  next.inspector = null;
}

function updateMockBrowserTab(next: UiState, url: string, title: string): void {
  const activeTabId = next.browser.activeTabId || next.browser.tabs.find((tab) => tab.isActive)?.tabId;
  if (!activeTabId) {
    return;
  }

  const wasInternalPage = Boolean(next.browser.internalPage);
  const internalPage = mockInternalPageFor(url, title);
  const tabs = next.browser.tabs.map((tab) => tab.tabId === activeTabId
    ? { ...tab, url, title, isActive: true }
    : { ...tab, isActive: false });
  const normalizedHistoryUrl = normalizeMockHistoryUrl(url);
  const historyEntry: BrowserHistoryEntryUiState | null = normalizedHistoryUrl
    ? {
        entryId: `mock-history-${Date.now()}`,
        title: title || url,
        url: normalizedHistoryUrl,
        visitedAtUtc: new Date().toISOString()
      }
    : null;
  next.browser = {
    ...next.browser,
    url,
    title,
    internalPage,
    tabs,
    activeTabId,
    history: historyEntry
      ? [historyEntry, ...next.browser.history.filter((entry) => entry.url !== normalizedHistoryUrl)].slice(0, 100)
      : next.browser.history
  };
  if (wasInternalPage || internalPage) clearMockObservation(next);
}

function createMockBrowserTab(next: UiState): void {
  let sequence = next.browser.tabs.length + 1;
  let tabId = `mock-tab-${sequence}`;
  while (next.browser.tabs.some((tab) => tab.tabId === tabId)) {
    sequence += 1;
    tabId = `mock-tab-${sequence}`;
  }

  const wasInternalPage = Boolean(next.browser.internalPage);
  next.browser = {
    ...next.browser,
    tabs: next.browser.tabs.map((tab) => ({ ...tab, isActive: false })).concat({
      tabId,
      title: 'New tab',
      url: 'about:blank',
      canGoBack: false,
      canGoForward: false,
      isActive: true
    }),
    activeTabId: tabId,
    url: 'about:blank',
    title: 'New tab',
    internalPage: null,
    canGoBack: false,
    canGoForward: false
  };
  if (wasInternalPage) clearMockObservation(next);
}

function mockStateForCommand(state: UiState, command: string, payload: unknown): UiState {
  let next = cloneState(state);
  if (command === 'browser.newTab') {
    createMockBrowserTab(next);
    next.statusMessage = 'New tab opened.';
  } else if (command === 'browser.selectTab' && typeof payload === 'object' && payload !== null) {
    const tabId = (payload as { tabId?: unknown }).tabId;
    if (typeof tabId === 'string' && next.browser.tabs.some((tab) => tab.tabId === tabId)) {
      const selected = next.browser.tabs.find((tab) => tab.tabId === tabId)!;
      next.browser = {
        ...next.browser,
        tabs: next.browser.tabs.map((tab) => ({ ...tab, isActive: tab.tabId === tabId })),
        activeTabId: tabId,
        url: selected.url,
        title: selected.title,
        internalPage: mockInternalPageFor(selected.url, selected.title),
        canGoBack: selected.canGoBack,
        canGoForward: selected.canGoForward
      };
      if (next.browser.internalPage) clearMockObservation(next);
    }
  } else if (command === 'browser.closeTab' && typeof payload === 'object' && payload !== null) {
    const tabId = (payload as { tabId?: unknown }).tabId;
    if (typeof tabId === 'string' && next.browser.tabs.length > 1) {
      const remaining = next.browser.tabs.filter((tab) => tab.tabId !== tabId);
      const selected = remaining.find((tab) => tab.isActive) ?? remaining[0];
      next.browser = {
        ...next.browser,
        tabs: remaining.map((tab) => ({ ...tab, isActive: tab.tabId === selected.tabId })),
        activeTabId: selected.tabId,
        url: selected.url,
        title: selected.title,
        internalPage: mockInternalPageFor(selected.url, selected.title),
        canGoBack: selected.canGoBack,
        canGoForward: selected.canGoForward
      };
      if (next.browser.internalPage) clearMockObservation(next);
    }
  } else if (command === 'browser.history') {
    createMockBrowserTab(next);
    updateMockBrowserTab(next, 'about:history', 'History');
    next.statusMessage = 'History opened in a new tab.';
  } else if (command === 'browser.home') {
    updateMockBrowserTab(next, 'about:blank', 'ModScope Home');
  } else if (command === 'browser.selectHistory' && typeof payload === 'object' && payload !== null) {
    const entryId = (payload as { entryId?: unknown }).entryId;
    const entry = typeof entryId === 'string'
      ? next.browser.history.find((historyEntry) => historyEntry.entryId === entryId)
      : undefined;
    if (entry) {
      updateMockBrowserTab(next, entry.url, entry.title);
    }
  } else if (command === 'browser.navigate' && typeof payload === 'object' && payload !== null) {
    const url = (payload as { url?: unknown }).url;
    if (typeof url === 'string' && url.length > 0) {
      updateMockBrowserTab(next, url, 'Mock page');
      next.statusMessage = 'Mock navigation completed.';
    }
  } else if (command === 'browser.observe') {
    next.observation = {
      url: next.browser.url,
      title: next.browser.title || 'Mock page',
      observedAtUtc: new Date().toISOString(),
      source: 'Mock WebView2',
      extractionStatus: 'succeeded',
      diagnostics: []
    };
    next.statusMessage = 'Mock page observed.';
  } else if (command === 'knowledge.useFixture') {
    next.knowledge = {
      session: {
        snapshotId: 'mock:snapshot',
        instanceName: 'synthetic-instance',
        profileName: 'default',
        createdAtUtc: new Date().toISOString(),
        parserVersion: 'mock',
        schemaVersion: 1,
        diagnostics: []
      },
      candidates: mockCandidatesForProfile('default'),
      profiles: [
        { name: 'default', loadState: 'ready' },
        { name: 'alternate', loadState: 'pending' }
      ],
      operation: {
        kind: 'idle',
        isBusy: false,
        isBackground: false,
        targetProfileName: null,
        phase: 'idle',
        completed: null,
        total: null
      }
    };
    next.deployment = {
      status: 'idle',
      profileName: 'default',
      entries: mockDeploymentEntriesForProfile('default'),
      planId: null,
      canApply: false,
      canLaunch: false,
      modChanges: [],
      junctionChanges: [],
      diagnostics: []
    };
    next.layout = {
      ...next.layout,
      actions: mockWorkspaceActions(true, next.deployment.entries.length)
    };
    next.analysis = initialState.analysis;
    next.statusMessage = 'Mock Local Knowledge loaded.';
  } else if (command === 'analysis.selectBaseData') {
    next.analysis = {
      ...next.analysis,
      inputs: { ...next.analysis.inputs, baseDataReady: true, baseDataStatus: 'manual' },
      conflict: null,
      runtimeComparison: null,
      diagnostics: []
    };
    next.statusMessage = 'Mock base Data/Config folder selected.';
  } else if (command === 'analysis.selectRuntimeLogs') {
    next.analysis = {
      ...next.analysis,
      inputs: { ...next.analysis.inputs, runtimeLogsReady: true },
      runtimeComparison: null,
      diagnostics: []
    };
    next.statusMessage = 'Mock runtime logs folder selected.';
  } else if (command === 'analysis.analyzeConflicts') {
    if (next.analysis.inputs.baseDataReady) {
      next.analysis = {
        ...next.analysis,
        conflict: mockAnalysisForFixture().conflict,
        operation: { kind: 'idle', isBusy: false },
        diagnostics: []
      };
      next.statusMessage = 'Mock conflict analysis completed.';
    } else {
      next.statusMessage = 'Select a base Data/Config folder first.';
    }
  } else if (command === 'analysis.compareRuntimeEvidence') {
    if (next.analysis.inputs.baseDataReady && next.analysis.inputs.runtimeLogsReady) {
      const analysis = mockAnalysisForFixture();
      const versions = typeof payload === 'object' && payload !== null
        ? payload as { toolVersion?: unknown; gameVersion?: unknown }
        : {};
      next.analysis = {
        ...next.analysis,
        runtimeComparison: {
          ...analysis.runtimeComparison!,
          runtimeEvidence: {
            ...analysis.runtimeComparison!.runtimeEvidence,
            toolVersion: typeof versions.toolVersion === 'string' && versions.toolVersion.length > 0
              ? versions.toolVersion
              : null,
            gameVersion: typeof versions.gameVersion === 'string' && versions.gameVersion.length > 0
              ? versions.gameVersion
              : null
          }
        },
        operation: { kind: 'idle', isBusy: false },
        diagnostics: []
      };
      next.statusMessage = 'Mock runtime evidence comparison completed.';
    } else {
      next.statusMessage = 'Select both analysis input folders first.';
    }
  } else if (command === 'analysis.useFixture') {
    const fixtureState = mockStateForCommand(next, 'knowledge.useFixture', {});
    next = fixtureState;
    next.identity = {
      candidateIdentity: 'Alpha Mod',
      selectedLocalModKey: 'Alpha Mod',
      recognitionStatus: 'manual-confirmed',
      matches: [],
      autoInspectToken: null
    };
    next.localContext = {
      candidateIdentity: 'Alpha Mod',
      status: 'installed',
      instanceName: 'synthetic-instance',
      profileName: 'default',
      localModKey: 'Alpha Mod',
      directoryName: 'Alpha Mod',
      enabledState: 'enabled',
      priority: 0,
      knownVersion: '1.2.3',
      evidence: [{ kind: 'profileModlist', source: { kind: 'profileFile', relativePath: 'profile/modlist.txt' } }],
      uncertainties: [],
      diagnostics: []
    };
    next.analysis = mockAnalysisForFixture();
    next.statusMessage = 'Mock Phase6 analysis fixture loaded.';
  } else if (command === 'knowledge.switchProfile' && typeof payload === 'object' && payload !== null) {
    const profileName = (payload as { profileName?: unknown }).profileName;
    if (typeof profileName === 'string' && profileName.length > 0 && next.knowledge.session) {
      next.knowledge.session = { ...next.knowledge.session, profileName };
      next.knowledge = {
        ...next.knowledge,
        candidates: mockCandidatesForProfile(profileName),
        profiles: next.knowledge.profiles.map((profile) => ({
          ...profile,
          loadState: profile.name === profileName ? 'ready' : profile.loadState
        }))
      };
      next.identity = {
        candidateIdentity: '',
        selectedLocalModKey: null,
        recognitionStatus: 'not-searched',
        matches: [],
        autoInspectToken: null
      };
      next.localContext = null;
      next.inspector = null;
      next.analysis = initialState.analysis;
      next.deployment = {
        status: 'idle',
        profileName,
        entries: mockDeploymentEntriesForProfile(profileName),
        planId: null,
        canApply: false,
        canLaunch: false,
        modChanges: [],
        junctionChanges: [],
        diagnostics: []
      };
      next.layout = {
        ...next.layout,
        actions: mockWorkspaceActions(true, next.deployment.entries.length)
      };
      next.statusMessage = 'Mock profile switched.';
    }
  } else if (command === 'inspector.open' && typeof payload === 'object' && payload !== null) {
    const modKey = (payload as { modKey?: unknown }).modKey;
    const candidate = typeof modKey === 'string'
      ? next.knowledge.candidates.find((item) => item.modKey === modKey)
      : undefined;
    if (candidate) {
      next.inspector = {
        modKey: candidate.modKey,
        directoryName: candidate.directoryName,
        profileState: candidate.profileState,
        enabledState: candidate.enabledState,
        priority: candidate.priority,
        modInfo: null,
        files: [],
        xmlFiles: [],
        diagnostics: candidate.diagnostics,
        source: candidate.source,
        packageRelation: candidate.packageRelation ?? null,
        conclusion: null,
        compatibilityObservations: [],
        compatibilityDiagnostics: []
      };
      next.statusMessage = `Mock Inspector opened for ${candidate.directoryName}.`;
    }
  } else if (command === 'deployment.preview' && typeof payload === 'object' && payload !== null) {
    const preview = payload as {
      profileName?: unknown;
      entries?: unknown;
    };
    if (typeof preview.profileName === 'string' && Array.isArray(preview.entries)) {
      const entries = preview.entries
        .filter((entry): entry is { modKey: string; enabled: boolean; order: number } =>
          typeof entry === 'object'
          && entry !== null
          && typeof (entry as { modKey?: unknown }).modKey === 'string'
          && typeof (entry as { enabled?: unknown }).enabled === 'boolean'
          && typeof (entry as { order?: unknown }).order === 'number')
        .sort((left, right) => left.order - right.order);
      next.deployment = {
        ...next.deployment,
        status: 'preview-ready',
        profileName: preview.profileName,
        planId: 'mock-deployment-plan',
        canApply: true,
        canLaunch: false,
        entries: next.deployment.entries.map((entry) => {
          const draft = entries.find((candidate) => candidate.modKey === entry.modKey);
          return draft ? { ...entry, enabled: draft.enabled } : entry;
        }),
        modChanges: [],
        junctionChanges: entries
          .filter((entry) => entry.enabled)
          .map((entry) => ({ action: 'keep', targetName: entry.modKey })),
        diagnostics: []
      };
      createMockBrowserTab(next);
      updateMockBrowserTab(next, 'about:deployment-preview', 'Deployment preview');
      next.statusMessage = 'Mock deployment preview is ready.';
    }
  } else if (command === 'deployment.apply' && typeof payload === 'object' && payload !== null) {
    const approval = payload as { planId?: unknown; approved?: unknown };
    if (approval.planId === next.deployment.planId && approval.approved === true) {
      next.deployment = {
        ...next.deployment,
        status: 'applied',
        planId: null,
        canApply: false,
        canLaunch: true
      };
      next.statusMessage = 'Mock deployment applied and verified.';
    }
  } else if (command === 'game.launch') {
    next.statusMessage = next.deployment.canLaunch
      ? 'Mock Steam launch requested.'
      : 'Apply the deployment before launching the game.';
  } else if (command === 'layout.setContextVisible' && typeof payload === 'object' && payload !== null) {
    const visible = (payload as { visible?: unknown }).visible;
    if (typeof visible === 'boolean') {
      next.layout = { ...next.layout, contextVisible: visible };
    }
  } else if (command === 'layout.setModListVisible' && typeof payload === 'object' && payload !== null) {
    const visible = (payload as { visible?: unknown }).visible;
    if (typeof visible === 'boolean') {
      next.layout = { ...next.layout, modListVisible: visible };
    }
  } else if (command === 'layout.setMoreOpen' && typeof payload === 'object' && payload !== null) {
    const open = (payload as { open?: unknown }).open;
    if (typeof open === 'boolean') {
      next.layout = { ...next.layout, moreOpen: open };
    }
  } else if (command === 'layout.setToolbarExpanded' && typeof payload === 'object' && payload !== null) {
    const expanded = (payload as { expanded?: unknown }).expanded;
    if (typeof expanded === 'boolean') {
      next.statusMessage = expanded ? 'History opened.' : 'History closed.';
    }
  } else if (command === 'layout.setContextMode' && typeof payload === 'object' && payload !== null) {
    const mode = (payload as { mode?: unknown }).mode;
    if (mode === 'context' || mode === 'settings' || mode === 'debug' || mode === 'analysis') {
      next.layout = { ...next.layout, contextMode: mode };
    }
  } else if (command === 'layout.setModListMode' && typeof payload === 'object' && payload !== null) {
    const mode = (payload as { mode?: unknown }).mode;
    if (mode === 'browse' || mode === 'deployment-edit') {
      next.layout = { ...next.layout, modListMode: mode };
    }
  }
  return next;
}

function scheduleMockProfilePreload(
  mockStateRef: { current: UiState },
  onMessage: (message: HostMessage) => void
): void {
  window.setTimeout(() => {
    if (mockStateRef.current.knowledge.session?.profileName !== 'default') {
      return;
    }

    mockStateRef.current = cloneState(mockStateRef.current);
    mockStateRef.current.knowledge = {
      ...mockStateRef.current.knowledge,
      profiles: mockStateRef.current.knowledge.profiles.map((profile) =>
        profile.name === 'alternate' ? { ...profile, loadState: 'loading' } : profile),
      operation: {
        kind: 'profile-preload',
        isBusy: true,
        isBackground: true,
        targetProfileName: 'alternate',
        phase: 'preloading-profile',
        completed: 0,
        total: 1
      }
    };
    onMessage({ kind: 'state', payload: mockStateRef.current });

    window.setTimeout(() => {
      if (mockStateRef.current.knowledge.session?.profileName !== 'default') {
        return;
      }

      mockStateRef.current = cloneState(mockStateRef.current);
      mockStateRef.current.knowledge = {
        ...mockStateRef.current.knowledge,
        profiles: mockStateRef.current.knowledge.profiles.map((profile) =>
          profile.name === 'alternate' ? { ...profile, loadState: 'ready' } : profile),
        operation: {
          kind: 'idle',
          isBusy: false,
          isBackground: false,
          targetProfileName: null,
          phase: 'idle',
          completed: null,
          total: null
        }
      };
      onMessage({ kind: 'state', payload: mockStateRef.current });
    }, 500);
  }, 120);
}

export function createBridge(onMessage: (message: HostMessage) => void): Bridge {
  const webview = getWebViewPort();
  const mockInitialState = cloneState(initialState);
  mockInitialState.layout = {
    ...mockInitialState.layout,
    actions: mockWorkspaceActions(false, 0)
  };
  const mockStateRef = { current: mockInitialState };
  let mockRequestId = 0;
  const layoutOnlyCommands = new Set([
    'layout.setContextVisible',
    'layout.setModListVisible',
    'layout.setMoreOpen',
    'layout.setContextMode',
    'layout.setModListMode'
  ]);

  const handleMessage = (event: MessageEvent) => {
    try {
      const message = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
      if (message && typeof message.kind === 'string') {
        onMessage(message as HostMessage);
      }
    } catch {
      onMessage({
        kind: 'error',
        payload: {
          code: 'bridge.message.invalid',
          message: 'The frontend received an invalid host message.'
        }
      });
    }
  };

  return {
    isDesktopHost: webview !== null,
    connect() {
      if (webview) {
        webview.addEventListener('message', handleMessage);
        webview.postMessage({
          contractVersion: 2,
          requestId: 'web-' + String(++mockRequestId),
          command: 'frontend.ready',
          payload: {}
        });
        return () => webview.removeEventListener('message', handleMessage);
      }

      onMessage({ kind: 'ready', payload: {} });
      const fixtureRequested = new URLSearchParams(window.location.search).get('fixture') === '1';
      if (fixtureRequested) {
        mockStateRef.current = mockStateForCommand(mockStateRef.current, 'knowledge.useFixture', {});
        onMessage({ kind: 'state', payload: mockStateRef.current });
        scheduleMockProfilePreload(mockStateRef, onMessage);
      } else {
        onMessage({ kind: 'state', payload: mockStateRef.current });
      }
      return () => undefined;
    },
    send(command, payload = {}) {
      const requestId = 'web-' + String(++mockRequestId);
      const message = {
        contractVersion: 2,
        requestId,
        command,
        payload
      };

      if (webview) {
        webview.postMessage(message);
        return;
      }

      mockStateRef.current = mockStateForCommand(mockStateRef.current, command, payload);
      if (layoutOnlyCommands.has(command)) {
        onMessage({ kind: 'layout', requestId, payload: mockStateRef.current.layout });
      } else {
        onMessage({ kind: 'state', requestId, payload: mockStateRef.current });
      }

      if (command === 'knowledge.useFixture') {
        scheduleMockProfilePreload(mockStateRef, onMessage);
      }
    }
  };
}
