export type JournalStatus =
  | "empty"
  | "draft"
  | "reviewing"
  | "processed"
  | "updated"
  | "attention"
  | "missing";

export type JournalDate = {
  value: string;
  year: string;
  month: string;
  isoDate: string;
  monthDay: string;
  markdownFileName: string;
};

export type RawInput = {
  id: string;
  date: JournalDate;
  createdAt: string;
  source: string;
  text: string;
};

export type JournalDraft = {
  date: JournalDate;
  status: JournalStatus;
  markdown: string;
  sourceRawInputIds: string[];
  errors: string[];
  updatedAt: string;
};

export type JournalEntry = {
  date: JournalDate;
  markdown: string;
  path: string;
  updatedAt: string;
};

export type TodayJournalState = {
  date: JournalDate;
  status: JournalStatus;
  rawInputs: RawInput[];
  draft: JournalDraft | null;
  entry: JournalEntry | null;
  errors: string[];
};

export type JmfSectionKind = "required" | "optionalSingleton" | "system";

export type JmfSectionDefinition = {
  id: string;
  title: string;
  order: number;
  kind: JmfSectionKind;
  isEditableInBlockMode: boolean;
};

export type JmfSection = {
  id: string;
  title: string;
  content: string;
  kind: JmfSectionKind;
  isEditableInBlockMode: boolean;
};

export type JmfValidationIssue = {
  code: string;
  message: string;
  repairHint: string;
};

export type JmfValidationResult = {
  isValid: boolean;
  issues: JmfValidationIssue[];
};

export type TodayEditorState = {
  date: JournalDate;
  status: JournalStatus;
  markdown: string;
  sections: JmfSection[];
  availableOptionalSections: JmfSectionDefinition[];
  validation: JmfValidationResult;
  canConfirm: boolean;
  today: TodayJournalState;
};

export type JournalBlockEditSection = {
  id: string;
  content: string;
};

export type HealthResponse = {
  app: string;
  status: string;
  version: string;
  environment: string;
  serverTime: string;
};

export type AppInfo = {
  name: string;
  version: string;
  releaseVersion: string;
  commit: string;
  buildTimeUtc: string;
  environment: string;
  dataRoot: string;
  indexPath: string;
};

export type AiProviderView = {
  id: string;
  type: string;
  displayName: string;
  preset: string;
  baseUrl: string;
  model: string;
  isEnabled: boolean;
  isActive: boolean;
  hasApiKey: boolean;
  apiKeyPreview: string;
  canRevealApiKey: boolean;
  source: string;
  timeoutSeconds: number;
  temperature: number;
  maxTokens: number;
  stylePreset: string;
  lastTestStatus: string;
};

export type AiSettingsView = {
  activeProviderId: string;
  runtime: string;
  providers: AiProviderView[];
};

export type AiProviderSaveRequest = {
  id: string;
  type: string;
  displayName: string;
  preset: string;
  baseUrl: string;
  model: string;
  apiKey: string;
  isEnabled: boolean;
  timeoutSeconds: number;
  temperature: number;
  maxTokens: number;
  stylePreset: string;
};

export type AiSettingsSaveRequest = {
  activeProviderId: string;
  providers: AiProviderSaveRequest[];
};

export type AiProviderApiKeyView = {
  providerId: string;
  source: string;
  apiKey: string;
};

export type AiProviderHealthResult = {
  isSuccess: boolean;
  status: string;
  safeResponseSnippet: string;
  httpStatus: number | null;
  latency: string | null;
  error: {
    stage: string;
    code: string;
    message: string;
    technicalDetails: string;
  } | null;
};

export type AiSettingsActivationResult = {
  saved: boolean;
  settings: AiSettingsView;
  testResult: AiProviderHealthResult;
};

export type JournalHarnessRunStatus =
  | "queued"
  | "running"
  | "reviewing"
  | "attention"
  | "no-change"
  | "failed"
  | "interrupted";

export type JournalHarnessRunMode = "append-input" | "reorganize-existing";

export type JournalHarnessAuditToolCall = {
  id: string;
  name: string;
  operationKind: string;
  targetSectionId: string;
  status: string;
  reason: string;
  resultSummary: string;
  rejectionReason: string | null;
};

export type JournalHarnessAuditRun = {
  id: string;
  date: JournalDate;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  status: JournalHarnessRunStatus;
  mode: JournalHarnessRunMode;
  providerId: string;
  promptVersion: string;
  currentRawInputId: string | null;
  toolCalls: JournalHarnessAuditToolCall[];
  errors: string[];
  summary: string;
};

export type JournalHarnessRunEvent = {
  type: string;
  runId: string;
  status: JournalHarnessRunStatus;
  message: string;
};

export type StartHarnessRunResponse = {
  today: TodayJournalState;
  run: JournalHarnessAuditRun;
};

export type StartAppendHarnessRunRequest = {
  mode: "append-input";
  text: string;
  source?: string;
};

export type StartReorganizeHarnessRunRequest = {
  mode: "reorganize-existing";
};

export type StartHarnessRunRequest = StartAppendHarnessRunRequest | StartReorganizeHarnessRunRequest;

export type JournalHistoryHit = {
  sourceType: "section" | "raw-input";
  sectionId: string | null;
  rawInputId: string | null;
  title: string;
  snippet: string;
};

export type JournalHistoryEntrySummary = {
  date: JournalDate;
  status: JournalStatus;
  mood: string | null;
  rawInputCount: number;
  versionCount: number;
  hits: JournalHistoryHit[];
  attentionReason: string | null;
};

export type JournalHistorySearchResult = {
  items: JournalHistoryEntrySummary[];
};

export type JournalAnniversaryWheelResult = {
  monthDay: string;
  items: JournalHistoryEntrySummary[];
};

export type JournalEntryVersion = {
  id: string;
  date: JournalDate;
  createdAt: string;
  reason: string;
  sourceEntryPath: string;
  markdownPath: string;
  metaPath: string;
  contentHash: string;
};

export type JournalVersionDetail = {
  version: JournalEntryVersion;
  markdown: string;
};

export type JournalDataExportManifest = {
  format: string;
  createdAt: string;
  appVersion: string;
  backendVersion: string;
  frontendVersion: string;
  entryCount: number;
  rawInputCount: number;
  versionCount: number;
  containsFullApiKeys: boolean;
};

export type JournalDataExportResult = {
  exportPath: string;
  manifest: JournalDataExportManifest;
};

export type JournalDataImportResult = {
  backupDirectory: string;
  manifest: JournalDataExportManifest;
};

export type JournalHistoryEntryDetail = {
  date: JournalDate;
  status: JournalStatus;
  attentionReason: string | null;
  markdown: string | null;
  sections: JmfSection[];
  versions: JournalEntryVersion[];
};

const defaultApiBaseUrl = import.meta.env.VITE_JOURNAL_API_URL ?? "http://localhost:5057";
let apiBaseUrl = defaultApiBaseUrl;
let desktopAccessToken: string | null = null;

function normalizeApiBaseUrl(value: string) {
  return value.replace(/\/+$/, "");
}

export function setApiBaseUrl(value: string | null | undefined) {
  if (!value?.trim()) {
    return null;
  }

  apiBaseUrl = normalizeApiBaseUrl(value.trim());
  return apiBaseUrl;
}

export function setDesktopAccessToken(value: string | null | undefined) {
  const normalized = value?.trim() ?? "";
  desktopAccessToken = normalized.length > 0 ? normalized : null;
  return desktopAccessToken;
}

export function initializeApiBaseUrlFromDesktop() {
  const desktop = globalThis.window?.journalDesktop;
  if (!desktop?.getApiBaseUrl && !desktop?.getDesktopAccessToken) {
    return apiBaseUrl;
  }

  return Promise.all([
    Promise.resolve(desktop.getApiBaseUrl?.()),
    Promise.resolve(desktop.getDesktopAccessToken?.())
  ])
    .then(([apiBase, token]) => {
      setDesktopAccessToken(token);
      return setApiBaseUrl(apiBase);
    })
    .catch(() => null);
}

export function resetApiBaseUrlForTests(nextApiBaseUrl = defaultApiBaseUrl) {
  apiBaseUrl = normalizeApiBaseUrl(nextApiBaseUrl);
  desktopAccessToken = null;
  return apiBaseUrl;
}

export const frontendBuildInfo = {
  frontendVersion: import.meta.env.VITE_JOURNAL_FRONTEND_VERSION ?? "0.1.0-dev",
  releaseVersion: import.meta.env.VITE_JOURNAL_RELEASE_VERSION ?? "0.1.0-dev",
  commit: import.meta.env.VITE_JOURNAL_COMMIT ?? "dev",
  buildTimeUtc: import.meta.env.VITE_JOURNAL_BUILD_TIME_UTC ?? "local"
};

type ErrorResponse = {
  error?: unknown;
};

async function readErrorMessage(response: Response): Promise<string | null> {
  try {
    const body = await response.json() as ErrorResponse;
    return typeof body.error === "string" ? body.error : null;
  } catch {
    return null;
  }
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const requestInit = withDesktopAccessToken(init);
  const response = await fetch(`${apiBaseUrl}${path}`, requestInit);
  if (!response.ok) {
    const errorMessage = await readErrorMessage(response);
    throw new Error(errorMessage ?? `${path} failed: ${response.status}`);
  }

  return await response.json() as T;
}

export function getHealth(): Promise<HealthResponse> {
  return requestJson<HealthResponse>("/health");
}

function withDesktopAccessToken(init?: RequestInit): RequestInit | undefined {
  if (!desktopAccessToken) {
    return init;
  }

  return {
    ...init,
    headers: {
      ...(init?.headers ?? {}),
      "X-Journal-Desktop-Token": desktopAccessToken
    }
  };
}

function withDesktopAccessTokenQuery(url: string) {
  if (!desktopAccessToken) {
    return url;
  }

  const separator = url.includes("?") ? "&" : "?";
  return `${url}${separator}desktopAccessToken=${encodeURIComponent(desktopAccessToken)}`;
}

export function getAppInfo(): Promise<AppInfo> {
  return requestJson<AppInfo>("/app/info");
}

export function getTodayEditor(): Promise<TodayEditorState> {
  return requestJson<TodayEditorState>("/journal/today/editor");
}

export function getAiSettings(): Promise<AiSettingsView> {
  return requestJson<AiSettingsView>("/settings/ai");
}

export function saveAiSettings(request: AiSettingsSaveRequest): Promise<AiSettingsView> {
  return requestJson<AiSettingsView>("/settings/ai", {
    method: "PUT",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });
}

export function testAiProvider(
  providerId: string,
  candidate?: AiSettingsSaveRequest
): Promise<AiProviderHealthResult> {
  return requestJson<AiProviderHealthResult>("/settings/ai/test", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(candidate ? { providerId, candidate } : { providerId })
  });
}

export function activateAiSettings(request: AiSettingsSaveRequest): Promise<AiSettingsActivationResult> {
  return requestJson<AiSettingsActivationResult>("/settings/ai/activate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });
}

export function revealAiProviderApiKey(providerId: string): Promise<AiProviderApiKeyView> {
  return requestJson<AiProviderApiKeyView>(`/settings/ai/${encodeURIComponent(providerId)}/api-key`);
}

export function addTodayInput(text: string, source = "text"): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/inputs", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ text, source })
  });
}

export function startHarnessRun(request: StartHarnessRunRequest): Promise<StartHarnessRunResponse> {
  return requestJson<StartHarnessRunResponse>("/journal/today/harness/runs", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request.mode === "append-input" ? { ...request, source: request.source ?? "text" } : request)
  });
}

export function startAppendHarnessRun(text: string, source = "text"): Promise<StartHarnessRunResponse> {
  return startHarnessRun({ mode: "append-input", text, source });
}

export function startReorganizeHarnessRun(): Promise<StartHarnessRunResponse> {
  return startHarnessRun({ mode: "reorganize-existing" });
}

export function openHarnessRunEvents(
  runId: string,
  onEvent: (event: JournalHarnessRunEvent) => void,
  onError?: (error: Error) => void
): EventSource {
  const events = new EventSource(withDesktopAccessTokenQuery(
    `${apiBaseUrl}/journal/harness/runs/${encodeURIComponent(runId)}/events`
  ));
  const eventNames = [
    "run-started",
    "run-status",
    "run-already-completed",
    "planner-started",
    "tool-collected",
    "tool-rejected",
    "tool-applied",
    "validation-completed",
    "draft-updated",
    "run-completed",
    "run-failed",
    "run-reconnected",
    "run-interrupted"
  ];
  const reportError = (caught: unknown) => {
    onError?.(caught instanceof Error ? caught : new Error("Harness run event stream failed."));
  };

  for (const name of eventNames) {
    events.addEventListener(name, event => {
      try {
        onEvent(JSON.parse((event as MessageEvent).data) as JournalHarnessRunEvent);
      } catch (caught) {
        reportError(caught);
      }
    });
  }
  events.addEventListener("error", () => reportError(new Error("Harness run event stream failed.")));

  return events;
}

export function getJournalAudit(date: string): Promise<JournalHarnessAuditRun[]> {
  return requestJson<JournalHarnessAuditRun[]>(`/journal/audit?date=${encodeURIComponent(date)}`);
}

export function getJournalHistory(params: {
  query?: string;
  status?: string;
  from?: string;
  to?: string;
  cursor?: string;
  limit?: number;
}): Promise<JournalHistorySearchResult> {
  const search = new URLSearchParams();
  if (params.query) search.set("query", params.query);
  if (params.status) search.set("status", params.status);
  if (params.from) search.set("from", params.from);
  if (params.to) search.set("to", params.to);
  if (params.cursor) search.set("cursor", params.cursor);
  if (params.limit) search.set("limit", String(params.limit));
  const suffix = search.toString();
  return requestJson<JournalHistorySearchResult>(`/journal/history${suffix ? `?${suffix}` : ""}`);
}

export function getJournalAnniversaryWheel(monthDay: string, limit = 50): Promise<JournalAnniversaryWheelResult> {
  const search = new URLSearchParams();
  search.set("limit", String(limit));
  return requestJson<JournalAnniversaryWheelResult>(
    `/journal/history/anniversary/${encodeURIComponent(monthDay)}?${search.toString()}`
  );
}

export function getJournalHistoryEntry(date: string): Promise<JournalHistoryEntryDetail> {
  return requestJson<JournalHistoryEntryDetail>(`/journal/history/${encodeURIComponent(date)}`);
}

export function getJournalHistoryVersions(date: string): Promise<JournalEntryVersion[]> {
  return requestJson<JournalEntryVersion[]>(`/journal/history/${encodeURIComponent(date)}/versions`);
}

export function getJournalHistoryVersion(date: string, versionId: string): Promise<JournalVersionDetail> {
  return requestJson<JournalVersionDetail>(
    `/journal/history/${encodeURIComponent(date)}/versions/${encodeURIComponent(versionId)}`
  );
}

export function exportJournalData(): Promise<JournalDataExportResult> {
  return requestJson<JournalDataExportResult>("/journal/data/export", {
    method: "POST"
  });
}

export function getJournalDataSummary(): Promise<JournalDataExportManifest> {
  return requestJson<JournalDataExportManifest>("/journal/data/summary");
}

export function importJournalData(packagePath: string): Promise<JournalDataImportResult> {
  return requestJson<JournalDataImportResult>("/journal/data/import", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ packagePath })
  });
}

export function restoreJournalHistoryVersionDraft(date: string, versionId: string): Promise<TodayEditorState> {
  return requestJson<TodayEditorState>(
    `/journal/history/${encodeURIComponent(date)}/versions/${encodeURIComponent(versionId)}/restore-draft`,
    { method: "POST" }
  );
}

export function confirmTodayDraft(): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/draft/confirm", {
    method: "POST"
  });
}

export function regenerateTodayDraft(providerId?: string): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/draft/regenerate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId: providerId ?? null })
  });
}

export function saveBlockDraft(sections: JournalBlockEditSection[]): Promise<TodayEditorState> {
  return requestJson<TodayEditorState>("/journal/today/editor/blocks", {
    method: "PUT",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ sections })
  });
}
