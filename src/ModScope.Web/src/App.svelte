<script lang="ts">
  import { onMount } from 'svelte';
  import { createBridge, type Bridge } from './bridge';
  import {
    initialState,
    type BridgeErrorPayload,
    type DiagnosticUiState,
    type HostMessage,
    type ModCandidateUiState,
    type UiState
  } from './contracts';

  const requestedSurface = new URLSearchParams(window.location.search).get('surface');
  const surface = requestedSurface === 'toolbar'
    ? 'toolbar'
    : requestedSurface === 'mod-list'
      ? 'mod-list'
      : 'context';

  let state: UiState = initialState;
  let address = initialState.browser.url;
  let contextPanelMode: 'context' | 'inspector' = 'context';
  let inspectorView: 'mod' | 'diagnosis' = 'mod';
  let inspectorModKey: string | null = null;
  let dismissedInspectorModKey: string | null = null;
  let inspectorFilesOpen = false;
  let modSearchOpen = false;
  let modSearchMode: 'browse' | 'recognition' = 'browse';
  let modSearchQuery = '';
  let pageDetailsOpen = false;
  let developerToolsOpen = false;
  let contextMode: 'context' | 'settings' | 'debug' = 'context';
  let historyOpen = false;
  let lastError: BridgeErrorPayload | null = null;
  let bridge: Bridge | undefined;
  let operationRailTimer: number | undefined;
  let operationRailVisible = false;
  let runtimeToolVersion = '';
  let runtimeGameVersion = '';
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

  $: profileCandidates = sortCandidates(
    state.knowledge.candidates.filter((candidate) => candidate.profileState !== 'unlisted')
  );
  $: unlistedProfileCandidates = sortCandidates(
    state.knowledge.candidates.filter((candidate) => candidate.profileState === 'unlisted')
  );
  $: enabledProfileCandidates = profileCandidates.filter((candidate) => candidate.enabledState === 'enabled');
  $: disabledProfileCandidates = profileCandidates.filter((candidate) => candidate.enabledState === 'disabled');
  $: unresolvedProfileCandidates = profileCandidates.filter((candidate) => candidate.profileState === 'unresolved');
  $: unknownProfileCount = profileCandidates.length - enabledProfileCandidates.length - disabledProfileCandidates.length;
  $: modSearchResults = searchCandidates(state.knowledge.candidates, modSearchQuery);
  $: analysisBusy = state.analysis.operation.isBusy;
  $: analysisGroups = state.analysis.conflict?.groups ?? [];
  $: candidateAnalysisGroups = state.analysis.conflict && state.localContext?.localModKey
    ? state.analysis.conflict.groups.filter((group) => group.operations.some(
      (operation) => operation.modKey === state.localContext?.localModKey))
    : [];
  $: inspectorConflictGroups = state.inspector && state.analysis.conflict
    ? state.analysis.conflict.groups.filter((group) => group.operations.some(
      (operation) => operation.modKey === state.inspector?.modKey))
    : [];
  $: inspectorRuntimeItems = state.inspector && state.analysis.runtimeComparison
    ? state.analysis.runtimeComparison.items.filter((item) => item.observations.some(
      (observation) => observation.modKey === state.inspector?.modKey))
    : [];
  $: inspectorCandidate = state.inspector
    ? state.knowledge.candidates.find((candidate) => candidate.modKey === state.inspector?.modKey)
    : null;
  $: operationBlocksInteraction = analysisBusy
    || (state.knowledge.operation.isBusy && !state.knowledge.operation.isBackground);

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
      const addressIsBeingEdited = document.activeElement instanceof HTMLInputElement
        && document.activeElement.classList.contains('toolbar-address');
      state = message.payload;
      if (!addressIsBeingEdited) {
        address = state.browser.url;
      }
      lastError = null;
      if (surface === 'context' && state.inspector?.modKey) {
        if (state.inspector.modKey !== dismissedInspectorModKey) {
          if (state.inspector.modKey !== inspectorModKey) {
            inspectorFilesOpen = false;
          }
          contextPanelMode = 'inspector';
          inspectorView = 'mod';
          inspectorModKey = state.inspector.modKey;
        }
      }
      if (
        contextPanelMode === 'inspector'
        && inspectorView === 'mod'
        && inspectorModKey
        && state.inspector?.modKey !== inspectorModKey
      ) {
        closeInspector();
      }
      return;
    }

    if (message.kind === 'error') {
      lastError = message.payload;
    }
  }

  function send(command: string, payload: unknown = {}) {
    lastError = null;
    if (shouldCloseInspectorFor(command)) {
      closeInspector();
    }
    bridge?.send(command, payload);
  }

  function shouldCloseInspectorFor(command: string): boolean {
    return [
      'browser.home',
      'browser.newTab',
      'browser.selectHistory',
      'browser.selectTab',
      'browser.navigate',
      'identity.confirm',
      'knowledge.loadSource',
      'knowledge.selectRoot',
      'knowledge.selectSource',
      'knowledge.switchProfile',
      'knowledge.useFixture',
      'analysis.selectBaseData',
      'analysis.selectRuntimeLogs',
      'analysis.useFixture'
    ].includes(command);
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

  function toggleModList() {
    send('layout.setModListVisible', { visible: !state.layout.modListVisible });
  }

  function openNewTab() {
    send('browser.newTab');
  }

  function selectTab(tabId: string) {
    send('browser.selectTab', { tabId });
  }

  function closeTab(tabId: string) {
    send('browser.closeTab', { tabId });
  }

  function openHome() {
    send('browser.home');
  }

  function handleHistoryToggle(event: Event) {
    const details = event.currentTarget as HTMLDetailsElement;
    historyOpen = details.open;
    send('layout.setToolbarExpanded', { expanded: historyOpen });
  }

  function selectHistory(entryId: string) {
    historyOpen = false;
    send('layout.setToolbarExpanded', { expanded: false });
    send('browser.selectHistory', { entryId });
  }

  function formatVisitedAt(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.valueOf()) ? 'Unknown time' : date.toLocaleString();
  }

  function handleShortcut(event: KeyboardEvent) {
    if (event.key === 'Escape' && modSearchOpen) {
      event.preventDefault();
      closeModSearch();
      return;
    }

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
      openInspectorForMod(state.localContext.localModKey);
    } else {
      openProfileDiagnosis();
    }
  }

  function openInspectorForMod(modKey: string) {
    inspectorView = 'mod';
    inspectorModKey = modKey;
    dismissedInspectorModKey = null;
    inspectorFilesOpen = false;
    contextPanelMode = 'inspector';
    send('inspector.open', { modKey });
  }

  function openProfileDiagnosis() {
    inspectorView = 'diagnosis';
    inspectorModKey = null;
    dismissedInspectorModKey = null;
    inspectorFilesOpen = false;
    contextPanelMode = 'inspector';
  }

  function openAnalysisInspector() {
    if (state.localContext?.localModKey) {
      openInspectorForMod(state.localContext.localModKey);
      return;
    }

    openProfileDiagnosis();
  }

  function showCandidateCompare(): boolean {
    return inspectorView === 'mod';
  }

  function closeInspector() {
    dismissedInspectorModKey = state.inspector?.modKey ?? inspectorModKey;
    contextPanelMode = 'context';
    inspectorView = 'mod';
    inspectorModKey = null;
    inspectorFilesOpen = false;
  }

  function compareRuntimeEvidence() {
    send('analysis.compareRuntimeEvidence', {
      toolVersion: runtimeToolVersion.trim() || null,
      gameVersion: runtimeGameVersion.trim() || null
    });
  }

  function openModSearch(mode: 'browse' | 'recognition' = 'browse') {
    modSearchMode = mode;
    modSearchQuery = '';
    modSearchOpen = true;
  }

  function closeModSearch() {
    modSearchOpen = false;
    modSearchQuery = '';
    modSearchMode = 'browse';
  }

  function modDisplayName(candidate: ModCandidateUiState): string {
    return candidate.displayName || candidate.directoryName || candidate.modKey;
  }

  function roleRank(candidate: ModCandidateUiState): number {
    switch (candidate.role?.role) {
      case 'Foundation':
        return 0;
      case 'Compatibility':
        return 1;
      case 'Content':
        return 2;
      default:
        return 3;
    }
  }

  function roleLabel(candidate: ModCandidateUiState): string {
    return candidate.role?.role || 'Unknown';
  }

  function roleAssessmentLabel(candidate: ModCandidateUiState): string {
    return candidate.role?.assessment || 'Unknown';
  }

  function sortCandidates(candidates: ModCandidateUiState[]): ModCandidateUiState[] {
    return [...candidates].sort((left, right) => {
      const leftPriority = left.priority ?? Number.MAX_SAFE_INTEGER;
      const rightPriority = right.priority ?? Number.MAX_SAFE_INTEGER;
      return roleRank(left) - roleRank(right)
        || leftPriority - rightPriority
        || left.modKey.localeCompare(right.modKey);
    });
  }

  function isWebsiteUrl(value: string | null | undefined): value is string {
    const trimmed = value?.trim();
    if (!trimmed) {
      return false;
    }

    try {
      const url = new URL(trimmed);
      return url.protocol === 'http:' || url.protocol === 'https:';
    } catch {
      return false;
    }
  }

  type ModWebsiteLink = {
    url: string | null;
    status: 'Verified' | 'Inferred' | 'No usable URL';
  };

  function slugifyModName(value: string): string {
    return value
      .normalize('NFKD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/&/g, ' and ')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '');
  }

  function resolveModWebsite(candidate: ModCandidateUiState): ModWebsiteLink {
    if (isWebsiteUrl(candidate.website)) {
      return { url: candidate.website.trim(), status: 'Verified' };
    }

    const name = [candidate.displayName, candidate.directoryName, candidate.modKey]
      .map((value) => value?.trim() ?? '')
      .find((value) => value.length > 0) ?? '';
    if (!name) {
      return { url: null, status: 'No usable URL' };
    }

    const slug = slugifyModName(name);
    if (slug) {
      return {
        url: `https://www.nexusmods.com/7daystodie/mods/${slug}`,
        status: 'Inferred'
      };
    }

    return {
      url: `https://www.nexusmods.com/7daystodie/search/?gsearch=${encodeURIComponent(name)}`,
      status: 'Inferred'
    };
  }

  function searchCandidates(candidates: ModCandidateUiState[], query: string): ModCandidateUiState[] {
    const normalizedQuery = query.trim().toLowerCase();
    if (normalizedQuery.length === 0) {
      return [];
    }

    return candidates.filter((candidate) => [
      candidate.displayName,
      candidate.directoryName,
      candidate.modKey
    ]
      .filter((value): value is string => Boolean(value))
      .some((value) => value.toLowerCase().includes(normalizedQuery)));
  }

  function openModPage(candidate: ModCandidateUiState) {
    const website = resolveModWebsite(candidate);
    if (!website.url) {
      return;
    }

    closeModSearch();
    send('browser.navigate', { url: website.url });
  }

  function chooseModForRecognition(candidate: ModCandidateUiState) {
    confirmIdentity(candidate.modKey);
    if (!lastError) {
      closeModSearch();
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

  function analysisLabel(value: string | null | undefined): string {
    const normalized = (value ?? '').toLowerCase().replace(/[^a-z]+/g, '');
    switch (normalized) {
      case 'match':
        return 'Match';
      case 'different':
      case 'conflict':
        return 'Different';
      case 'possible':
        return 'Possible';
      case 'notassessed':
        return 'Not assessed';
      case 'inferred':
      case 'inferredmatch':
      case 'inferreddifferent':
        return 'Inferred';
      case 'runtimeonly':
      case 'staticonly':
        return 'Not assessed';
      case 'unknown':
      case '':
        return 'Unknown';
      default:
        return formatLabel(value);
    }
  }

  function analysisStatusClass(value: string | null | undefined): string {
    return 'analysis-status-' + (value ?? 'unknown').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function analysisOperationLabel(): string {
    switch (state.analysis.operation.kind) {
      case 'conflict-analysis':
        return 'Analyzing static XML conflicts';
      case 'runtime-comparison':
        return 'Comparing runtime evidence';
      default:
        return 'Analysis idle';
    }
  }

  function analysisSummaryStatus(): string {
    if (analysisBusy) {
      return 'Running';
    }

    if (state.analysis.diagnostics.some((diagnostic) => diagnostic.severity.toLowerCase() === 'error')) {
      return 'Issue';
    }

    return state.analysis.conflict ? 'Assessed' : 'Not assessed';
  }

  function analysisSummaryStatusClass(): string {
    if (analysisBusy) {
      return 'analysis-status-possible';
    }

    if (state.analysis.diagnostics.some((diagnostic) => diagnostic.severity.toLowerCase() === 'error')) {
      return 'analysis-status-different';
    }

    return state.analysis.conflict ? 'analysis-status-ready' : 'analysis-status-not-assessed';
  }

  function staticAnalysisActionLabel(): string {
    if (!state.analysis.inputs.baseDataReady) {
      return 'Select base Data/Config';
    }

    return state.analysis.conflict ? 'Re-run static analysis' : 'Analyze static';
  }

  function startStaticAnalysis() {
    if (!state.analysis.inputs.baseDataReady) {
      send('analysis.selectBaseData');
      return;
    }

    send('analysis.analyzeConflicts');
  }

  function modTooltip(candidate: ModCandidateUiState): string {
    const websiteLink = resolveModWebsite(candidate);
    const website = websiteLink.status === 'Verified'
      ? 'Verified Website'
      : websiteLink.status;
    return [
      `${roleLabel(candidate)} · ${roleAssessmentLabel(candidate)}`,
      `Priority ${candidate.priority ?? 'Unknown'}`,
      website
    ].join(' · ');
  }

  function roleReasonSummary(candidate: ModCandidateUiState): string {
    const reason = candidate.role?.reason?.trim() ?? '';
    if (reason.length === 0) {
      return 'Unknown';
    }

    const match = reason.match(/^.*?(?:[.!?](?:\s|$)|$)/);
    const summary = (match?.[0] ?? reason).trim().replace(/[.!?]$/, '');
    return summary || 'Unknown';
  }

  function enabledLampLabel(candidate: ModCandidateUiState): string {
    return candidate.enabledState === 'enabled' ? 'Enabled' : formatLabel(candidate.enabledState);
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
      case 'preloading-profile':
        return `Preparing profile${profile}`;
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

    return state.knowledge.operation.phase === 'preloading-profile'
      ? `${completed} / ${total} profiles`
      : `${completed} / ${total} MOD folders`;
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

{#if surface === 'mod-list'}
  <main class="mod-list-surface">
    <header class="mod-list-header">
      <div>
        <span class="eyebrow">ACTIVE PROFILE</span>
        <div class="mod-list-title-row">
          <h1>{state.knowledge.session?.profileName || state.knowledge.operation.targetProfileName || 'No profile'}</h1>
          {#if state.knowledge.session}
            {@const activeProfile = state.knowledge.profiles.find((profile) => profile.name === state.knowledge.session?.profileName)}
            <span class="status-chip {statusClass(activeProfile?.loadState)}">
              {formatLabel(activeProfile?.loadState || 'ready')}
            </span>
          {:else if state.knowledge.operation.isBusy}
            <span class="status-chip status-loading">{formatLabel(state.knowledge.operation.phase)}</span>
          {/if}
        </div>
      </div>
      <button
        class="icon-button"
        type="button"
        title="Collapse MOD list"
        aria-label="Collapse MOD list"
        onclick={() => send('layout.setModListVisible', { visible: false })}
      >×</button>
    </header>

    {#if operationRailVisible}
      <div class="mod-list-operation-rail">
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

    {#if state.knowledge.session}
      <label class="mod-list-profile-picker">
        <span>Profile</span>
        <select
          aria-label="Active profile"
          value={state.knowledge.session.profileName}
          disabled={operationBlocksInteraction}
          onchange={switchProfile}
        >
          {#each state.knowledge.profiles as profile (profile.name)}
            <option value={profile.name}>
              {profile.name} · {formatLabel(profile.loadState)}
            </option>
          {/each}
        </select>
      </label>

      <div class="profile-count-summary" aria-label="Profile MOD counts">
        <span>In profile <strong>{profileCandidates.length}</strong></span>
        <span>Enabled <strong>{enabledProfileCandidates.length}</strong></span>
        <span>Disabled <strong>{disabledProfileCandidates.length}</strong></span>
        <span>Unresolved <strong>{unresolvedProfileCandidates.length}</strong></span>
      </div>

      <div class="mod-list-scroll" aria-label="Active profile MOD list">
        {#if profileCandidates.length > 0}
          <div class="mod-list-section-label">PROFILE MODLIST · {profileCandidates.length}</div>
          <div class="mod-list-items">
            {#each profileCandidates as candidate (candidate.modKey)}
              <article
                class="mod-list-item"
                class:mod-list-item-disabled={candidate.enabledState === 'disabled'}
                class:mod-list-item-unresolved={candidate.profileState === 'unresolved'}
                title={modTooltip(candidate)}
              >
                <div class="mod-list-item-top">
                  {#if resolveModWebsite(candidate).url}
                    <button
                      type="button"
                      class="mod-list-item-main"
                      aria-label={`Open ${modDisplayName(candidate)} page · ${resolveModWebsite(candidate).status}`}
                      onclick={() => openModPage(candidate)}
                    >
                      <strong>{modDisplayName(candidate)}</strong>
                      {#if candidate.version}<span>v{candidate.version}</span>{/if}
                    </button>
                  {:else}
                    <div class="mod-list-item-main mod-card-main-disabled">
                      <strong>{modDisplayName(candidate)}</strong>
                      {#if candidate.version}<span>v{candidate.version}</span>{/if}
                    </div>
                  {/if}
                  <button
                    type="button"
                    class="icon-button compact-icon-button mod-list-item-inspect"
                    title="Inspect evidence"
                    aria-label={`Inspect ${modDisplayName(candidate)} evidence`}
                    onclick={() => openInspectorForMod(candidate.modKey)}
                  >⌕</button>
                  <span
                    class="mod-enabled-lamp"
                    class:enabled={candidate.enabledState === 'enabled'}
                    class:disabled={candidate.enabledState !== 'enabled'}
                    role="img"
                    aria-label={enabledLampLabel(candidate)}
                  ></span>
                </div>
                <span class="mod-list-item-tooltip" role="tooltip">{modTooltip(candidate)}</span>
              </article>
            {/each}
          </div>
        {:else}
          <p class="empty-state">No MOD entry is available in this profile.</p>
        {/if}

        <details class="profile-outside-section">
          <summary>Profile外 · {unlistedProfileCandidates.length}</summary>
          {#if unlistedProfileCandidates.length > 0}
            <div class="mod-list-items">
              {#each unlistedProfileCandidates as candidate (candidate.modKey)}
                <article
                  class="mod-list-item"
                  class:mod-list-item-disabled={candidate.enabledState === 'disabled'}
                  class:mod-list-item-unresolved={candidate.profileState === 'unresolved'}
                  title={modTooltip(candidate)}
                >
                  <div class="mod-list-item-top">
                    {#if resolveModWebsite(candidate).url}
                      <button
                        type="button"
                        class="mod-list-item-main"
                        aria-label={`Open ${modDisplayName(candidate)} page · ${resolveModWebsite(candidate).status}`}
                        onclick={() => openModPage(candidate)}
                      >
                        <strong>{modDisplayName(candidate)}</strong>
                        {#if candidate.version}<span>v{candidate.version}</span>{/if}
                      </button>
                    {:else}
                      <div class="mod-list-item-main mod-card-main-disabled">
                        <strong>{modDisplayName(candidate)}</strong>
                        {#if candidate.version}<span>v{candidate.version}</span>{/if}
                      </div>
                    {/if}
                    <button
                      type="button"
                      class="icon-button compact-icon-button mod-list-item-inspect"
                      title="Inspect evidence"
                      aria-label={`Inspect ${modDisplayName(candidate)} evidence`}
                      onclick={() => openInspectorForMod(candidate.modKey)}
                    >⌕</button>
                    <span
                      class="mod-enabled-lamp"
                      class:enabled={candidate.enabledState === 'enabled'}
                      class:disabled={candidate.enabledState !== 'enabled'}
                      role="img"
                      aria-label={enabledLampLabel(candidate)}
                    ></span>
                  </div>
                  <span class="mod-list-item-tooltip" role="tooltip">{modTooltip(candidate)} · Profile outside</span>
                </article>
              {/each}
            </div>
          {:else}
            <p class="empty-state">No MOD exists outside this profile.</p>
          {/if}
        </details>
      </div>
    {:else}
      <div class="mod-list-empty-state">
        <span class="eyebrow">LOCAL MODS</span>
        <p class="subtle">Load an MO2 source to show the active profile.</p>
      </div>
    {/if}
  </main>
{:else if surface === 'toolbar'}
  <main class="toolbar-surface">
    <div class="toolbar-tabs-row toolbar-row">
      <div class="browser-tabs" aria-label="Browser tabs">
        {#each state.browser.tabs as tab (tab.tabId)}
          <div
            class:active={tab.isActive}
            class="browser-tab"
          >
            <button type="button" class="browser-tab-select" aria-label={`Select tab ${tab.title || 'New tab'}`} aria-pressed={tab.isActive} onclick={() => selectTab(tab.tabId)}>
              <span>{tab.title || 'New tab'}</span>
            </button>
            <button
              type="button"
              class="browser-tab-close"
              title="Close tab"
              aria-label={`Close tab ${tab.title || 'New tab'}`}
              onclick={() => closeTab(tab.tabId)}
            >×</button>
          </div>
        {/each}
      </div>
      <button type="button" class="icon-button compact-icon-button" title="New tab" aria-label="New tab" onclick={openNewTab}>+</button>
    </div>

    <div class="toolbar-controls-row toolbar-row">
      <div class="toolbar-navigation" aria-label="Browser navigation">
        <button class="icon-button" title="Back" aria-label="Back" disabled={!state.browser.canGoBack} onclick={() => send('browser.back')}>←</button>
        <button class="icon-button" title="Forward" aria-label="Forward" disabled={!state.browser.canGoForward} onclick={() => send('browser.forward')}>→</button>
        <button class="icon-button" title="Reload" aria-label="Reload" onclick={() => send('browser.reload')}>↻</button>
      </div>

      <button class="icon-button" title="Home" aria-label="Open Browse Home" onclick={openHome}>⌂</button>

      <div class="toolbar-omnibox" aria-label="Browser address controls">
        <input
          class="toolbar-address"
          aria-label="Browser URL"
          placeholder="https://example.com"
          bind:value={address}
          onkeydown={(event) => event.key === 'Enter' && navigate()}
        />
        <button class="secondary-button toolbar-go-button" type="button" onclick={navigate}>Go</button>
      </div>

      <details
        class="history-menu"
        bind:open={historyOpen}
        ontoggle={handleHistoryToggle}
      >
        <summary
          class="icon-button"
          title={`History (${state.browser.history.length})`}
          aria-label={`Open history (${state.browser.history.length} entries)`}
        >◷</summary>
        <div class="history-popover">
          <strong>History</strong>
          {#if state.browser.history.length === 0}
            <p class="subtle">No visited pages.</p>
          {:else}
            {#each state.browser.history as entry (entry.entryId)}
              <button type="button" class="history-entry" onclick={() => selectHistory(entry.entryId)}>
                <strong>{entry.title || 'Untitled page'}</strong>
                <span>{entry.url}</span>
                <small>{formatVisitedAt(entry.visitedAtUtc)}</small>
              </button>
            {/each}
          {/if}
        </div>
      </details>

      <button
        class="pane-toggle-button"
        class:active={state.layout.modListVisible}
        title={state.layout.modListVisible ? 'Hide MOD list pane' : 'Show MOD list pane'}
        aria-label={state.layout.modListVisible ? 'Hide MOD list pane' : 'Show MOD list pane'}
        aria-pressed={state.layout.modListVisible}
        onclick={toggleModList}
      ><span aria-hidden="true">◧</span></button>
      <button
        class="pane-toggle-button"
        class:active={state.layout.contextVisible}
        title={state.layout.contextVisible ? 'Hide Context pane' : 'Show Context pane'}
        aria-label={state.layout.contextVisible ? 'Hide Context pane' : 'Show Context pane'}
        aria-pressed={state.layout.contextVisible}
        onclick={toggleContext}
      ><span aria-hidden="true">◨</span></button>
      <span class="shortcut-hint">Ctrl/Cmd+I</span>
    </div>

    {#if lastError}
      <p class="error-notice"><strong>{lastError.code}</strong> {lastError.message}</p>
    {/if}
  </main>
{:else}
  <main class="shell">
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

    <nav class="context-mode-switch" aria-label="Context mode">
      <button type="button" class:active={contextMode === 'context'} aria-pressed={contextMode === 'context'} onclick={() => (contextMode = 'context')}>Context</button>
      <button type="button" class:active={contextMode === 'settings'} aria-pressed={contextMode === 'settings'} onclick={() => (contextMode = 'settings')}>Settings</button>
      <button type="button" class:active={contextMode === 'debug'} aria-pressed={contextMode === 'debug'} onclick={() => (contextMode = 'debug')}>Debug</button>
    </nav>

    {#if contextMode === 'settings' || !state.knowledge.session}
      <section class="panel source-discovery-panel">
        <div class="summary-header">
          <div>
            <span class="eyebrow">{state.knowledge.session ? 'SETTINGS' : 'ONBOARDING'}</span>
            <h2>{state.knowledge.session ? 'MO2 source' : 'Choose a local source'}</h2>
            <p class="summary-meta">ModScope checks known MO2 locations and keeps this read-only.</p>
          </div>
          <span class="muted-badge">No absolute paths sent to Web</span>
        </div>

        {#if state.knowledge.session}
          <p class="source-status-line">
            Active source · {state.knowledge.session.instanceName || 'Unknown instance'} · {state.knowledge.session.profileName || 'Profile unknown'}
          </p>
          <div class="action-row">
            <button class="secondary-button" disabled={operationBlocksInteraction} onclick={discoverSources}>Reload source discovery</button>
            <button class="secondary-button" disabled={operationBlocksInteraction} onclick={selectRoot}>Change MO2 source</button>
          </div>
        {/if}

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
                    disabled={operationBlocksInteraction}
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
          <button class="secondary-button" disabled={operationBlocksInteraction} onclick={discoverSources}>Scan again</button>
          <button class="secondary-button" disabled={operationBlocksInteraction} onclick={selectRoot}>Select MO2 folder</button>
        </div>
      </section>
    {/if}

    {#if contextMode === 'context' && state.knowledge.session && contextPanelMode === 'context'}
      <section class="panel context-summary-panel" aria-labelledby="recognize-title">
        <div class="recognize-header">
          <div>
            <span class="eyebrow">RECOGNIZE</span>
            {#if hasConclusion() && state.localContext}
              <h2 id="recognize-title">{pageIdentity() || 'Current page'}</h2>
            {:else if state.observation}
              <h2 id="recognize-title">Couldn’t recognize this page</h2>
            {:else}
              <h2 id="recognize-title">Browse a MOD page</h2>
            {/if}
          </div>
          <button
            class="analysis-lamp"
            class:analysis-lamp-issue={analysisSummaryStatusClass() === 'analysis-status-different'}
            class:analysis-lamp-ready={analysisSummaryStatusClass() === 'analysis-status-ready'}
            disabled={operationBlocksInteraction}
            title={`Open analysis inspector · ${analysisSummaryStatus()}`}
            aria-label={`Open analysis inspector · ${analysisSummaryStatus()}`}
            onclick={openAnalysisInspector}
          >
            <span class="analysis-lamp-dot" aria-hidden="true"></span>
            <span>{analysisSummaryStatus()}</span>
          </button>
        </div>

        {#if hasConclusion() && state.localContext}
          <div class="local-summary-line" aria-label="Local MOD summary">
            <span class="status-chip {statusClass(state.localContext.status)}">{formatLabel(state.localContext.status)}</span>
            <span class="status-chip {statusClass(state.localContext.enabledState)}">{formatLabel(state.localContext.enabledState)}</span>
          </div>
          {#if state.localContext.status === 'installed' && state.localContext.localModKey}
            <button class="secondary-button action-button" onclick={openInspector}>Inspect MOD</button>
          {/if}
        {:else if state.observation}
          <p class="subtle">Choose a local MOD or mark this page as not installed.</p>
          {#if state.knowledge.candidates.length > 0}
            <p class="subtle">Search the local MOD catalog to confirm the page identity.</p>
            <div class="action-row">
              <button class="primary-button" type="button" onclick={() => openModSearch('recognition')}>Search local MODs</button>
              <button class="secondary-button" type="button" onclick={() => confirmIdentity(null)}>Mark as not installed</button>
            </div>
          {:else}
            <p class="notice">No local MOD candidates are loaded. Open Developer tools to load a profile.</p>
            <div class="action-row">
              <button class="secondary-button" type="button" onclick={() => { contextMode = 'debug'; developerToolsOpen = true; }}>Open Debug</button>
              <button class="secondary-button" type="button" onclick={() => confirmIdentity(null)}>Mark as not installed</button>
            </div>
          {/if}
        {:else}
          <p class="subtle">ModScope will observe the current page and show local context here.</p>
        {/if}
      </section>
    {/if}

    {#if contextMode === 'context' && state.knowledge.session && contextPanelMode === 'inspector' && inspectorView === 'diagnosis'}
    <section class="panel analysis-panel inspector-analysis-panel" aria-labelledby="analysis-title">
      <div class="inspector-mode-header">
        <div>
          <span class="eyebrow">INSPECTOR</span>
          <h2>{inspectorView === 'diagnosis' ? 'Profile diagnosis' : state.inspector?.directoryName || 'Loading evidence'}</h2>
        </div>
        <button class="secondary-button" type="button" onclick={closeInspector}>Back to Context</button>
      </div>
      <div class="summary-header">
        <div>
          <span class="eyebrow">ANALYSIS</span>
          <h2 id="analysis-title">Compare &amp; Diagnose</h2>
          <p class="summary-meta">Static evidence and runtime evidence stay separate.</p>
        </div>
        <span class="status-chip {analysisSummaryStatusClass()}">
          {analysisSummaryStatus()}
        </span>
      </div>

      <div class="analysis-input-status" aria-label="Analysis input status">
        <span class="status-chip {state.analysis.inputs.baseDataReady ? 'status-ready' : 'status-unknown'}">
          Base Data/Config · {state.analysis.inputs.baseDataReady ? 'Ready' : 'Not selected'}
        </span>
        <span class="status-chip {state.analysis.inputs.runtimeLogsReady ? 'status-ready' : 'status-unknown'}">
          Runtime logs · {state.analysis.inputs.runtimeLogsReady ? 'Ready' : 'Not selected'}
        </span>
        {#if analysisBusy}<span class="subtle" role="status">{analysisOperationLabel()}…</span>{/if}
      </div>

      <div class="analysis-summary-actions">
        <button
          class="primary-button"
          disabled={operationBlocksInteraction}
          onclick={startStaticAnalysis}
        >{staticAnalysisActionLabel()}</button>
        <span class="subtle">Static XML analysis uses the selected base Data/Config folder.</span>
      </div>

      {#if state.analysis.diagnostics.length > 0}
        <div class="diagnostic-list">
          {#each state.analysis.diagnostics as diagnostic}
            <p class={diagnosticClass(diagnostic.severity)}>
              <strong>{diagnostic.code}</strong> {diagnostic.message}
            </p>
          {/each}
        </div>
      {/if}

      {#if showCandidateCompare()}
      <details class="analysis-section" open>
        <summary>
          <span>Compare</span>
          <span class="subtle">Confirmed candidate MOD only</span>
        </summary>

        {#if !state.analysis.conflict}
          <p class="notice">未確認。静的解析を実行してください。</p>
        {:else if !state.localContext?.localModKey}
          <p class="notice">確認済みcandidate MODがありません。Compareは未確認です。</p>
        {:else if candidateAnalysisGroups.length === 0}
          <p class="notice">確認済みcandidate MODに関係する評価がありません。競合なしとは判定していません。</p>
        {:else}
          <div class="analysis-card-list">
            {#each candidateAnalysisGroups as group}
              <article class="analysis-card">
                <div class="analysis-card-heading">
                  <div>
                    <strong>{group.targetXml || 'Target XML unknown'}</strong>
                    <code>{group.xPath || 'XPath unknown'}</code>
                  </div>
                  <div class="analysis-badges">
                    <span class="status-chip {analysisStatusClass(group.assessment)}">{analysisLabel(group.assessment)}</span>
                    <span class="status-chip {analysisStatusClass(group.confidence)}">Confidence · {analysisLabel(group.confidence)}</span>
                  </div>
                </div>
                <p class="analysis-meta">Effective status · {analysisLabel(group.effectiveStatus)}</p>

                <div class="evidence-card static-evidence-card">
                  <span class="eyebrow">STATIC EVIDENCE</span>
                  <ol class="operation-sequence">
                    {#each group.operations as operation, operationIndex}
                      <li>
                        <div class="operation-heading">
                          <strong>{operationIndex + 1}. {operation.modKey}</strong>
                          <span>Priority {operation.priority ?? 'Unknown'}</span>
                        </div>
                        <div class="analysis-meta">
                          {operation.xmlFileRelativePath} · {operation.elementPath} · {operation.rawOperationName}
                          {#if operation.normalizedKind} · {operation.normalizedKind}{/if}
                        </div>
                        {#if operation.targetXml || operation.xPath}
                          <div class="analysis-code-pair">
                            <span>Target XML</span><code>{operation.targetXml || 'Unknown'}</code>
                            <span>XPath</span><code>{operation.xPath || 'Unknown'}</code>
                          </div>
                        {/if}
                        {#if operation.attributeName || operation.value}
                          <div class="analysis-meta">
                            Attribute {operation.attributeName || 'Unknown'} · Value {operation.value || 'Unknown'}
                          </div>
                        {/if}
                        <p class="provenance-line">Source · {operation.source.kind} · {operation.source.relativePath}</p>
                      </li>
                    {/each}
                  </ol>
                </div>

                {#if group.effectiveChanges.length > 0}
                  <details class="evidence-detail">
                    <summary>Effective changes · {group.effectiveChanges.length}</summary>
                    {#each group.effectiveChanges as change}
                      <p class="analysis-meta">
                        {change.matchPath} · {change.attributeName || 'element'} ·
                        {change.beforeValue || 'Unknown'} → {change.afterValue || 'Unknown'}
                      </p>
                    {/each}
                  </details>
                {/if}

                {#if group.uncertainties.length > 0}
                  <div class="notice-list">
                    {#each group.uncertainties as uncertainty}<p class="notice">Uncertainty · {uncertainty}</p>{/each}
                  </div>
                {/if}
                {#if group.diagnostics.length > 0}
                  <div class="diagnostic-list">
                    {#each groupDiagnostics(group.diagnostics) as groupDiagnostic}
                      <p class={diagnosticClass(groupDiagnostic.diagnostic.severity)}>
                        <strong>{groupDiagnostic.diagnostic.code}</strong> {groupDiagnostic.diagnostic.message}
                        {#if groupDiagnostic.diagnostic.source}<span class="diagnostic-source">{groupDiagnostic.diagnostic.source.relativePath}</span>{/if}
                      </p>
                    {/each}
                  </div>
                {/if}
              </article>
            {/each}
          </div>
        {/if}
      </details>
      {/if}

      <details class="analysis-section" open>
        <summary>
          <span>Diagnosis</span>
          <span class="subtle">Active profile · {state.knowledge.session?.profileName || 'Unknown'}</span>
        </summary>

        {#if !state.analysis.conflict}
          <p class="notice">未確認。active profile全体のDiagnosisは解析後に表示します。</p>
        {:else if analysisGroups.length === 0}
          <p class="notice">解析結果の評価groupがありません。評価は未確認です。</p>
        {:else}
          <div class="diagnosis-list">
            {#each analysisGroups as group}
              <article class="diagnosis-row">
                <div>
                  <strong>{group.targetXml || 'Target XML unknown'}</strong>
                  <code>{group.xPath || 'XPath unknown'}</code>
                </div>
                <div class="analysis-badges">
                  <span class="status-chip {analysisStatusClass(group.assessment)}">{analysisLabel(group.assessment)}</span>
                  <span class="status-chip {analysisStatusClass(group.confidence)}">{analysisLabel(group.confidence)}</span>
                </div>
                <p class="analysis-meta">
                  {group.operations.length} operations ·
                  {#each group.operations as operation, operationIndex}
                    {#if operationIndex > 0} / {/if}{operation.modKey} ({operation.priority ?? 'Unknown'})
                  {/each}
                </p>
              </article>
            {/each}
          </div>
        {/if}
      </details>

      <details class="analysis-section">
        <summary>
          <span>Static evidence</span>
          <span class="subtle">Base files · {state.analysis.conflict?.baseFiles.length ?? 0}</span>
        </summary>
        {#if state.analysis.conflict}
          {#each state.analysis.conflict.baseFiles as file}
            <article class="evidence-row">
              <div>
                <strong>{file.targetXml}</strong>
                <p class="analysis-meta">{sizeLabel(file.size)} · SHA-256 {file.sha256}</p>
              </div>
              <span class="status-chip {statusClass(file.parseStatus || 'unknown')}">{formatLabel(file.parseStatus)}</span>
              <p class="provenance-line">Source · {file.source.kind} · {file.source.relativePath}</p>
            </article>
          {/each}
          {#if state.analysis.conflict.diagnostics.length > 0}
            <div class="diagnostic-list">
              {#each state.analysis.conflict.diagnostics as diagnostic}
                <p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
              {/each}
            </div>
          {/if}
        {:else}
          <p class="notice">未確認。静的解析を実行してください。</p>
        {/if}
      </details>

      <details class="analysis-section">
        <summary>
          <span>Runtime evidence</span>
          <span class="subtle">RuntimeOCD comparison</span>
        </summary>
        {#if !state.analysis.runtimeComparison}
          <p class="notice">未確認。runtime logを選択して比較を実行してください。</p>
        {:else}
          {@const runtimeEvidence = state.analysis.runtimeComparison.runtimeEvidence}
          <div class="evidence-card runtime-evidence-card">
            <span class="eyebrow">RUNTIME EVIDENCE</span>
            <div class="summary-grid">
              <div><span>Tool</span><strong>{runtimeEvidence.toolName}</strong></div>
              <div><span>Tool version</span><strong>{runtimeEvidence.toolVersion || 'Unknown'}</strong></div>
              <div><span>Game version</span><strong>{runtimeEvidence.gameVersion || 'Unknown'}</strong></div>
              <div><span>Captured</span><strong>{runtimeEvidence.capturedAtUtc}</strong></div>
            </div>
            {#each state.analysis.runtimeComparison.items as item}
              <article class="runtime-comparison-row">
                <div class="analysis-card-heading">
                  <div>
                    <strong>{item.targetXml || 'Target XML unknown'}</strong>
                    <code>{item.xPath || 'XPath unknown'}</code>
                  </div>
                  <div class="analysis-badges">
                    <span class="status-chip {analysisStatusClass(item.status)}">{analysisLabel(item.status)}</span>
                    <span class="status-chip {analysisStatusClass(item.staticAssessment)}">Static · {analysisLabel(item.staticAssessment)}</span>
                    <span class="status-chip {analysisStatusClass(item.runtimeAssessment)}">Runtime · {analysisLabel(item.runtimeAssessment)}</span>
                  </div>
                </div>
                {#each item.observations as observation}
                  <p class="analysis-meta">
                    {observation.modKey || 'MOD unknown'} · {observation.observedOperation || 'Operation unknown'} ·
                    {observation.observedCategory || 'Category unknown'} · {analysisLabel(observation.normalizedAssessment)}
                  </p>
                {/each}
                {#if item.diagnostics.length > 0}
                  {#each item.diagnostics as diagnostic}
                    <p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
                  {/each}
                {/if}
              </article>
            {/each}
            {#if runtimeEvidence.diagnostics.length > 0}
              <div class="diagnostic-list">
                {#each runtimeEvidence.diagnostics as diagnostic}
                  <p class={diagnosticClass(diagnostic.severity)}>
                    <strong>{diagnostic.code}</strong> {diagnostic.message}
                    {#if diagnostic.source}<span class="diagnostic-source">{diagnostic.source.relativePath}</span>{/if}
                  </p>
                {/each}
              </div>
            {/if}
          </div>
        {/if}
      </details>
    </section>
    {/if}

    {#if contextMode === 'debug' && state.knowledge.session && state.diagnostics.length > 0}
      {@const diagnosticGroups = groupDiagnostics(state.diagnostics)}
      <section class="panel diagnostics-panel">
        <span class="eyebrow">DIAGNOSTICS</span>
        <p class="diagnostic-summary">
          {diagnosticGroups.length} types · {state.diagnostics.length} occurrences · details in Debug
        </p>
        {#each diagnosticGroups as group}
          <p class={diagnosticClass(group.diagnostic.severity)}>
            <strong>{group.diagnostic.code}</strong>
            {#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}
            {group.diagnostic.message}
          </p>
        {/each}
      </section>
    {/if}

    {#if contextMode === 'debug' && state.observation}
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

    {#if contextMode === 'debug'}
    <details class="panel developer-tools" bind:open={developerToolsOpen}>
      <summary>
        <span>
          <span class="eyebrow">DEVELOPER</span>
          <strong>Developer tools</strong>
        </span>
        <span class="muted-badge">Read-only</span>
      </summary>

      <div class="developer-actions">
        <button class="secondary-button" disabled={operationBlocksInteraction} onclick={() => send('knowledge.useFixture')}>Use fixture</button>
        <button class="primary-button" disabled={operationBlocksInteraction} onclick={loadSource}>Load source</button>
        <button class="secondary-button" onclick={() => send('browser.observe')}>Observe now</button>
      </div>

      <div class="analysis-developer-tools">
        <div class="analysis-tool-header">
          <div>
            <span class="eyebrow">PHASE6 ANALYSIS</span>
            <strong>Static and runtime evidence inputs</strong>
          </div>
          <div class="analysis-badges">
            <span class="status-chip {state.analysis.inputs.baseDataReady ? 'status-ready' : 'status-unknown'}">
              Base {state.analysis.inputs.baseDataReady ? 'ready' : 'missing'}
            </span>
            <span class="status-chip {state.analysis.inputs.runtimeLogsReady ? 'status-ready' : 'status-unknown'}">
              Logs {state.analysis.inputs.runtimeLogsReady ? 'ready' : 'missing'}
            </span>
          </div>
        </div>
        <div class="developer-actions">
          <button class="secondary-button" disabled={operationBlocksInteraction} onclick={() => send('analysis.selectBaseData')}>Select base Data/Config</button>
          <button class="secondary-button" disabled={operationBlocksInteraction} onclick={() => send('analysis.selectRuntimeLogs')}>Select runtime logs</button>
          <button
            class="primary-button"
            disabled={operationBlocksInteraction || !state.analysis.inputs.baseDataReady}
            onclick={() => send('analysis.analyzeConflicts')}
          >Analyze conflicts</button>
          <button
            class="primary-button"
            disabled={operationBlocksInteraction || !state.analysis.inputs.baseDataReady || !state.analysis.inputs.runtimeLogsReady}
            onclick={compareRuntimeEvidence}
          >Compare runtime</button>
          <button class="secondary-button" disabled={operationBlocksInteraction} onclick={() => send('analysis.useFixture')}>Use Phase6 fixture</button>
        </div>
        <div class="source-grid analysis-version-grid">
          <label>Tool version<input bind:value={runtimeToolVersion} placeholder="Unknown" disabled={analysisBusy} /></label>
          <label>Game version<input bind:value={runtimeGameVersion} placeholder="Unknown" disabled={analysisBusy} /></label>
        </div>
        <p class="subtle developer-status">Paths stay in the Desktop session. Runtime log bodies and raw results stay out of Web state.</p>
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
    {/if}

    {#if modSearchOpen}
      <button
        type="button"
        class="drawer-backdrop"
        aria-label="Close MOD search"
        onclick={closeModSearch}
      ></button>
      <aside
        class="mod-search-drawer"
        aria-labelledby="mod-search-title"
      >
        <div class="drawer-heading">
          <div>
            <span class="eyebrow">MOD CATALOG</span>
            <h2 id="mod-search-title">
              {modSearchMode === 'recognition' ? 'Choose a local MOD' : 'Search all MODs'}
            </h2>
          </div>
          <button class="icon-button" type="button" title="Close MOD search" aria-label="Close MOD search" onclick={closeModSearch}>×</button>
        </div>

        <p class="subtle mod-search-description">
          Search by display name, directory name, or MOD key. Website links use verified values or inferred Nexus destinations.
        </p>

        <label class="mod-search-field">
          <span>Search MODs</span>
          <input
            bind:value={modSearchQuery}
            aria-label="Search MODs"
            placeholder="e.g. Alpha Mod"
          />
        </label>

        {#if modSearchQuery.trim().length === 0}
          <p class="empty-state mod-search-empty">Enter a search term to show matching MODs.</p>
        {:else if modSearchResults.length === 0}
          <p class="empty-state mod-search-empty">No matching MODs were found.</p>
        {:else}
          <div class="mod-search-results" aria-live="polite">
            <p class="mod-search-result-count">{modSearchResults.length} matching MODs</p>
            {#each modSearchResults as candidate (candidate.modKey)}
              <article class="mod-search-card">
                {#if resolveModWebsite(candidate).url}
                  <button
                    type="button"
                    class="mod-card-main"
                    aria-label={`Open ${modDisplayName(candidate)} page · ${resolveModWebsite(candidate).status}`}
                    onclick={() => openModPage(candidate)}
                  >
                    <strong>{modDisplayName(candidate)}</strong>
                    <span>{candidate.version ? `v${candidate.version}` : 'Version unknown'}</span>
                  </button>
                {:else}
                  <div class="mod-card-main mod-card-main-disabled">
                    <strong>{modDisplayName(candidate)}</strong>
                    <span>{candidate.version ? `v${candidate.version}` : 'Version unknown'}</span>
                  </div>
                {/if}

                <div class="mod-card-meta">
                  <span class="status-chip {statusClass(candidate.profileState)}">{formatLabel(candidate.profileState)}</span>
                  <span class="status-chip {statusClass(candidate.enabledState)}">{formatLabel(candidate.enabledState)}</span>
                  <span class="subtle">Priority {candidate.priority ?? 'Unknown'}</span>
                  {#if resolveModWebsite(candidate).url}
                    <span class="mod-link-hint">Open page · {resolveModWebsite(candidate).status}</span>
                  {:else}
                    <span class="mod-link-hint">No usable URL</span>
                  {/if}
                </div>

                {#if modSearchMode === 'recognition'}
                  <button
                    type="button"
                    class="secondary-button mod-recognition-button"
                    onclick={() => chooseModForRecognition(candidate)}
                  >
                    Use for recognition
                  </button>
                {/if}
                <button
                  type="button"
                  class="secondary-button mod-recognition-button"
                  onclick={() => openInspectorForMod(candidate.modKey)}
                >
                  Inspect evidence
                </button>
              </article>
            {/each}
          </div>
        {/if}
      </aside>
    {/if}

    {#if contextMode === 'context' && state.knowledge.session && contextPanelMode === 'inspector' && inspectorView === 'mod'}
      <section class="panel inspector-panel">
        <div class="drawer-heading inspector-panel-heading">
          <div>
            <span class="eyebrow">INSPECTOR</span>
            <h2>{state.inspector?.directoryName || 'Loading evidence'}</h2>
          </div>
          <button class="secondary-button" type="button" onclick={closeInspector}>Back to Context</button>
        </div>

        <div class="drawer-section inspector-analysis-summary">
          <div class="inspector-analysis-summary-header">
            <div>
              <span class="eyebrow">ANALYSIS</span>
              <strong>{analysisSummaryStatus()}</strong>
            </div>
            <span class="status-chip {analysisSummaryStatusClass()}">{analysisSummaryStatus()}</span>
          </div>

          {#if !state.analysis.inputs.baseDataReady}
            <p class="subtle">Base Data/Config is not selected.</p>
            <button class="primary-button action-button" disabled={operationBlocksInteraction} onclick={startStaticAnalysis}>Select base Data/Config</button>
          {:else if analysisBusy}
            <p class="subtle" role="status">{analysisOperationLabel()}…</p>
          {:else}
            <button class="secondary-button action-button" disabled={operationBlocksInteraction} onclick={startStaticAnalysis}>{staticAnalysisActionLabel()}</button>
          {/if}

          {#if state.analysis.conflict}
            <p class="analysis-meta">Static analysis result is available in the closed evidence sections below.</p>
          {:else}
            <p class="notice">Not assessed. This is not a no-conflict conclusion.</p>
          {/if}
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

          {#if inspectorCandidate?.role}
            <div class="drawer-section">
              <span class="eyebrow">MOD ROLE</span>
              <p class="analysis-meta role-summary-line">
                <span class="role-chip role-{roleLabel(inspectorCandidate).toLowerCase()}">{roleLabel(inspectorCandidate)}</span>
                <span class="status-chip status-role-assessment">{roleAssessmentLabel(inspectorCandidate)}</span>
              </p>
              <p class="analysis-meta role-reason-summary" title={inspectorCandidate.role.reason || 'Unknown'}>
                Reason: {roleReasonSummary(inspectorCandidate)}
              </p>
              <details class="role-detail">
                <summary>Role evidence · {inspectorCandidate.role.evidence.length}</summary>
                <p class="analysis-meta">{inspectorCandidate.role.reason || 'Unknown'}</p>
                {#each inspectorCandidate.role.evidence as evidence}
                  <p class="provenance-line">{formatLabel(evidence.kind)} · {evidence.detail} · {evidence.source.relativePath}</p>
                {/each}
              </details>
            </div>
          {/if}

          {#if inspectorConflictGroups.length > 0}
            <div class="drawer-section">
              <span class="eyebrow">RELATED STATIC EVIDENCE</span>
              {#each inspectorConflictGroups as group}
                <p class="analysis-meta">
                  {group.targetXml || 'Target XML unknown'} · {group.xPath || 'XPath unknown'} ·
                  {analysisLabel(group.assessment)} · {analysisLabel(group.confidence)}
                </p>
              {/each}
            </div>
          {/if}

          {#if inspectorRuntimeItems.length > 0}
            <div class="drawer-section">
              <span class="eyebrow">RELATED RUNTIME COMPARISON</span>
              {#each inspectorRuntimeItems as item}
                <p class="analysis-meta">
                  {item.targetXml || 'Target XML unknown'} · {item.xPath || 'XPath unknown'} · {analysisLabel(item.status)}
                </p>
              {/each}
            </div>
          {/if}

          <details class="drawer-section inspector-disclosure" bind:open={inspectorFilesOpen}>
            <summary class="inspector-disclosure-summary">
              <span class="eyebrow">FILES</span>
              <span class="subtle">{state.inspector.files.length} files</span>
            </summary>
            <ul class="compact-list">
              {#each state.inspector.files as file}
                <li><code>{file.relativePath}</code><span>{sizeLabel(file.size)}</span></li>
              {/each}
            </ul>
          </details>

          <div class="drawer-section">
            <span class="eyebrow">XML · {state.inspector.xmlFiles.length}</span>
            {#each state.inspector.xmlFiles as xml}
              <details class="xml-item">
                <summary><code>{xml.relativePath}</code><span>{formatLabel(xml.parseStatus)}</span></summary>
                <p>{xml.rootElementName || 'Unknown root'} · {xml.elementCount} elements · {xml.attributeCount} attributes</p>
                {#each xml.xPathCandidates as xpath}
                  <code class="block-code">{xpath.rawValue}</code>
                {/each}
                {#if xml.patchOperations.length > 0}
                  <details class="patch-operation-list">
                    <summary>Patch operations · {xml.patchOperations.length}</summary>
                    {#each xml.patchOperations as patch}
                      <details class="patch-operation-item">
                        <summary>
                          <span>{patch.rawOperationName}</span>
                          <span class="subtle">{patch.normalizedKind || 'Unknown'} · {patch.elementPath}</span>
                        </summary>
                        <p class="analysis-meta">Source · {patch.source.kind} · {patch.source.relativePath}</p>
                        {#if patch.xPathCandidates.length > 0}
                          <div class="analysis-code-pair">
                            <span>XPath</span>
                            {#each patch.xPathCandidates as xpath}<code>{xpath.rawValue}</code>{/each}
                          </div>
                        {/if}
                        {#if patch.targetXmlCandidates.length > 0}
                          <p class="analysis-meta">Target XML · {patch.targetXmlCandidates.map((candidate) => candidate.normalizedValue || candidate.rawValue).join(', ')}</p>
                        {/if}
                        <details class="raw-detail">
                          <summary>Raw XML observation</summary>
                          <code class="block-code">{patch.rawObservation.elementPath} · &lt;{patch.rawObservation.elementName}&gt;</code>
                          {#each patch.rawObservation.attributes as attribute}
                            <p class="analysis-meta">Attribute · {attribute.name} = {attribute.value}</p>
                          {/each}
                          {#if patch.rawObservation.innerText}<pre>{patch.rawObservation.innerText}</pre>{/if}
                        </details>
                        {#if patch.diagnostics.length > 0}
                          {#each patch.diagnostics as diagnostic}
                            <p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>
                          {/each}
                        {/if}
                      </details>
                    {/each}
                  </details>
                {/if}
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
      </section>
    {/if}
  </main>
{/if}
