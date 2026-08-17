<script lang="ts">
  import type { DeploymentEntryUiState, ModCandidateUiState, UiState } from '../contracts';
  import type { ModListMode } from './ui-types';
  import { resolveModWebsite, type ModWebsiteLink } from '../mod-links';

  export let state: UiState;
  export let modListMode: ModListMode;
  export let operationRailVisible = false;
  export let operationBlocksInteraction = false;
  export let inspectorOpen = false;
  export let inspectorModKey: string | null = null;
  export let deploymentDraftEntries: DeploymentEntryUiState[] = [];
  export let onSwitchProfile: (event: Event) => void;
  export let onStartDeploymentEdit: () => void;
  export let onCancelDeploymentEdit: () => void;
  export let onDraftChange: (entries: DeploymentEntryUiState[]) => void;
  export let onPreviewDeployment: () => void;
  export let onLaunchGame: () => void;
  export let onOpenInspectorForMod: (modKey: string) => void;
  export let onOpenModPage: (candidate: ModCandidateUiState) => void;
  export let onCollapse: () => void;

  let draggedDeploymentEntryId: string | null = null;
  let deploymentDropGap: number | null = null;
  let selectedModKey: string | null = null;
  let selectionScope = '';

  $: profileCandidates = sortCandidates(
    state.knowledge.candidates.filter((candidate) => candidate.profileState !== 'unlisted')
  );
  $: unlistedProfileCandidates = sortCandidates(
    state.knowledge.candidates.filter((candidate) => candidate.profileState === 'unlisted')
  );
  $: enabledCount = profileCandidates.filter((candidate) => candidate.enabledState === 'enabled').length;
  $: disabledCount = profileCandidates.filter((candidate) => candidate.enabledState === 'disabled').length;
  $: unresolvedCount = profileCandidates.filter((candidate) => candidate.profileState === 'unresolved').length;
  $: editableEntryCount = deploymentDraftEntries.filter((entry) => entry.isEditable).length;
  $: separatorEntryCount = deploymentDraftEntries.filter((entry) => entry.isSeparator).length;
  $: deploymentEditMode = modListMode === 'deployment-edit';

  $: {
    const nextSelectionScope = state.knowledge.session
      ? `${state.knowledge.session.snapshotId}:${state.knowledge.session.profileName}`
      : '';
    if (selectionScope && selectionScope !== nextSelectionScope) selectedModKey = null;
    selectionScope = nextSelectionScope;
  }

  function formatLabel(value: string | null | undefined): string {
    if (!value) return 'Unknown';
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/-/g, ' ')
      .replace(/:/g, ' · ')
      .replace(/^./, (character) => character.toUpperCase());
  }

  function statusClass(status: string | undefined): string {
    return 'status-' + (status ?? 'unknown').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function modDisplayName(candidate: ModCandidateUiState): string {
    return candidate.displayName || candidate.directoryName || candidate.modKey;
  }

  type ModLibraryState = 'Enabled' | 'Disabled' | 'Needs review' | 'Unresolved';

  function normalizedState(value: string | null | undefined): string {
    return value?.trim().toLowerCase().replace(/[^a-z]+/g, '-') ?? '';
  }

  function modLibraryState(candidate: ModCandidateUiState): ModLibraryState {
    if (normalizedState(candidate.profileState) === 'unresolved') return 'Unresolved';

    if (!candidate.version?.trim()) return 'Needs review';

    const packageRelation = candidate.packageRelation;
    const comparisonStatus = normalizedState(packageRelation?.comparison.status);
    if (['unknown', 'unresolved', 'missing', 'not-assessed', 'not-comparable', 'needs-review'].includes(comparisonStatus)) {
      return 'Needs review';
    }

    return normalizedState(candidate.enabledState) === 'enabled' ? 'Enabled' : 'Disabled';
  }

  function modLibraryStateClass(value: ModLibraryState): string {
    return value.toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function roleRank(candidate: ModCandidateUiState): number {
    switch (candidate.role?.role) {
      case 'Foundation': return 0;
      case 'Compatibility': return 1;
      case 'Content': return 2;
      default: return 3;
    }
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

  function websiteButtonLabel(website: ModWebsiteLink): string {
    switch (website.kind) {
      case 'nexus': return 'Nexus';
      case 'website': return 'Website';
      case 'nexus-search': return 'Nexus search';
      default: return 'No usable URL';
    }
  }

  function websiteButtonAriaLabel(website: ModWebsiteLink, candidate: ModCandidateUiState): string {
    const action = website.kind === 'nexus'
      ? 'Open exact Nexus page'
      : website.kind === 'website'
        ? 'Open source website'
        : 'Open inferred Nexus search';
    return `${action} for ${modDisplayName(candidate)}`;
  }

  function selectMod(modKey: string) {
    selectedModKey = modKey;
  }

  function handleModSelectionKeydown(event: KeyboardEvent, modKey: string) {
    if (event.key === 'Escape') {
      if (selectedModKey === modKey) {
        event.preventDefault();
        selectedModKey = null;
      }
      return;
    }

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      selectMod(modKey);
    }
  }

  function modTooltip(candidate: ModCandidateUiState): string {
    const website = resolveModWebsite(candidate).status;
    return [
      `${candidate.role?.role || 'Unknown'} · ${candidate.role?.assessment || 'Unknown'}`,
      `Priority ${candidate.priority ?? 'Unknown'}`,
      website
    ].join(' · ');
  }

  function inspectorActionLabel(modKey: string): string {
    return inspectorOpen && inspectorModKey === modKey ? 'Close Inspector' : 'Inspect evidence';
  }

  function inspectorButtonLabel(modKey: string): string {
    return inspectorOpen && inspectorModKey === modKey ? 'Close' : 'Inspect';
  }

  function operationLabel(): string {
    const operation = state.knowledge.operation;
    const profile = operation.targetProfileName ? ` ${operation.targetProfileName}` : '';
    switch (operation.phase) {
      case 'discovering-source': return 'Finding MO2 source';
      case 'reading-profile': return `Reading profile${profile}`;
      case 'checking-cache': return 'Checking static MOD knowledge';
      case 'scanning-mod-folders': return `Scanning MOD folders${profile}`;
      case 'reusing-static-knowledge': return 'Reusing static MOD knowledge';
      case 'building-index': return 'Building local knowledge index';
      case 'projecting-profile': return `Applying profile${profile}`;
      case 'preloading-profile': return `Preparing profile${profile}`;
      default: return 'Loading local MO2 knowledge';
    }
  }

  function operationProgress(): number | null {
    const { completed, total } = state.knowledge.operation;
    if (typeof completed !== 'number' || typeof total !== 'number' || total <= 0) return null;
    return Math.min(100, Math.max(0, (completed / total) * 100));
  }

  function operationCountLabel(): string | null {
    const { completed, total } = state.knowledge.operation;
    if (typeof completed !== 'number' || typeof total !== 'number') return null;
    return state.knowledge.operation.phase === 'preloading-profile'
      ? `${completed} / ${total} profiles`
      : `${completed} / ${total} MOD folders`;
  }

  function deploymentEditableIndex(entryId: string): number {
    return deploymentDraftEntries.filter((entry) => entry.isEditable).findIndex((entry) => entry.entryId === entryId);
  }

  function startDeploymentDrag(event: DragEvent, entry: DeploymentEntryUiState) {
    if (!entry.isEditable) {
      event.preventDefault();
      return;
    }
    draggedDeploymentEntryId = entry.entryId;
    deploymentDropGap = null;
    event.dataTransfer?.setData('text/plain', entry.entryId);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  function allowDeploymentDrop(event: DragEvent, entry: DeploymentEntryUiState) {
    if (!draggedDeploymentEntryId || !entry.isEditable || entry.entryId === draggedDeploymentEntryId) return;
    event.preventDefault();
    const targetElement = event.currentTarget as HTMLElement | null;
    const targetIndex = deploymentEditableIndex(entry.entryId);
    if (targetElement && targetIndex >= 0) {
      const bounds = targetElement.getBoundingClientRect();
      deploymentDropGap = targetIndex + (event.clientY >= bounds.top + bounds.height / 2 ? 1 : 0);
    }
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
  }

  function dropDeploymentEntry(event: DragEvent, targetEntry: DeploymentEntryUiState) {
    event.preventDefault();
    const draggedEntryId = draggedDeploymentEntryId;
    draggedDeploymentEntryId = null;
    const requestedGap = deploymentDropGap;
    deploymentDropGap = null;
    if (!draggedEntryId || !targetEntry.isEditable || draggedEntryId === targetEntry.entryId || requestedGap === null) return;

    const editableEntries = deploymentDraftEntries.filter((entry) => entry.isEditable);
    const draggedIndex = editableEntries.findIndex((entry) => entry.entryId === draggedEntryId);
    const targetIndex = editableEntries.findIndex((entry) => entry.entryId === targetEntry.entryId);
    if (draggedIndex < 0 || targetIndex < 0) return;
    const [draggedEntry] = editableEntries.splice(draggedIndex, 1);
    const adjustedGap = requestedGap > draggedIndex ? requestedGap - 1 : requestedGap;
    editableEntries.splice(Math.max(0, Math.min(adjustedGap, editableEntries.length)), 0, draggedEntry);
    let editableIndex = 0;
    onDraftChange(deploymentDraftEntries.map((entry) => entry.isEditable ? editableEntries[editableIndex++] : entry));
  }

  function toggleDeploymentEntry(entryId: string) {
    onDraftChange(deploymentDraftEntries.map((entry) => (
      entry.entryId === entryId && entry.isEditable ? { ...entry, enabled: !entry.enabled } : entry
    )));
  }
</script>

<main class="mod-list-surface">
  <header class="mod-list-header">
    <div>
      <span class="eyebrow">MOD LIBRARY</span>
      <div class="mod-list-title-row">
        <h1>{state.knowledge.session?.profileName || state.knowledge.operation.targetProfileName || 'No profile'}</h1>
        {#if state.knowledge.session}
          {@const activeProfile = state.knowledge.profiles.find((profile) => profile.name === state.knowledge.session?.profileName)}
          <span class="status-chip {statusClass(activeProfile?.loadState)}">{formatLabel(activeProfile?.loadState || 'ready')}</span>
        {:else if state.knowledge.operation.isBusy}
          <span class="status-chip status-loading">{formatLabel(state.knowledge.operation.phase)}</span>
        {/if}
      </div>
    </div>
    <button class="icon-button" type="button" title="Collapse Mod Library" aria-label="Collapse Mod Library" disabled={operationBlocksInteraction} onclick={onCollapse}>×</button>
  </header>

  {#if operationRailVisible}
    <div class="mod-list-operation-rail">
      <div class="operation-progress-track" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow={operationProgress() ?? undefined} aria-label={operationLabel()}>
        <div class="operation-progress-fill" class:operation-progress-indeterminate={operationProgress() === null} style:width={operationProgress() === null ? undefined : `${operationProgress()}%`}></div>
      </div>
      <div class="operation-rail-meta" role="status" aria-live="polite">
        <span class="operation-rail-label">{operationLabel()}…</span>
        {#if operationCountLabel()}<span class="operation-rail-count">{operationCountLabel()}</span>{/if}
      </div>
    </div>
  {/if}

  {#if state.knowledge.session}
    {#if operationBlocksInteraction}
      <div class="local-skeleton-panel mod-list-pending" aria-busy="true">
        {#if state.analysis.operation.isBusy}
          <p class="subtle local-skeleton-status" role="status">Analysis is running…</p>
        {:else if state.knowledge.operation.isBusy}
          <p class="subtle local-skeleton-status" role="status">Loading local profile…</p>
        {/if}
        <div class="local-skeleton-stack" aria-hidden="true">
          <span class="local-skeleton local-skeleton-control"></span>
          <span class="local-skeleton local-skeleton-summary"></span>
          <span class="local-skeleton local-skeleton-label"></span>
          <span class="local-skeleton local-skeleton-row"></span>
          <span class="local-skeleton local-skeleton-row local-skeleton-row-short"></span>
          <span class="local-skeleton local-skeleton-row"></span>
          <span class="local-skeleton local-skeleton-row local-skeleton-row-medium"></span>
          <span class="local-skeleton local-skeleton-row"></span>
          <span class="local-skeleton local-skeleton-row local-skeleton-row-short"></span>
          <span class="local-skeleton local-skeleton-row"></span>
          <span class="local-skeleton local-skeleton-row-medium"></span>
        </div>
      </div>
    {:else}
      <div class="deployment-toolbar" aria-label="Profile deployment controls">
        {#if deploymentEditMode}
          <span class="deployment-mode-label">Edit profile</span>
          <button class="secondary-button" type="button" onclick={onCancelDeploymentEdit}>Cancel</button>
          <button class="secondary-button" type="button" onclick={onPreviewDeployment}>Preview deployment</button>
        {:else}
          <button class="secondary-button" type="button" onclick={onStartDeploymentEdit}>Edit profile</button>
        {/if}
        {#if state.deployment.canLaunch}<button class="secondary-button" type="button" onclick={onLaunchGame}>Launch 7DTD</button>{/if}
      </div>

      <label class="mod-list-profile-picker">
        <span>Profile</span>
        <select aria-label="Active profile" value={state.knowledge.session.profileName} onchange={onSwitchProfile}>
          {#each state.knowledge.profiles as profile (profile.name)}
            <option value={profile.name}>{profile.name} · {formatLabel(profile.loadState)}</option>
          {/each}
        </select>
      </label>

      <p class="mod-list-compact-summary" aria-label="Profile MOD status">
        {profileCandidates.length} Local MOD records · {enabledCount} enabled · {disabledCount} disabled · {unresolvedCount} unresolved
      </p>
      <p class="subtle mod-list-count-note">This summary counts Local MOD records. The editor counts profile rows, including separators.</p>

      {#if deploymentEditMode}
        <div class="mod-list-scroll" aria-label="Edit active profile MOD list">
          <div class="mod-list-section-label">EDIT PROFILE ORDER · {deploymentDraftEntries.length} profile rows</div>
          <p class="subtle mod-list-count-note">{editableEntryCount} editable · {separatorEntryCount} separators.</p>
          <p class="subtle deployment-help">Drag to reorder. Use + or − to change the enabled state.</p>
          <div class="mod-list-items deployment-edit-list">
            {#each deploymentDraftEntries as entry (entry.entryId)}
              {#if entry.isSeparator}
                <div class="deployment-separator" aria-label="Profile separator">{entry.modKey}</div>
              {:else}
                {#if deploymentDropGap === deploymentEditableIndex(entry.entryId)}<div class="deployment-drop-line" role="status" aria-label={`Drop before ${entry.modKey}`}></div>{/if}
                <article class="mod-list-item deployment-edit-item" class:mod-list-item-disabled={!entry.enabled} draggable={entry.isEditable} ondragstart={(event) => startDeploymentDrag(event, entry)} ondragover={(event) => allowDeploymentDrop(event, entry)} ondrop={(event) => dropDeploymentEntry(event, entry)} ondragend={() => { draggedDeploymentEntryId = null; deploymentDropGap = null; }}>
                  <div class="deployment-row-controls">
                    <span class="deployment-drag-handle" title="Drag to reorder" aria-hidden="true">☷</span>
                    <button class="deployment-toggle-button" type="button" title={entry.enabled ? 'Disable MOD' : 'Enable MOD'} aria-label={entry.enabled ? `Disable ${entry.modKey}` : `Enable ${entry.modKey}`} disabled={!entry.isEditable} onclick={() => toggleDeploymentEntry(entry.entryId)}>{entry.enabled ? '−' : '+'}</button>
                  </div>
                  <div class="deployment-edit-main"><strong>{entry.modKey}</strong>{#if entry.priority !== null && entry.priority !== undefined}<span>priority {entry.priority}</span>{/if}</div>
                </article>
              {/if}
            {/each}
          </div>
          {#if deploymentDraftEntries.length === 0}<p class="empty-state">No editable MOD entry is available in this profile.</p>{/if}
        </div>
      {:else}
        <div class="mod-list-scroll" aria-label="Active profile MOD list">
          <div class="mod-library-active-profile" aria-label="Active profile">
            <span>Active profile</span>
            <strong>{state.knowledge.session.profileName || 'Unknown profile'}</strong>
          </div>
          {#if profileCandidates.length > 0}
            <div class="mod-list-section-label">PROFILE MODLIST · {profileCandidates.length}</div>
            <div class="mod-library-table-wrap">
              <table class="mod-library-table" aria-label="Compact active profile MOD table">
                <colgroup>
                  <col class="mod-library-column-order" />
                  <col class="mod-library-column-name" />
                  <col class="mod-library-column-state" />
                  <col class="mod-library-column-version" />
                </colgroup>
                <thead>
                  <tr>
                    <th class="mod-library-order" scope="col">#</th>
                    <th scope="col">Name</th>
                    <th scope="col">State</th>
                    <th scope="col">Version</th>
                  </tr>
                </thead>
                <tbody>
                  {#each profileCandidates as candidate, index (candidate.modKey)}
                    {@const website = resolveModWebsite(candidate)}
                    {@const libraryState = modLibraryState(candidate)}
                    <tr class="mod-library-table-row" class:mod-list-item-selected={selectedModKey === candidate.modKey} class:mod-list-item-disabled={normalizedState(candidate.enabledState) === 'disabled'} class:mod-list-item-unresolved={normalizedState(candidate.profileState) === 'unresolved'} title={modTooltip(candidate)}>
                      <td class="mod-library-order">{index + 1}</td>
                      <td class="mod-library-name-cell">
                        <div class="mod-library-name-wrap">
                          <button type="button" class="mod-library-select" aria-pressed={selectedModKey === candidate.modKey} aria-label={`Select ${modDisplayName(candidate)}`} onclick={() => selectMod(candidate.modKey)} onkeydown={(event) => handleModSelectionKeydown(event, candidate.modKey)}>
                            <strong>{modDisplayName(candidate)}</strong>
                          </button>
                          <div class="mod-library-row-actions" aria-label={`Actions for ${modDisplayName(candidate)}`}>
                            {#if website.url}
                              <button type="button" class="secondary-button mod-library-row-action" title={`${website.status}: ${modDisplayName(candidate)}`} aria-label={websiteButtonAriaLabel(website, candidate)} onclick={() => onOpenModPage(candidate)}>{websiteButtonLabel(website)}</button>
                            {:else}
                              <span class="mod-library-row-action-unavailable" aria-label="No usable URL">No usable URL</span>
                            {/if}
                            <button type="button" class="icon-button compact-icon-button mod-library-row-action mod-library-row-action-inspect" title={inspectorActionLabel(candidate.modKey)} aria-label={`${inspectorActionLabel(candidate.modKey)} for ${modDisplayName(candidate)}`} aria-pressed={inspectorOpen && inspectorModKey === candidate.modKey} onclick={() => onOpenInspectorForMod(candidate.modKey)}><span class="mod-library-inspect-icon" aria-hidden="true">{inspectorOpen && inspectorModKey === candidate.modKey ? '×' : '⌕'}</span><span class="mod-library-inspect-label">{inspectorButtonLabel(candidate.modKey)}</span></button>
                          </div>
                        </div>
                      </td>
                      <td class="mod-library-state-cell">
                        <span class="mod-library-state {modLibraryStateClass(libraryState)}" aria-label={libraryState}><span class="mod-library-state-dot" aria-hidden="true"></span>{libraryState}</span>
                      </td>
                      <td class="mod-library-version">{candidate.version || 'Unknown'}</td>
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
          {:else}
            <p class="empty-state">No MOD entry is available in this profile.</p>
          {/if}

          <details class="profile-outside-section">
            <summary>Profile外 · {unlistedProfileCandidates.length}</summary>
            {#if unlistedProfileCandidates.length > 0}
              <div class="mod-library-table-wrap">
                <table class="mod-library-table" aria-label="Compact MOD table outside active profile">
                  <colgroup>
                    <col class="mod-library-column-order" />
                    <col class="mod-library-column-name" />
                    <col class="mod-library-column-state" />
                    <col class="mod-library-column-version" />
                  </colgroup>
                  <thead>
                    <tr>
                      <th class="mod-library-order" scope="col">#</th>
                      <th scope="col">Name</th>
                      <th scope="col">State</th>
                      <th scope="col">Version</th>
                    </tr>
                  </thead>
                  <tbody>
                    {#each unlistedProfileCandidates as candidate, index (candidate.modKey)}
                      {@const website = resolveModWebsite(candidate)}
                      {@const libraryState = modLibraryState(candidate)}
                      <tr class="mod-library-table-row" class:mod-list-item-selected={selectedModKey === candidate.modKey} class:mod-list-item-disabled={normalizedState(candidate.enabledState) === 'disabled'} class:mod-list-item-unresolved={normalizedState(candidate.profileState) === 'unresolved'} title={`${modTooltip(candidate)} · Profile outside`}>
                        <td class="mod-library-order">{index + 1}</td>
                        <td class="mod-library-name-cell">
                          <div class="mod-library-name-wrap">
                            <button type="button" class="mod-library-select" aria-pressed={selectedModKey === candidate.modKey} aria-label={`Select ${modDisplayName(candidate)}`} onclick={() => selectMod(candidate.modKey)} onkeydown={(event) => handleModSelectionKeydown(event, candidate.modKey)}>
                              <strong>{modDisplayName(candidate)}</strong>
                            </button>
                            <div class="mod-library-row-actions" aria-label={`Actions for ${modDisplayName(candidate)}`}>
                              {#if website.url}
                                <button type="button" class="secondary-button mod-library-row-action" title={`${website.status}: ${modDisplayName(candidate)}`} aria-label={websiteButtonAriaLabel(website, candidate)} onclick={() => onOpenModPage(candidate)}>{websiteButtonLabel(website)}</button>
                              {:else}
                                <span class="mod-library-row-action-unavailable" aria-label="No usable URL">No usable URL</span>
                              {/if}
                              <button type="button" class="icon-button compact-icon-button mod-library-row-action mod-library-row-action-inspect" title={inspectorActionLabel(candidate.modKey)} aria-label={`${inspectorActionLabel(candidate.modKey)} for ${modDisplayName(candidate)}`} aria-pressed={inspectorOpen && inspectorModKey === candidate.modKey} onclick={() => onOpenInspectorForMod(candidate.modKey)}><span class="mod-library-inspect-icon" aria-hidden="true">{inspectorOpen && inspectorModKey === candidate.modKey ? '×' : '⌕'}</span><span class="mod-library-inspect-label">{inspectorButtonLabel(candidate.modKey)}</span></button>
                            </div>
                          </div>
                        </td>
                        <td class="mod-library-state-cell">
                          <span class="mod-library-state {modLibraryStateClass(libraryState)}" aria-label={libraryState}><span class="mod-library-state-dot" aria-hidden="true"></span>{libraryState}</span>
                        </td>
                        <td class="mod-library-version">{candidate.version || 'Unknown'}</td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            {:else}<p class="empty-state">No MOD exists outside this profile.</p>{/if}
          </details>
        </div>
      {/if}
    {/if}
  {:else}
    <div class="mod-list-empty-state" role={state.knowledge.operation.isBusy ? 'status' : undefined} aria-busy={state.knowledge.operation.isBusy ? 'true' : 'false'}>
      <span class="eyebrow">LOCAL MODS</span>
      {#if state.knowledge.operation.isBusy}
        <p class="subtle">Preparing local MO2 knowledge. Browser remains available.</p>
        <div class="local-skeleton-stack" aria-hidden="true">
          <span class="local-skeleton local-skeleton-row"></span>
          <span class="local-skeleton local-skeleton-row-short"></span>
          <span class="local-skeleton local-skeleton-row-medium"></span>
        </div>
      {:else}
        <p class="subtle">Load an MO2 source to show the active profile.</p>
      {/if}
    </div>
  {/if}
</main>

<style>
  .mod-library-active-profile {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 8px;
    min-width: 0;
    margin: 9px 0 7px;
    padding: 8px 9px;
    border: 1px solid rgba(125, 211, 252, 0.22);
    border-radius: 7px;
    background: rgba(14, 116, 144, 0.14);
  }

  .mod-library-active-profile span {
    flex: 0 0 auto;
    color: #7dd3fc;
    font-size: 9px;
    font-weight: 800;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .mod-library-active-profile strong {
    min-width: 0;
    overflow: hidden;
    color: #f8fafc;
    font-size: 11px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .mod-library-table-wrap {
    min-width: 0;
    max-width: 100%;
    margin-top: 5px;
    overflow: hidden;
  }

  .mod-library-table {
    width: 100%;
    max-width: 100%;
    min-width: 0;
    table-layout: fixed;
    border-collapse: collapse;
  }

  .mod-library-column-order {
    width: 10%;
  }

  .mod-library-column-name {
    width: 39%;
  }

  .mod-library-column-state {
    width: 31%;
  }

  .mod-library-column-version {
    width: 20%;
  }

  .mod-library-table th,
  .mod-library-table td {
    min-width: 0;
    padding: 6px 5px;
    border-bottom: 1px solid rgba(60, 64, 67, 0.68);
    text-align: left;
    vertical-align: middle;
  }

  .mod-library-table th {
    color: #64748b;
    font-size: 9px;
    font-weight: 800;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    white-space: nowrap;
  }

  .mod-library-table td {
    overflow: hidden;
    color: #e2e8f0;
    font-size: 11px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .mod-library-order {
    color: #64748b !important;
    font-variant-numeric: tabular-nums;
    text-align: right !important;
  }

  .mod-library-table-row {
    transition: background-color 140ms ease, box-shadow 140ms ease;
  }

  .mod-library-table-row:hover,
  .mod-library-table-row:focus-within {
    background: rgba(56, 189, 248, 0.08);
  }

  .mod-library-table-row.mod-list-item-selected {
    background: rgba(14, 116, 144, 0.25);
    box-shadow: inset 2px 0 0 #38bdf8;
  }

  .mod-library-table-row.mod-list-item-disabled {
    background: rgba(41, 42, 45, 0.72);
    filter: grayscale(0.6);
    opacity: 0.62;
  }

  .mod-library-table-row.mod-list-item-unresolved td {
    border-bottom-style: dashed;
  }

  .mod-library-name-cell {
    position: relative;
    overflow: hidden;
  }

  .mod-library-name-wrap {
    position: relative;
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
  }

  .mod-library-select {
    display: block;
    width: 100%;
    min-width: 0;
    overflow: hidden;
    padding: 4px 3px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: #e2e8f0;
    text-align: left;
  }

  .mod-library-select:hover:not(:disabled),
  .mod-library-select:focus-visible {
    background: rgba(56, 189, 248, 0.1);
  }

  .mod-library-select strong {
    display: block;
    min-width: 0;
    overflow: hidden;
    color: #f8fafc;
    font-size: 11px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .mod-library-row-actions {
    position: absolute;
    top: 50%;
    right: 2px;
    z-index: 1;
    display: flex;
    align-items: center;
    gap: 3px;
    max-width: calc(100% - 4px);
    padding-left: 4px;
    transform: translateY(-50%);
    visibility: hidden;
    opacity: 0;
    background: var(--chrome-navigation);
    transition: opacity 140ms ease;
    pointer-events: none;
  }

  .mod-library-table-row:hover .mod-library-row-actions,
  .mod-library-table-row:focus-within .mod-library-row-actions,
  .mod-library-table-row.mod-list-item-selected .mod-library-row-actions {
    visibility: visible;
    opacity: 1;
    pointer-events: auto;
  }

  .mod-library-row-action {
    min-width: 0;
    max-width: 74px;
    min-height: 24px;
    overflow: hidden;
    padding: 3px 6px;
    border-radius: 6px;
    font-size: 9px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .mod-library-row-action-inspect {
    width: 24px;
    height: 24px;
    flex: 0 0 24px;
    padding: 0;
    font-size: 13px;
  }

  .mod-library-inspect-label {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip: rect(0 0 0 0);
    clip-path: inset(50%);
    white-space: nowrap;
  }

  .mod-library-table-row.mod-list-item-selected .mod-library-row-action-inspect {
    width: auto;
    min-width: 54px;
    gap: 4px;
    padding: 3px 6px;
    border-color: rgba(125, 211, 252, 0.2);
    background: rgba(14, 116, 144, 0.16);
    color: #bae6fd;
    font-size: 9px;
  }

  .mod-library-table-row.mod-list-item-selected .mod-library-inspect-label {
    position: static;
    width: auto;
    height: auto;
    overflow: visible;
    clip: auto;
    clip-path: none;
    white-space: nowrap;
  }

  .mod-library-row-action-unavailable {
    display: inline-flex;
    align-items: center;
    min-height: 24px;
    overflow: hidden;
    color: #64748b;
    font-size: 9px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .mod-library-select:focus-visible,
  .mod-library-row-action:focus-visible {
    outline: 2px solid #7dd3fc;
    outline-offset: -1px;
  }

  .mod-library-state {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .mod-library-state-dot {
    width: 6px;
    height: 6px;
    flex: 0 0 6px;
    border: 1px solid #64748b;
    border-radius: 50%;
    background: #475569;
  }

  .mod-library-state.enabled .mod-library-state-dot {
    border-color: #86efac;
    background: #4ade80;
  }

  .mod-library-state.disabled .mod-library-state-dot {
    border-color: #64748b;
    background: #475569;
  }

  .mod-library-state.needs-review {
    color: #fdd663;
  }

  .mod-library-state.needs-review .mod-library-state-dot {
    border-color: #fdd663;
    background: #eab308;
  }

  .mod-library-state.unresolved {
    color: #f28b82;
  }

  .mod-library-state.unresolved .mod-library-state-dot {
    border-color: #f28b82;
    background: #ef4444;
  }

  .mod-library-version {
    color: #cbd5e1 !important;
    font-variant-numeric: tabular-nums;
  }

  @media (max-width: 360px) {
    .mod-library-table th,
    .mod-library-table td {
      padding-right: 3px;
      padding-left: 3px;
    }

    .mod-library-row-action {
      max-width: 58px;
      padding-right: 4px;
      padding-left: 4px;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .mod-library-table-row,
    .mod-library-row-actions {
      transition: none;
    }
  }
</style>
