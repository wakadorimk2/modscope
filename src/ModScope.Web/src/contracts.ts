export type BrowserTabUiState = {
  tabId: string;
  title: string;
  url: string;
  canGoBack: boolean;
  canGoForward: boolean;
  isActive: boolean;
};

export type BrowserHistoryEntryUiState = {
  entryId: string;
  title: string;
  url: string;
  visitedAtUtc: string;
};

export type BrowserUiState = {
  url: string;
  title: string;
  canGoBack: boolean;
  canGoForward: boolean;
  tabs: BrowserTabUiState[];
  activeTabId: string;
  history: BrowserHistoryEntryUiState[];
};

export type SourceReferenceUiState = {
  kind: string;
  relativePath: string;
  lineNumber?: number | null;
  columnNumber?: number | null;
};

export type DiagnosticUiState = {
  code: string;
  severity: string;
  message: string;
  source?: SourceReferenceUiState | null;
  rawValue?: string | null;
};

export type EvidenceReferenceUiState = {
  kind: string;
  source: SourceReferenceUiState;
};

export type PageObservationUiState = {
  url: string;
  title: string;
  observedAtUtc: string;
  source: string;
  extractionStatus: string;
  diagnostics: DiagnosticUiState[];
};

export type KnowledgeSessionUiState = {
  snapshotId: string;
  instanceName: string;
  profileName: string;
  createdAtUtc: string;
  parserVersion: string;
  schemaVersion: number;
  diagnostics: DiagnosticUiState[];
  versionEvidenceManifest?: VersionEvidenceManifestUiState | null;
};

export type VersionEvidenceManifestUiState = {
  isLoaded: boolean;
  displayName?: string | null;
  status?: string | null;
  diagnostics: DiagnosticUiState[];
};

export type ModRoleEvidenceUiState = {
  kind: string;
  detail: string;
  source: SourceReferenceUiState;
};

export type ModRoleUiState = {
  role: string;
  assessment: string;
  reason: string;
  evidence: ModRoleEvidenceUiState[];
};

export type ModCandidateUiState = {
  modKey: string;
  directoryName: string;
  displayName?: string | null;
  version?: string | null;
  website?: string | null;
  profileState: string;
  enabledState: string;
  priority?: number | null;
  source: SourceReferenceUiState;
  priorityEvidence?: EvidenceReferenceUiState | null;
  diagnostics: DiagnosticUiState[];
  role?: ModRoleUiState | null;
  packageRelation?: PackageRelationUiState | null;
};

export type ProfileUiState = {
  name: string;
  loadState: string;
};

export type KnowledgeOperationUiState = {
  kind: string;
  isBusy: boolean;
  isBackground: boolean;
  targetProfileName?: string | null;
  phase: string;
  completed?: number | null;
  total?: number | null;
};

export type KnowledgeUiState = {
  session?: KnowledgeSessionUiState | null;
  candidates: ModCandidateUiState[];
  profiles: ProfileUiState[];
  operation: KnowledgeOperationUiState;
};

export type SourceCandidateUiState = {
  candidateId: string;
  instanceName: string;
  gameName: string;
  profileName: string;
  readiness: string;
  isReady: boolean;
  gameTargetReady: boolean;
  evidence: string[];
  diagnostics: DiagnosticUiState[];
};

export type SourceDiscoveryUiState = {
  candidates: SourceCandidateUiState[];
  selectedCandidateId?: string | null;
};

export type LocalModMatchUiState = {
  modKey: string;
  directoryName: string;
  displayName?: string | null;
  profileState: string;
  enabledState: string;
  matchKind: string;
  strength: string;
  evidence: string;
  autoConfirmEligible: boolean;
};

export type IdentityUiState = {
  candidateIdentity: string;
  selectedLocalModKey?: string | null;
  recognitionStatus: string;
  matches: LocalModMatchUiState[];
  autoInspectToken?: string | null;
};

export type LocalContextUiState = {
  candidateIdentity: string;
  status: string;
  instanceName: string;
  profileName: string;
  localModKey?: string | null;
  directoryName?: string | null;
  enabledState: string;
  priority?: number | null;
  knownVersion?: string | null;
  evidence: EvidenceReferenceUiState[];
  uncertainties: string[];
  diagnostics: DiagnosticUiState[];
};

export type ModInfoUiState = {
  relativePath: string;
  parseStatus: string;
  name?: string | null;
  displayName?: string | null;
  version?: string | null;
  description?: string | null;
  author?: string | null;
  website?: string | null;
  unknownObservations: RawXmlObservationUiState[];
  diagnostics: DiagnosticUiState[];
  source: SourceReferenceUiState;
};

export type ModFileUiState = {
  relativePath: string;
  size: number;
  sha256: string;
  source: SourceReferenceUiState;
  evidenceKind: string;
};

export type XmlAttributeObservationUiState = {
  name: string;
  value: string;
};

export type RawXmlObservationUiState = {
  elementPath: string;
  elementName: string;
  attributes: XmlAttributeObservationUiState[];
  innerText?: string | null;
  source: SourceReferenceUiState;
  hasChildElements: boolean;
};

export type XmlXPathCandidateUiState = {
  rawValue: string;
  elementPath: string;
  source: SourceReferenceUiState;
};

export type XmlReferenceCandidateUiState = {
  rawValue: string;
  normalizedValue?: string | null;
  elementPath: string;
  evidenceKind: string;
  source: SourceReferenceUiState;
};

export type XmlPatchOperationUiState = {
  elementPath: string;
  rawOperationName: string;
  normalizedKind?: string | null;
  rawObservation: RawXmlObservationUiState;
  xPathCandidates: XmlXPathCandidateUiState[];
  targetXmlCandidates: XmlReferenceCandidateUiState[];
  entityCandidates: XmlReferenceCandidateUiState[];
  propertyCandidates: XmlReferenceCandidateUiState[];
  attributeCandidates: XmlReferenceCandidateUiState[];
  diagnostics: DiagnosticUiState[];
  source: SourceReferenceUiState;
};

export type XmlFileUiState = {
  relativePath: string;
  parseStatus: string;
  encodingName?: string | null;
  rootElementName?: string | null;
  elementCount: number;
  attributeCount: number;
  xPathCandidates: XmlXPathCandidateUiState[];
  rawObservations: RawXmlObservationUiState[];
  patchOperations: XmlPatchOperationUiState[];
  diagnostics: DiagnosticUiState[];
  source: SourceReferenceUiState;
};

export type InspectorUiState = {
  modKey: string;
  directoryName: string;
  profileState: string;
  enabledState: string;
  priority?: number | null;
  modInfo?: ModInfoUiState | null;
  files: ModFileUiState[];
  xmlFiles: XmlFileUiState[];
  diagnostics: DiagnosticUiState[];
  source: SourceReferenceUiState;
  packageRelation?: PackageRelationUiState | null;
  conclusion?: InspectorConclusionUiState | null;
  compatibilityObservations: CompatibilityObservationUiState[];
  compatibilityDiagnostics: DiagnosticUiState[];
};

export type InspectorConclusionUiState = {
  installedVersion?: string | null;
  latestObservedVersion?: string | null;
  versionStatus: string;
  versionReason: string;
  compatibilityStatus: string;
  compatibilityReason: string;
  identityState: string;
  identityConfidence: string;
  why: string;
  installedSource?: SourceReferenceUiState | null;
  latestSource?: SourceReferenceUiState | null;
  latestSourceSite?: string | null;
  latestTargetUrl?: string | null;
  latestObservedAtUtc?: string | null;
  summary: string;
  compatibilityTarget?: string | null;
  compatibilityRelation?: string | null;
  compatibilityEvidence?: string | null;
  compatibilityCondition?: string | null;
  compatibilitySource?: SourceReferenceUiState | null;
  compatibilitySourceSite?: string | null;
  compatibilityTargetUrl?: string | null;
  compatibilityObservedAtUtc?: string | null;
  compatibilityDiagnostics: DiagnosticUiState[];
};

export type SourceArtifactUiState = {
  artifactId: string;
  kind: string;
  name?: string | null;
  modId?: string | null;
  fileId?: string | null;
  sourceUrl?: string | null;
  source: SourceReferenceUiState;
};

export type VersionObservationUiState = {
  ownerKey: string;
  role: string;
  sourceKind: string;
  rawValue?: string | null;
  normalizedValue?: string | null;
  scheme: string;
  source: SourceReferenceUiState;
  observedAtUtc: string;
  diagnostics: DiagnosticUiState[];
  sourceSite?: string | null;
  targetUrl?: string | null;
  evidence?: string | null;
};

export type CompatibilityObservationUiState = {
  ownerKey: string;
  relation: string;
  gameContext: string;
  rawValue?: string | null;
  normalizedValue?: string | null;
  build?: string | null;
  matchedLine: string;
  source: SourceReferenceUiState;
  observedAtUtc: string;
  diagnostics: DiagnosticUiState[];
  sourceSite?: string | null;
  targetUrl?: string | null;
};

export type VersionComparisonUiState = {
  status: string;
  reason: string;
  observations: VersionObservationUiState[];
};

export type PackageRelationUiState = {
  packageDirectoryName: string;
  modletCount: number;
  sharedAcrossModlets: boolean;
  identityState: string;
  identityReason: string;
  metadataStatus: string;
  packageModId?: string | null;
  packageFileId?: string | null;
  packageVersion?: string | null;
  packageSource: SourceReferenceUiState;
  sourceArtifacts: SourceArtifactUiState[];
  versionObservations: VersionObservationUiState[];
  comparison: VersionComparisonUiState;
  diagnostics: DiagnosticUiState[];
};

export type BaseDataFileUiState = {
  targetXml: string;
  size: number;
  sha256: string;
  parseStatus?: string | null;
  source: SourceReferenceUiState;
  diagnostics: DiagnosticUiState[];
};

export type SemanticConflictOperationUiState = {
  operationKey: string;
  modKey: string;
  priority?: number | null;
  xmlFileRelativePath: string;
  elementPath: string;
  rawOperationName: string;
  normalizedKind?: string | null;
  targetXml?: string | null;
  xPath?: string | null;
  attributeName?: string | null;
  value?: string | null;
  source: SourceReferenceUiState;
  evidence: EvidenceReferenceUiState[];
  diagnostics: DiagnosticUiState[];
  hasChildElements: boolean;
};

export type EffectiveChangeUiState = {
  matchPath: string;
  attributeName?: string | null;
  beforeValue?: string | null;
  afterValue?: string | null;
  existedBefore: boolean;
  existsAfter: boolean;
  source: SourceReferenceUiState;
};

export type SemanticConflictGroupUiState = {
  targetXml?: string | null;
  xPath?: string | null;
  assessment: string;
  confidence: string;
  effectiveStatus: string;
  operations: SemanticConflictOperationUiState[];
  effectiveChanges: EffectiveChangeUiState[];
  evidence: EvidenceReferenceUiState[];
  uncertainties: string[];
  diagnostics: DiagnosticUiState[];
};

export type ConflictAnalysisUiState = {
  snapshotId: string;
  instanceName: string;
  profileName: string;
  baseFiles: BaseDataFileUiState[];
  groups: SemanticConflictGroupUiState[];
  diagnostics: DiagnosticUiState[];
};

export type RuntimeEvidenceObservationUiState = {
  modKey?: string | null;
  targetXml?: string | null;
  xPath?: string | null;
  observedOperation?: string | null;
  observedCategory?: string | null;
  normalizedAssessment?: string | null;
  diagnostics: DiagnosticUiState[];
};

export type RuntimeEvidenceUiState = {
  snapshotId: string;
  instanceName: string;
  profileName: string;
  toolName: string;
  toolVersion?: string | null;
  gameVersion?: string | null;
  capturedAtUtc: string;
  observations: RuntimeEvidenceObservationUiState[];
  diagnostics: DiagnosticUiState[];
};

export type RuntimeEvidenceComparisonItemUiState = {
  targetXml?: string | null;
  xPath?: string | null;
  status: string;
  staticAssessment?: string | null;
  runtimeAssessment?: string | null;
  observations: RuntimeEvidenceObservationUiState[];
  diagnostics: DiagnosticUiState[];
};

export type RuntimeEvidenceComparisonUiState = {
  snapshotId: string;
  instanceName: string;
  profileName: string;
  runtimeEvidence: RuntimeEvidenceUiState;
  items: RuntimeEvidenceComparisonItemUiState[];
  diagnostics: DiagnosticUiState[];
};

export type AnalysisInputUiState = {
  baseDataReady: boolean;
  runtimeLogsReady: boolean;
  baseDataStatus: 'inferred' | 'manual' | 'missing' | string;
};

export type AnalysisOperationUiState = {
  kind: string;
  isBusy: boolean;
};

export type AnalysisUiState = {
  inputs: AnalysisInputUiState;
  conflict?: ConflictAnalysisUiState | null;
  runtimeComparison?: RuntimeEvidenceComparisonUiState | null;
  operation: AnalysisOperationUiState;
  diagnostics: DiagnosticUiState[];
};

export type DeploymentEntryUiState = {
  entryId: string;
  modKey: string;
  enabled: boolean;
  priority?: number | null;
  isSeparator: boolean;
  isEditable: boolean;
};

export type DeploymentModChangeUiState = {
  modKey: string;
  beforeEnabled: boolean;
  afterEnabled: boolean;
  beforeOrder: number;
  afterOrder: number;
};

export type DeploymentJunctionChangeUiState = {
  action: string;
  targetName: string;
};

export type DeploymentUiState = {
  status: string;
  profileName: string;
  entries: DeploymentEntryUiState[];
  planId?: string | null;
  canApply: boolean;
  canLaunch: boolean;
  modChanges: DeploymentModChangeUiState[];
  junctionChanges: DeploymentJunctionChangeUiState[];
  diagnostics: DiagnosticUiState[];
};

export type LayoutUiState = {
  contextVisible: boolean;
  modListVisible: boolean;
};

export type UiState = {
  browser: BrowserUiState;
  observation?: PageObservationUiState | null;
  knowledge: KnowledgeUiState;
  sourceDiscovery: SourceDiscoveryUiState;
  identity: IdentityUiState;
  localContext?: LocalContextUiState | null;
  inspector?: InspectorUiState | null;
  analysis: AnalysisUiState;
  deployment: DeploymentUiState;
  layout: LayoutUiState;
  statusMessage: string;
  diagnostics: DiagnosticUiState[];
};

export type BridgeErrorPayload = {
  code: string;
  message: string;
};

export type HostMessage =
  | { kind: 'ready'; requestId?: string | null; payload: Record<string, never> }
  | { kind: 'state'; requestId?: string | null; payload: UiState }
  | { kind: 'error'; requestId?: string | null; payload: BridgeErrorPayload };

export const initialState: UiState = {
  browser: {
    url: 'about:blank',
    title: '',
    canGoBack: false,
    canGoForward: false,
    tabs: [{
      tabId: 'initial',
      title: 'New tab',
      url: 'about:blank',
      canGoBack: false,
      canGoForward: false,
      isActive: true
    }],
    activeTabId: 'initial',
    history: []
  },
  observation: null,
  knowledge: {
    session: null,
    candidates: [],
    profiles: [],
    operation: {
      kind: 'idle',
      isBusy: false,
      isBackground: false,
      targetProfileName: null,
      phase: 'idle',
      completed: null,
      total: null
    }
  },
  sourceDiscovery: {
    candidates: [],
    selectedCandidateId: null
  },
  identity: {
    candidateIdentity: '',
    selectedLocalModKey: null,
    recognitionStatus: 'not-searched',
    matches: [],
    autoInspectToken: null
  },
  localContext: null,
  inspector: null,
  analysis: {
    inputs: {
      baseDataReady: false,
      runtimeLogsReady: false,
      baseDataStatus: 'missing'
    },
    conflict: null,
    runtimeComparison: null,
    operation: {
      kind: 'idle',
      isBusy: false
    },
    diagnostics: []
  },
  deployment: {
    status: 'idle',
    profileName: '',
    entries: [],
    planId: null,
    canApply: false,
    canLaunch: false,
    modChanges: [],
    junctionChanges: [],
    diagnostics: []
  },
  layout: {
    contextVisible: true,
    modListVisible: true
  },
  statusMessage: 'Load a source and observe the current page.',
  diagnostics: []
};
