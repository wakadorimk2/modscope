<script lang="ts">
  import type {
    BridgeErrorPayload,
    DiagnosticUiState,
    InspectorUiState,
    LocalContextUiState,
    ModCandidateUiState,
    RuntimeEvidenceComparisonItemUiState,
    SemanticConflictGroupUiState,
    UiState
  } from '../contracts';

  import type { ContextMode } from './ui-types';
  import { resolveModWebsite } from '../mod-links';
  import EvidenceInspector from './EvidenceInspector.svelte';

  export let state: UiState;
  export let mode: ContextMode = 'context';
  export let inspectorOpen = false;
  export let inspector: InspectorUiState | null = null;
  export let inspectorCandidate: ModCandidateUiState | null = null;
  export let inspectorConflictGroups: SemanticConflictGroupUiState[] = [];
  export let inspectorRuntimeItems: RuntimeEvidenceComparisonItemUiState[] = [];
  export let operationBlocksInteraction = false;
  export let error: BridgeErrorPayload | null = null;
  export let pageDetailsOpen = false;
  export let developerToolsOpen = false;
  export let runtimeToolVersion = '';
  export let runtimeGameVersion = '';
  export let modSearchOpen = false;
  export let modSearchMode: 'browse' | 'recognition' = 'browse';
  export let modSearchQuery = '';
  export let modSearchResults: ModCandidateUiState[] = [];
  export let onSetContextMode: (mode: ContextMode) => void;
  export let onDiscoverSources: () => void;
  export let onSelectRoot: () => void;
  export let onSelectSource: (candidateId: string) => void;
  export let onUseFixture: () => void;
  export let onSelectEvidenceManifest: () => void;
  export let onObserve: () => void;
  export let onOpenAnalysis: () => void;
  export let onToggleInspector: () => void;
  export let onOpenModSearch: (mode?: 'browse' | 'recognition') => void;
  export let onCloseModSearch: () => void;
  export let onOpenModPage: (candidate: ModCandidateUiState) => void;
  export let onChooseModForRecognition: (candidate: ModCandidateUiState) => void;
  export let onConfirmIdentity: (localModKey: string | null) => void;
  export let onStartStaticAnalysis: () => void;
  export let onSelectBaseData: () => void;
  export let onSelectRuntimeLogs: () => void;
  export let onAnalyzeConflicts: () => void;
  export let onCompareRuntimeEvidence: () => void;
  export let onUseAnalysisFixture: () => void;
  export let onOpenInspectorForMod: (modKey: string) => void;
  export let onSetWebVersionObservation: () => void;
  export let onObserveNexusFileVersion: () => void;
  export let inspectorFilesOpen = false;
  export let webObservedVersion = '';

  type ContextCell = {
    label: 'Installed' | 'Enabled' | 'Version' | 'Profile';
    value: string;
    evidence: 'Observed' | 'Unknown';
    tone: 'positive' | 'unknown' | 'neutral';
  };

  $: analysisBusy = state.analysis.operation.isBusy;
  $: analysisGroups = state.analysis.conflict?.groups ?? [];
  $: candidateAnalysisGroups = state.analysis.conflict && state.localContext?.localModKey
    ? state.analysis.conflict.groups.filter((group) => group.operations.some((operation) => operation.modKey === state.localContext?.localModKey))
    : [];
  $: hasConclusion = state.localContext?.status === 'installed' || state.localContext?.status === 'notInstalled';
  $: localContextCells = state.localContext ? buildContextCells(state.localContext) : [];
  $: localContextReviewItems = state.localContext ? buildReviewCueItems(state.localContext) : [];
  $: localContextCandidate = state.localContext?.localModKey
    ? state.knowledge.candidates.find((candidate) => candidate.modKey === state.localContext?.localModKey) ?? null
    : null;
  $: localContextPageUrl = localContextCandidate ? resolveModWebsite(localContextCandidate).url : null;
  $: canOpenLocalInspector = state.localContext?.status === 'installed' && Boolean(state.localContext?.localModKey);

  type DiagnosticGroup = { diagnostic: DiagnosticUiState; count: number };

  function groupDiagnostics(diagnostics: DiagnosticUiState[]): DiagnosticGroup[] {
    const groups = new Map<string, DiagnosticGroup>();
    for (const diagnostic of diagnostics) {
      const key = [diagnostic.code, diagnostic.severity, diagnostic.message, diagnostic.rawValue ?? ''].join('\u0000');
      const existing = groups.get(key);
      if (existing) existing.count += 1;
      else groups.set(key, { diagnostic, count: 1 });
    }
    return Array.from(groups.values());
  }

  function diagnosticClass(severity: string): string {
    return `diagnostic diagnostic-${severity.toLowerCase()}`;
  }

  function formatLabel(value: string | null | undefined): string {
    if (!value) return 'Unknown';
    return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/-/g, ' ').replace(/:/g, ' · ').replace(/^./, (character) => character.toUpperCase());
  }

  function normalizedValue(value: string | null | undefined): string {
    return (value ?? '').toLowerCase().replace(/[^a-z]+/g, '');
  }

  function resolvedText(value: string | null | undefined): string {
    const trimmed = value?.trim() ?? '';
    return ['unknown', 'unresolved', 'missing', 'notassessed'].includes(normalizedValue(trimmed)) ? '' : trimmed;
  }

  function buildContextCells(context: LocalContextUiState): ContextCell[] {
    const installed = normalizedValue(context.status);
    const enabled = normalizedValue(context.enabledState);
    const knownVersion = resolvedText(context.knownVersion);
    const profileName = resolvedText(context.profileName);
    const installedResolved = installed === 'installed' || installed === 'notinstalled';
    const enabledResolved = enabled === 'enabled' || enabled === 'disabled';

    return [
      {
        label: 'Installed',
        value: installed === 'installed' ? 'Yes' : installed === 'notinstalled' ? 'No' : 'Unknown',
        evidence: installedResolved ? 'Observed' : 'Unknown',
        tone: installed === 'installed' ? 'positive' : installedResolved ? 'neutral' : 'unknown'
      },
      {
        label: 'Enabled',
        value: enabled === 'enabled' ? 'Yes' : enabled === 'disabled' ? 'No' : 'Unknown',
        evidence: enabledResolved ? 'Observed' : 'Unknown',
        tone: enabled === 'enabled' ? 'positive' : enabledResolved ? 'neutral' : 'unknown'
      },
      {
        label: 'Version',
        value: knownVersion || 'Unknown',
        evidence: knownVersion ? 'Observed' : 'Unknown',
        tone: knownVersion ? 'neutral' : 'unknown'
      },
      {
        label: 'Profile',
        value: profileName || 'Unknown',
        evidence: profileName ? 'Observed' : 'Unknown',
        tone: profileName ? 'neutral' : 'unknown'
      }
    ];
  }

  function buildReviewCueItems(context: LocalContextUiState): string[] {
    return normalizedValue(context.status) === 'installed' && !resolvedText(context.knownVersion)
      ? ['Installed version has no confirmed source observation.']
      : [];
  }

  function openLocalModPage() {
    if (!localContextCandidate || !localContextPageUrl) return;
    onOpenModPage(localContextCandidate);
  }

  function contextConclusionLabel(status: string | null | undefined): string {
    const normalized = (status ?? '').toLowerCase().replace(/[^a-z]+/g, '');
    if (normalized === 'installed') return 'Installed in active profile';
    if (normalized === 'notinstalled') return 'Not installed in active profile';
    return formatLabel(status);
  }

  function recognitionStrengthClass(strength: string | null | undefined): string {
    return (strength ?? '').toLowerCase().replace(/[^a-z]+/g, '') === 'strong' ? 'status-ready' : '';
  }

  function statusClass(status: string | undefined): string {
    return 'status-' + (status ?? 'unknown').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function analysisLabel(value: string | null | undefined): string {
    const normalized = (value ?? '').toLowerCase().replace(/[^a-z]+/g, '');
    if (normalized === 'match') return 'Match';
    if (normalized === 'different' || normalized === 'conflict') return 'Different';
    if (normalized === 'possible') return 'Possible';
    if (normalized === 'notassessed' || normalized === 'staticonly' || normalized === 'runtimeonly') return 'Not assessed';
    if (normalized.includes('inferred')) return 'Inferred';
    if (!normalized || normalized === 'unknown') return 'Unknown';
    return formatLabel(value);
  }

  function analysisStatusClass(value: string | null | undefined): string {
    return 'analysis-status-' + (value ?? 'unknown').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function analysisSummaryStatus(): string {
    if (analysisBusy) return 'Running';
    if (state.analysis.diagnostics.some((diagnostic) => diagnostic.severity.toLowerCase() === 'error')) return 'Issue';
    return state.analysis.conflict ? 'Assessed' : 'Not assessed';
  }

  function analysisSummaryStatusClass(): string {
    if (analysisBusy) return 'analysis-status-possible';
    if (state.analysis.diagnostics.some((diagnostic) => diagnostic.severity.toLowerCase() === 'error')) return 'analysis-status-different';
    return state.analysis.conflict ? 'analysis-status-ready' : 'analysis-status-not-assessed';
  }

  function analysisOperationLabel(): string {
    return state.analysis.operation.kind === 'conflict-analysis'
      ? 'Analyzing static XML conflicts'
      : state.analysis.operation.kind === 'runtime-comparison'
        ? 'Comparing runtime evidence'
        : 'Analysis idle';
  }

  function baseDataStatusLabel(): string {
    switch (state.analysis.inputs.baseDataStatus) {
      case 'inferred': return 'MO2 gamePathからData\\Configを検出済み';
      case 'manual': return '別のData\\Configを選択済み';
      default: return 'Data\\Configが見つかりません';
    }
  }

  function sizeLabel(size: number): string {
    return size < 1024 ? `${size} B` : `${(size / 1024).toFixed(1)} KB`;
  }

  function internalPageTitle(page: string | null | undefined): string {
    switch (page) {
      case 'history': return 'History';
      case 'deployment-preview': return 'Deployment preview';
      case 'home': return 'Browse Home';
      default: return 'Internal page';
    }
  }

  function internalPageDescription(page: string | null | undefined): string {
    switch (page) {
      case 'history': return 'History is an internal page. Page recognition is not active here.';
      case 'deployment-preview': return 'Deployment preview is an internal page. Page recognition is paused here.';
      case 'home': return 'Browse Home is an internal page. Open an external MOD page to recognize it.';
      default: return 'Page recognition is not active on this internal page.';
    }
  }
</script>

{#if error}<p class="error-notice"><strong>{error.code}</strong> {error.message}</p>{/if}

{#if mode === 'context' && state.browser.internalPage}
  <section class="panel context-summary-panel internal-page-context" aria-labelledby="internal-page-title">
    <div class="recognize-header">
      <div>
        <span class="eyebrow">INTERNAL PAGE</span>
        <h2 id="internal-page-title">{internalPageTitle(state.browser.internalPage)}</h2>
      </div>
    </div>
    <p class="subtle">{internalPageDescription(state.browser.internalPage)}</p>
    {#if state.knowledge.session}
      <p class="notice">The previous Web page context is cleared. Return to an external Web page to start recognition again.</p>
    {:else}
      <p class="notice">No local profile is loaded. History and other internal pages do not use page recognition.</p>
    {/if}
  </section>
{:else if mode === 'settings' || !state.knowledge.session}
  <section class="panel source-discovery-panel">
    <div class="summary-header"><div><span class="eyebrow">{state.knowledge.session ? 'SETTINGS' : 'ONBOARDING'}</span><h2>{state.knowledge.session ? 'MO2 source' : 'Choose a local source'}</h2><p class="summary-meta">ModScope checks known MO2 locations and keeps this read-only.</p></div><span class="muted-badge">No absolute paths sent to Web</span></div>
    {#if state.knowledge.session}<p class="source-status-line">Active source · {state.knowledge.session.instanceName || 'Unknown instance'} · {state.knowledge.session.profileName || 'Profile unknown'}</p><div class="action-row"><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onDiscoverSources}>Reload source discovery</button><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onSelectRoot}>Change MO2 source</button></div>{/if}
    {#if state.knowledge.operation.isBusy && !state.knowledge.session}
      <p class="notice" role="status">Local MO2 knowledge is loading. Browser remains available while ModScope checks the source.</p>
    {:else if state.sourceDiscovery.candidates.length === 0}
      <p class="notice">No MO2 source is ready. Scan again or choose an MO2 instance folder.</p>
    {:else}
      <div class="source-candidate-list">
        {#each state.sourceDiscovery.candidates as candidate (candidate.candidateId)}
          <article class="source-candidate-card"><div class="source-candidate-header"><div><strong>{candidate.instanceName || 'Unknown instance'} · {candidate.profileName || 'Profile unknown'}</strong><p class="subtle">{candidate.gameName || 'Game unknown'}</p></div><span class="status-chip {statusClass(candidate.readiness)}">{formatLabel(candidate.readiness)}</span></div>
            {#if candidate.evidence.length > 0}<div class="evidence-strip">{#each candidate.evidence as evidence}<span class="evidence-tag">{formatLabel(evidence)}</span>{/each}</div>{/if}
            {#if candidate.diagnostics.length > 0}<div class="diagnostic-list">{#each groupDiagnostics(candidate.diagnostics) as group}<p class={diagnosticClass(group.diagnostic.severity)}><strong>{group.diagnostic.code}</strong>{#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}{group.diagnostic.message}</p>{/each}</div>{/if}
            {#if candidate.isReady}<button class="primary-button action-button" disabled={operationBlocksInteraction} onclick={() => onSelectSource(candidate.candidateId)}>Use this source</button>{/if}
          </article>
        {/each}
      </div>
    {/if}
    <div class="action-row"><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onDiscoverSources}>Scan again</button><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onSelectRoot}>Select MO2 folder</button></div>
  </section>
{:else if mode === 'context' && state.knowledge.session}
  <section class="panel context-summary-panel" aria-labelledby="recognize-title">
    <div class="recognize-header"><div><span class="eyebrow">RECOGNIZE</span>{#if hasConclusion && state.localContext}<h2 id="recognize-title">{contextConclusionLabel(state.localContext.status)}</h2><p class="summary-meta">{state.localContext.candidateIdentity || state.identity.candidateIdentity || state.observation?.title || 'Current page'}</p>{:else if state.observation}<h2 id="recognize-title">Couldn’t recognize this page</h2>{:else}<h2 id="recognize-title">Browse a MOD page</h2>{/if}</div><button class="analysis-lamp" class:analysis-lamp-issue={analysisSummaryStatusClass() === 'analysis-status-different'} class:analysis-lamp-ready={analysisSummaryStatusClass() === 'analysis-status-ready'} disabled={operationBlocksInteraction} title={`Open analysis · ${analysisSummaryStatus()}`} aria-label={`Open analysis · ${analysisSummaryStatus()}`} onclick={onOpenAnalysis}><span class="analysis-lamp-dot" aria-hidden="true"></span><span>{analysisSummaryStatus()}</span></button></div>
    {#if operationBlocksInteraction}
      <div class="local-skeleton-panel recognition-skeleton-panel" aria-busy="true">
        <p class="subtle local-skeleton-status" role="status">{analysisBusy ? 'Analysis is running…' : 'Loading local profile…'}</p>
        <div class="local-skeleton-stack" aria-hidden="true">
          <span class="local-skeleton local-skeleton-card"></span>
          <span class="local-skeleton local-skeleton-card local-skeleton-card-short"></span>
          <div class="local-skeleton-actions">
            <span class="local-skeleton local-skeleton-action"></span>
            <span class="local-skeleton local-skeleton-action local-skeleton-action-secondary"></span>
          </div>
        </div>
      </div>
    {:else if hasConclusion && state.localContext}
      <div class="context-reference-grid" aria-label="Local MOD context">
        {#each localContextCells as cell}
          <div
            class="context-reference-cell"
            class:context-reference-cell-version={cell.label === 'Version'}
            class:context-reference-cell-version-review={cell.label === 'Version' && localContextReviewItems.length > 0}
            id={cell.label === 'Version' ? 'recognize-version' : undefined}
            aria-describedby={cell.label === 'Version' && localContextReviewItems.length > 0 ? 'recognize-review-cue' : undefined}
          >
            <span class="context-reference-label">{cell.label}</span>
            <strong class:context-reference-value-positive={cell.tone === 'positive'} class:context-reference-value-unknown={cell.tone === 'unknown'}>{cell.value}</strong>
            <span class="context-reference-evidence context-reference-evidence-{cell.evidence.toLowerCase()}" aria-label={`Evidence: ${cell.evidence}`}><i aria-hidden="true"></i>{cell.evidence}</span>
          </div>
        {/each}
      </div>
      {#if localContextReviewItems.length > 0}
        <aside id="recognize-review-cue" class="context-reference-review" aria-label="Needs review">
          <strong>Needs review</strong>
          <ul>
            {#each localContextReviewItems as item}<li>{item}</li>{/each}
          </ul>
        </aside>
      {/if}
      {#if canOpenLocalInspector || inspectorOpen || (localContextCandidate && localContextPageUrl)}
        <div class="context-reference-actions">
          {#if canOpenLocalInspector || inspectorOpen}<button class="primary-button context-reference-action" type="button" disabled={operationBlocksInteraction} aria-expanded={inspectorOpen} aria-controls="inspector-inline-panel" onclick={onToggleInspector}>{inspectorOpen ? 'Close Inspector' : 'Inspect'}</button>{/if}
          {#if localContextCandidate && localContextPageUrl}<button class="secondary-button context-reference-action" type="button" disabled={operationBlocksInteraction} onclick={openLocalModPage}>Open page</button>{/if}
        </div>
      {/if}
    {:else if state.observation}
      <p class="subtle">Identity confirmation is required. Choose a local MOD or mark this page as not installed.</p>
      {#if state.identity.matches.length > 0}
        <div class="recognition-candidate-list" aria-label="Local MOD recognition candidates">
          {#each state.identity.matches.slice(0, 6) as match}
            <article class="recognition-candidate">
              <button
                type="button"
                class="recognition-candidate-action"
                disabled={operationBlocksInteraction}
                aria-label={`Use ${match.displayName || match.directoryName || match.modKey} as the local MOD`}
                onclick={() => onConfirmIdentity(match.modKey)}
              >
                <span class="recognition-candidate-heading">
                  <strong>{match.displayName || match.directoryName || match.modKey}</strong>
                  <span class="status-chip {recognitionStrengthClass(match.strength)}">{formatLabel(match.strength)}</span>
                </span>
                <span class="analysis-meta">Match evidence · {match.evidence}</span>
                <span class="recognition-candidate-action-label">Use this MOD</span>
              </button>
            </article>
          {/each}
        </div>
      {/if}
      {#if state.knowledge.candidates.length > 0}<p class="subtle">Search the local MOD catalog to confirm the page identity.</p><div class="action-row"><button class="primary-button" type="button" disabled={operationBlocksInteraction} onclick={() => onOpenModSearch('recognition')}>Search local MODs</button><button class="secondary-button" type="button" disabled={operationBlocksInteraction} onclick={() => onConfirmIdentity(null)}>Mark as not installed</button></div>{:else}<p class="notice">No local MOD candidates are loaded. Open Debug to load a profile.</p><div class="action-row"><button class="secondary-button" type="button" onclick={() => onSetContextMode('debug')}>Open Debug</button><button class="secondary-button" type="button" onclick={() => onConfirmIdentity(null)}>Mark as not installed</button></div>{/if}
    {:else}<p class="subtle">ModScope will observe the current page and show local context here.</p>{/if}
  </section>
{/if}

{#if mode === 'context' && state.knowledge.session && (inspectorOpen || inspector)}
  <div id="inspector-inline-panel" class="inspector-inline-region" hidden={!inspectorOpen} aria-hidden={inspectorOpen ? 'false' : 'true'}>
    <EvidenceInspector
      {state}
      {inspector}
      {inspectorCandidate}
      {inspectorConflictGroups}
      {inspectorRuntimeItems}
      {operationBlocksInteraction}
      bind:inspectorFilesOpen
      bind:webObservedVersion
      onClose={onToggleInspector}
      onSetWebVersionObservation={onSetWebVersionObservation}
      onObserveNexusFileVersion={onObserveNexusFileVersion}
      onStartStaticAnalysis={onStartStaticAnalysis}
    />
  </div>
{/if}

{#if mode === 'analysis' && state.knowledge.session}
  <section class="panel analysis-panel" aria-labelledby="analysis-title">
    <div class="summary-header"><div><span class="eyebrow">ANALYSIS</span><h2 id="analysis-title">Compare &amp; Diagnose</h2><p class="summary-meta">Static evidence and runtime evidence stay separate.</p></div><span class="status-chip {analysisSummaryStatusClass()}" role="status">{analysisSummaryStatus()}</span></div>
    <div class="analysis-input-status" aria-label="Analysis input status"><span class="status-chip {state.analysis.inputs.baseDataReady ? 'status-ready' : 'status-unknown'}">Base Data/Config · {state.analysis.inputs.baseDataReady ? 'Ready' : 'Not selected'}</span><span class="status-chip {state.analysis.inputs.runtimeLogsReady ? 'status-ready' : 'status-unknown'}">Runtime logs · {state.analysis.inputs.runtimeLogsReady ? 'Ready' : 'Not selected'}</span>{#if analysisBusy}<span class="subtle" role="status">{analysisOperationLabel()}…</span>{/if}</div>
    {#if operationBlocksInteraction}
      <div class="local-skeleton-panel analysis-skeleton-panel" aria-busy="true">
        <p class="subtle local-skeleton-status" role="status">{analysisBusy ? `${analysisOperationLabel()}…` : 'Loading local analysis data…'}</p>
        <div class="local-skeleton-stack" aria-hidden="true">
          <span class="local-skeleton local-skeleton-action"></span>
          <span class="local-skeleton local-skeleton-analysis-card"></span>
          <span class="local-skeleton local-skeleton-analysis-card local-skeleton-card-short"></span>
          <span class="local-skeleton local-skeleton-analysis-card"></span>
          <span class="local-skeleton local-skeleton-analysis-card local-skeleton-card-short"></span>
        </div>
      </div>
    {:else}
      <div class="analysis-summary-actions"><button class="primary-button" onclick={onStartStaticAnalysis}>{state.analysis.inputs.baseDataReady ? (state.analysis.conflict ? 'Re-run static analysis' : 'Analyze static') : '別のData\\Configを選択'}</button><span class="subtle">Static XML analysis uses the selected base Data/Config folder.</span></div>
      {#if state.analysis.diagnostics.length > 0}<div class="diagnostic-list">{#each state.analysis.diagnostics as diagnostic}<p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}</div>{/if}

      <details class="analysis-section" open><summary><span>Compare</span><span class="subtle">Confirmed candidate MOD</span></summary>
      {#if !state.analysis.conflict}<p class="notice">未確認。静的解析を実行してください。</p>{:else if !state.localContext?.localModKey}<p class="notice">確認済みcandidate MODがありません。Compareは未確認です。</p>{:else if candidateAnalysisGroups.length === 0}<p class="notice">確認済みcandidate MODに関係する評価がありません。競合なしとは判定していません。</p>{:else}<div class="analysis-card-list">{#each candidateAnalysisGroups as group}<article class="analysis-card"><div class="analysis-card-heading"><div><strong>{group.targetXml || 'Target XML unknown'}</strong><code>{group.xPath || 'XPath unknown'}</code></div><div class="analysis-badges"><span class="status-chip {analysisStatusClass(group.assessment)}">{analysisLabel(group.assessment)}</span><span class="status-chip {analysisStatusClass(group.confidence)}">Confidence · {analysisLabel(group.confidence)}</span></div></div><p class="analysis-meta">Effective status · {analysisLabel(group.effectiveStatus)}</p><div class="evidence-card static-evidence-card"><span class="eyebrow">STATIC EVIDENCE</span><ol class="operation-sequence">{#each group.operations as operation, index}<li><div class="operation-heading"><strong>{index + 1}. {operation.modKey}</strong><span>Priority {operation.priority ?? 'Unknown'}</span></div><div class="analysis-meta">{operation.xmlFileRelativePath} · {operation.elementPath} · {operation.rawOperationName}</div><p class="provenance-line">Source · {operation.source.kind} · {operation.source.relativePath}</p></li>{/each}</ol></div></article>{/each}</div>{/if}
      </details>

      <details class="analysis-section" open><summary><span>Diagnosis</span><span class="subtle">Active profile · {state.knowledge.session.profileName}</span></summary>{#if !state.analysis.conflict}<p class="notice">未確認。active profile全体のDiagnosisは解析後に表示します。</p>{:else if analysisGroups.length === 0}<p class="notice">解析結果の評価groupがありません。評価は未確認です。</p>{:else}<div class="diagnosis-list">{#each analysisGroups as group}<article class="diagnosis-row"><div><strong>{group.targetXml || 'Target XML unknown'}</strong><code>{group.xPath || 'XPath unknown'}</code></div><div class="analysis-badges"><span class="status-chip {analysisStatusClass(group.assessment)}">{analysisLabel(group.assessment)}</span><span class="status-chip {analysisStatusClass(group.confidence)}">{analysisLabel(group.confidence)}</span></div><p class="analysis-meta">{group.operations.length} operations · {group.operations.map((operation) => `${operation.modKey} (${operation.priority ?? 'Unknown'})`).join(' / ')}</p></article>{/each}</div>{/if}</details>

      <details class="analysis-section"><summary><span>Static evidence</span><span class="subtle">Base files · {state.analysis.conflict?.baseFiles.length ?? 0}</span></summary>{#if state.analysis.conflict}{#each state.analysis.conflict.baseFiles as file}<article class="evidence-row"><div><strong>{file.targetXml}</strong><p class="analysis-meta">{sizeLabel(file.size)} · SHA-256 {file.sha256}</p></div><span class="status-chip {statusClass(file.parseStatus || 'unknown')}">{formatLabel(file.parseStatus)}</span><p class="provenance-line">Source · {file.source.kind} · {file.source.relativePath}</p></article>{/each}{:else}<p class="notice">未確認。静的解析を実行してください。</p>{/if}</details>

      <details class="analysis-section"><summary><span>Runtime evidence</span><span class="subtle">RuntimeOCD comparison</span></summary>{#if !state.analysis.runtimeComparison}<p class="notice">未確認。runtime logを選択して比較を実行してください。</p>{:else}{@const runtimeEvidence = state.analysis.runtimeComparison.runtimeEvidence}<div class="evidence-card runtime-evidence-card"><span class="eyebrow">RUNTIME EVIDENCE</span><div class="summary-grid"><div><span>Tool</span><strong>{runtimeEvidence.toolName}</strong></div><div><span>Tool version</span><strong>{runtimeEvidence.toolVersion || 'Unknown'}</strong></div><div><span>Game version</span><strong>{runtimeEvidence.gameVersion || 'Unknown'}</strong></div><div><span>Captured</span><strong>{runtimeEvidence.capturedAtUtc}</strong></div></div>{#each state.analysis.runtimeComparison.items as item}<article class="runtime-comparison-row"><div class="analysis-card-heading"><div><strong>{item.targetXml || 'Target XML unknown'}</strong><code>{item.xPath || 'XPath unknown'}</code></div><span class="status-chip {analysisStatusClass(item.status)}">{analysisLabel(item.status)}</span></div>{#each item.observations as observation}<p class="analysis-meta">{observation.modKey || 'MOD unknown'} · {observation.observedOperation || 'Operation unknown'} · {analysisLabel(observation.normalizedAssessment)}</p>{/each}</article>{/each}</div>{/if}</details>
    {/if}
  </section>
{/if}

{#if mode === 'debug' && state.knowledge.session && state.diagnostics.length > 0}
  {@const diagnosticGroups = groupDiagnostics(state.diagnostics)}
  <section class="panel diagnostics-panel"><span class="eyebrow">DIAGNOSTICS</span><p class="diagnostic-summary">{diagnosticGroups.length} types · {state.diagnostics.length} occurrences</p>{#each diagnosticGroups as group}<p class={diagnosticClass(group.diagnostic.severity)}><strong>{group.diagnostic.code}</strong>{#if group.count > 1}<span class="diagnostic-count">× {group.count}</span>{/if}{group.diagnostic.message}</p>{/each}</section>
{/if}

{#if mode === 'debug' && state.observation}
  <details class="panel page-details" bind:open={pageDetailsOpen}><summary><span>Page details</span><span class="status-chip {statusClass(state.observation.extractionStatus)}">{formatLabel(state.observation.extractionStatus)}</span></summary><div class="page-details-grid"><div><span>Title</span><strong>{state.observation.title || 'Untitled page'}</strong></div><div><span>Observed</span><strong>{state.observation.observedAtUtc}</strong></div></div><p class="subtle">Page body stays in the Desktop session and is not sent to Web state.</p>{#each state.observation.diagnostics as diagnostic}<p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}</details>
{/if}

{#if mode === 'debug'}
  <details class="panel developer-tools" bind:open={developerToolsOpen}><summary><span><span class="eyebrow">DEVELOPER</span><strong>Developer tools</strong></span><span class="muted-badge">Read-only</span></summary>
    <div class="developer-actions"><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onUseFixture}>Use fixture</button><button class="secondary-button" disabled={operationBlocksInteraction || !state.knowledge.session} onclick={onSelectEvidenceManifest}>Select version manifest</button><button class="primary-button" disabled={operationBlocksInteraction} onclick={onSelectRoot}>Select MO2 source</button><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onObserve}>Observe now</button></div>
    <div class="analysis-developer-tools"><div class="analysis-tool-header"><div><span class="eyebrow">PHASE6 ANALYSIS</span><strong>Static and runtime evidence inputs</strong></div><div class="analysis-badges"><span class="status-chip {state.analysis.inputs.baseDataReady ? 'status-ready' : 'status-unknown'}">{baseDataStatusLabel()}</span><span class="status-chip {state.analysis.inputs.runtimeLogsReady ? 'status-ready' : 'status-unknown'}">Logs {state.analysis.inputs.runtimeLogsReady ? 'ready' : 'missing'}</span></div></div><div class="developer-actions"><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onSelectBaseData}>別のData\Configを選択</button><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onSelectRuntimeLogs}>Select runtime logs</button><button class="primary-button" disabled={operationBlocksInteraction || !state.analysis.inputs.baseDataReady} onclick={onAnalyzeConflicts}>Analyze conflicts</button><button class="primary-button" disabled={operationBlocksInteraction || !state.analysis.inputs.baseDataReady || !state.analysis.inputs.runtimeLogsReady} onclick={onCompareRuntimeEvidence}>Compare runtime</button><button class="secondary-button" disabled={operationBlocksInteraction} onclick={onUseAnalysisFixture}>Use Phase6 fixture</button></div><div class="source-grid analysis-version-grid"><label>Tool version<input bind:value={runtimeToolVersion} placeholder="Unknown" disabled={analysisBusy} /></label><label>Game version<input bind:value={runtimeGameVersion} placeholder="Unknown" disabled={analysisBusy} /></label></div><p class="subtle developer-status">Paths stay in the Desktop session. Runtime log bodies and raw results stay out of Web state.</p></div>
    {#if state.knowledge.session}<p class="subtle developer-status">{state.knowledge.session.instanceName} / {state.knowledge.session.profileName} · {state.knowledge.candidates.length} MOD records · {state.knowledge.profiles.length} profiles</p>{/if}<p class="subtle developer-status">{state.statusMessage}</p>
  </details>
{/if}

{#if modSearchOpen}
  <button type="button" class="drawer-backdrop" aria-label="Close MOD search" onclick={onCloseModSearch}></button>
  <aside class="mod-search-drawer" aria-labelledby="mod-search-title"><div class="drawer-heading"><div><span class="eyebrow">MOD CATALOG</span><h2 id="mod-search-title">{modSearchMode === 'recognition' ? 'Choose a local MOD' : 'Search all MODs'}</h2></div><button class="icon-button" type="button" title="Close MOD search" aria-label="Close MOD search" onclick={onCloseModSearch}>×</button></div><p class="subtle mod-search-description">Search by display name, directory name, or MOD key. Website links use exact package identity, source Website, or inferred Nexus destinations.</p><label class="mod-search-field"><span>Search MODs</span><input bind:value={modSearchQuery} aria-label="Search MODs" placeholder="e.g. Alpha Mod" /></label>
    {#if modSearchQuery.trim().length === 0}<p class="empty-state mod-search-empty">Enter a search term to show matching MODs.</p>{:else if modSearchResults.length === 0}<p class="empty-state mod-search-empty">No matching MODs were found.</p>{:else}<div class="mod-search-results" aria-live="polite"><p class="mod-search-result-count">{modSearchResults.length} matching MODs</p>{#each modSearchResults as candidate (candidate.modKey)}<article class="mod-search-card"><button type="button" class="mod-card-main" aria-label={`Inspect ${candidate.displayName || candidate.modKey}`} onclick={() => onOpenModPage(candidate)}><strong>{candidate.displayName || candidate.directoryName || candidate.modKey}</strong><span>{candidate.version ? `v${candidate.version}` : 'Version unknown'}</span></button><div class="mod-card-meta"><span class="status-chip {statusClass(candidate.profileState)}">{formatLabel(candidate.profileState)}</span><span class="status-chip {statusClass(candidate.enabledState)}">{formatLabel(candidate.enabledState)}</span><span class="subtle">Priority {candidate.priority ?? 'Unknown'}</span></div>{#if modSearchMode === 'recognition'}<button type="button" class="secondary-button mod-recognition-button" onclick={() => onChooseModForRecognition(candidate)}>Use for recognition</button>{/if}<button type="button" class="secondary-button mod-recognition-button" onclick={() => onOpenInspectorForMod(candidate.modKey)}>Inspect evidence</button></article>{/each}</div>{/if}
  </aside>
{/if}

<style>
  .context-reference-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 8px;
    margin: 12px 0;
  }

  .context-reference-cell {
    display: grid;
    gap: 5px;
    min-width: 0;
    padding: 9px;
    border: 1px solid rgba(148, 163, 184, 0.16);
    border-radius: 8px;
    background: rgba(15, 23, 42, 0.44);
  }

  .context-reference-cell-version-review {
    border-color: rgba(250, 204, 21, 0.42);
  }

  .context-reference-label {
    color: #94a3b8;
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .context-reference-cell strong {
    min-width: 0;
    overflow: hidden;
    color: #e2e8f0;
    font-size: 12px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .context-reference-value-positive {
    color: #86efac !important;
  }

  .context-reference-value-unknown {
    color: #cbd5e1 !important;
  }

  .context-reference-evidence {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    min-width: 0;
    color: #94a3b8;
    font-size: 9px;
  }

  .context-reference-evidence i {
    width: 6px;
    height: 6px;
    flex: 0 0 6px;
    border-radius: 50%;
    background: #64748b;
  }

  .context-reference-evidence-observed i {
    background: #4ade80;
  }

  .context-reference-evidence-unknown {
    color: #fcd34d;
  }

  .context-reference-evidence-unknown i {
    background: #facc15;
  }

  .context-reference-review {
    display: grid;
    gap: 6px;
    margin-top: 8px;
    padding: 10px 11px;
    border-left: 2px solid #facc15;
    background: rgba(120, 53, 15, 0.2);
  }

  .context-reference-review strong {
    color: #fde68a;
    font-size: 11px;
  }

  .context-reference-review ul {
    display: grid;
    gap: 3px;
    margin: 0;
    padding-left: 17px;
    color: #d6c79e;
    font-size: 10px;
    line-height: 1.5;
  }

  .context-reference-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 7px;
    margin-top: 10px;
    padding-top: 10px;
    border-top: 1px solid rgba(148, 163, 184, 0.12);
  }

  .context-reference-action {
    min-width: 0;
  }

  @media (max-width: 420px) {
    .context-reference-grid {
      grid-template-columns: 1fr;
    }
  }
</style>
