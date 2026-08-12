import { initialState, type HostMessage, type UiState } from './contracts';

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
      candidates: [
        {
          modKey: 'Alpha Mod',
          directoryName: 'Alpha Mod',
          displayName: 'Alpha Mod',
          version: '1.2.3',
          profileState: 'listed',
          enabledState: 'enabled',
          priority: 0,
          source: { kind: 'modDirectory', relativePath: 'mods/Alpha Mod' },
          priorityEvidence: null,
          diagnostics: []
        }
      ],
      profiles: [{ name: 'default' }],
      operation: {
        kind: 'idle',
        isBusy: false,
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
      next.identity = { candidateIdentity: '', selectedLocalModKey: null };
      next.localContext = null;
      next.inspector = null;
      next.statusMessage = 'Mock profile switched.';
    }
  } else if (command === 'layout.setContextVisible' && typeof payload === 'object' && payload !== null) {
    const visible = (payload as { visible?: unknown }).visible;
    if (typeof visible === 'boolean') {
      next.layout = { contextVisible: visible };
    }
  }
  return next;
}

export function createBridge(onMessage: (message: HostMessage) => void): Bridge {
  const webview = getWebViewPort();
  let mockState = cloneState(initialState);
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
        return () => webview.removeEventListener('message', handleMessage);
      }

      onMessage({ kind: 'ready', payload: {} });
      onMessage({ kind: 'state', payload: mockState });
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

      mockState = mockStateForCommand(mockState, command, payload);
      onMessage({ kind: 'state', requestId, payload: mockState });
    }
  };
}
