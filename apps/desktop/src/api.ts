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

export function getToday(): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today");
}

export function getTodayEditor(): Promise<TodayEditorState> {
  return requestJson<TodayEditorState>("/journal/today/editor");
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

export function confirmTodayDraft(): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/draft/confirm", {
    method: "POST"
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

export function saveSourceDraft(markdown: string): Promise<TodayEditorState> {
  return requestJson<TodayEditorState>("/journal/today/editor/source", {
    method: "PUT",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ markdown })
  });
}
