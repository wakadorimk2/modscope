<script lang="ts">
  import type { DeploymentEntryUiState, ModCandidateUiState, UiState } from '../contracts';

  export let state: UiState;
  export let operationRailVisible = false;
  export let operationBlocksInteraction = false;
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

  $: profileCandidates = sortCandidates(
    state.knowledge.candidates.filter((candidate) => candidate.profileState !== 'unlisted')
  );
  $: unlistedProfileCandidates = sortCandidates(
    state.knowledge.candidates.filter((candidate) => candidate.profileState === 'unlisted')
  );
  $: enabledCount = profileCandidates.filter((candidate) => candidate.enabledState === 'enabled').length;
  $: disabledCount = profileCandidates.filter((candidate) => candidate.enabledState === 'disabled').length;
  $: unresolvedCount = profileCandidates.filter((candidate) => candidate.profileState === 'unresolved').length;
  $: deploymentEditMode = state.layout.modListMode === 'deployment-edit';

  type ModWebsiteLink = {
    url: string | null;
    status: 'Verified' | 'Inferred' | 'No usable URL';
    nexusSearchName?: string;
  };

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

  function resolveModWebsite(candidate: ModCandidateUiState): ModWebsiteLink {
    if (isWebsiteUrl(candidate.website)) {
      return { url: candidate.website.trim(), status: 'Verified' };
    }
    const name = [candidate.displayName, candidate.directoryName, candidate.modKey]
      .map((value) => value?.trim() ?? '')
      .find((value) => value.length > 0) ?? '';
    if (!name) return { url: null, status: 'No usable URL' };
    return {
      url: `https://www.nexusmods.com/games/7daystodie/mods?keyword=${encodeURIComponent(name)}`,
      status: 'Inferred',
      nexusSearchName: name
    };
  }

  function enabledLampLabel(candidate: ModCandidateUiState): string {
    return candidate.enabledState === 'enabled' ? 'Enabled' : formatLabel(candidate.enabledState);
  }

  function modTooltip(candidate: ModCandidateUiState): string {
    const website = resolveModWebsite(candidate).status;
    return [
      `${candidate.role?.role || 'Unknown'} · ${candidate.role?.assessment || 'Unknown'}`,
      `Priority ${candidate.priority ?? 'Unknown'}`,
      website
    ].join(' · ');
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
    <div class="deployment-toolbar" aria-label="Profile deployment controls">
      {#if deploymentEditMode}
        <span class="deployment-mode-label">Edit profile</span>
        <button class="secondary-button" type="button" disabled={operationBlocksInteraction} onclick={onCancelDeploymentEdit}>Cancel</button>
        <button class="secondary-button" type="button" disabled={operationBlocksInteraction} onclick={onPreviewDeployment}>Preview deployment</button>
      {:else}
        <button class="secondary-button" type="button" disabled={operationBlocksInteraction} onclick={onStartDeploymentEdit}>Edit profile</button>
      {/if}
      {#if state.deployment.canLaunch}<button class="secondary-button" type="button" disabled={operationBlocksInteraction} onclick={onLaunchGame}>Launch 7DTD</button>{/if}
    </div>

    <label class="mod-list-profile-picker">
      <span>Profile</span>
      <select aria-label="Active profile" value={state.knowledge.session.profileName} disabled={operationBlocksInteraction} onchange={onSwitchProfile}>
        {#each state.knowledge.profiles as profile (profile.name)}
          <option value={profile.name}>{profile.name} · {formatLabel(profile.loadState)}</option>
        {/each}
      </select>
    </label>

    <p class="mod-list-compact-summary" aria-label="Profile MOD status">
      {profileCandidates.length} in profile · {enabledCount} enabled · {disabledCount} disabled · {unresolvedCount} unresolved
    </p>

    {#if deploymentEditMode}
      <div class="mod-list-scroll" aria-label="Edit active profile MOD list">
        <div class="mod-list-section-label">EDIT PROFILE ORDER · {deploymentDraftEntries.length}</div>
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
                  <button class="deployment-toggle-button" type="button" title={entry.enabled ? 'Disable MOD' : 'Enable MOD'} aria-label={entry.enabled ? `Disable ${entry.modKey}` : `Enable ${entry.modKey}`} disabled={!entry.isEditable || operationBlocksInteraction} onclick={() => toggleDeploymentEntry(entry.entryId)}>{entry.enabled ? '−' : '+'}</button>
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
        {#if profileCandidates.length > 0}
          <div class="mod-list-section-label">PROFILE MODLIST · {profileCandidates.length}</div>
          <div class="mod-list-items">
            {#each profileCandidates as candidate (candidate.modKey)}
              <article class="mod-list-item" class:mod-list-item-disabled={candidate.enabledState === 'disabled'} class:mod-list-item-unresolved={candidate.profileState === 'unresolved'} title={modTooltip(candidate)}>
                <div class="mod-list-item-top">
                  {#if resolveModWebsite(candidate).url}
                    <button type="button" class="mod-list-item-main" aria-label={`Open ${modDisplayName(candidate)} page · ${resolveModWebsite(candidate).status}`} disabled={operationBlocksInteraction} onclick={() => onOpenModPage(candidate)}><strong>{modDisplayName(candidate)}</strong>{#if candidate.version}<span>v{candidate.version}</span>{/if}</button>
                  {:else}
                    <div class="mod-list-item-main mod-card-main-disabled"><strong>{modDisplayName(candidate)}</strong>{#if candidate.version}<span>v{candidate.version}</span>{/if}</div>
                  {/if}
                  <button type="button" class="icon-button compact-icon-button mod-list-item-inspect" title="Inspect evidence" aria-label={`Inspect ${modDisplayName(candidate)} evidence`} disabled={operationBlocksInteraction} onclick={() => onOpenInspectorForMod(candidate.modKey)}>⌕</button>
                  <span class="mod-enabled-lamp" class:enabled={candidate.enabledState === 'enabled'} class:disabled={candidate.enabledState !== 'enabled'} role="img" aria-label={enabledLampLabel(candidate)}></span>
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
                <article class="mod-list-item mod-list-item-unresolved" title={modTooltip(candidate)}>
                  <div class="mod-list-item-top">
                    {#if resolveModWebsite(candidate).url}
                      <button type="button" class="mod-list-item-main" aria-label={`Open ${modDisplayName(candidate)} page · ${resolveModWebsite(candidate).status}`} disabled={operationBlocksInteraction} onclick={() => onOpenModPage(candidate)}><strong>{modDisplayName(candidate)}</strong>{#if candidate.version}<span>v{candidate.version}</span>{/if}</button>
                    {:else}<div class="mod-list-item-main mod-card-main-disabled"><strong>{modDisplayName(candidate)}</strong></div>{/if}
                    <button type="button" class="icon-button compact-icon-button mod-list-item-inspect" title="Inspect evidence" aria-label={`Inspect ${modDisplayName(candidate)} evidence`} disabled={operationBlocksInteraction} onclick={() => onOpenInspectorForMod(candidate.modKey)}>⌕</button>
                    <span class="mod-enabled-lamp disabled" role="img" aria-label={enabledLampLabel(candidate)}></span>
                  </div>
                  <span class="mod-list-item-tooltip" role="tooltip">{modTooltip(candidate)} · Profile outside</span>
                </article>
              {/each}
            </div>
          {:else}<p class="empty-state">No MOD exists outside this profile.</p>{/if}
        </details>
      </div>
    {/if}
  {:else}
    <div class="mod-list-empty-state"><span class="eyebrow">LOCAL MODS</span><p class="subtle">Load an MO2 source to show the active profile.</p></div>
  {/if}
</main>
