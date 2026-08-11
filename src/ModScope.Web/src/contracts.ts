export type BrowserUiState = {
  url: string;
  title: string;
  canGoBack: boolean;
  canGoForward: boolean;
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
  contentPreview?: string | null;
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
};

export type ModCandidateUiState = {
  modKey: string;
  directoryName: string;
  displayName?: string | null;
  version?: string | null;
  profileState: string;
  enabledState: string;
  priority?: number | null;
  source: SourceReferenceUiState;
  priorityEvidence?: EvidenceReferenceUiState | null;
  diagnostics: DiagnosticUiState[];
};

export type KnowledgeUiState = {
  session?: KnowledgeSessionUiState | null;
  candidates: ModCandidateUiState[];
};

export type IdentityUiState = {
  candidateIdentity: string;
  selectedLocalModKey?: string | null;
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
};

export type XmlXPathCandidateUiState = {
  rawValue: string;
  elementPath: string;
  source: SourceReferenceUiState;
};

export type XmlFileUiState = {
  relativePath: string;
  parseStatus: string;
  encodingName?: string | null;
  rootElementName?: string | null;
  elementCount: number;
  attributeCount: number;
  xpathCandidates: XmlXPathCandidateUiState[];
  rawObservations: RawXmlObservationUiState[];
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
};

export type UiState = {
  browser: BrowserUiState;
  observation?: PageObservationUiState | null;
  knowledge: KnowledgeUiState;
  identity: IdentityUiState;
  localContext?: LocalContextUiState | null;
  inspector?: InspectorUiState | null;
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
    canGoForward: false
  },
  observation: null,
  knowledge: {
    session: null,
    candidates: []
  },
  identity: {
    candidateIdentity: '',
    selectedLocalModKey: null
  },
  localContext: null,
  inspector: null,
  statusMessage: 'Load a source and observe the current page.',
  diagnostics: []
};
