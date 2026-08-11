<script lang="ts">
  import { onMount } from 'svelte';
  import { createBridge, type Bridge } from './bridge';
  import { initialState, type BridgeErrorPayload, type HostMessage, type UiState } from './contracts';

  let state: UiState = initialState;
  let address = initialState.browser.url;
  let selectedModKey = '';
  let inspectorOpen = false;
  let pageDetailsOpen = false;
  let developerToolsOpen = false;
  let lastError: BridgeErrorPayload | null = null;
  let bridge: Bridge | undefined;
  let source = {
    instanceName: 'explicit-instance',
    profileName: 'default',
    instanceRootPath: '',
    profilePath: '',
    modsPath: ''
  };

  onMount(() => {
    bridge = createBridge(handleHostMessage);
    return bridge.connect();
  });

  function handleHostMessage(message: HostMessage) {
    if (message.kind === 'state') {
      state = message.payload;
      address = state.browser.url;
      selectedModKey = state.identity.selectedLocalModKey ?? '';
      lastError = null;
      if (state.inspector) {
        inspectorOpen = true;
      }
      return;
    }

    if (message.kind === 'error') {
      lastError = message.payload;
    }
  }

  function send(command: string, payload: unknown = {}) {
    lastError = null;
    bridge?.send(command, payload);
  }

  function navigate() {
    const url = address.trim();
    if (url.length > 0) {
      send('browser.navigate', { url });
    }
  }

  function loadSource() {
    send('knowledge.loadSource', source);
  }

  function pageIdentity(): string {
    return (
      state.localContext?.candidateIdentity ||
      state.identity.candidateIdentity ||
      state.observation?.title ||
      state.browser.title ||
      ''
    ).trim();
  }

  function confirmIdentity(localModKey: string | null) {
    const identity = pageIdentity();
    if (identity.length === 0) {
      lastError = {
        code: 'identity.missing',
        message: 'The page has no title. Enter a URL with a readable page title.'
      };
      return;
    }

    send('identity.confirm', {
      candidateIdentity: identity,
      localModKey
    });
  }

  function openInspector() {
    if (state.localContext?.localModKey) {
      inspectorOpen = true;
      send('inspector.open', { modKey: state.localContext.localModKey });
    }
  }

  function hasConclusion(): boolean {
    const status = state.localContext?.status;
    return status === 'installed' || status === 'notInstalled';
  }

  function showRecognitionFallback(): boolean {
    return Boolean(state.observation) && !hasConclusion();
  }

  function formatLabel(value: string | null | undefined): string {
    if (!value) {
      return 'Unknown';
    }
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/-/g, ' ')
      .replace(/^./, (character) => character.toUpperCase());
  }

  function statusClass(status: string | undefined): string {
    return 'status-' + (status ?? 'unknown').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function sizeLabel(size: number): string {
    if (size < 1024) {
      return size + ' B';
    }
    return (size / 1024).toFixed(1) + ' KB';
  }
</script>

<svelte:head>
  <title>ModScope</title>
</svelte:head>

<main class="shell">
  <header class="brand-bar">
    <div>
      <span class="eyebrow">MOD WORKSPACE</span>
      <h1>ModScope</h1>
    </div>
    <span class="muted-badge">Read-only</span>
  </header>

  {#if lastError}
    <p class="error-banner"><strong>{lastError.code}</strong> {lastError.message}</p>
  {/if}

  <section class="browser-chrome panel">
    <div class="navigation-row">
      <button class="icon-button" title="Back" aria-label="Back" disabled={!state.browser.canGoBack} onclick={() => send('browser.back')}>←</button>
      <button class="icon-button" title="Forward" aria-label="Forward" disabled={!state.browser.canGoForward} onclick={() => send('browser.forward')}>→</button>
      <button class="icon-button" title="Reload" aria-label="Reload" onclick={() => send('browser.reload')}>↻</button>
      <input
        aria-label="URL"
        class="address-input"
        bind:value={address}
        onkeydown={(event) => event.key === 'Enter' && navigate()}
      />
    </div>
    <div class="page-meta">
      <span class="page-title">{state.browser.title || 'No page title'}</span>
      <span class="page-url">{state.browser.url}</span>
    </div>
  </section>

  <section class="panel context-summary-panel">
    {#if hasConclusion() && state.localContext}
      <div class="summary-header">
        <div>
          <span class="eyebrow">LOCAL CONTEXT</span>
          <h2>{pageIdentity() || 'Current page'}</h2>
          <p class="summary-meta">
            {state.localContext.profileName || 'Profile unknown'}
            {#if state.localContext.knownVersion} · v{state.localContext.knownVersion}{/if}
          </p>
        </div>
        <span class="status-chip {statusClass(state.localContext.status)}">
          {formatLabel(state.localContext.status)}
        </span>
      </div>

      <div class="summary-grid">
        <div><span>Enabled</span><strong>{formatLabel(state.localContext.enabledState)}</strong></div>
        <div><span>Priority</span><strong>{state.localContext.priority ?? 'Unknown'}</strong></div>
        <div><span>Version</span><strong>{state.localContext.knownVersion || 'Unknown'}</strong></div>
        <div><span>Profile</span><strong>{state.localContext.profileName || 'Unknown'}</strong></div>
      </div>

      {#if state.localContext.evidence.length > 0}
        <div class="evidence-strip">
          <span class="eyebrow">EVIDENCE</span>
          {#each state.localContext.evidence as evidence}
            <span class="evidence-tag">
              <span class="status-dot" aria-hidden="true">✓</span>
              {formatLabel(evidence.kind)} · <code>{evidence.source.relativePath}</code>
            </span>
          {/each}
        </div>
      {/if}

      {#if state.localContext.uncertainties.length > 0}
        <div class="notice-list">
          {#each state.localContext.uncertainties as uncertainty}
            <p class="notice">{uncertainty}</p>
          {/each}
        </div>
      {/if}

      {#if state.localContext.diagnostics.length > 0}
        <div class="diagnostic-list">
          {#each state.localContext.diagnostics as diagnostic}
            <p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
          {/each}
        </div>
      {/if}

      {#if state.localContext.status === 'installed' && state.localContext.localModKey}
        <button class="primary-button action-button" onclick={openInspector}>Inspect</button>
      {/if}
    {:else if showRecognitionFallback()}
      <div class="exception-card">
        <span class="eyebrow">RECOGNIZE</span>
        <h2>Couldn’t recognize this page</h2>
        <p class="subtle">Choose a local MOD or mark this page as not installed.</p>

        {#if state.localContext}
          <span class="status-chip {statusClass(state.localContext.status)}">
            {formatLabel(state.localContext.status)}
          </span>
        {/if}

        {#if state.knowledge.candidates.length > 0}
          <label class="select-label">
            Local MOD
            <select bind:value={selectedModKey}>
              <option value="">Choose a local MOD</option>
              {#each state.knowledge.candidates as candidate (candidate.modKey)}
                <option value={candidate.modKey}>
                  {candidate.displayName || candidate.directoryName}
                  {candidate.version ? ' · v' + candidate.version : ''}
                </option>
              {/each}
            </select>
          </label>
          <div class="action-row">
            <button class="primary-button" disabled={!selectedModKey} onclick={() => confirmIdentity(selectedModKey)}>Choose local mod</button>
            <button class="secondary-button" onclick={() => confirmIdentity(null)}>Mark as not installed</button>
          </div>
        {:else}
          <p class="notice">No local MOD candidates are loaded. Open Developer tools to load a profile.</p>
          <button class="secondary-button" onclick={() => (developerToolsOpen = true)}>Open Developer tools</button>
        {/if}

        {#if state.localContext?.uncertainties.length}
          <div class="notice-list">
            {#each state.localContext.uncertainties as uncertainty}
              <p class="notice">{uncertainty}</p>
            {/each}
          </div>
        {/if}

        {#if state.localContext?.diagnostics.length}
          <div class="diagnostic-list">
            {#each state.localContext.diagnostics as diagnostic}
              <p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
            {/each}
          </div>
        {/if}
      </div>
    {:else}
      <div class="empty-card">
        <span class="eyebrow">LOCAL CONTEXT</span>
        <h2>Browse a MOD page</h2>
        <p class="subtle">ModScope will observe the current page and show local context here.</p>
      </div>
    {/if}
  </section>

  {#if state.observation}
    <details class="panel page-details" bind:open={pageDetailsOpen}>
      <summary>
        <span>Page details</span>
        <span class="status-chip status-{state.observation.extractionStatus}">{formatLabel(state.observation.extractionStatus)}</span>
      </summary>
      <div class="page-details-grid">
        <div><span>Title</span><strong>{state.observation.title || 'Untitled page'}</strong></div>
        <div><span>Observed</span><strong>{state.observation.observedAtUtc}</strong></div>
      </div>
      <pre>{state.observation.contentPreview || 'No body text was returned.'}</pre>
      {#if state.observation.diagnostics.length > 0}
        {#each state.observation.diagnostics as diagnostic}
          <p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
        {/each}
      {/if}
    </details>
  {/if}

  <details class="panel developer-tools" bind:open={developerToolsOpen}>
    <summary>
      <span>
        <span class="eyebrow">DEVELOPER</span>
        <strong>Developer tools</strong>
      </span>
      <span class="muted-badge">Read-only</span>
    </summary>

    <div class="developer-actions">
      <button class="secondary-button" onclick={() => send('knowledge.useFixture')}>Use fixture</button>
      <button class="primary-button" onclick={loadSource}>Load source</button>
      <button class="secondary-button" onclick={() => send('browser.observe')}>Observe now</button>
    </div>

    <details class="source-details">
      <summary>Explicit MO2 source paths</summary>
      <div class="source-grid">
        <label>Instance name<input bind:value={source.instanceName} /></label>
        <label>Profile name<input bind:value={source.profileName} /></label>
        <label>Instance root<input bind:value={source.instanceRootPath} /></label>
        <label>Profile path<input bind:value={source.profilePath} /></label>
        <label>Mods path<input bind:value={source.modsPath} /></label>
      </div>
    </details>

    {#if state.knowledge.session}
      <p class="subtle developer-status">
        {state.knowledge.session.instanceName} / {state.knowledge.session.profileName}
        · {state.knowledge.candidates.length} MOD records
      </p>
    {/if}
    {#if state.statusMessage}
      <p class="subtle developer-status">{state.statusMessage}</p>
    {/if}
  </details>

  {#if inspectorOpen}
    <aside class="inspector-drawer">
      <div class="drawer-heading">
        <div>
          <span class="eyebrow">INSPECTOR</span>
          <h2>{state.inspector?.directoryName || 'Loading evidence'}</h2>
        </div>
        <button class="icon-button" title="Close inspector" aria-label="Close inspector" onclick={() => (inspectorOpen = false)}>×</button>
      </div>

      {#if state.inspector}
        {#if state.inspector.modInfo}
          <div class="drawer-section">
            <span class="eyebrow">METADATA</span>
            <dl>
              <dt>Display name</dt><dd>{state.inspector.modInfo.displayName || 'Unknown'}</dd>
              <dt>Version</dt><dd>{state.inspector.modInfo.version || 'Unknown'}</dd>
              <dt>Author</dt><dd>{state.inspector.modInfo.author || 'Unknown'}</dd>
              <dt>Parse status</dt><dd>{formatLabel(state.inspector.modInfo.parseStatus)}</dd>
            </dl>
          </div>
        {/if}

        <div class="drawer-section">
          <span class="eyebrow">FILES · {state.inspector.files.length}</span>
          <ul class="compact-list">
            {#each state.inspector.files as file}
              <li><code>{file.relativePath}</code><span>{sizeLabel(file.size)}</span></li>
            {/each}
          </ul>
        </div>

        <div class="drawer-section">
          <span class="eyebrow">XML · {state.inspector.xmlFiles.length}</span>
          {#each state.inspector.xmlFiles as xml}
            <details class="xml-item">
              <summary><code>{xml.relativePath}</code><span>{formatLabel(xml.parseStatus)}</span></summary>
              <p>{xml.rootElementName || 'Unknown root'} · {xml.elementCount} elements · {xml.attributeCount} attributes</p>
              {#each xml.xpathCandidates as xpath}
                <code class="block-code">{xpath.rawValue}</code>
              {/each}
              {#each xml.diagnostics as diagnostic}
                <p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
              {/each}
            </details>
          {/each}
        </div>

        {#if state.inspector.diagnostics.length > 0}
          <div class="drawer-section">
            <span class="eyebrow">DIAGNOSTICS</span>
            {#each state.inspector.diagnostics as diagnostic}
              <p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
            {/each}
          </div>
        {/if}
      {:else}
        <p class="empty-state">Inspector evidence is loading.</p>
      {/if}
    </aside>
  {/if}
</main>
