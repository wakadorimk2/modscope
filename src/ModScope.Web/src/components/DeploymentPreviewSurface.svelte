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

  function deploymentRiskLabel(): string {
    const status = state.deployment.status.toLowerCase();
    if (status === 'recovery-required') return 'Recovery required';
    if (blockingDiagnosticCount > 0) return `Blocked by ${blockingDiagnosticCount} error${blockingDiagnosticCount === 1 ? '' : 's'}`;
    if (state.deployment.canApply && state.deployment.planId) return 'Review required before apply';
    return 'Apply blocked';
  }

  function deploymentRiskDescription(): string {
    const status = state.deployment.status.toLowerCase();
    if (status === 'recovery-required') return 'Resolve the recovery diagnostics before another deployment change.';
    if (blockingDiagnosticCount > 0) return 'Resolve blocking diagnostics before explicit approval.';
    if (state.deployment.canApply && state.deployment.planId) return 'The plan is ready for review. Explicit approval is still required.';
    return 'The host has not produced an approvable deployment plan.';
  }

  function deploymentBlockReason(): string {
    const status = state.deployment.status.toLowerCase();
    if (status === 'recovery-required') return 'Review the recovery diagnostics before another deployment change.';
    if (blockingDiagnosticCount > 0) return `Resolve ${blockingDiagnosticCount} blocking diagnostic${blockingDiagnosticCount === 1 ? '' : 's'} before explicit approval.`;
    if (!state.deployment.planId) return 'No active preview plan is available. Preview the current profile again.';
    return 'The host has not marked this preview as approvable.';
  }

  function deploymentRollbackLabel(): string {
    const status = state.deployment.status.toLowerCase();
    if (status === 'recovery-required') return 'Recovery is required before another apply.';
    if (status === 'applied') return 'The deployment was applied and verified.';
    return 'ModScope backs up modlist.txt and uses the rollback path if verification fails.';
  }
</script>

<main class="deployment-preview-surface">
  {#if error}<p class="error-notice"><strong>{error.code}</strong> {error.message}</p>{/if}

  <header class="deployment-preview-header">
    <div><span class="eyebrow">DEPLOYMENT PREVIEW</span><h1>Review profile and junction changes</h1><p class="summary-meta">Profile · {state.deployment.profileName || 'Unknown profile'}</p></div>
    <span class="status-chip status-{state.deployment.status.toLowerCase()}">{formatLabel(state.deployment.status)}</span>
  </header>

  <section class="deployment-preview-summary" aria-label="Deployment summary">
    <div class="deployment-preview-summary-item"><span>MODLIST changes</span><strong>{state.deployment.modChanges.length}</strong><small>{enabledChangeCount} enabled-state · {orderChangeCount} order</small></div>
    <div class="deployment-preview-summary-item"><span>Junction operations</span><strong>{state.deployment.junctionChanges.length}</strong><small>{junctionCreateCount} create/adopt · {junctionRemoveCount} remove</small></div>
    <div class="deployment-preview-summary-item"><span>Diagnostics</span><strong>{state.deployment.diagnostics.length}</strong><small>{blockingDiagnosticCount} blocking</small></div>
  </section>

  <section class="deployment-preview-safety" aria-labelledby="deployment-safety-title">
    <div>
      <span class="eyebrow">CONTROLLED WRITE</span>
      <h2 id="deployment-safety-title">Target, risk, and rollback</h2>
    </div>
    <dl class="deployment-preview-safety-grid">
      <div>
        <dt>Target</dt>
        <dd><strong>{state.deployment.profileName || 'Unknown profile'}</strong><small>{state.deployment.junctionChanges.length} managed junction operation(s) are listed below.</small></dd>
      </div>
      <div>
        <dt>Risk</dt>
        <dd><strong class="deployment-preview-risk">{deploymentRiskLabel()}</strong><small>{deploymentRiskDescription()}</small></dd>
      </div>
      <div>
        <dt>Rollback</dt>
        <dd><strong class="deployment-preview-rollback">{deploymentRollbackLabel()}</strong></dd>
      </div>
      <div>
        <dt>Plan</dt>
        <dd><code class="deployment-preview-plan-id">{state.deployment.planId || 'No active plan'}</code></dd>
      </div>
    </dl>
    <details class="deployment-preview-junction-targets">
      <summary>Junction targets · {state.deployment.junctionChanges.length}</summary>
      {#if state.deployment.junctionChanges.length > 0}
        <ul>
          {#each state.deployment.junctionChanges as change, index (change.targetName + change.action + index)}
            <li><strong>{change.targetName}</strong><span>{formatLabel(change.action)}</span></li>
          {/each}
        </ul>
      {:else}
        <p class="subtle">No junction target is included in this plan.</p>
      {/if}
    </details>
  </section>

  <section class="deployment-preview-actions" aria-live="polite">
    {#if applyPending}
      <div class="deployment-preview-action-bar"><p><strong>Applying deployment…</strong> ModScope is verifying the profile and junction results.</p></div>
    {:else if state.deployment.canApply && state.deployment.planId}
      {#if applyConfirmOpen}
        <div class="deployment-apply-confirmation" role="alertdialog" aria-labelledby="deployment-apply-title" aria-describedby="deployment-apply-description">
          <span class="eyebrow">EXPLICIT APPROVAL</span><h2 id="deployment-apply-title">Apply this deployment?</h2>
          <p id="deployment-apply-description">ModScope will back up <code>modlist.txt</code>, apply the profile and junction changes, then verify both results.</p>
          <div class="deployment-preview-confirm-summary"><span>{state.deployment.modChanges.length} MODLIST changes</span><span>{state.deployment.junctionChanges.length} junction operations</span></div>
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
      <div class="deployment-preview-action-bar deployment-preview-blocked"><p><strong>Apply is blocked.</strong> {deploymentBlockReason()}</p></div>
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
      {#if diagnosticsOpen || searchActive}<div class="deployment-preview-detail-list">{#if diagnostics.length === 0}<p class="empty-state">No diagnostics match this search.</p>{:else}{#each diagnostics as diagnostic, index (diagnostic.code + diagnostic.message + index)}<article class="deployment-preview-detail-row deployment-preview-diagnostic-row"><strong>{diagnostic.code}</strong><span class="deployment-preview-diagnostic-severity">{formatLabel(diagnostic.severity)}</span><small>{diagnostic.message}{#if diagnostic.rawValue} · observed value: {diagnostic.rawValue}{/if}</small></article>{/each}{/if}</div>{/if}
    </section>
  </div>
</main>

<style>
  .deployment-preview-surface {
    --deployment-base: #202124;
    --deployment-navigation: #292a2d;
    --deployment-panel: #303134;
    --deployment-panel-2: #27282b;
    --deployment-border: #3c4043;
    --deployment-text: #e8eaed;
    --deployment-muted: #9aa0a6;
    --deployment-dim: #6f747a;
    --deployment-blue: #8ab4f8;
    --deployment-green: #81c995;
    --deployment-yellow: #fdd663;
    --deployment-red: #f28b82;

    min-height: 100%;
    padding: 20px clamp(18px, 2.4vw, 34px) 28px;
    overflow-x: hidden;
    overflow-y: auto;
    background: var(--deployment-base);
    color: var(--deployment-text);
    font-size: 12px;
    line-height: 1.45;
  }

  .deployment-preview-surface > * {
    width: min(100%, 1180px);
    margin-right: auto;
    margin-left: auto;
  }

  .deployment-preview-surface .eyebrow {
    color: var(--deployment-blue);
    font-size: 9px;
    letter-spacing: 0.1em;
  }

  .deployment-preview-header {
    align-items: flex-start;
    gap: 16px;
    padding-bottom: 13px;
    border-bottom: 1px solid var(--deployment-border);
  }

  .deployment-preview-header h1 {
    margin: 5px 0 4px;
    color: var(--deployment-text);
    font-size: clamp(20px, 2.2vw, 27px);
    letter-spacing: -0.025em;
    line-height: 1.15;
  }

  .deployment-preview-header .summary-meta {
    margin: 0;
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-header > .status-chip {
    flex: 0 0 auto;
    align-self: flex-start;
    margin-top: 1px;
    border-color: var(--deployment-border);
    border-radius: 4px;
    background: var(--deployment-panel);
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-header > .status-chip.status-preview,
  .deployment-preview-header > .status-chip.status-review {
    border-color: rgba(138, 180, 248, 0.38);
    background: rgba(66, 91, 126, 0.34);
    color: var(--deployment-blue);
  }

  .deployment-preview-header > .status-chip.status-applied,
  .deployment-preview-header > .status-chip.status-success {
    border-color: rgba(129, 201, 149, 0.38);
    background: rgba(53, 93, 65, 0.34);
    color: var(--deployment-green);
  }

  .deployment-preview-header > .status-chip.status-blocked,
  .deployment-preview-header > .status-chip.status-recovery-required {
    border-color: rgba(253, 214, 99, 0.44);
    background: rgba(101, 82, 34, 0.34);
    color: var(--deployment-yellow);
  }

  .deployment-preview-surface .error-notice {
    margin-top: 10px;
    border: 1px solid rgba(242, 139, 130, 0.38);
    border-left: 3px solid var(--deployment-red);
    border-radius: 4px;
    background: rgba(95, 44, 43, 0.34);
    color: #f6d5d2;
    font-size: 11px;
  }

  .deployment-preview-summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 7px;
    margin-top: 13px;
  }

  .deployment-preview-summary-item {
    display: grid;
    gap: 2px;
    padding: 10px 11px;
    border: 1px solid var(--deployment-border);
    border-radius: 5px;
    background: var(--deployment-navigation);
  }

  .deployment-preview-summary-item span,
  .deployment-preview-summary-item small {
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-summary-item span {
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  .deployment-preview-summary-item strong {
    color: var(--deployment-text);
    font-size: 20px;
    line-height: 1.1;
  }

  .deployment-preview-summary-item small {
    overflow-wrap: anywhere;
  }

  .deployment-preview-safety {
    display: grid;
    gap: 11px;
    margin-top: 12px;
    padding: 13px;
    border: 1px solid rgba(138, 180, 248, 0.32);
    border-left: 3px solid var(--deployment-blue);
    border-radius: 5px;
    background: var(--deployment-panel);
  }

  .deployment-preview-safety h2 {
    margin: 4px 0 0;
    color: var(--deployment-text);
    font-size: 16px;
    letter-spacing: -0.015em;
    line-height: 1.2;
  }

  .deployment-preview-safety-grid {
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 7px;
    margin: 0;
  }

  .deployment-preview-safety-grid > div {
    min-width: 0;
    padding: 9px 10px;
    border: 1px solid var(--deployment-border);
    border-radius: 4px;
    background: var(--deployment-panel-2);
  }

  .deployment-preview-safety-grid dt {
    color: var(--deployment-dim);
    font-size: 9px;
    font-weight: 800;
    letter-spacing: 0.1em;
    text-transform: uppercase;
  }

  .deployment-preview-safety-grid dd {
    display: grid;
    gap: 5px;
    margin: 6px 0 0;
    overflow-wrap: anywhere;
  }

  .deployment-preview-safety-grid dd strong {
    color: var(--deployment-text);
    font-size: 11px;
    line-height: 1.3;
  }

  .deployment-preview-safety-grid dd small {
    color: var(--deployment-muted);
    font-size: 10px;
    line-height: 1.4;
  }

  .deployment-preview-risk {
    color: var(--deployment-yellow) !important;
  }

  .deployment-preview-rollback {
    color: var(--deployment-green) !important;
  }

  .deployment-preview-plan-id {
    color: var(--deployment-blue);
    overflow-wrap: anywhere;
    font-size: 10px;
  }

  .deployment-preview-junction-targets {
    margin-top: 0;
    border-top: 1px solid var(--deployment-border);
    padding-top: 9px;
  }

  .deployment-preview-junction-targets summary {
    color: var(--deployment-muted);
    cursor: pointer;
    font-size: 10px;
    font-weight: 700;
  }

  .deployment-preview-junction-targets ul {
    display: grid;
    gap: 4px;
    margin: 8px 0 0;
    padding: 0;
    list-style: none;
  }

  .deployment-preview-junction-targets li {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 8px;
    padding: 6px 8px;
    border: 1px solid var(--deployment-border);
    border-radius: 4px;
    background: var(--deployment-base);
  }

  .deployment-preview-junction-targets li strong {
    min-width: 0;
    overflow-wrap: anywhere;
    color: var(--deployment-text);
    font-size: 11px;
  }

  .deployment-preview-junction-targets li span {
    flex: 0 0 auto;
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-actions {
    margin-top: 11px;
  }

  .deployment-preview-action-bar,
  .deployment-apply-confirmation {
    gap: 12px;
    padding: 11px 12px;
    border: 1px solid rgba(138, 180, 248, 0.3);
    border-radius: 5px;
    background: var(--deployment-navigation);
  }

  .deployment-preview-action-bar p,
  .deployment-apply-confirmation p {
    color: var(--deployment-muted);
    font-size: 11px;
    line-height: 1.45;
  }

  .deployment-preview-action-bar p strong {
    color: var(--deployment-text);
  }

  .deployment-preview-success {
    border-color: rgba(129, 201, 149, 0.38);
    border-left: 3px solid var(--deployment-green);
    background: rgba(53, 93, 65, 0.28);
  }

  .deployment-preview-success p strong {
    color: var(--deployment-green);
  }

  .deployment-preview-blocked {
    border-color: rgba(253, 214, 99, 0.4);
    border-left: 3px solid var(--deployment-yellow);
    background: rgba(101, 82, 34, 0.28);
  }

  .deployment-preview-blocked p strong {
    color: var(--deployment-yellow);
  }

  .deployment-apply-confirmation {
    display: grid;
    justify-content: stretch;
    gap: 9px;
    border-color: rgba(253, 214, 99, 0.5);
    border-left: 3px solid var(--deployment-yellow);
    background: #342f24;
  }

  .deployment-apply-confirmation h2 {
    margin: 0;
    color: var(--deployment-text);
    font-size: 16px;
    line-height: 1.2;
  }

  .deployment-apply-confirmation code {
    color: #f4df9a;
  }

  .deployment-preview-confirm-summary {
    display: flex;
    flex-wrap: wrap;
    gap: 5px 10px;
    color: var(--deployment-yellow);
    font-size: 10px;
  }

  .deployment-apply-confirmation .action-row {
    justify-content: flex-end;
    gap: 6px;
    margin-top: 2px;
  }

  .deployment-preview-surface .primary-button,
  .deployment-preview-surface .secondary-button {
    min-height: 31px;
    padding: 7px 11px;
    border-radius: 4px;
    font-size: 10px;
  }

  .deployment-preview-surface .primary-button {
    border-color: var(--deployment-blue);
    background: var(--deployment-blue);
    color: #17283e;
  }

  .deployment-preview-surface .secondary-button {
    border-color: var(--deployment-border);
    background: #3a3b3f;
    color: var(--deployment-text);
  }

  .deployment-preview-surface button:focus-visible,
  .deployment-preview-surface input:focus-visible,
  .deployment-preview-surface summary:focus-visible {
    outline: 2px solid var(--deployment-blue);
    outline-offset: 2px;
  }

  .deployment-preview-search {
    gap: 5px;
    margin-top: 13px;
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-search span {
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .deployment-preview-search input {
    height: 33px;
    padding: 7px 10px;
    border: 1px solid var(--deployment-border);
    border-radius: 4px;
    background: var(--deployment-base);
    color: var(--deployment-text);
    font-size: 11px;
  }

  .deployment-preview-search input::placeholder {
    color: var(--deployment-dim);
  }

  .deployment-preview-sections {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 7px;
    margin-top: 9px;
    padding-bottom: 20px;
  }

  .deployment-preview-section {
    min-width: 0;
    overflow: hidden;
    border: 1px solid var(--deployment-border);
    border-radius: 5px;
    background: var(--deployment-panel);
  }

  .deployment-preview-section:nth-child(3) {
    grid-column: 1 / -1;
  }

  .deployment-preview-section-toggle {
    min-height: 37px;
    padding: 9px 11px;
    color: var(--deployment-text);
    font-size: 10px;
    letter-spacing: 0.09em;
  }

  .deployment-preview-section-toggle:hover {
    background: #3a3b3f;
  }

  .deployment-preview-section-toggle span:last-child {
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-detail-list {
    max-height: min(42vh, 430px);
    gap: 4px;
    padding: 0 8px 8px;
    scrollbar-color: rgba(111, 116, 122, 0.85) transparent;
  }

  .deployment-preview-detail-row {
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 3px 10px;
    padding: 8px 9px;
    border: 1px solid rgba(60, 64, 67, 0.9);
    border-radius: 4px;
    background: var(--deployment-base);
  }

  .deployment-preview-detail-row strong {
    color: var(--deployment-text);
    font-size: 11px;
    font-weight: 650;
  }

  .deployment-preview-detail-row span {
    color: #c5c9ce;
    font-size: 10px;
  }

  .deployment-preview-detail-row small {
    color: var(--deployment-muted);
    font-size: 10px;
    line-height: 1.35;
  }

  .deployment-preview-diagnostic-row {
    grid-template-columns: minmax(0, auto) minmax(0, auto);
  }

  .deployment-preview-diagnostic-row small {
    grid-column: 1 / -1;
  }

  .deployment-preview-diagnostic-severity {
    color: var(--deployment-yellow) !important;
    text-align: right !important;
  }

  .deployment-preview-surface .empty-state,
  .deployment-preview-surface .subtle {
    color: var(--deployment-muted);
    font-size: 10px;
  }

  .deployment-preview-surface .empty-state {
    margin: 4px 2px 2px;
  }

  @media (max-width: 940px) {
    .deployment-preview-safety-grid {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }

  @media (max-width: 720px) {
    .deployment-preview-surface {
      padding: 16px 14px 22px;
    }

    .deployment-preview-header,
    .deployment-preview-action-bar {
      align-items: flex-start;
      flex-direction: column;
    }

    .deployment-preview-header > .status-chip {
      margin-top: 0;
    }

    .deployment-preview-summary {
      grid-template-columns: 1fr;
    }

    .deployment-preview-action-bar .primary-button,
    .deployment-apply-confirmation .action-row {
      width: 100%;
    }

    .deployment-preview-action-bar .primary-button {
      text-align: center;
    }

    .deployment-preview-sections {
      grid-template-columns: 1fr;
    }

    .deployment-preview-section:nth-child(3) {
      grid-column: auto;
    }
  }

  @media (max-width: 520px) {
    .deployment-preview-safety-grid {
      grid-template-columns: 1fr;
    }

    .deployment-preview-junction-targets li {
      align-items: flex-start;
      flex-direction: column;
      gap: 3px;
    }

    .deployment-apply-confirmation .action-row {
      align-items: stretch;
      flex-direction: column-reverse;
    }
  }
</style>
