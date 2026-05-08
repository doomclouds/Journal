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

export type HealthResponse = {
  app: string;
  status: string;
  version: string;
  environment: string;
  serverTime: string;
};

const apiBaseUrl = import.meta.env.VITE_JOURNAL_API_URL ?? "http://localhost:5057";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, init);
  if (!response.ok) {
    throw new Error(`${path} failed: ${response.status}`);
  }

  return await response.json() as T;
}

export function getHealth(): Promise<HealthResponse> {
  return request<HealthResponse>("/health");
}

export function getToday(): Promise<TodayJournalState> {
  return request<TodayJournalState>("/journal/today");
}

export function addTodayInput(text: string, source = "text"): Promise<TodayJournalState> {
  return request<TodayJournalState>("/journal/today/inputs", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ text, source })
  });
}

export function confirmTodayDraft(): Promise<TodayJournalState> {
  return request<TodayJournalState>("/journal/today/draft/confirm", {
    method: "POST"
  });
}
