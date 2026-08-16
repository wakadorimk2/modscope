<script lang="ts">
  import type {
    InspectorUiState,
    ModCandidateUiState,
    PackageRelationUiState,
    RuntimeEvidenceComparisonItemUiState,
    SemanticConflictGroupUiState,
    UiState
  } from '../contracts';

  export let state: UiState;
  export let inspector: InspectorUiState | null = null;
  export let inspectorCandidate: ModCandidateUiState | null = null;
  export let inspectorConflictGroups: SemanticConflictGroupUiState[] = [];
  export let inspectorRuntimeItems: RuntimeEvidenceComparisonItemUiState[] = [];
  export let operationBlocksInteraction = false;
  export let inspectorFilesOpen = false;
  export let webObservedVersion = '';
  export let onClose: () => void;
  export let onSetWebVersionObservation: () => void;
  export let onObserveNexusFileVersion: () => void;
  export let onStartStaticAnalysis: () => void;

  function normalizedValue(value: string | null | undefined): string {
    return (value ?? '').toLowerCase().replace(/[^a-z]+/g, '');
  }

  function formatLabel(value: string | null | undefined): string {
    if (!value) return 'Unknown';
    return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[-_]/g, ' ').replace(/:/g, ' · ').replace(/^./, (character) => character.toUpperCase());
  }

  function statusLabel(value: string | null | undefined): string {
    const normalized = normalizedValue(value);
    switch (normalized) {
      case 'updateavailable': return 'Update available';
      case 'uptodate': return 'Up to date';
      case 'installednewer': return 'Installed newer';
      case 'notassessed': return 'Not assessed';
      case 'notcomparable': return 'Not comparable';
      case 'observed': return 'Observed';
      case 'inferred': return 'Inferred';
      case 'needsreview': return 'Needs review';
      case 'confirmed': return 'Confirmed';
      case 'unknown': return 'Unknown';
      default: return formatLabel(value);
    }
  }

  function statusClass(status: string | undefined): string {
    const normalized = normalizedValue(status);
    const neutralStates = new Set([
      'ambiguous',
      'conflicting',
      'humanreview',
      'inferred',
      'missing',
      'needsreview',
      'observed',
      'partiallyresolved',
      'unresolved',
      'unknown'
    ]);
    if (!normalized || neutralStates.has(normalized)) return '';
    return 'status-' + (status ?? '').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  function identityLabel(value: string | null | undefined): string {
    switch (normalizedValue(value)) {
      case 'exact':
      case 'autoresolved': return 'Auto resolved';
      case 'humanreview': return 'Human review';
      case 'partiallyresolved': return 'Partially resolved';
      case 'missing': return 'Missing';
      case 'ambiguous': return 'Ambiguous';
      case 'conflicting': return 'Conflicting';
      case 'unresolved': return 'Unresolved';
      default: return formatLabel(value);
    }
  }

  function reviewStateLabel(value: string | null | undefined): string | null {
    const normalized = normalizedValue(value);
    return normalized === 'humanreview' || normalized === 'needsreview' || normalized === 'review'
      ? 'Needs review'
      : null;
  }

  function nexusArtifact(relation: PackageRelationUiState): PackageRelationUiState['sourceArtifacts'][number] | null {
    return relation.sourceArtifacts.find((artifact) => artifact.kind.toLowerCase() === 'nexus-file') ?? null;
  }

  function observationBySourceKind(relation: PackageRelationUiState, sourceKind: string): string {
    return relation.versionObservations.find((observation) => observation.sourceKind.toLowerCase() === sourceKind.toLowerCase())?.rawValue || 'Unknown';
  }

  function identityEvidencePath(relation: PackageRelationUiState): string {
    return relation.versionObservations.find((observation) => observation.sourceKind.toLowerCase() === 'mo2metaini')?.source.relativePath
      || nexusArtifact(relation)?.source.relativePath
      || relation.packageSource.relativePath;
  }

  function hasNexusApiObservation(relation: PackageRelationUiState | null | undefined): boolean {
    return relation?.versionObservations.some((observation) => observation.sourceKind.toLowerCase() === 'nexusapi') ?? false;
  }

  $: inspectorNexusArtifact = inspector?.packageRelation ? nexusArtifact(inspector.packageRelation) : null;

  function analysisLabel(value: string | null | undefined): string {
    const normalized = normalizedValue(value);
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

  function diagnosticClass(severity: string): string {
    return `diagnostic diagnostic-${severity.toLowerCase()}`;
  }

  function sizeLabel(size: number): string {
    return size < 1024 ? `${size} B` : `${(size / 1024).toFixed(1)} KB`;
  }

  function roleReasonSummary(candidate: ModCandidateUiState): string {
    const reason = candidate.role?.reason?.trim() ?? '';
    if (!reason) return 'Unknown';
    const match = reason.match(/^.*?(?:[.!?](?:\s|$)|$)/);
    return (match?.[0] ?? reason).trim().replace(/[.!?]$/, '') || 'Unknown';
  }
</script>

<section class="panel inspector-panel" aria-labelledby="inspector-title">
  <div class="drawer-heading inspector-panel-heading">
    <div>
      <span class="eyebrow">INSPECTOR</span>
      <h2 id="inspector-title">{inspector?.directoryName || 'Loading evidence'}</h2>
    </div>
    <button class="secondary-button" type="button" onclick={onClose}>Back to Context</button>
  </div>

  {#if inspector}
    {#if operationBlocksInteraction}
      <div class="local-skeleton-panel inspector-skeleton-panel" aria-busy="true">
        <p class="subtle local-skeleton-status" role="status">{state.analysis.operation.isBusy ? 'Analysis is running…' : 'Loading local evidence…'}</p>
        <div class="local-skeleton-stack" aria-hidden="true">
          <span class="local-skeleton local-skeleton-inspector-hero"></span>
          <div class="local-skeleton-grid">
            <span class="local-skeleton local-skeleton-grid-cell"></span>
            <span class="local-skeleton local-skeleton-grid-cell"></span>
            <span class="local-skeleton local-skeleton-grid-cell"></span>
            <span class="local-skeleton local-skeleton-grid-cell"></span>
          </div>
          <span class="local-skeleton local-skeleton-line"></span>
          <span class="local-skeleton local-skeleton-line local-skeleton-line-short"></span>
          <span class="local-skeleton local-skeleton-card"></span>
          <span class="local-skeleton local-skeleton-card local-skeleton-card-short"></span>
        </div>
      </div>
    {:else}
    <section class="drawer-section inspector-human-conclusion" aria-labelledby="inspector-conclusion-title">
      <div class="inspector-conclusion-heading">
        <div>
          <span class="eyebrow">CONCLUSION</span>
          <h3 id="inspector-conclusion-title">{inspector.conclusion?.summary || 'Release comparison not assessed'}</h3>
        </div>
        <span class="status-chip {statusClass(inspector.conclusion?.versionStatus)}">{statusLabel(inspector.conclusion?.versionStatus)}</span>
      </div>
      <div class="inspector-conclusion-status-grid">
        <div class="inspector-conclusion-status-item"><span>Installed</span><strong>{inspector.conclusion?.installedVersion || inspector.modInfo?.version || 'Unknown'}</strong></div>
        <div class="inspector-conclusion-status-item"><span>{hasNexusApiObservation(inspector.packageRelation) ? 'Nexus File observed' : 'Latest observed'}</span><strong>{inspector.conclusion?.latestObservedVersion || 'Unknown'}</strong></div>
        <div class="inspector-conclusion-status-item"><span>Version status</span><strong class="status-chip {statusClass(inspector.conclusion?.versionStatus)}">{statusLabel(inspector.conclusion?.versionStatus)}</strong></div>
        <div class="inspector-conclusion-status-item"><span>Game compatibility</span><strong class="status-chip {statusClass(inspector.conclusion?.compatibilityStatus)}">{statusLabel(inspector.conclusion?.compatibilityStatus)}</strong></div>
      </div>
      <div class="inspector-conclusion-explanation">
        <p><span>Package identity</span> {identityLabel(inspector.conclusion?.identityState)} · confidence {inspector.conclusion?.identityConfidence || 'Unknown'}</p>
        {#if reviewStateLabel(inspector.conclusion?.identityState)}<p><span>Review state</span> <span class="status-chip">{reviewStateLabel(inspector.conclusion?.identityState)}</span></p>{/if}
        <p><span>Why</span> {inspector.conclusion?.why || 'Conclusion evidence is not available.'}</p>
        <p><span>Compatibility reason</span> {inspector.conclusion?.compatibilityReason || 'No compatibility evidence was observed.'}</p>
        {#if inspector.conclusion?.compatibilityTarget}<p><span>Observed target</span> {inspector.conclusion.compatibilityTarget}</p>{/if}
        <p><span>Release match</span> {inspector.conclusion?.releaseAssociationReason || 'Release association was not assessed.'}</p>
        {#if inspector.conclusion?.releaseAssociationEvidence}<p><span>Release evidence</span> {inspector.conclusion.releaseAssociationEvidence}</p>{/if}
        {#if inspector.conclusion?.selectedLatestReleaseScopeLine}<p><span>Latest release scope</span> {inspector.conclusion.selectedLatestReleaseScopeLine}</p>{/if}
        {#if inspector.conclusion?.versionReason}<p><span>Version reason</span> {inspector.conclusion.versionReason}</p>{/if}
      </div>
      <div class="inspector-local-summary">
        <span>Local profile</span><strong>{formatLabel(inspector.profileState)}</strong>
        <span>Enabled</span><strong>{formatLabel(inspector.enabledState)}</strong>
        <span>Priority</span><strong>{inspector.priority ?? 'Unknown'}</strong>
      </div>
    </section>

    {#key inspector.modKey}
      {#if inspector.compatibilityObservations.length > 0 || inspector.compatibilityDiagnostics.length > 0 || (inspector.conclusion?.compatibilityDiagnostics?.length || 0) > 0}
        <details class="drawer-section inspector-disclosure">
          <summary class="inspector-disclosure-summary"><span class="eyebrow">COMPATIBILITY EVIDENCE</span><span class="subtle">Web claims · not runtime verification</span></summary>
          {#each inspector.compatibilityObservations as observation}
            <div class="compatibility-evidence-item">
              <p class="provenance-line"><strong>{formatLabel(observation.relation)}</strong> · {observation.gameContext} · {observation.rawValue || 'Missing value'}</p>
              <p class="provenance-line">Normalized · {observation.normalizedValue || 'Unknown'} · Build · {observation.build || 'None'}</p>
              <p class="provenance-line">Matched line · {observation.matchedLine}</p>
              <p class="provenance-line">
                Release scope · {observation.releaseScopeKind || 'Unresolved'} ·
                {observation.releaseScopeVersion || observation.releaseScopeRawVersion || 'Unknown'} ·
                {observation.releaseScopeUrl || 'URL unresolved'}
              </p>
              {#if observation.releaseScopeMatchedLine}<p class="provenance-line">Release scope line · {observation.releaseScopeMatchedLine}</p>{/if}
              <p class="provenance-line">Source · {observation.sourceSite || 'Web'} · {observation.targetUrl || observation.source.relativePath} · observed {observation.observedAtUtc}</p>
              {#each observation.diagnostics as diagnostic}<p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
            </div>
          {/each}
          {#each inspector.compatibilityDiagnostics as diagnostic}<p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
          {#each inspector.conclusion?.compatibilityDiagnostics || [] as diagnostic}<p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
        </details>
      {/if}

      {#if inspector.packageRelation}
        <details class="drawer-section inspector-disclosure">
            <summary class="inspector-disclosure-summary"><span class="eyebrow">VERSION EVIDENCE</span><span class="subtle">Observed values · identity, package, and release observations</span></summary>
          <div class="inspector-conclusion-grid">
            <div><span>Identity</span><strong class="status-chip {statusClass(inspector.packageRelation.identityState)}">{identityLabel(inspector.packageRelation.identityState)}</strong></div>
            <div><span>Version</span><strong class="status-chip {statusClass(inspector.packageRelation.comparison.status)}">{statusLabel(inspector.packageRelation.comparison.status)}</strong></div>
            <div><span>Package</span><strong>{inspector.packageRelation.packageDirectoryName}</strong></div>
            <div><span>Modlets</span><strong>{inspector.packageRelation.modletCount}</strong></div>
            {#if reviewStateLabel(inspector.packageRelation.identityState)}<div><span>Review state</span><strong class="status-chip">{reviewStateLabel(inspector.packageRelation.identityState)}</strong></div>{/if}
          </div>
          <div class="inspector-conclusion-grid">
            <div><span>Nexus MOD ID</span><strong>{inspectorNexusArtifact?.modId || inspector.packageRelation.packageModId || 'Unknown'}</strong></div>
            <div><span>Nexus File ID</span><strong>{inspectorNexusArtifact?.fileId || inspector.packageRelation.packageFileId || 'Unknown'}</strong></div>
            <div><span>MO2 meta.ini</span><strong>{observationBySourceKind(inspector.packageRelation, 'Mo2MetaIni')}</strong></div>
            <div><span>ModInfo.xml</span><strong>{observationBySourceKind(inspector.packageRelation, 'ModInfoXml')}</strong></div>
          </div>
          <p class="analysis-meta">{inspector.packageRelation.comparison.reason}</p>
          <p class="analysis-meta">{inspector.packageRelation.identityReason}</p>
          <p class="provenance-line">Identity evidence · {identityEvidencePath(inspector.packageRelation)}</p>
          {#if inspectorNexusArtifact}
            <p class="provenance-line">Artifact · {inspectorNexusArtifact.artifactId} · {inspectorNexusArtifact.sourceUrl || 'Locator unknown'}</p>
            <button
              class="secondary-button action-button"
              type="button"
              disabled={operationBlocksInteraction || inspector.packageRelation.identityState.toLowerCase() !== 'exact'}
              onclick={onObserveNexusFileVersion}
            >Observe Nexus File version</button>
          {/if}
          <details class="inspector-disclosure inspector-advanced-evidence">
            <summary>Manual Web version fallback</summary>
            <div class="web-version-observation">
              <label>Manual Web version<input bind:value={webObservedVersion} placeholder="Optional, e.g. 1.2.3" /></label>
              <button class="secondary-button" type="button" disabled={operationBlocksInteraction || webObservedVersion.trim().length === 0} onclick={onSetWebVersionObservation}>Record for this session</button>
            </div>
          </details>
          <details class="inspector-disclosure">
            <summary>Version observations · {inspector.packageRelation.versionObservations.length}</summary>
            {#each inspector.packageRelation.versionObservations as observation}
                <p class="provenance-line">
                  {formatLabel(observation.sourceKind)} · {observation.rawValue || 'Missing'} · {formatLabel(observation.scheme)} · {observation.source.relativePath}
                  {#if observation.sourceSite} · {observation.sourceSite}{/if}
                  {#if observation.targetUrl} · {observation.targetUrl}{/if}
                  {#if observation.releaseScopeKind} · scope {observation.releaseScopeKind}{/if}
                  {#if observation.releaseScopeVersion} · {observation.releaseScopeVersion}{/if}
                  · observed {observation.observedAtUtc}
                  {#if observation.evidence}<br />{observation.evidence}{/if}
                  {#if observation.releaseScopeMatchedLine}<br />scope line · {observation.releaseScopeMatchedLine}{/if}
                </p>
                {#each observation.diagnostics as diagnostic}<p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
            {/each}
          </details>
          {#if inspector.packageRelation.diagnostics.length > 0}
            <details class="inspector-disclosure">
              <summary>Package diagnostics · {inspector.packageRelation.diagnostics.length}</summary>
              {#each inspector.packageRelation.diagnostics as diagnostic}<p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
            </details>
          {/if}
        </details>
      {/if}

      {#if inspector.modInfo}
        <details class="drawer-section inspector-disclosure">
          <summary class="inspector-disclosure-summary"><span class="eyebrow">METADATA</span><span class="subtle">{formatLabel(inspector.modInfo.parseStatus)}</span></summary>
          <dl>
            <dt>Display name</dt><dd>{inspector.modInfo.displayName || 'Unknown'}</dd>
            <dt>Version</dt><dd>{inspector.modInfo.version || 'Unknown'}</dd>
            <dt>Author</dt><dd>{inspector.modInfo.author || 'Unknown'}</dd>
            <dt>Parse status</dt><dd>{formatLabel(inspector.modInfo.parseStatus)}</dd>
          </dl>
        </details>
      {/if}

      {#if inspectorCandidate?.role}
        <details class="drawer-section inspector-disclosure">
          <summary class="inspector-disclosure-summary"><span class="eyebrow">MOD ROLE</span><span class="subtle">{inspectorCandidate.role.role}</span></summary>
          <p class="analysis-meta role-summary-line"><span class="role-chip role-{inspectorCandidate.role.role.toLowerCase()}">{inspectorCandidate.role.role}</span><span class="status-chip status-role-assessment">{inspectorCandidate.role.assessment || 'Unknown'}</span></p>
          <p class="analysis-meta role-reason-summary">Reason: {roleReasonSummary(inspectorCandidate)}</p>
          <details class="role-detail">
            <summary>Role evidence · {inspectorCandidate.role.evidence.length}</summary>
            <p class="analysis-meta">{inspectorCandidate.role.reason || 'Unknown'}</p>
            {#each inspectorCandidate.role.evidence as evidence}<p class="provenance-line">{formatLabel(evidence.kind)} · {evidence.detail} · {evidence.source.relativePath}</p>{/each}
          </details>
        </details>
      {/if}

      {#if inspectorConflictGroups.length > 0 || inspectorRuntimeItems.length > 0}
        <details class="drawer-section inspector-disclosure">
          <summary class="inspector-disclosure-summary"><span class="eyebrow">RELATED EVIDENCE</span><span class="subtle">{inspectorConflictGroups.length + inspectorRuntimeItems.length} results</span></summary>
          {#if inspectorConflictGroups.length > 0}
            <div class="inspector-related-evidence-group"><span class="eyebrow">RELATED STATIC EVIDENCE</span>{#each inspectorConflictGroups as group}<p class="analysis-meta">{group.targetXml || 'Target XML unknown'} · {group.xPath || 'XPath unknown'} · {analysisLabel(group.assessment)} · {analysisLabel(group.confidence)}</p>{/each}</div>
          {/if}
          {#if inspectorRuntimeItems.length > 0}
            <div class="inspector-related-evidence-group"><span class="eyebrow">RELATED RUNTIME COMPARISON</span>{#each inspectorRuntimeItems as item}<p class="analysis-meta">{item.targetXml || 'Target XML unknown'} · {item.xPath || 'XPath unknown'} · {analysisLabel(item.status)}</p>{/each}</div>
          {/if}
        </details>
      {/if}

      <details class="drawer-section inspector-disclosure">
        <summary class="inspector-disclosure-summary"><span class="eyebrow">STATIC / RUNTIME ANALYSIS</span><span class="status-chip {state.analysis.conflict ? 'analysis-status-ready' : 'analysis-status-not-assessed'}">{state.analysis.conflict ? 'Assessed' : 'Not assessed'}</span></summary>
        {#if !state.analysis.inputs.baseDataReady}<p class="subtle">Select a base Data/Config folder to analyze.</p>{:else if state.analysis.operation.isBusy}<p class="subtle" role="status">Analysis is running…</p>{:else}<button class="secondary-button action-button" disabled={operationBlocksInteraction} onclick={onStartStaticAnalysis}>Re-run static analysis</button>{/if}
        <p class="notice">No result is not a no-conflict conclusion.</p>
      </details>

      <details class="drawer-section inspector-disclosure" bind:open={inspectorFilesOpen}>
        <summary class="inspector-disclosure-summary"><span class="eyebrow">FILES</span><span class="subtle">{inspector.files.length} files</span></summary>
        <ul class="compact-list">{#each inspector.files as file}<li><code>{file.relativePath}</code><span>{sizeLabel(file.size)}</span></li>{/each}</ul>
      </details>

      <details class="drawer-section inspector-disclosure">
        <summary class="inspector-disclosure-summary"><span class="eyebrow">XML · {inspector.xmlFiles.length}</span><span class="subtle">Raw details closed</span></summary>
        {#each inspector.xmlFiles as xml}
          <details class="xml-item">
            <summary><code>{xml.relativePath}</code><span>{formatLabel(xml.parseStatus)}</span></summary>
            <p>{xml.rootElementName || 'Unknown root'} · {xml.elementCount} elements · {xml.attributeCount} attributes</p>
            {#each xml.xPathCandidates as xpath}<code class="block-code">{xpath.rawValue}</code>{/each}
            {#if xml.patchOperations.length > 0}
              <details class="patch-operation-list">
                <summary>Patch operations · {xml.patchOperations.length}</summary>
                {#each xml.patchOperations as patch}
                  <details class="patch-operation-item">
                    <summary><span>{patch.rawOperationName}</span><span class="subtle">{patch.normalizedKind || 'Unknown'} · {patch.elementPath}</span></summary>
                    <p class="analysis-meta">Source · {patch.source.kind} · {patch.source.relativePath}</p>
                    {#if patch.xPathCandidates.length > 0}<div class="analysis-code-pair"><span>XPath</span>{#each patch.xPathCandidates as xpath}<code>{xpath.rawValue}</code>{/each}</div>{/if}
                    <details class="raw-detail">
                      <summary>Raw XML observation</summary>
                      <code class="block-code">{patch.rawObservation.elementPath} · &lt;{patch.rawObservation.elementName}&gt;</code>
                      {#each patch.rawObservation.attributes as attribute}<p class="analysis-meta">Attribute · {attribute.name} = {attribute.value}</p>{/each}
                      {#if patch.rawObservation.innerText}<pre>{patch.rawObservation.innerText}</pre>{/if}
                    </details>
                    {#each patch.diagnostics as diagnostic}<p class={diagnosticClass(diagnostic.severity)}><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
                  </details>
                {/each}
              </details>
            {/if}
            {#each xml.diagnostics as diagnostic}<p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
          </details>
        {/each}
      </details>

      {#if inspector.diagnostics.length > 0}
        <details class="drawer-section inspector-disclosure">
          <summary class="inspector-disclosure-summary"><span class="eyebrow">DIAGNOSTICS</span><span class="subtle">{inspector.diagnostics.length}</span></summary>
          {#each inspector.diagnostics as diagnostic}<p class="diagnostic"><strong>{diagnostic.code}</strong> {diagnostic.message}</p>{/each}
        </details>
      {/if}

      <details class="drawer-section inspector-disclosure">
        <summary class="inspector-disclosure-summary"><span class="eyebrow">PROVENANCE</span><span class="subtle">Source reference</span></summary>
        <p class="subtle">{inspector.source.kind} · {inspector.source.relativePath}</p>
      </details>
    {/key}
    {/if}
  {:else}
    <p class="empty-state">Inspector evidence is loading.</p>
  {/if}
</section>
