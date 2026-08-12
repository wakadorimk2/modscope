<script lang="ts">
  import { onMount } from 'svelte';
  import { createBridge, type Bridge } from './bridge';
  import {
    initialState,
    type BridgeErrorPayload,
    type DiagnosticUiState,
    type HostMessage,
    type UiState
  } from './contracts';

  const surface = new URLSearchParams(window.location.search).get('surface') === 'toolbar'
    ? 'toolbar'
    : 'context';

  let state: UiState = initialState;
  let address = initialState.browser.url;
  let selectedModKey = '';
  let inspectorOpen = false;
  let pageDetailsOpen = false;
  let developerToolsOpen = false;
  let lastError: BridgeErrorPayload | null = null;
  let bridge: Bridge | undefined;
  let operationRailTimer: number | undefined;
  let operationRailVisible = false;
  let source = {
    instanceName: 'explicit-instance',
    profileName: 'default',
    instanceRootPath: '',
    profilePath: '',
    modsPath: ''
  };

  $: {
    const operationBusy = state.knowledge.operation.isBusy;
    if (operationBusy && !operationRailVisible && operationRailTimer === undefined) {
      operationRailTimer = window.setTimeout(() => {
        operationRailTimer = undefined;
        if (state.knowledge.operation.isBusy) {
          operationRailVisible = true;
        }
      }, 150);
    } else if (!operationBusy) {
      if (operationRailTimer !== undefined) {
        window.clearTimeout(operationRailTimer);
        operationRailTimer = undefined;
      }
      operationRailVisible = false;
    }
  }

  type DiagnosticGroup = {
    diagnostic: DiagnosticUiState;
    count: number;
  };

  function groupDiagnostics(diagnostics: DiagnosticUiState[]): DiagnosticGroup[] {
    const groups = new Map<string, DiagnosticGroup>();

    for (const diagnostic of diagnostics) {
      const key = [
        diagnostic.code,
        diagnostic.severity,
        diagnostic.message,
        diagnostic.rawValue ?? ''
      ].join('\u0000');
      const existing = groups.get(key);

      if (existing) {
        existing.count += 1;
      } else {
        groups.set(key, { diagnostic, count: 1 });
      }
    }

    return Array.from(groups.values());
  }

  function diagnosticClass(severity: string): string {
    return `diagnostic diagnostic-${severity.toLowerCase()}`;
  }

  onMount(() => {
    bridge = createBridge(handleHostMessage);
    const disconnect = bridge.connect();
    window.addEventListener('keydown', handleShortcut);

    return () => {
      disconnect();
      if (operationRailTimer !== undefined) {
        window.clearTimeout(operationRailTimer);
      }
      window.removeEventListener('keydown', handleShortcut);
    };
  });

  function handleHostMessage(message: HostMessage) {
    if (message.kind === 'state') {
      state = message.payload;
      address = state.browser.url;
      selectedModKey = state.identity.selectedLocalModKey ?? '';
      lastError = null;
      inspectorOpen = Boolean(state.inspector);
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

  function switchProfile(event: Event) {
    const profileName = (event.currentTarget as HTMLSelectElement).value;
    if (profileName.length > 0) {
      send('knowledge.switchProfile', { profileName });
    }
  }

  function toggleContext() {
    send('layout.setContextVisible', { visible: !state.layout.contextVisible });
  }

  function handleShortcut(event: KeyboardEvent) {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'i') {
      event.preventDefault();
      toggleContext();
    }
  }

  function loadSource() {
    send('knowledge.loadSource', source);
  }

  function discoverSources() {
    send('knowledge.discoverSources');
  }

  function selectSource(candidateId: string) {
    send('knowledge.selectSource', { candidateId });
  }

  function selectRoot() {
    send('knowledge.selectRoot');
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

  function formatLabel(value: string | null | undefined): string {
    if (!value) {
      return 'Unknown';
    }
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/-/g, ' ')
      .replace(/:/g, ' · ')
      .replace(/^./, (character) => character.toUpperCase());
  }

  function statusClass(status: string | undefined): string {
    return 'status-' + (status ?? 'unknown').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function operationLabel(): string {
    const operation = state.knowledge.operation;
    const profile = operation.targetProfileName ? ` ${operation.targetProfileName}` : '';

    switch (operation.phase) {
      case 'discovering-source':
        return 'Finding MO2 source';
      case 'reading-profile':
        return `Reading profile${profile}`;
      case 'checking-cache':
        return 'Checking static MOD knowledge';
      case 'scanning-mod-folders':
        return `Scanning MOD folders${profile}`;
      case 'reusing-static-knowledge':
        return 'Reusing static MOD knowledge';
      case 'building-index':
        return 'Building local knowledge index';
      case 'projecting-profile':
        return `Applying profile${profile}`;
      default:
        return 'Loading local MO2 knowledge';
    }
  }

  function operationProgress(): number | null {
    const { completed, total } = state.knowledge.operation;
    if (typeof completed !== 'number' || typeof total !== 'number' || total <= 0) {
      return null;
    }

    return Math.min(100, Math.max(0, (completed / total) * 100));
  }

  function operationCountLabel(): string | null {
    const { completed, total } = state.knowledge.operation;
    if (typeof completed !== 'number' || typeof total !== 'number') {
      return null;
    }

    return `${completed} / ${total} MOD folders`;
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

{#if surface === 'toolbar'}
  <main class="toolbar-surface">
    {#if operationRailVisible}
      <div class="operation-rail">
        <div
          class="operation-progress-track"
          role="progressbar"
          aria-valuemin="0"
          aria-valuemax="100"
          aria-valuenow={operationProgress() ?? undefined}
          aria-label={operationLabel()}
        >
          <div
            class="operation-progress-fill"
            class:operation-progress-indeterminate={operationProgress() === null}
            style:width={operationProgress() === null ? undefined : `${operationProgress()}%`}
          ></div>
        </div>
        <div class="operation-rail-meta" role="status" aria-live="polite">
          <span class="operation-rail-label">{operationLabel()}…</span>
          {#if operationCountLabel()}<span class="operation-rail-count">{operationCountLabel()}</span>{/if}
        </div>
      </div>
    {/if}
    <div class="toolbar-row">
      <div class="toolbar-navigation" aria-label="Browser navigation">
        <button class="icon-button" title="Back" aria-label="Back" disabled={!state.browser.canGoBack} onclick={() => send('browser.back')}>←</button>
        <button class="icon-button" title="Forward" aria-label="Forward" disabled={!state.browser.canGoForward} onclick={() => send('browser.forward')}>→</button>
        <button class="icon-button" title="Reload" aria-label="Reload" onclick={() => send('browser.reload')}>↻</button>
      </div>

      <input
        aria-label="URL"
        class="toolbar-address"
        bind:value={address}
        onkeydown={(event) => event.key === 'Enter' && navigate()}
      />

      <label class="profile-picker">
        <span>7 Days to Die ·</span>
        <select
          aria-label="Active profile"
          value={state.knowledge.session?.profileName ?? ''}
          disabled={state.knowledge.profiles.length === 0 || state.knowledge.operation.isBusy}
          onchange={switchProfile}
        >
          {#if state.knowledge.profiles.length === 0}
            <option value="">No profile</option>
          {:else}
            {#each state.knowledge.profiles as profile (profile.name)}
              <option value={profile.name}>{profile.name}</option>
            {/each}
          {/if}
        </select>
      </label>
      <button class="toolbar-context-button" onclick={toggleContext}>
        {state.layout.contextVisible ? 'Context' : 'Show context'}
      </button>
      <span class="shortcut-hint">Ctrl/Cmd+I</span>
    </div>

    {#if lastError}
      <p class="error-notice"><strong>{lastError.code}</strong> {lastError.message}</p>
    {/if}
  </main>
{:else}
  <main class="shell">
    {#if operationRailVisible}
      <div class="operation-rail">
        <div
          class="operation-progress-track"
          role="progressbar"
          aria-valuemin="0"
          aria-valuemax="100"
          aria-valuenow={operationProgress() ?? undefined}
          aria-label={operationLabel()}
        >
          <div
            class="operation-progress-fill"
            class:operation-progress-indeterminate={operationProgress() === null}
            style:width={operationProgress() === null ? undefined : `${operationProgress()}%`}
          ></div>
        </div>
        <div class="operation-rail-meta" role="status" aria-live="polite">
          <span class="operation-rail-label">{operationLabel()}…</span>
          {#if operationCountLabel()}<span class="operation-rail-count">{operationCountLabel()}</span>{/if}
        </div>
      </div>
    {/if}
    <header class="brand-bar">
      <div>
        <span class="eyebrow">MOD WORKSPACE</span>
        <h1>ModScope</h1>
      </div>
      <span class="muted-badge">Read-only</span>
    </header>

    {#if lastError}
      <p class="error-notice"><strong>{lastError.code}</strong> {lastError.message}</p>
    {/if}

    {#if state.sourceDiscovery.candidates.length > 0 || !state.knowledge.session}
      <section class="panel source-discovery-panel">
        <div class="summary-header">
          <div>
            <span class="eyebrow">MO2 SOURCE</span>
            <h2>Choose a local source</h2>
            <p class="summary-meta">ModScope checks known MO2 locations and keeps this read-only.</p>
          </div>
          <span class="muted-badge">No absolute paths sent to Web</span>
        </div>

        {#if state.sourceDiscovery.candidates.length === 0}
          <p class="notice">No MO2 source is ready. Scan again or choose an MO2 instance folder.</p>
        {:else}
          <div class="source-candidate-list">
            {#each state.sourceDiscovery.candidates as candidate (candidate.candidateId)}
              <article class="source-candidate-card">
                <div class="source-candidate-header">
                  <div>
                    <strong>{candidate.instanceName || 'Unknown instance'} · {candidate.profileName || 'Profile unknown'}</strong>
                    <p class="subtle">{candidate.gameName || 'Game unknown'}</p>
                  </div>
                  <span class="status-chip {statusClass(candidate.readiness)}">{formatLabel(candidate.readiness)}</span>
                </div>

                {#if candidate.evidence.length > 0}
                  <div class="evidence-strip">
                    {#each candidate.evidence as evidence}
                      <span class="evidence-tag">{formatLabel(evidence)}</span>
                    {/each}
                  </div>
                {/if}

                {#if candidate.diagnostics.length > 0}
                  <div class="diagnostic-list">
                    {#each groupDiagnostics(candidate.diagnostics) as group}
                      <p class={diagnosticClass(group.diagnostic.severity)}>
                        <strong>{group.diagnostic.code}</strong>
                        {#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}
                        {group.diagnostic.message}
                      </p>
                    {/each}
                  </div>
                {/if}

                {#if candidate.isReady}
                  <button
                    class="primary-button action-button"
                    disabled={state.knowledge.operation.isBusy}
                    onclick={() => selectSource(candidate.candidateId)}
                  >
                    Use this source
                  </button>
                {/if}
              </article>
            {/each}
          </div>
        {/if}

        <div class="action-row">
          <button class="secondary-button" disabled={state.knowledge.operation.isBusy} onclick={discoverSources}>Scan again</button>
          <button class="secondary-button" disabled={state.knowledge.operation.isBusy} onclick={selectRoot}>Select MO2 folder</button>
        </div>
      </section>
    {/if}

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
            {#each groupDiagnostics(state.localContext.diagnostics) as group}
              <p class={diagnosticClass(group.diagnostic.severity)}>
                <strong>{group.diagnostic.code}</strong>
                {#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}
                {group.diagnostic.message}
                {#if group.diagnostic.rawValue}<code class="diagnostic-raw">{group.diagnostic.rawValue}</code>{/if}
                {#if group.diagnostic.source}<span class="diagnostic-source">Example: {group.diagnostic.source.relativePath}</span>{/if}
              </p>
            {/each}
          </div>
        {/if}

        {#if state.localContext.status === 'installed' && state.localContext.localModKey}
          <button class="primary-button action-button" onclick={openInspector}>Inspect</button>
        {/if}
      {:else if state.observation && !hasConclusion()}
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
              {#each groupDiagnostics(state.localContext.diagnostics) as group}
                <p class={diagnosticClass(group.diagnostic.severity)}>
                  <strong>{group.diagnostic.code}</strong>
                  {#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}
                  {group.diagnostic.message}
                  {#if group.diagnostic.rawValue}<code class="diagnostic-raw">{group.diagnostic.rawValue}</code>{/if}
                  {#if group.diagnostic.source}<span class="diagnostic-source">Example: {group.diagnostic.source.relativePath}</span>{/if}
                </p>
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

    {#if state.diagnostics.length > 0}
      {@const diagnosticGroups = groupDiagnostics(state.diagnostics)}
      <section class="panel diagnostics-panel">
        <span class="eyebrow">DIAGNOSTICS</span>
        <p class="diagnostic-summary">
          {diagnosticGroups.length} types · {state.diagnostics.length} occurrences
        </p>
        {#each diagnosticGroups as group}
          <p class={diagnosticClass(group.diagnostic.severity)}>
            <strong>{group.diagnostic.code}</strong>
            {#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}
            {group.diagnostic.message}
            {#if group.diagnostic.rawValue}<code class="diagnostic-raw">{group.diagnostic.rawValue}</code>{/if}
            {#if group.diagnostic.source}<span class="diagnostic-source">Example: {group.diagnostic.source.relativePath}</span>{/if}
          </p>
        {/each}
      </section>
    {/if}

    {#if state.observation}
      <details class="panel page-details" bind:open={pageDetailsOpen}>
        <summary>
          <span>Page details</span>
          <span class="status-chip {statusClass(state.observation.extractionStatus)}">{formatLabel(state.observation.extractionStatus)}</span>
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
        <button class="secondary-button" disabled={state.knowledge.operation.isBusy} onclick={() => send('knowledge.useFixture')}>Use fixture</button>
        <button class="primary-button" disabled={state.knowledge.operation.isBusy} onclick={loadSource}>Load source</button>
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
          · {state.knowledge.profiles.length} profiles
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

          <div class="drawer-section">
            <span class="eyebrow">PROVENANCE</span>
            <p class="subtle">{state.inspector.source.kind} · {state.inspector.source.relativePath}</p>
          </div>
        {:else}
          <p class="empty-state">Inspector evidence is loading.</p>
        {/if}
      </aside>
    {/if}
  </main>
{/if}
