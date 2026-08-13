import { initialState, type HostMessage, type ModCandidateUiState, type UiState } from './contracts';

type WebViewPort = {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: MessageEvent) => void): void;
  removeEventListener(type: 'message', listener: (event: MessageEvent) => void): void;
};

export type Bridge = {
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
        diagnostics: []
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
      diagnostics: []
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
      diagnostics: []
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
      }]
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
      diagnostics: []
    }
  ];
}

function mockStateForCommand(state: UiState, command: string, payload: unknown): UiState {
  const next = cloneState(state);
  if (command === 'browser.navigate' && typeof payload === 'object' && payload !== null) {
    const url = (payload as { url?: unknown }).url;
    if (typeof url === 'string' && url.length > 0) {
      next.browser = { ...next.browser, url, title: 'Mock page' };
      next.statusMessage = 'Mock navigation completed.';
    }
  } else if (command === 'browser.observe') {
    next.observation = {
      url: next.browser.url,
      title: next.browser.title || 'Mock page',
      contentPreview: 'Development mock observation.',
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
    next.statusMessage = 'Mock Local Knowledge loaded.';
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
      next.identity = { candidateIdentity: '', selectedLocalModKey: null };
      next.localContext = null;
      next.inspector = null;
      next.statusMessage = 'Mock profile switched.';
    }
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
  const mockStateRef = { current: cloneState(initialState) };
  let mockRequestId = 0;

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
    connect() {
      if (webview) {
        webview.addEventListener('message', handleMessage);
        webview.postMessage({
          contractVersion: 1,
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
        contractVersion: 1,
        requestId,
        command,
        payload
      };

      if (webview) {
        webview.postMessage(message);
        return;
      }

      mockStateRef.current = mockStateForCommand(mockStateRef.current, command, payload);
      onMessage({ kind: 'state', requestId, payload: mockStateRef.current });

      if (command === 'knowledge.useFixture') {
        scheduleMockProfilePreload(mockStateRef, onMessage);
      }
    }
  };
}
