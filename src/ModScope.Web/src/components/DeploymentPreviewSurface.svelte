<script lang="ts">
  import type { BridgeErrorPayload, UiState } from '../contracts';

  export let state: UiState;
  export let error: BridgeErrorPayload | null = null;
  export let applyPending = false;
  export let applyConfirmOpen = false;
  export let search = '';
  export let onRequestApply: () => void;
  export let onCancelApply: () => void;
  export let onApply: () => void;

  let modChangesOpen = false;
  let junctionChangesOpen = false;
  let diagnosticsOpen = false;

  $: searchValue = search.trim().toLocaleLowerCase();
  $: searchActive = searchValue.length > 0;
  $: modChanges = state.deployment.modChanges.filter((change) => matches(`${change.modKey} ${change.beforeEnabled ? 'enabled' : 'disabled'} ${change.afterEnabled ? 'enabled' : 'disabled'}`));
  $: junctionChanges = state.deployment.junctionChanges.filter((change) => matches(`${change.action} ${change.targetName}`));
  $: diagnostics = state.deployment.diagnostics.filter((diagnostic) => matches(`${diagnostic.code} ${diagnostic.message} ${diagnostic.rawValue ?? ''}`));
  $: enabledChangeCount = state.deployment.modChanges.filter((change) => change.beforeEnabled !== change.afterEnabled).length;
  $: orderChangeCount = state.deployment.modChanges.filter((change) => change.beforeOrder !== change.afterOrder).length;
  $: junctionCreateCount = state.deployment.junctionChanges.filter((change) => change.action === 'create' || change.action === 'adopt').length;
  $: junctionRemoveCount = state.deployment.junctionChanges.filter((change) => change.action === 'remove').length;
  $: blockingDiagnosticCount = state.deployment.diagnostics.filter((diagnostic) => diagnostic.severity.toLowerCase() === 'error').length;

  function matches(value: string): boolean {
    return searchValue.length === 0 || value.toLocaleLowerCase().includes(searchValue);
  }

  function formatLabel(value: string | null | undefined): string {
    if (!value) return 'Unknown';
    return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/-/g, ' ').replace(/^./, (character) => character.toUpperCase());
  }
</script>

<main class="deployment-preview-surface">
  {#if error}<p class="error-notice"><strong>{error.code}</strong> {error.message}</p>{/if}

  <header class="deployment-preview-header">
    <div><span class="eyebrow">DEPLOYMENT PREVIEW</span><h1>Review profile and junction changes</h1><p class="summary-meta">Profile · {state.deployment.profileName || 'Unknown profile'}</p></div>
    <span class="status-chip status-{state.deployment.status.toLowerCase()}">{formatLabel(state.deployment.status)}</span>
  </header>

  <section class="deployment-preview-summary" aria-label="Deployment summary">
    <div class="deployment-preview-summary-item"><span>MOD changes</span><strong>{state.deployment.modChanges.length}</strong><small>{enabledChangeCount} enabled-state · {orderChangeCount} order</small></div>
    <div class="deployment-preview-summary-item"><span>Junction changes</span><strong>{state.deployment.junctionChanges.length}</strong><small>{junctionCreateCount} create/adopt · {junctionRemoveCount} remove</small></div>
    <div class="deployment-preview-summary-item"><span>Diagnostics</span><strong>{state.deployment.diagnostics.length}</strong><small>{blockingDiagnosticCount} blocking</small></div>
  </section>

  <section class="deployment-preview-actions" aria-live="polite">
    {#if applyPending}
      <div class="deployment-preview-action-bar"><p><strong>Applying deployment…</strong> ModScope is verifying the profile and junction results.</p></div>
    {:else if state.deployment.canApply && state.deployment.planId}
      {#if applyConfirmOpen}
        <div class="deployment-apply-confirmation" role="alertdialog" aria-labelledby="deployment-apply-title" aria-describedby="deployment-apply-description">
          <span class="eyebrow">EXPLICIT APPROVAL</span><h2 id="deployment-apply-title">Apply this deployment?</h2>
          <p id="deployment-apply-description">ModScope will back up <code>modlist.txt</code>, apply the profile and junction changes, then verify both results.</p>
          <div class="deployment-preview-confirm-summary"><span>{state.deployment.modChanges.length} MOD changes</span><span>{state.deployment.junctionChanges.length} junction changes</span></div>
          <div class="action-row"><button class="secondary-button" type="button" onclick={onCancelApply}>Cancel</button><button class="primary-button" type="button" onclick={onApply}>Apply profile and junctions</button></div>
        </div>
      {:else}
        <div class="deployment-preview-action-bar"><p class="subtle">No changes are written until you approve this plan.</p><button class="primary-button" type="button" onclick={onRequestApply}>Review and apply</button></div>
      {/if}
    {:else if state.deployment.status === 'applied'}
      <div class="deployment-preview-action-bar deployment-preview-success"><p><strong>Deployment applied and verified.</strong> Return to the Mod Library when you are ready.</p></div>
    {:else if state.deployment.status === 'recovery-required'}
      <div class="deployment-preview-action-bar deployment-preview-blocked"><p><strong>Recovery is required.</strong> Review the diagnostics below before making another deployment change.</p></div>
    {:else}
      <div class="deployment-preview-action-bar deployment-preview-blocked"><p><strong>Apply is blocked.</strong> Resolve the diagnostics below, then preview the current profile again.</p></div>
    {/if}
  </section>

  <label class="deployment-preview-search"><span>Search changes</span><input type="search" aria-label="Search deployment changes" placeholder="MOD name, junction action, or diagnostic" bind:value={search} /></label>

  <div class="deployment-preview-sections">
    <section class="deployment-preview-section" aria-labelledby="deployment-mod-changes-title">
      <button class="deployment-preview-section-toggle" type="button" aria-expanded={modChangesOpen || searchActive} onclick={() => (modChangesOpen = !modChangesOpen)}><span id="deployment-mod-changes-title">MODLIST CHANGES</span><span>{modChanges.length}/{state.deployment.modChanges.length}</span></button>
      {#if modChangesOpen || searchActive}<div class="deployment-preview-detail-list">{#if modChanges.length === 0}<p class="empty-state">No MOD changes match this search.</p>{:else}{#each modChanges as change, index (change.modKey + index)}<article class="deployment-preview-detail-row"><strong>{change.modKey}</strong><span>{change.beforeEnabled ? '+' : '−'} → {change.afterEnabled ? '+' : '−'}</span>{#if change.beforeOrder !== change.afterOrder}<small>priority {change.beforeOrder} → {change.afterOrder}</small>{/if}</article>{/each}{/if}</div>{/if}
    </section>
    <section class="deployment-preview-section" aria-labelledby="deployment-junction-changes-title">
      <button class="deployment-preview-section-toggle" type="button" aria-expanded={junctionChangesOpen || searchActive} onclick={() => (junctionChangesOpen = !junctionChangesOpen)}><span id="deployment-junction-changes-title">JUNCTION CHANGES</span><span>{junctionChanges.length}/{state.deployment.junctionChanges.length}</span></button>
      {#if junctionChangesOpen || searchActive}<div class="deployment-preview-detail-list">{#if junctionChanges.length === 0}<p class="empty-state">No junction changes match this search.</p>{:else}{#each junctionChanges as change, index (change.action + change.targetName + index)}<article class="deployment-preview-detail-row"><strong>{change.targetName}</strong><span>{formatLabel(change.action)}</span></article>{/each}{/if}</div>{/if}
    </section>
    <section class="deployment-preview-section" aria-labelledby="deployment-diagnostics-title">
      <button class="deployment-preview-section-toggle" type="button" aria-expanded={diagnosticsOpen || searchActive} onclick={() => (diagnosticsOpen = !diagnosticsOpen)}><span id="deployment-diagnostics-title">DIAGNOSTICS</span><span>{diagnostics.length}/{state.deployment.diagnostics.length}</span></button>
      {#if diagnosticsOpen || searchActive}<div class="deployment-preview-detail-list">{#if diagnostics.length === 0}<p class="empty-state">No diagnostics match this search.</p>{:else}{#each diagnostics as diagnostic, index (diagnostic.code + diagnostic.message + index)}<article class="deployment-preview-detail-row deployment-preview-diagnostic-row"><strong>{diagnostic.code}</strong><span>{diagnostic.message}</span></article>{/each}{/if}</div>{/if}
    </section>
  </div>
</main>
