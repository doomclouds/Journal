export type JournalStatus =
  | "empty"
  | "draft"
  | "reviewing"
  | "processed"
  | "updated"
  | "attention";

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
  providerId: string;
  promptVersion: string;
  currentRawInputId: string;
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

const apiBaseUrl = import.meta.env.VITE_JOURNAL_API_URL ?? "http://localhost:5057";

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
  const response = await fetch(`${apiBaseUrl}${path}`, init);
  if (!response.ok) {
    const errorMessage = await readErrorMessage(response);
    throw new Error(errorMessage ?? `${path} failed: ${response.status}`);
  }

  return await response.json() as T;
}

export function getHealth(): Promise<HealthResponse> {
  return requestJson<HealthResponse>("/health");
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

export function startHarnessRun(text: string, source = "text"): Promise<StartHarnessRunResponse> {
  return requestJson<StartHarnessRunResponse>("/journal/today/harness/runs", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ text, source })
  });
}

export function openHarnessRunEvents(
  runId: string,
  onEvent: (event: JournalHarnessRunEvent) => void,
  onError?: (error: Error) => void
): EventSource {
  const events = new EventSource(`${apiBaseUrl}/journal/harness/runs/${encodeURIComponent(runId)}/events`);
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
