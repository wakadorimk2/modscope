<script lang="ts">
  import { onMount } from 'svelte';
  import { createBridge, type Bridge } from './bridge';
  import {
    initialState,
    type BridgeErrorPayload,
    type DeploymentEntryUiState,
    type HostMessage,
    type ModCandidateUiState,
    type UiState
  } from './contracts';
  import WorkspaceToolbar from './components/WorkspaceToolbar.svelte';
  import ModLibraryPane from './components/ModLibraryPane.svelte';
  import ContextPane from './components/ContextPane.svelte';
  import EvidenceInspector from './components/EvidenceInspector.svelte';
  import DeploymentPreviewSurface from './components/DeploymentPreviewSurface.svelte';
  import type { ContextMode, ModListMode } from './components/ui-types';

  const requestedSurface = new URLSearchParams(window.location.search).get('surface');
  const surface = requestedSurface === 'toolbar'
    ? 'toolbar'
    : requestedSurface === 'mod-list'
      ? 'mod-list'
      : requestedSurface === 'deployment-preview'
        ? 'deployment-preview'
        : 'context';

  let state: UiState = initialState;
  let address = initialState.browser.url;
  let contextMode: ContextMode = 'context';
  let modListMode: ModListMode = 'browse';
  let contextPanelMode: 'context' | 'inspector' = 'context';
  let inspectorView: 'mod' | 'diagnosis' = 'mod';
  let inspectorModKey: string | null = null;
  let dismissedInspectorModKey: string | null = null;
  let handledAutoInspectToken: string | null = null;
  let inspectorFilesOpen = false;
  let modSearchOpen = false;
  let modSearchMode: 'browse' | 'recognition' = 'browse';
  let modSearchQuery = '';
  let pageDetailsOpen = false;
  let developerToolsOpen = false;
  let deploymentDraftEntries: DeploymentEntryUiState[] = initialState.deployment.entries;
  let deploymentApplyConfirmOpen = false;
  let deploymentApplyPending = false;
  let deploymentPreviewSearch = '';
  let lastError: BridgeErrorPayload | null = null;
  let bridge: Bridge | undefined;
  let showHtmlMoreMenu = false;
  let runtimeToolVersion = '';
  let runtimeGameVersion = '';
  let webObservedVersion = '';

  $: operationBlocksInteraction = state.analysis.operation.isBusy
    || (state.knowledge.operation.isBusy && !state.knowledge.operation.isBackground);
  $: operationRailVisible = state.knowledge.operation.isBusy;
  $: modSearchResults = searchCandidates(state.knowledge.candidates, modSearchQuery);
  $: inspectorCandidate = state.inspector
    ? state.knowledge.candidates.find((candidate) => candidate.modKey === state.inspector?.modKey) ?? null
    : null;
  $: inspectorConflictGroups = state.inspector && state.analysis.conflict
    ? state.analysis.conflict.groups.filter((group) => group.operations.some((operation) => operation.modKey === state.inspector?.modKey))
    : [];
  $: inspectorRuntimeItems = state.inspector && state.analysis.runtimeComparison
    ? state.analysis.runtimeComparison.items.filter((item) => item.observations.some((observation) => observation.modKey === state.inspector?.modKey))
    : [];

  onMount(() => {
    bridge = createBridge(handleHostMessage);
    showHtmlMoreMenu = !bridge.isDesktopHost;
    const disconnect = bridge.connect();
    window.addEventListener('keydown', handleShortcut);
    return () => {
      disconnect();
      window.removeEventListener('keydown', handleShortcut);
    };
  });

  function normalizeContextMode(value: string | undefined): ContextMode {
    return value === 'settings' || value === 'debug' || value === 'analysis' ? value : 'context';
  }

  function normalizeModListMode(value: string | undefined): ModListMode {
    return value === 'deployment-edit' ? value : 'browse';
  }

  function handleHostMessage(message: HostMessage) {
    if (message.kind === 'state') {
      const addressIsBeingEdited = document.activeElement instanceof HTMLInputElement
        && document.activeElement.classList.contains('toolbar-address');
      state = message.payload;
      contextMode = normalizeContextMode(state.layout.contextMode);
      modListMode = normalizeModListMode(state.layout.modListMode);
      if (modListMode === 'browse') {
        deploymentDraftEntries = state.deployment.entries;
      }
      if (!addressIsBeingEdited) address = state.browser.url;
      lastError = null;

      if (state.deployment.status === 'applied') {
        deploymentApplyConfirmOpen = false;
        deploymentApplyPending = false;
        modListMode = 'browse';
      } else if (!state.deployment.canApply) {
        deploymentApplyConfirmOpen = false;
        deploymentApplyPending = false;
      }

      if (surface === 'context' && state.inspector?.modKey) {
        if (state.identity.autoInspectToken && state.identity.autoInspectToken !== handledAutoInspectToken) {
          handledAutoInspectToken = state.identity.autoInspectToken;
          dismissedInspectorModKey = null;
        }
        if (state.inspector.modKey !== dismissedInspectorModKey) {
          if (state.inspector.modKey !== inspectorModKey) {
            inspectorFilesOpen = false;
            webObservedVersion = '';
          }
          contextMode = 'context';
          contextPanelMode = 'inspector';
          inspectorView = 'mod';
          inspectorModKey = state.inspector.modKey;
        }
      }

      if (contextPanelMode === 'inspector' && inspectorView === 'mod' && inspectorModKey && state.inspector?.modKey !== inspectorModKey) {
        closeInspector();
      }
      return;
    }

    if (message.kind === 'error') {
      lastError = message.payload;
      deploymentApplyPending = false;
    }
  }

  function send(command: string, payload: unknown = {}) {
    lastError = null;
    if (['browser.home', 'browser.newTab', 'browser.history', 'browser.selectHistory', 'browser.selectTab', 'browser.navigate', 'identity.confirm', 'knowledge.loadSource', 'knowledge.selectRoot', 'knowledge.selectSource', 'knowledge.switchProfile', 'knowledge.useFixture', 'knowledge.selectEvidenceManifest', 'deployment.preview', 'deployment.apply', 'game.launch'].includes(command)) {
      contextPanelMode = 'context';
      inspectorModKey = null;
    }
    bridge?.send(command, payload);
  }

  function navigate() {
    if (address.trim()) send('browser.navigate', { url: address.trim() });
  }

  function switchProfile(event: Event) {
    const profileName = (event.currentTarget as HTMLSelectElement).value;
    if (profileName) {
      deploymentDraftEntries = [];
      setModListMode('browse');
      send('knowledge.switchProfile', { profileName });
    }
  }

  function setContextMode(mode: ContextMode) {
    contextMode = mode;
    if (mode !== 'context') contextPanelMode = 'context';
    send('layout.setContextMode', { mode });
  }

  function setModListMode(mode: ModListMode) {
    if (mode === 'deployment-edit') {
      if (!state.knowledge.session || operationBlocksInteraction) return;
      deploymentDraftEntries = state.deployment.entries.map((entry) => ({ ...entry }));
    } else {
      deploymentDraftEntries = state.deployment.entries;
    }
    modListMode = mode;
    send('layout.setModListMode', { mode });
  }

  function startDeploymentEdit() {
    setModListMode('deployment-edit');
  }

  function cancelDeploymentEdit() {
    deploymentDraftEntries = state.deployment.entries;
    setModListMode('browse');
  }

  function previewDeployment() {
    const profileName = state.knowledge.session?.profileName || state.deployment.profileName;
    if (!profileName || deploymentDraftEntries.length === 0) return;
    send('deployment.preview', {
      profileName,
      entries: deploymentDraftEntries.filter((entry) => entry.isEditable).map((entry, order) => ({
        modKey: entry.modKey,
        enabled: entry.enabled,
        order
      }))
    });
  }

  function requestApplyDeployment() {
    if (state.deployment.planId && state.deployment.canApply) deploymentApplyConfirmOpen = true;
  }

  function cancelApplyDeployment() {
    deploymentApplyConfirmOpen = false;
  }

  function applyDeployment() {
    if (!state.deployment.planId || !state.deployment.canApply || !deploymentApplyConfirmOpen) return;
    deploymentApplyConfirmOpen = false;
    deploymentApplyPending = true;
    send('deployment.apply', { planId: state.deployment.planId, approved: true });
    setModListMode('browse');
  }

  function launchGame() {
    if (state.deployment.canLaunch) send('game.launch');
  }

  function toggleContext() {
    send('layout.setContextVisible', { visible: !state.layout.contextVisible });
  }

  function toggleModList() {
    send('layout.setModListVisible', { visible: !state.layout.modListVisible });
  }

  function openNewTab() { send('browser.newTab'); }
  function selectTab(tabId: string) { send('browser.selectTab', { tabId }); }
  function closeTab(tabId: string) { send('browser.closeTab', { tabId }); }
  function openHome() { send('browser.home'); }
  function openHistory() { send('browser.history'); }
  function setMoreOpen(open: boolean) { send('layout.setMoreOpen', { open }); }

  function handleShortcut(event: KeyboardEvent) {
    if (event.key === 'Escape' && modSearchOpen) {
      event.preventDefault();
      closeModSearch();
      return;
    }
    if (event.key === 'Escape' && deploymentApplyConfirmOpen) {
      event.preventDefault();
      cancelApplyDeployment();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'i') {
      event.preventDefault();
      toggleContext();
    }
  }

  function discoverSources() { send('knowledge.discoverSources'); }
  function selectSource(candidateId: string) { send('knowledge.selectSource', { candidateId }); }
  function selectRoot() { send('knowledge.selectRoot'); }
  function useFixture() { send('knowledge.useFixture'); }
  function selectEvidenceManifest() { send('knowledge.selectEvidenceManifest'); }
  function observe() { send('browser.observe'); }

  function pageIdentity(): string {
    return (state.localContext?.candidateIdentity || state.identity.candidateIdentity || state.observation?.title || state.browser.title || '').trim();
  }

  function confirmIdentity(localModKey: string | null) {
    const identity = pageIdentity();
    if (!identity) {
      lastError = { code: 'identity.missing', message: 'The page has no title. Enter a URL with a readable page title.' };
      return;
    }
    send('identity.confirm', { candidateIdentity: identity, localModKey });
  }

  function openInspectorForMod(modKey: string) {
    const inspectorChanged = inspectorModKey !== modKey;
    if (inspectorChanged) webObservedVersion = '';
    contextMode = 'context';
    contextPanelMode = 'inspector';
    inspectorView = 'mod';
    inspectorModKey = modKey;
    dismissedInspectorModKey = null;
    if (inspectorChanged) inspectorFilesOpen = false;
    send('layout.setContextMode', { mode: 'context' });
    send('inspector.open', { modKey });
  }

  function openProfileDiagnosis() {
    contextMode = 'analysis';
    contextPanelMode = 'inspector';
    inspectorView = 'diagnosis';
    inspectorModKey = null;
    dismissedInspectorModKey = null;
    send('layout.setContextMode', { mode: 'analysis' });
  }

  function openAnalysisInspector() {
    if (state.localContext?.localModKey) openInspectorForMod(state.localContext.localModKey);
    else openProfileDiagnosis();
  }

  function openCurrentInspector() {
    if (state.localContext?.localModKey) openInspectorForMod(state.localContext.localModKey);
  }

  function closeInspector() {
    dismissedInspectorModKey = state.inspector?.modKey ?? inspectorModKey;
    contextPanelMode = 'context';
    inspectorView = 'mod';
    inspectorModKey = null;
    inspectorFilesOpen = false;
    setContextMode('context');
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

  function isWebsiteUrl(value: string | null | undefined): value is string {
    const trimmed = value?.trim();
    if (!trimmed) return false;
    try {
      const url = new URL(trimmed);
      return url.protocol === 'http:' || url.protocol === 'https:';
    } catch {
      return false;
    }
  }

  function resolveModWebsite(candidate: ModCandidateUiState): { url: string | null; nexusSearchName?: string } {
    if (isWebsiteUrl(candidate.website)) return { url: candidate.website.trim() };
    const name = [candidate.displayName, candidate.directoryName, candidate.modKey].map((value) => value?.trim() ?? '').find((value) => value.length > 0) ?? '';
    return name ? { url: `https://www.nexusmods.com/games/7daystodie/mods?keyword=${encodeURIComponent(name)}`, nexusSearchName: name } : { url: null };
  }

  function openModPage(candidate: ModCandidateUiState) {
    const website = resolveModWebsite(candidate);
    if (!website.url) return;
    closeModSearch();
    send('browser.navigate', { url: website.url, ...(website.nexusSearchName ? { nexusSearchName: website.nexusSearchName } : {}) });
  }

  function chooseModForRecognition(candidate: ModCandidateUiState) {
    confirmIdentity(candidate.modKey);
    if (!lastError) closeModSearch();
  }

  function searchCandidates(candidates: ModCandidateUiState[], query: string): ModCandidateUiState[] {
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) return [];
    return candidates.filter((candidate) => [candidate.displayName, candidate.directoryName, candidate.modKey].filter((value): value is string => Boolean(value)).some((value) => value.toLowerCase().includes(normalizedQuery)));
  }

  function startStaticAnalysis() {
    if (!state.analysis.inputs.baseDataReady) send('analysis.selectBaseData');
    else send('analysis.analyzeConflicts');
  }

  function compareRuntimeEvidence() {
    send('analysis.compareRuntimeEvidence', { toolVersion: runtimeToolVersion.trim() || null, gameVersion: runtimeGameVersion.trim() || null });
  }

  function setWebVersionObservation() {
    if (state.inspector && webObservedVersion.trim()) send('knowledge.setWebVersionObservation', { rawValue: webObservedVersion.trim() });
  }
</script>

<svelte:head><title>ModScope</title></svelte:head>

{#if surface === 'toolbar'}
  <WorkspaceToolbar
    bind:address
    {state}
    disabled={operationBlocksInteraction}
    {showHtmlMoreMenu}
    error={lastError}
    onNavigate={navigate}
    onBack={() => send('browser.back')}
    onForward={() => send('browser.forward')}
    onReload={() => send('browser.reload')}
    onHome={openHome}
    onOpenHistory={openHistory}
    onNewTab={openNewTab}
    onSelectTab={selectTab}
    onCloseTab={closeTab}
    onToggleModList={toggleModList}
    onToggleContext={toggleContext}
    onSetContextMode={setContextMode}
    onSetModListMode={setModListMode}
    onSetMoreOpen={setMoreOpen}
  />
{:else if surface === 'mod-list'}
  <ModLibraryPane
    {state}
    {operationRailVisible}
    {operationBlocksInteraction}
    deploymentDraftEntries={deploymentDraftEntries}
    onSwitchProfile={switchProfile}
    onStartDeploymentEdit={startDeploymentEdit}
    onCancelDeploymentEdit={cancelDeploymentEdit}
    onDraftChange={(entries) => (deploymentDraftEntries = entries)}
    onPreviewDeployment={previewDeployment}
    onLaunchGame={launchGame}
    onOpenInspectorForMod={openInspectorForMod}
    onOpenModPage={openModPage}
    onCollapse={() => send('layout.setModListVisible', { visible: false })}
  />
{:else if surface === 'deployment-preview'}
  <DeploymentPreviewSurface
    {state}
    error={lastError}
    bind:search={deploymentPreviewSearch}
    applyPending={deploymentApplyPending}
    applyConfirmOpen={deploymentApplyConfirmOpen}
    onRequestApply={requestApplyDeployment}
    onCancelApply={cancelApplyDeployment}
    onApply={applyDeployment}
  />
{:else}
  <main class="shell">
    <ContextPane
      {state}
      mode={contextMode}
      {contextPanelMode}
      {operationBlocksInteraction}
      error={lastError}
      bind:pageDetailsOpen
      bind:developerToolsOpen
      bind:runtimeToolVersion
      bind:runtimeGameVersion
      bind:modSearchQuery
      {modSearchOpen}
      {modSearchMode}
      {modSearchResults}
      onSetContextMode={setContextMode}
      onDiscoverSources={discoverSources}
      onSelectRoot={selectRoot}
      onSelectSource={selectSource}
      onUseFixture={useFixture}
      onSelectEvidenceManifest={selectEvidenceManifest}
      onObserve={observe}
      onOpenAnalysis={openAnalysisInspector}
      onOpenInspector={openCurrentInspector}
      onOpenModSearch={openModSearch}
      onCloseModSearch={closeModSearch}
      onOpenModPage={openModPage}
      onChooseModForRecognition={chooseModForRecognition}
      onConfirmIdentity={confirmIdentity}
      onStartStaticAnalysis={startStaticAnalysis}
      onSelectBaseData={() => send('analysis.selectBaseData')}
      onSelectRuntimeLogs={() => send('analysis.selectRuntimeLogs')}
      onAnalyzeConflicts={() => send('analysis.analyzeConflicts')}
      onCompareRuntimeEvidence={compareRuntimeEvidence}
      onUseAnalysisFixture={() => send('analysis.useFixture')}
      onOpenInspectorForMod={openInspectorForMod}
    />
    {#if contextMode === 'context' && state.knowledge.session && contextPanelMode === 'inspector' && inspectorView === 'mod'}
      <EvidenceInspector
        {state}
        inspector={state.inspector}
        {inspectorCandidate}
        {inspectorConflictGroups}
        {inspectorRuntimeItems}
        {operationBlocksInteraction}
        bind:inspectorFilesOpen
        bind:webObservedVersion
        onClose={closeInspector}
        onSetWebVersionObservation={setWebVersionObservation}
        onStartStaticAnalysis={startStaticAnalysis}
      />
    {/if}
  </main>
{/if}
