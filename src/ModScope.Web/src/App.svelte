<script lang="ts">
  import { onMount } from 'svelte';
  import { createBridge, type Bridge } from './bridge';
  import {
    initialState,
    type BridgeErrorPayload,
    type DeploymentEntryUiState,
    type HostMessage,
    type LayoutUiState,
    type ModCandidateUiState,
    type UiState
  } from './contracts';
  import WorkspaceToolbar from './components/WorkspaceToolbar.svelte';
  import ModLibraryPane from './components/ModLibraryPane.svelte';
  import ContextPane from './components/ContextPane.svelte';
  import DeploymentPreviewSurface from './components/DeploymentPreviewSurface.svelte';
  import type { ContextMode, ModListMode } from './components/ui-types';
  import { resolveModWebsite } from './mod-links';

  const requestedSurface = new URLSearchParams(window.location.search).get('surface');
  const surface = requestedSurface === 'toolbar'
    ? 'toolbar'
    : requestedSurface === 'mod-list'
      ? 'mod-list'
      : requestedSurface === 'deployment-preview'
        ? 'deployment-preview'
        : 'context';

  let state: UiState = initialState;
  let layout: LayoutUiState = initialState.layout;
  let address = initialState.browser.url;
  let contextMode: ContextMode = 'context';
  let modListMode: ModListMode = 'browse';
  let inspectorOpen = false;
  let inspectorModKey: string | null = null;
  let pendingInspectorModKey: string | null = null;
  let pendingRecognitionModKey: string | null = null;
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
  let browserHostReady = false;
  let showHtmlMoreMenu = false;
  let runtimeToolVersion = '';
  let runtimeGameVersion = '';
  let webObservedVersion = '';
  let inspectorPresentationChannel: BroadcastChannel | null = null;

  type InspectorPresentationMessage =
    | { kind: 'open'; modKey: string }
    | { kind: 'close' };

  $: knowledgeBlocksInteraction = state.knowledge.operation.isBusy
    && !state.knowledge.operation.isBackground;
  $: operationBlocksInteraction = state.analysis.operation.isBusy
    || knowledgeBlocksInteraction;
  $: browserToolbarDisabled = !browserHostReady;
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
  let recognitionPageUrl = externalPageUrl(initialState.browser.url);

  onMount(() => {
    bridge = createBridge(handleHostMessage);
    showHtmlMoreMenu = !bridge.isDesktopHost;
    const disconnect = bridge.connect();
    if (typeof BroadcastChannel !== 'undefined') {
      inspectorPresentationChannel = new BroadcastChannel('modscope-inspector-presentation');
      inspectorPresentationChannel.addEventListener('message', handleInspectorPresentationMessage);
    }
    window.addEventListener('keydown', handleShortcut);
    return () => {
      disconnect();
      inspectorPresentationChannel?.removeEventListener('message', handleInspectorPresentationMessage);
      inspectorPresentationChannel?.close();
      inspectorPresentationChannel = null;
      window.removeEventListener('keydown', handleShortcut);
    };
  });

  function handleInspectorPresentationMessage(event: MessageEvent) {
    const value = event.data;
    if (!value || typeof value !== 'object') return;
    const message = value as { kind?: unknown; modKey?: unknown };
    if (message.kind === 'open' && typeof message.modKey === 'string' && message.modKey.length > 0) {
      if (inspectorModKey !== message.modKey) {
        inspectorFilesOpen = false;
        webObservedVersion = '';
      }
      pendingInspectorModKey = message.modKey;
      inspectorOpen = true;
      contextMode = 'context';
      inspectorModKey = message.modKey;
    } else if (message.kind === 'close') {
      pendingInspectorModKey = null;
      inspectorOpen = false;
    }
  }

  function broadcastInspectorPresentation(message: InspectorPresentationMessage) {
    try {
      inspectorPresentationChannel?.postMessage(message);
    } catch {
      // Presentation sync is optional. Bridge state remains authoritative.
    }
  }

  function normalizeContextMode(value: string | undefined): ContextMode {
    return value === 'settings' || value === 'debug' || value === 'analysis' ? value : 'context';
  }

  function normalizeModListMode(value: string | undefined): ModListMode {
    return value === 'deployment-edit' ? value : 'browse';
  }

  function isTransientSurfaceUrl(url: string): boolean {
    return url.trim() === 'about:deployment-preview';
  }

  function externalPageUrl(url: string): string | null {
    const trimmed = url.trim();
    return trimmed && !isTransientSurfaceUrl(trimmed) ? trimmed : null;
  }

  function handleHostMessage(message: HostMessage) {
    if (message.kind === 'state') {
      const addressIsBeingEdited = document.activeElement instanceof HTMLInputElement
        && document.activeElement.classList.contains('toolbar-address');
      const previousState = state;
      const pageChanged = previousState.browser.url !== message.payload.browser.url
        || previousState.browser.activeTabId !== message.payload.browser.activeTabId;
      const profileChanged = previousState.knowledge.session?.profileName !== message.payload.knowledge.session?.profileName;
      const knowledgeContextChanged = previousState.knowledge.session?.snapshotId !== message.payload.knowledge.session?.snapshotId
        || profileChanged;
      const nextExternalPageUrl = externalPageUrl(message.payload.browser.url);
      const recognitionPageChanged = nextExternalPageUrl !== null
        && recognitionPageUrl !== null
        && nextExternalPageUrl !== recognitionPageUrl;
      if (nextExternalPageUrl !== null) recognitionPageUrl = nextExternalPageUrl;
      const identityChanged = previousState.identity.candidateIdentity !== message.payload.identity.candidateIdentity
        || previousState.identity.selectedLocalModKey !== message.payload.identity.selectedLocalModKey
        || previousState.localContext?.localModKey !== message.payload.localContext?.localModKey;
      state = message.payload;
      layout = message.payload.layout;
      browserHostReady = Boolean(
        state.browser.activeTabId
        && state.browser.tabs.some((tab) => tab.isActive)
      );
      const requestedInspectorKey = pendingInspectorModKey;
      if (requestedInspectorKey && state.inspector?.modKey === requestedInspectorKey) {
        pendingInspectorModKey = null;
      }
      const inspectorTransitionPending = pendingInspectorModKey !== null;
      contextMode = inspectorTransitionPending ? 'context' : normalizeContextMode(layout.contextMode);
      modListMode = normalizeModListMode(layout.modListMode);
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

      if (recognitionPageChanged || knowledgeContextChanged || identityChanged) {
        pendingRecognitionModKey = null;
      }

      if (!inspectorTransitionPending && (pageChanged || profileChanged || identityChanged) && inspectorOpen) {
        inspectorOpen = false;
        inspectorModKey = null;
        inspectorFilesOpen = false;
        webObservedVersion = '';
        broadcastInspectorPresentation({ kind: 'close' });
      } else if (!inspectorTransitionPending && (pageChanged || profileChanged || identityChanged) && inspectorModKey) {
        inspectorModKey = null;
        inspectorFilesOpen = false;
        webObservedVersion = '';
      }

      if (!inspectorTransitionPending && inspectorOpen && inspectorModKey && state.inspector?.modKey !== inspectorModKey) {
        inspectorOpen = false;
        inspectorModKey = null;
        inspectorFilesOpen = false;
        webObservedVersion = '';
      }
      return;
    }

    if (message.kind === 'layout') {
      layout = message.payload;
      contextMode = normalizeContextMode(layout.contextMode);
      modListMode = normalizeModListMode(layout.modListMode);
      return;
    }

    if (message.kind === 'error') {
      if (pendingInspectorModKey !== null) {
        pendingInspectorModKey = null;
        inspectorOpen = false;
        inspectorModKey = null;
        broadcastInspectorPresentation({ kind: 'close' });
      }
      lastError = message.payload;
      deploymentApplyPending = false;
    }
  }

  function send(command: string, payload: unknown = {}) {
    lastError = null;
    if (['browser.home', 'browser.newTab', 'browser.history', 'browser.selectHistory', 'browser.selectTab', 'browser.navigate', 'identity.confirm', 'knowledge.loadSource', 'knowledge.selectRoot', 'knowledge.selectSource', 'knowledge.switchProfile', 'knowledge.useFixture', 'knowledge.selectEvidenceManifest', 'deployment.preview', 'deployment.apply', 'game.launch'].includes(command)) {
      pendingInspectorModKey = null;
      inspectorOpen = false;
      inspectorModKey = null;
      inspectorFilesOpen = false;
      webObservedVersion = '';
      broadcastInspectorPresentation({ kind: 'close' });
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
    if (mode !== 'context') {
      pendingInspectorModKey = null;
      inspectorOpen = false;
      inspectorModKey = null;
      inspectorFilesOpen = false;
      webObservedVersion = '';
      broadcastInspectorPresentation({ kind: 'close' });
    }
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
    send('layout.setContextVisible', { visible: !layout.contextVisible });
  }

  function toggleModList() {
    send('layout.setModListVisible', { visible: !layout.modListVisible });
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
    pendingInspectorModKey = modKey;
    inspectorOpen = true;
    contextMode = 'context';
    inspectorModKey = modKey;
    if (inspectorChanged) inspectorFilesOpen = false;
    broadcastInspectorPresentation({ kind: 'open', modKey });
    send('inspector.open', { modKey });
    send('layout.setContextMode', { mode: 'context' });
  }

  function openProfileDiagnosis() {
    pendingInspectorModKey = null;
    inspectorOpen = false;
    contextMode = 'analysis';
    inspectorModKey = null;
    broadcastInspectorPresentation({ kind: 'close' });
    send('layout.setContextMode', { mode: 'analysis' });
  }

  function openAnalysisInspector() {
    if (state.localContext?.localModKey) openInspectorForMod(state.localContext.localModKey);
    else openProfileDiagnosis();
  }

  function toggleCurrentInspector() {
    if (inspectorOpen) {
      closeInspector();
    } else if (state.localContext?.localModKey) {
      openInspectorForMod(state.localContext.localModKey);
    }
  }

  function toggleInspectorForMod(modKey: string) {
    if (inspectorOpen && inspectorModKey === modKey) {
      closeInspector();
    } else {
      openInspectorForMod(modKey);
    }
  }

  function closeInspector() {
    pendingInspectorModKey = null;
    inspectorOpen = false;
    broadcastInspectorPresentation({ kind: 'close' });
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

  function openModPage(candidate: ModCandidateUiState) {
    const website = resolveModWebsite(candidate);
    if (!website.url) return;
    closeModSearch();
    send('browser.navigate', {
      url: website.url,
      ...(website.nexusSearchNames ? { nexusSearchNames: website.nexusSearchNames } : {})
    });
  }

  function chooseModForRecognition(modKey: string) {
    pendingRecognitionModKey = modKey;
    closeModSearch();
  }

  function clearRecognitionSelection() {
    pendingRecognitionModKey = null;
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

  function observeNexusFileVersion() {
    if (state.inspector) send('knowledge.observeNexusFileVersion');
  }
</script>

<svelte:head><title>ModScope</title></svelte:head>

{#if surface === 'toolbar'}
  <WorkspaceToolbar
    bind:address
    {state}
    {layout}
    disabled={browserToolbarDisabled}
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
    {modListMode}
    {operationRailVisible}
    {operationBlocksInteraction}
    {inspectorOpen}
    {inspectorModKey}
    deploymentDraftEntries={deploymentDraftEntries}
    onSwitchProfile={switchProfile}
    onStartDeploymentEdit={startDeploymentEdit}
    onCancelDeploymentEdit={cancelDeploymentEdit}
    onDraftChange={(entries) => (deploymentDraftEntries = entries)}
    onPreviewDeployment={previewDeployment}
    onLaunchGame={launchGame}
    onOpenInspectorForMod={toggleInspectorForMod}
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
      {inspectorOpen}
      inspector={state.inspector}
      {inspectorCandidate}
      {inspectorConflictGroups}
      {inspectorRuntimeItems}
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
      {pendingRecognitionModKey}
      onSetContextMode={setContextMode}
      onDiscoverSources={discoverSources}
      onSelectRoot={selectRoot}
      onSelectSource={selectSource}
      onUseFixture={useFixture}
      onSelectEvidenceManifest={selectEvidenceManifest}
      onObserve={observe}
      onOpenAnalysis={openAnalysisInspector}
      onToggleInspector={toggleCurrentInspector}
      onOpenModSearch={openModSearch}
      onCloseModSearch={closeModSearch}
      onOpenModPage={openModPage}
      onChooseModForRecognition={chooseModForRecognition}
      onClearRecognitionSelection={clearRecognitionSelection}
      onConfirmIdentity={confirmIdentity}
      onStartStaticAnalysis={startStaticAnalysis}
      onSelectBaseData={() => send('analysis.selectBaseData')}
      onSelectRuntimeLogs={() => send('analysis.selectRuntimeLogs')}
      onAnalyzeConflicts={() => send('analysis.analyzeConflicts')}
      onCompareRuntimeEvidence={compareRuntimeEvidence}
      onUseAnalysisFixture={() => send('analysis.useFixture')}
      onOpenInspectorForMod={toggleInspectorForMod}
      onSetWebVersionObservation={setWebVersionObservation}
      onObserveNexusFileVersion={observeNexusFileVersion}
      bind:inspectorFilesOpen
      bind:webObservedVersion
    />
  </main>
{/if}
