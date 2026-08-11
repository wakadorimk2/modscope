<script lang="ts">
  import { onMount } from 'svelte';
  import { createBridge, type Bridge } from './bridge';
  import { initialState, type HostMessage, type UiState } from './contracts';

  let state: UiState = initialState;
  let address = initialState.browser.url;
  let identity = '';
  let selectedModKey = '';
  let inspectorOpen = false;
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
      identity = state.identity.candidateIdentity;
      selectedModKey = state.identity.selectedLocalModKey ?? '';
      if (state.inspector) {
        inspectorOpen = true;
      }
    }
  }

  function send(command: string, payload: unknown = {}) {
    bridge?.send(command, payload);
  }

  function navigate() {
    send('browser.navigate', { url: address.trim() });
  }

  function loadSource() {
    send('knowledge.loadSource', source);
  }

  function confirmIdentity(localModKey: string | null) {
    send('identity.confirm', {
      candidateIdentity: identity.trim(),
      localModKey
    });
  }

  function openInspector() {
    if (state.localContext?.localModKey) {
      inspectorOpen = true;
      send('inspector.open', { modKey: state.localContext.localModKey });
    }
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
    <p class="status-line">{state.statusMessage}</p>
  </header>

  <section class="browser-chrome panel">
    <div class="navigation-row">
      <button class="icon-button" title="Back" disabled={!state.browser.canGoBack} onclick={() => send('browser.back')}>←</button>
      <button class="icon-button" title="Forward" disabled={!state.browser.canGoForward} onclick={() => send('browser.forward')}>→</button>
      <button class="icon-button" title="Reload" onclick={() => send('browser.reload')}>↻</button>
      <input
        aria-label="URL"
        class="address-input"
        bind:value={address}
        onkeydown={(event) => event.key === 'Enter' && navigate()}
      />
      <button class="primary-button" onclick={navigate}>Navigate</button>
      <button class="secondary-button" onclick={() => send('browser.observe')}>Observe</button>
    </div>
    <div class="page-meta">
      <span class="page-title">{state.browser.title || 'No page title'}</span>
      <span class="page-url">{state.browser.url}</span>
    </div>
  </section>

  <section class="panel source-panel">
    <div class="section-heading">
      <div>
        <span class="eyebrow">LOCAL KNOWLEDGE</span>
        <h2>Choose a source</h2>
      </div>
      <div class="button-row">
        <button class="secondary-button" onclick={() => send('knowledge.useFixture')}>Use fixture</button>
        <button class="primary-button" onclick={loadSource}>Load source</button>
      </div>
    </div>
    <details>
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
      <p class="subtle">
        {state.knowledge.session.instanceName} / {state.knowledge.session.profileName}
        · {state.knowledge.candidates.length} MOD records
      </p>
    {/if}
  </section>

  <section class="panel identity-panel">
    <div class="section-heading">
      <div>
        <span class="eyebrow">RECOGNIZE</span>
        <h2>Confirm page identity</h2>
      </div>
      <span class="muted-badge">Manual confirmation</span>
    </div>
    <div class="identity-grid">
      <label>
        Page MOD identity
        <input bind:value={identity} placeholder="Example: Alpha Mod" />
      </label>
      <label>
        Local MOD record
        <select bind:value={selectedModKey}>
          <option value="">No local match</option>
          {#each state.knowledge.candidates as candidate (candidate.modKey)}
            <option value={candidate.modKey}>
              {candidate.displayName || candidate.directoryName}
              {candidate.version ? ' · v' + candidate.version : ''}
            </option>
          {/each}
        </select>
      </label>
    </div>
    <div class="button-row">
      <button class="primary-button" onclick={() => confirmIdentity(selectedModKey || null)}>Confirm identity</button>
      <button class="secondary-button" onclick={() => confirmIdentity(null)}>Confirm not installed</button>
    </div>
  </section>

  <section class="panel context-panel">
    <div class="section-heading">
      <div>
        <span class="eyebrow">LOCAL AWARENESS</span>
        <h2>Current context</h2>
      </div>
      {#if state.localContext}
        <span class:status-installed={state.localContext.status === 'installed'} class="status-chip {statusClass(state.localContext.status)}">
          {formatLabel(state.localContext.status)}
        </span>
      {/if}
    </div>

    {#if state.localContext}
      <div class="context-grid">
        <div><span>Profile</span><strong>{state.localContext.profileName || 'Unknown'}</strong></div>
        <div><span>Enabled</span><strong>{formatLabel(state.localContext.enabledState)}</strong></div>
        <div><span>Priority</span><strong>{state.localContext.priority ?? 'Unknown'}</strong></div>
        <div><span>Known version</span><strong>{state.localContext.knownVersion || 'Unknown'}</strong></div>
      </div>

      {#if state.localContext.evidence.length > 0}
        <div class="subsection">
          <span class="eyebrow">EVIDENCE</span>
          {#each state.localContext.evidence as evidence}
            <div class="evidence-row">
              <span class="status-chip status-source">{formatLabel(evidence.kind)}</span>
              <code>{evidence.source.relativePath}</code>
            </div>
          {/each}
        </div>
      {/if}

      {#if state.localContext.uncertainties.length > 0}
        <div class="subsection">
          <span class="eyebrow">UNCERTAINTY</span>
          {#each state.localContext.uncertainties as uncertainty}
            <p class="notice">{uncertainty}</p>
          {/each}
        </div>
      {/if}

      {#if state.localContext.diagnostics.length > 0}
        <div class="subsection">
          <span class="eyebrow">DIAGNOSTICS</span>
          {#each state.localContext.diagnostics as diagnostic}
            <p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
          {/each}
        </div>
      {/if}

      <button class="wide-button" disabled={state.localContext.status !== 'installed'} onclick={openInspector}>
        Inspect local evidence
      </button>
    {:else}
      <p class="empty-state">Observe a page and confirm its identity to reveal local context.</p>
    {/if}
  </section>

  {#if state.observation}
    <section class="panel observation-panel">
      <div class="section-heading">
        <div>
          <span class="eyebrow">OBSERVATION</span>
          <h2>{state.observation.title || 'Untitled page'}</h2>
        </div>
        <span class="status-chip status-{state.observation.extractionStatus}">{formatLabel(state.observation.extractionStatus)}</span>
      </div>
      <pre>{state.observation.contentPreview || 'No body text was returned.'}</pre>
    </section>
  {/if}

  {#if inspectorOpen}
    <aside class="inspector-drawer">
      <div class="drawer-heading">
        <div>
          <span class="eyebrow">INSPECTOR</span>
          <h2>{state.inspector?.directoryName || 'Loading evidence'}</h2>
        </div>
        <button class="icon-button" title="Close inspector" onclick={() => (inspectorOpen = false)}>×</button>
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
