import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";
import {
  activateAiSettings,
  getJournalAudit,
  getAiSettings,
  getTodayEditor,
  openHarnessRunEvents,
  regenerateTodayDraft,
  revealAiProviderApiKey,
  saveAiSettings,
  saveBlockDraft,
  startHarnessRun,
  testAiProvider,
  type AiProviderSaveRequest,
  type AiSettingsSaveRequest,
  type JournalHarnessAuditRun,
  type JournalHarnessRunEvent,
  type JournalDraft,
  type JournalEntryVersion,
  type JournalHistoryEntryDetail,
  type JournalHistoryEntrySummary,
  type TodayJournalState,
  type TodayEditorState
} from "./api";
import { JournalEditor } from "./JournalEditor";
import { LlmSettingsPanel } from "./LlmSettingsPanel";

type MockResponse = {
  ok?: boolean;
  status?: number;
  body: unknown;
};

type Deferred<T> = {
  promise: Promise<T>;
  resolve: (value: T) => void;
};

const healthResponse = {
  app: "Journal.Api",
  status: "ok",
  version: "0.1.0",
  environment: "Development",
  serverTime: "2026-05-08T08:00:00+08:00"
};

const aiSettings = {
  activeProviderId: "mock",
  runtime: "OpenAI-compatible runtime · Agent Framework 1.5.0",
  providers: [
    {
      id: "mock",
      type: "mock",
      displayName: "Mock",
      preset: "mock",
      baseUrl: "local",
      model: "mock-journal",
      isEnabled: true,
      isActive: true,
      hasApiKey: true,
      apiKeyPreview: "sk-***1234",
      canRevealApiKey: false,
      source: "default",
      timeoutSeconds: 1,
      temperature: 0,
      maxTokens: 0,
      stylePreset: "faithful",
      lastTestStatus: "not-tested"
    },
    {
      id: "deepseek",
      type: "openai-compatible",
      displayName: "DeepSeek",
      preset: "deepseek",
      baseUrl: "https://api.deepseek.com",
      model: "deepseek-v4-flash",
      isEnabled: false,
      isActive: false,
      hasApiKey: false,
      apiKeyPreview: "",
      canRevealApiKey: true,
      source: "preset",
      timeoutSeconds: 45,
      temperature: 0.2,
      maxTokens: 1200,
      stylePreset: "faithful",
      lastTestStatus: "not-tested"
    }
  ]
};

const deepSeekAiSettings = {
  ...aiSettings,
  activeProviderId: "deepseek",
  providers: aiSettings.providers.map(provider => ({
    ...provider,
    isActive: provider.id === "deepseek",
    isEnabled: provider.id === "deepseek" ? true : provider.isEnabled
  }))
};

const missingActiveAiSettings = {
  ...aiSettings,
  activeProviderId: "missing-provider",
  providers: aiSettings.providers.map(provider => ({
    ...provider,
    isActive: false
  }))
};

const journalDate = {
  value: "2026-05-08",
  year: "2026",
  month: "05",
  isoDate: "2026-05-08",
  monthDay: "05-08",
  markdownFileName: "2026-05-08.md"
};

const emptyToday: TodayJournalState = {
  date: journalDate,
  status: "empty",
  rawInputs: [],
  draft: null,
  entry: null,
  errors: []
};

const reviewingDraft: JournalDraft = {
  date: journalDate,
  status: "reviewing",
  markdown: "# 2026-05-08\n\n## 晨间记录\n\n今天完成 Phase 2 API 连接",
  sourceRawInputIds: ["raw-1"],
  errors: [],
  updatedAt: "2026-05-08T08:05:00+08:00"
};

const reviewingToday: TodayJournalState = {
  ...emptyToday,
  status: "reviewing",
  rawInputs: [
    {
      id: "raw-1",
      date: journalDate,
      createdAt: "2026-05-08T08:05:00+08:00",
      source: "text",
      text: "今天完成 Phase 2 API 连接"
    }
  ],
  draft: reviewingDraft
};

const queuedHarnessRun: JournalHarnessAuditRun = {
  id: "run-1",
  date: journalDate,
  createdAt: "2026-05-08T09:30:00+08:00",
  startedAt: null,
  completedAt: null,
  status: "queued",
  providerId: "mock",
  promptVersion: "journal-harness-v1",
  currentRawInputId: "raw-1",
  toolCalls: [],
  errors: [],
  summary: "Queued."
};

const historySummary: JournalHistoryEntrySummary = {
  date: journalDate,
  status: "processed",
  mood: "平静",
  rawInputCount: 1,
  versionCount: 1,
  attentionReason: null,
  hits: [{
    sourceType: "section",
    sectionId: "today-focus",
    rawInputId: null,
    title: "今日重点",
    snippet: "推进 Phase 4A 历史搜索"
  }]
};

const historyVersion: JournalEntryVersion = {
  id: "version-2026-05-08T09-30-00+08-00",
  date: journalDate,
  createdAt: "2026-05-08T09:30:00+08:00",
  reason: "confirm-draft",
  sourceEntryPath: "entries/2026/05/2026-05-08.md",
  markdownPath: ".journal/versions/2026/05/2026-05-08/version.md",
  metaPath: ".journal/versions/2026/05/2026-05-08/version.meta.json",
  contentHash: "sha256:history"
};

function historyDetail(date = journalDate, content = "- 推进 Phase 4A 历史搜索"): JournalHistoryEntryDetail {
  return {
    date,
    status: "processed",
    attentionReason: null,
    markdown: `# ${date.isoDate}\n\n${content}`,
    sections: [{
      id: "today-focus",
      title: "今日重点",
      content,
      kind: "required",
      isEditableInBlockMode: true
    }],
    versions: []
  };
}

function processedToday(entryPath = "C:\\Journal\\entries\\2026\\05\\2026-05-08.md"): TodayJournalState {
  return {
    ...reviewingToday,
    status: "processed",
    draft: {
      ...reviewingDraft,
      status: "processed"
    },
    entry: {
      date: journalDate,
      markdown: reviewingDraft.markdown,
      path: entryPath,
      updatedAt: "2026-05-08T08:06:00+08:00"
    }
  };
}

const attentionToday: TodayJournalState = {
  ...reviewingToday,
  status: "attention",
  draft: {
    ...reviewingDraft,
    status: "attention",
    markdown: "# AI JSON validation failed\n\n## Errors\n\n- title is required",
    errors: ["title is required"]
  },
  errors: ["title is required"]
};

const editorMarkdown = `# 2026-05-08

<!-- jmf:section id="today-focus" -->
## 今日重点

推进 Phase 3
<!-- /jmf:section -->

<!-- jmf:section id="raw-inputs" -->
## 原始输入

今天要保留原始表达
<!-- /jmf:section -->
`;

function createEditorState(overrides: Partial<TodayEditorState> = {}): TodayEditorState {
  return {
    date: journalDate,
    status: "reviewing",
    markdown: editorMarkdown,
    sections: [
      {
        id: "today-focus",
        title: "今日重点",
        content: "推进 Phase 3",
        kind: "required",
        isEditableInBlockMode: true
      },
      {
        id: "raw-inputs",
        title: "原始输入",
        content: "今天要保留原始表达",
        kind: "system",
        isEditableInBlockMode: false
      }
    ],
    availableOptionalSections: [
      {
        id: "mood",
        title: "情绪感受",
        order: 30,
        kind: "optionalSingleton",
        isEditableInBlockMode: true
      }
    ],
    validation: {
      isValid: true,
      issues: []
    },
    canConfirm: true,
    today: reviewingToday,
    ...overrides
  };
}

function mockFetchSequence(responses: MockResponse[]) {
  const fetchMock = vi.fn(async () => {
    const response = responses.shift();
    if (!response) {
      throw new Error("Unexpected fetch call");
    }

    return {
      ok: response.ok ?? true,
      status: response.status ?? 200,
      json: async () => response.body
    };
  });

  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function createDeferred<T>(): Deferred<T> {
  let resolve: (value: T) => void = () => {};
  const promise = new Promise<T>(promiseResolve => {
    resolve = promiseResolve;
  });

  return { promise, resolve };
}

function createEventSourceMock() {
  const listeners = new Map<string, Array<(event: MessageEvent | Event) => void>>();
  const close = vi.fn();
  const EventSourceMock = vi.fn(function (_url: string) {
    return {
      addEventListener: (name: string, listener: (event: MessageEvent | Event) => void) => {
        const existing = listeners.get(name) ?? [];
        existing.push(listener);
        listeners.set(name, existing);
      },
      close
    } as unknown as EventSource;
  });

  vi.stubGlobal("EventSource", EventSourceMock);

  return {
    EventSourceMock,
    close,
    emit(name: string, event: JournalHarnessRunEvent) {
      for (const listener of listeners.get(name) ?? []) {
        listener({ data: JSON.stringify(event) } as MessageEvent);
      }
    }
  };
}

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body
  } as Response;
}

function createInitialFetchMock() {
  return vi
    .fn()
    .mockResolvedValueOnce(mockJsonResponse(healthResponse))
    .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings));
}

async function openLlmSettingsFromNativeMenu() {
  fireEvent(window, new CustomEvent("journal:native-menu-command", { detail: "open-llm-settings" }));
  return screen.findByRole("region", { name: "LLM 配置面板" });
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});
describe("App", () => {
  test("prevents stale initial load race by disabling submit until editor state resolves", async () => {
    const eventSource = createEventSourceMock();
    const healthDeferred = createDeferred<Response>();
    const editorDeferred = createDeferred<Response>();
    const aiSettingsDeferred = createDeferred<Response>();
    const startHarnessDeferred = createDeferred<Response>();
    const refreshedEditorDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockReturnValueOnce(healthDeferred.promise)
      .mockReturnValueOnce(editorDeferred.promise)
      .mockReturnValueOnce(aiSettingsDeferred.promise)
      .mockReturnValueOnce(startHarnessDeferred.promise)
      .mockReturnValueOnce(refreshedEditorDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const input = screen.getByLabelText("补充今天的自然语言输入");
    const submitButton = screen.getByRole("button", { name: "生成草稿" });

    fireEvent.change(input, { target: { value: "初始加载未完成时不能提交" } });
    fireEvent.click(submitButton);

    expect(submitButton).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(3);

    healthDeferred.resolve({
      ok: true,
      status: 200,
      json: async () => healthResponse
    } as Response);
    editorDeferred.resolve({
      ok: true,
      status: 200,
      json: async () => createEditorState({
        status: "empty",
        markdown: "",
        sections: [],
        availableOptionalSections: [],
        canConfirm: false,
        today: emptyToday
      })
    } as Response);
    aiSettingsDeferred.resolve(mockJsonResponse(aiSettings));

    await waitFor(() => expect(submitButton).toBeEnabled());

    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(submitButton);

    expect(submitButton).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(4);

    startHarnessDeferred.resolve(mockJsonResponse({ today: emptyToday, run: queuedHarnessRun }));
    await waitFor(() =>
      expect(eventSource.EventSourceMock).toHaveBeenCalledWith("http://localhost:5057/journal/harness/runs/run-1/events")
    );
    eventSource.emit("run-completed", {
      type: "run-completed",
      runId: "run-1",
      status: "reviewing",
      message: "done"
    });
    refreshedEditorDeferred.resolve(mockJsonResponse(createEditorState()));

    await waitFor(() =>
      expect(screen.getAllByText("可保存").length).toBeGreaterThan(0)
    );
    expect(screen.getByText("推进 Phase 3")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
  });

  test("shows friendly empty productized state on initial render", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      {
        body: createEditorState({
          status: "empty",
          markdown: "",
          sections: [],
          availableOptionalSections: [],
          validation: {
            isValid: false,
            issues: [
              {
                code: "missing-front-matter",
                message: "JMF front matter is missing.",
                repairHint: "Add the required YAML front matter block."
              }
            ]
          },
          canConfirm: false,
          today: emptyToday
        })
      },
      { body: aiSettings }
    ]);

    render(<App />);

    const stage = await screen.findByLabelText("日记纸面");

    expect(within(stage).getByRole("heading", { name: "今天先写一句", level: 1 })).toBeInTheDocument();
    expect(screen.getByText("Journal")).toBeInTheDocument();
    expect(screen.getByText("本地优先晨间日记")).toBeInTheDocument();
    expect(screen.getByText(/2026年5月8日/)).toBeInTheDocument();
    expect(screen.getAllByText("今日材料").length).toBeGreaterThan(0);
    expect(screen.getByText("Today Assistant")).toBeInTheDocument();
    expect(screen.getAllByText("待开始").length).toBeGreaterThan(0);
    expect(screen.getAllByText("今天先写一句").length).toBeGreaterThan(0);
    expect(screen.getAllByText("不用先想结构。写一段自然语言，Journal 会保留原话，再帮你整理成可确认的日记草稿。").length).toBeGreaterThan(0);
    expect(screen.getByLabelText("补充今天的自然语言输入")).toBeInTheDocument();
    expect(screen.queryByText("补充今天的自然语言输入")).not.toBeInTheDocument();
    expect(screen.queryByText("默认阅读，点击段落才编辑")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "生成草稿" })).toBeInTheDocument();
    expect(screen.queryByText("还没有可编辑的 JMF 草稿")).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(1, "http://localhost:5057/health", undefined);
    expect(fetchMock).toHaveBeenNthCalledWith(2, "http://localhost:5057/journal/today/editor", undefined);
    expect(fetchMock).toHaveBeenNthCalledWith(3, "http://localhost:5057/settings/ai", undefined);
    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today", undefined);
  });

  test("renders the composer as an icon toolbar with tooltips instead of visible text buttons", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    const composer = await screen.findByLabelText("底部输入和主操作");
    const submitButton = within(composer).getByRole("button", { name: "生成草稿" });
    const regenerateButton = within(composer).getByRole("button", { name: "重新整理" });
    const saveButton = within(composer).getByRole("button", { name: "保存日记" });

    expect(submitButton).toHaveAttribute("title", "写入今天的材料");
    expect(regenerateButton).toHaveAttribute("title", "重新整理草稿");
    expect(saveButton).toHaveAttribute("title", "保存日记");
    expect(submitButton.querySelector("svg")).toBeInTheDocument();
    expect(regenerateButton.querySelector("svg")).toBeInTheDocument();
    expect(saveButton.querySelector("svg")).toBeInTheDocument();
    expect(within(composer).queryByText("生成草稿")).not.toBeInTheDocument();
    expect(within(composer).queryByText("重新整理")).not.toBeInTheDocument();
    expect(within(composer).queryByText("保存日记")).not.toBeInTheDocument();
  });

  test("renders the workbench directly without prototype window chrome or a web menu", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    const { container } = render(<App />);

    const stage = await screen.findByLabelText("日记纸面");

    expect(container.querySelector("main.desktop-shell")).toHaveAttribute("aria-label", "Journal 今日工作台");
    expect(within(stage).getByRole("heading", { name: "把今天收好", level: 1 })).toBeInTheDocument();
    expect(screen.getByLabelText("今日上下文")).toBeInTheDocument();
    expect(screen.getByLabelText("今日助手")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "应用菜单" })).not.toBeInTheDocument();
    expect(container.querySelector(".app-window")).toBeNull();
    expect(container.querySelector(".titlebar")).toBeNull();
    expect(container.querySelector(".window-controls")).toBeNull();
    expect(container.querySelector(".menubar")).toBeNull();
    expect(container.querySelector(".menu-panel")).toBeNull();
  });

  test("opens LLM settings panel from the native menu bridge", async () => {
    const nativeMenuHandlers: Array<(command: string) => void> = [];
    const unsubscribe = vi.fn();
    vi.stubGlobal("journalDesktop", {
      onNativeMenuCommand: (handler: (command: string) => void) => {
        nativeMenuHandlers.push(handler);
        return unsubscribe;
      }
    });
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    await screen.findByLabelText("今日助手");
    nativeMenuHandlers[0]("open-llm-settings");

    await waitFor(() =>
      expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument()
    );
  });

  test("opens LLM settings panel from the native menu DOM fallback event", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    await screen.findByLabelText("今日助手");
    await openLlmSettingsFromNativeMenu();

    await waitFor(() =>
      expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument()
    );
  });

  test("shows productized draft language without backend status leaks", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    await waitFor(() =>
      expect(screen.getAllByText("可保存").length).toBeGreaterThan(0)
    );
    expect(screen.getAllByText("今日材料").length).toBeGreaterThan(0);
    expect(screen.queryByText("整理状态")).not.toBeInTheDocument();
    expect(screen.queryByText("快捷动作")).not.toBeInTheDocument();
    expect(screen.getByLabelText("今日材料数量")).toHaveTextContent("1 条");
    expect(screen.getAllByText("下一步").length).toBeGreaterThan(0);
    expect(screen.getByText(/下一步：/)).toHaveTextContent("下一步：");
    expect(screen.getByRole("heading", { name: "今天被这样整理" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "统计" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "整理证据" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "整理解释" })).not.toBeInTheDocument();
    expect(screen.queryByText(/AI 将今天的输入理解为/)).not.toBeInTheDocument();
    expect(screen.queryByText("可追溯")).not.toBeInTheDocument();
    expect(screen.queryByText("低噪音")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "统计" }).compareDocumentPosition(screen.getByRole("heading", { name: "整理证据" }))).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(screen.getByLabelText("统计")).toBeInTheDocument();
    expect(screen.queryByLabelText("AI 整理统计")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "回到日记" })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/当前 LLM/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /打开 LLM 配置/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("日记状态：可保存")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("今日状态")).not.toBeInTheDocument();
    expect(screen.getByLabelText("LLM：Mock")).toHaveClass("assistant-meta-provider");
    expect(screen.queryByText("reviewing")).not.toBeInTheDocument();
    expect(screen.queryByText("Raw inputs")).not.toBeInTheDocument();
  });

  test("view switch toggles between journal-only and journal-with-assistant", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    const { container } = render(<App />);

    await screen.findByLabelText("今日助手");

    const journalOnly = screen.getByRole("button", { name: "只看日记" });
    const withAssistant = screen.getByRole("button", { name: "日记 + 助手" });
    expect(withAssistant).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByLabelText("今日助手")).toBeInTheDocument();

    fireEvent.click(journalOnly);

    expect(journalOnly).toHaveAttribute("aria-pressed", "true");
    expect(withAssistant).toHaveAttribute("aria-pressed", "false");
    expect(screen.queryByLabelText("今日助手")).not.toBeInTheDocument();
    expect(container.querySelector(".command-workspace")).toHaveClass("journal-only");
    expect(fetchMock).toHaveBeenCalledTimes(3);

    fireEvent.click(withAssistant);

    expect(withAssistant).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByLabelText("今日助手")).toBeInTheDocument();
  });

  test("opens audit workbench from Today Assistant and returns to today", async () => {
    const editorState = createEditorState();
    const auditRuns = [
      {
        id: "run-1",
        date: reviewingToday.date,
        createdAt: "2026-05-08T09:30:00+08:00",
        startedAt: "2026-05-08T09:30:01+08:00",
        completedAt: "2026-05-08T09:30:03+08:00",
        status: "reviewing",
        providerId: "mock",
        promptVersion: "journal-harness-v1",
        currentRawInputId: "raw-1",
        toolCalls: [
          {
            id: "tool-1",
            name: "appendJournalSection",
            operationKind: "append",
            targetSectionId: "today-focus",
            status: "applied",
            reason: "整理新增内容",
            resultSummary: "追加 1 条",
            rejectionReason: null
          }
        ],
        errors: [],
        summary: "AI 追加 1 条。"
      }
    ];
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(editorState))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockResolvedValueOnce(mockJsonResponse(auditRuns));
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "查看审计" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/audit?date=2026-05-08", undefined)
    );
    expect(await screen.findByRole("heading", { name: "工具调用时间线" })).toBeInTheDocument();
    expect(screen.getByText("appendJournalSection")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "返回今日" }));

    expect(await screen.findByLabelText("日记纸面")).toBeInTheDocument();
  });

  test("blocks audit workbench while inline block edits are dirty", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "还没保存的审计前编辑" }
    });

    fireEvent.click(screen.getByRole("button", { name: "查看审计" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("先保存或取消当前编辑，再继续补充或重新整理。");
    expect(screen.getByLabelText("日记纸面")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("还没保存的审计前编辑");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/audit?date=2026-05-08", undefined);
  });

  test("keeps newest audit date runs when date requests resolve out of order", async () => {
    const firstDateAuditDeferred = createDeferred<Response>();
    const secondDateAuditDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockResolvedValueOnce(mockJsonResponse([
        {
          id: "run-open",
          date: reviewingToday.date,
          createdAt: "2026-05-08T09:30:00+08:00",
          startedAt: "2026-05-08T09:30:01+08:00",
          completedAt: "2026-05-08T09:30:03+08:00",
          status: "reviewing",
          providerId: "mock",
          promptVersion: "journal-harness-v1",
          currentRawInputId: "raw-1",
          toolCalls: [
            {
              id: "tool-open",
              name: "appendJournalSection",
              operationKind: "append",
              targetSectionId: "today-focus",
              status: "applied",
              reason: "打开审计",
              resultSummary: "打开记录",
              rejectionReason: null
            }
          ],
          errors: [],
          summary: "打开审计记录"
        }
      ]))
      .mockReturnValueOnce(firstDateAuditDeferred.promise)
      .mockReturnValueOnce(secondDateAuditDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "查看审计" }));
    expect(await screen.findByRole("heading", { name: "工具调用时间线" })).toBeInTheDocument();

    const dateInput = screen.getByLabelText("审计日期");
    fireEvent.change(dateInput, { target: { value: "2026-05-09" } });
    fireEvent.change(dateInput, { target: { value: "2026-05-10" } });

    secondDateAuditDeferred.resolve(mockJsonResponse([
      {
        id: "run-new",
        date: { ...journalDate, value: "2026-05-10", isoDate: "2026-05-10", monthDay: "05-10", markdownFileName: "2026-05-10.md" },
        createdAt: "2026-05-10T09:30:00+08:00",
        startedAt: "2026-05-10T09:30:01+08:00",
        completedAt: "2026-05-10T09:30:03+08:00",
        status: "reviewing",
        providerId: "mock",
        promptVersion: "journal-harness-v1",
        currentRawInputId: "raw-new",
        toolCalls: [
          {
            id: "tool-new",
            name: "appendNewestAuditSection",
            operationKind: "append",
            targetSectionId: "today-focus",
            status: "applied",
            reason: "最新日期",
            resultSummary: "最新记录",
            rejectionReason: null
          }
        ],
        errors: [],
        summary: "最新日期审计"
      }
    ]));

    expect(await screen.findByText("appendNewestAuditSection")).toBeInTheDocument();
    expect(screen.queryByText("appendOlderAuditSection")).not.toBeInTheDocument();

    await act(async () => {
      firstDateAuditDeferred.resolve(mockJsonResponse([
        {
          id: "run-old",
          date: { ...journalDate, value: "2026-05-09", isoDate: "2026-05-09", monthDay: "05-09", markdownFileName: "2026-05-09.md" },
          createdAt: "2026-05-09T09:30:00+08:00",
          startedAt: "2026-05-09T09:30:01+08:00",
          completedAt: "2026-05-09T09:30:03+08:00",
          status: "reviewing",
          providerId: "mock",
          promptVersion: "journal-harness-v1",
          currentRawInputId: "raw-old",
          toolCalls: [
            {
              id: "tool-old",
              name: "appendOlderAuditSection",
              operationKind: "append",
              targetSectionId: "today-focus",
              status: "applied",
              reason: "旧日期",
              resultSummary: "旧记录",
              rejectionReason: null
            }
          ],
          errors: [],
          summary: "旧日期审计"
        }
      ]));
      await firstDateAuditDeferred.promise;
    });

    expect(screen.getByText("appendNewestAuditSection")).toBeInTheDocument();
    expect(screen.queryByText("appendOlderAuditSection")).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/audit?date=2026-05-09", undefined);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/audit?date=2026-05-10", undefined);
  });

  test("opens history workbench from Today Assistant and returns to today", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockResolvedValueOnce(mockJsonResponse({ items: [historySummary] }))
      .mockResolvedValueOnce(mockJsonResponse(historyDetail()))
      .mockResolvedValueOnce(mockJsonResponse([historyVersion]))
      .mockResolvedValueOnce(mockJsonResponse({ items: [{ ...historySummary, status: "reviewing" }] }))
      .mockResolvedValueOnce(mockJsonResponse(historyDetail()))
      .mockResolvedValueOnce(mockJsonResponse([historyVersion]));
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "查看历史" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history?limit=50", undefined)
    );
    expect(await screen.findByRole("heading", { name: "历史与版本" })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-08", undefined);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-08/versions", undefined);
    expect(screen.getByText("- 推进 Phase 4A 历史搜索")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "待确认" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history?status=reviewing&limit=50", undefined)
    );

    fireEvent.click(screen.getByRole("button", { name: "返回今日" }));

    expect(await screen.findByLabelText("日记纸面")).toBeInTheDocument();
  });

  test("keeps newest selected history date when detail requests resolve out of order", async () => {
    const olderDate = {
      ...journalDate,
      value: "2026-05-09",
      isoDate: "2026-05-09",
      monthDay: "05-09",
      markdownFileName: "2026-05-09.md"
    };
    const newestDate = {
      ...journalDate,
      value: "2026-05-10",
      isoDate: "2026-05-10",
      monthDay: "05-10",
      markdownFileName: "2026-05-10.md"
    };
    const olderDetailDeferred = createDeferred<Response>();
    const olderVersionsDeferred = createDeferred<Response>();
    const newestDetailDeferred = createDeferred<Response>();
    const newestVersionsDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockResolvedValueOnce(mockJsonResponse({
        items: [
          historySummary,
          { ...historySummary, date: olderDate, hits: [{ ...historySummary.hits[0], snippet: "旧日期摘要" }] },
          { ...historySummary, date: newestDate, hits: [{ ...historySummary.hits[0], snippet: "新日期摘要" }] }
        ]
      }))
      .mockResolvedValueOnce(mockJsonResponse(historyDetail()))
      .mockResolvedValueOnce(mockJsonResponse([historyVersion]))
      .mockReturnValueOnce(olderDetailDeferred.promise)
      .mockReturnValueOnce(olderVersionsDeferred.promise)
      .mockReturnValueOnce(newestDetailDeferred.promise)
      .mockReturnValueOnce(newestVersionsDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "查看历史" }));
    expect(await screen.findByRole("button", { name: /2026-05-09/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /2026-05-09/ }));
    fireEvent.click(screen.getByRole("button", { name: /2026-05-10/ }));

    newestDetailDeferred.resolve(mockJsonResponse(historyDetail(newestDate, "- 最新日期详情")));
    newestVersionsDeferred.resolve(mockJsonResponse([]));

    expect(await screen.findByText("- 最新日期详情")).toBeInTheDocument();
    expect(screen.queryByText("- 旧日期详情")).not.toBeInTheDocument();

    await act(async () => {
      olderDetailDeferred.resolve(mockJsonResponse(historyDetail(olderDate, "- 旧日期详情")));
      olderVersionsDeferred.resolve(mockJsonResponse([]));
      await olderDetailDeferred.promise;
      await olderVersionsDeferred.promise;
    });

    expect(screen.getByText("- 最新日期详情")).toBeInTheDocument();
    expect(screen.queryByText("- 旧日期详情")).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-09", undefined);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-10", undefined);
  });

  test("clears stale history detail and version actions while a new selected date is loading", async () => {
    const nextDate = {
      ...journalDate,
      value: "2026-05-09",
      isoDate: "2026-05-09",
      monthDay: "05-09",
      markdownFileName: "2026-05-09.md"
    };
    const nextDetailDeferred = createDeferred<Response>();
    const nextVersionsDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockResolvedValueOnce(mockJsonResponse({
        items: [
          historySummary,
          { ...historySummary, date: nextDate, hits: [{ ...historySummary.hits[0], snippet: "新日期摘要" }] }
        ]
      }))
      .mockResolvedValueOnce(mockJsonResponse(historyDetail()))
      .mockResolvedValueOnce(mockJsonResponse([historyVersion]))
      .mockReturnValueOnce(nextDetailDeferred.promise)
      .mockReturnValueOnce(nextVersionsDeferred.promise)
      .mockResolvedValue(mockJsonResponse(createEditorState()));
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "查看历史" }));
    expect(await screen.findByText("- 推进 Phase 4A 历史搜索")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "恢复为草稿" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /2026-05-09/ }));

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-09", undefined);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-09/versions", undefined);
    expect(screen.queryByText("- 推进 Phase 4A 历史搜索")).not.toBeInTheDocument();
    const staleRestoreButton = screen.queryByRole("button", { name: "恢复为草稿" });
    if (staleRestoreButton) {
      fireEvent.click(staleRestoreButton);
    }

    expect(fetchMock).not.toHaveBeenCalledWith(
      "http://localhost:5057/journal/history/2026-05-09/versions/version-2026-05-08T09-30-00%2B08-00/restore-draft",
      { method: "POST" }
    );
    expect(staleRestoreButton).not.toBeInTheDocument();
  });

  test("restores selected history version to draft and returns to today editor", async () => {
    const restoredEditor = createEditorState({
      sections: [{
        id: "today-focus",
        title: "今日重点",
        content: "从历史版本恢复的草稿",
        kind: "required",
        isEditableInBlockMode: true
      }]
    });
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockResolvedValueOnce(mockJsonResponse({ items: [historySummary] }))
      .mockResolvedValueOnce(mockJsonResponse(historyDetail()))
      .mockResolvedValueOnce(mockJsonResponse([historyVersion]))
      .mockResolvedValueOnce(mockJsonResponse(restoredEditor));
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "查看历史" }));
    fireEvent.click(await screen.findByRole("button", { name: "恢复为草稿" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "http://localhost:5057/journal/history/2026-05-08/versions/version-2026-05-08T09-30-00%2B08-00/restore-draft",
        { method: "POST" }
      )
    );
    expect(await screen.findByLabelText("日记纸面")).toBeInTheDocument();
    expect(screen.getByText("从历史版本恢复的草稿")).toBeInTheDocument();
  });

  test("blocks history workbench while inline block edits are dirty", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "还没保存的历史前编辑" }
    });

    fireEvent.click(screen.getByRole("button", { name: "查看历史" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("先保存或取消当前编辑，再继续补充或重新整理。");
    expect(screen.getByLabelText("日记纸面")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("还没保存的历史前编辑");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/history?limit=50", undefined);
  });

  test("uses the top status dot for API health instead of exposing an API text pill", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    expect(await screen.findByLabelText("API 连接正常")).toHaveAttribute("title", "API 连接正常");
    expect(screen.getByLabelText("API 连接正常")).toHaveClass("api-health-ok");
    expect(screen.queryByText(/API ok/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/API checking/i)).not.toBeInTheDocument();
  });

  test("marks API health as abnormal through the status dot color", async () => {
    mockFetchSequence([
      { body: { ...healthResponse, status: "error" } },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    expect(await screen.findByLabelText("API 连接异常")).toHaveClass("api-health-error");
    expect(screen.queryByText(/API error/i)).not.toBeInTheDocument();
  });

  test("keeps current LLM provider only in the assistant meta strip", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    const llmStatus = await screen.findByLabelText("LLM：Mock");
    expect(llmStatus).toHaveTextContent("Mock");
    expect(llmStatus).not.toHaveTextContent("Mock 可用");
    expect(llmStatus).toHaveClass("assistant-meta-provider");
    expect(screen.queryByLabelText(/当前 LLM/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /打开 LLM 配置/ })).not.toBeInTheDocument();
  });

  test("shows DeepSeek LLM provider in the assistant meta strip", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: deepSeekAiSettings }
    ]);

    render(<App />);

    const llmStatus = await screen.findByLabelText("LLM：DeepSeek");
    expect(llmStatus).toHaveTextContent("DeepSeek");
    expect(llmStatus).not.toHaveTextContent("DeepSeek 可用");
    expect(llmStatus).toHaveClass("assistant-meta-provider");
  });

  test("shows unknown active LLM id as needing configuration instead of falling back to Mock", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: missingActiveAiSettings }
    ]);

    render(<App />);

    const llmStatus = await screen.findByLabelText("LLM：missing-provider");
    expect(llmStatus).toHaveTextContent("missing-provider");
    expect(llmStatus).not.toHaveTextContent("missing-provider 需要配置");
    expect(llmStatus).not.toHaveTextContent("missing-provider 可用");
    expect(llmStatus).toHaveClass("assistant-meta-provider");
    expect(screen.queryByLabelText("LLM：Mock")).not.toBeInTheDocument();
  });

  test("keeps LLM settings entry in the native menu instead of the top status strip", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    expect(await screen.findByLabelText("LLM：Mock")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /打开 LLM 配置/ })).not.toBeInTheDocument();
    await openLlmSettingsFromNativeMenu();

    await waitFor(() => expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument());
    expect(screen.getByRole("button", { name: /DeepSeek/ })).toBeInTheDocument();
    expect(screen.getByText("测试会向当前 LLM 发送一次最小请求，可能产生少量 token 消耗。")).toBeInTheDocument();
  });

  test("LLM settings shows provider configuration without style selector buttons", async () => {
    vi.stubGlobal("fetch", createInitialFetchMock());

    render(<App />);

    await screen.findByLabelText("LLM：Mock");
    await openLlmSettingsFromNativeMenu();

    const panel = screen.getByRole("region", { name: "LLM 配置面板" });
    expect(within(panel).queryByText("模型来源")).not.toBeInTheDocument();
    expect(within(panel).queryByText("选择今天用于整理日记的模型。")).not.toBeInTheDocument();
    expect(within(panel).getByText("连接信息")).toBeInTheDocument();
    expect(within(panel).getByText("配置来源")).toBeInTheDocument();
    expect(within(panel).getByText("最近诊断")).toBeInTheDocument();
    expect(within(panel).getByText("忠实整理")).toBeInTheDocument();
    expect(within(panel).getByLabelText("当前 LLM 标识 Mock")).toHaveTextContent("M");
    expect(within(panel).getByLabelText("Mock 标识")).toHaveTextContent("M");
    expect(within(panel).getByLabelText("DeepSeek 标识")).toHaveTextContent("D");
    expect(within(panel).getByRole("button", { name: "关闭" }).textContent).toBe("");
    expect(within(panel).getByRole("button", { name: "测试连接" }).textContent).toBe("");
    expect(within(panel).getByRole("button", { name: "保存并启用" }).textContent).toBe("");
    expect(within(panel).getByRole("button", { name: "展开高级参数" })).toHaveTextContent("temperature 0");
    expect(within(panel).getByText("高级参数")).toBeInTheDocument();
    expect(within(panel).getByText("max tokens 0")).toBeInTheDocument();
    expect(within(panel).getByText("timeout 1s")).toBeInTheDocument();
    expect(within(panel).getByText("JSON mode on")).toBeInTheDocument();
    expect(within(panel).queryByText("测试当前表单")).not.toBeInTheDocument();
    expect(within(panel).queryByText("保存并启用")).not.toBeInTheDocument();
    expect(within(panel).queryByText("展开")).not.toBeInTheDocument();
    expect(within(panel).queryByRole("button", { name: "轻度润色" })).not.toBeInTheDocument();
    expect(within(panel).queryByRole("button", { name: "结构优先" })).not.toBeInTheDocument();
  });

  test("LLM settings uses a modal scrim and expands technical advanced parameters", async () => {
    vi.stubGlobal("fetch", createInitialFetchMock());

    render(<App />);

    await screen.findByLabelText("LLM：Mock");
    await openLlmSettingsFromNativeMenu();

    expect(screen.getByTestId("llm-settings-backdrop")).toBeInTheDocument();
    const panel = screen.getByRole("region", { name: "LLM 配置面板" });
    fireEvent.click(within(panel).getByRole("button", { name: /DeepSeek/ }));
    expect(within(panel).getByText("max tokens 1200")).toBeInTheDocument();
    expect(within(panel).getByText("timeout 45s")).toBeInTheDocument();
    fireEvent.click(within(panel).getByRole("button", { name: "展开高级参数" }));

    expect(within(panel).getByRole("button", { name: "收起高级参数" })).toBeInTheDocument();
    const temperatureInput = within(panel).getByLabelText("Temperature");
    expect(temperatureInput).toHaveAttribute("title", "控制输出随机性；越低越稳定，越高越发散。");
    expect(within(panel).queryByText("控制输出随机性；越低越稳定，越高越发散。")).not.toBeInTheDocument();
    const maxTokensInput = within(panel).getByLabelText("Max tokens");
    expect(maxTokensInput).toHaveAttribute("title", "限制单次整理输出长度，避免内容过长。");
    expect(within(panel).queryByText("限制单次整理输出长度，避免内容过长。")).not.toBeInTheDocument();
    const timeoutInput = within(panel).getByLabelText("Timeout");
    expect(timeoutInput).toHaveAttribute("title", "请求等待时间，超过后停止本次调用。");
    expect(within(panel).queryByText("请求等待时间，超过后停止本次调用。")).not.toBeInTheDocument();
    const jsonModeStatus = within(panel).getByLabelText("JSON mode");
    expect(jsonModeStatus).toHaveTextContent("on");
    expect(jsonModeStatus).toHaveAttribute("title", "固定开启，确保模型返回结构化 JSON。");
    expect(within(panel).queryByText("固定开启，确保模型返回结构化 JSON。")).not.toBeInTheDocument();
  });

  test("tests provider and shows safe technical details", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      {
        body: {
          isSuccess: false,
          status: "unauthorized",
          safeResponseSnippet: "",
          httpStatus: 401,
          latency: "00:00:00.1200000",
          error: {
            stage: "provider-call",
            code: "unauthorized",
            message: "LLM rejected the API key.",
            technicalDetails: "httpStatus: 401 authorization: [redacted]"
          }
        }
      }
    ]);

    render(<App />);

    await screen.findByLabelText("LLM：Mock");
    await openLlmSettingsFromNativeMenu();
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "测试连接" }));

    expect(await screen.findByText("测试失败，配置没有保存")).toBeInTheDocument();
    expect(screen.getByText("LLM rejected the API key.")).toBeInTheDocument();
    expect(screen.getByText("技术详情")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/settings/ai/test", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        providerId: "deepseek",
        candidate: {
          activeProviderId: "deepseek",
          providers: [
            {
              id: "mock",
              type: "mock",
              displayName: "Mock",
              preset: "mock",
              baseUrl: "local",
              model: "mock-journal",
              apiKey: "",
              isEnabled: false,
              timeoutSeconds: 1,
              temperature: 0,
              maxTokens: 0,
              stylePreset: "faithful"
            },
            {
              id: "deepseek",
              type: "openai-compatible",
              displayName: "DeepSeek",
              preset: "deepseek",
              baseUrl: "https://api.deepseek.com",
              model: "deepseek-v4-flash",
              apiKey: "",
              isEnabled: true,
              timeoutSeconds: 45,
              temperature: 0.2,
              maxTokens: 1200,
              stylePreset: "faithful"
            }
          ]
        }
      })
    });
  });

  test("shows inline failure when activating LLM settings request fails", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { ok: false, status: 500, body: { error: "settings save failed" } }
    ]);

    render(<App />);

    await screen.findByLabelText("LLM：Mock");
    await openLlmSettingsFromNativeMenu();
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

    expect(await screen.findByText("测试失败，配置没有保存")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("settings save failed");
    expect(screen.getByRole("button", { name: "保存并启用" })).toBeEnabled();
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/settings/ai/activate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: expect.stringContaining('"activeProviderId":"deepseek"')
    });
  });

  test("ignores stale LLM settings save response when a later save wins", async () => {
    const firstSaveDeferred = createDeferred<Response>();
    const secondSaveDeferred = createDeferred<Response>();
    const successfulActivation = {
      saved: true,
      settings: aiSettings,
      testResult: {
        isSuccess: true,
        status: "success",
        safeResponseSnippet: "{\"ok\":true}",
        httpStatus: 200,
        latency: "00:00:00.0100000",
        error: null
      }
    };
    const successfulDeepSeekActivation = {
      ...successfulActivation,
      settings: deepSeekAiSettings
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(firstSaveDeferred.promise)
      .mockReturnValueOnce(secondSaveDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    await screen.findByLabelText("LLM：Mock");
    await openLlmSettingsFromNativeMenu();
    const providerList = within(screen.getByRole("navigation", { name: "LLM 列表" }));
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

    fireEvent.click(providerList.getByRole("button", { name: /Mock/ }));
    fireEvent.submit(screen.getByRole("button", { name: "保存并启用" }).closest("form")!);

    secondSaveDeferred.resolve(mockJsonResponse(successfulActivation));
    await waitFor(() => expect(screen.getByLabelText("LLM：Mock")).toBeInTheDocument());

    firstSaveDeferred.resolve(mockJsonResponse(successfulDeepSeekActivation));
    await Promise.resolve();

    expect(screen.getByLabelText("LLM：Mock")).toBeInTheDocument();
    expect(screen.queryByLabelText("LLM：DeepSeek")).not.toBeInTheDocument();
  });

  test("regenerates draft after confirmation prompt", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { body: reviewingToday },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "重新整理" }));
    const dialog = screen.getByRole("dialog", { name: "重新整理草稿" });
    expect(within(dialog).getByText("这会覆盖当前草稿内容，但不会影响正式日记。")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "确认重新整理" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ providerId: null })
      })
    );
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", undefined));
  });

  test("keeps regenerated editor refresh when LLM settings refresh fails", async () => {
    const refreshedEditor = createEditorState({
      markdown: editorMarkdown.replace("推进 Phase 3", "重新生成后的重点"),
      sections: [
        {
          id: "today-focus",
          title: "今日重点",
          content: "重新生成后的重点",
          kind: "required",
          isEditableInBlockMode: true
        }
      ]
    });
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { body: reviewingToday },
      { body: refreshedEditor },
      { ok: false, status: 500, body: { error: "settings refresh failed" } }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "重新整理" }));
    fireEvent.click(within(screen.getByRole("dialog", { name: "重新整理草稿" })).getByRole("button", { name: "确认重新整理" }));

    await waitFor(() => expect(screen.getByText("重新生成后的重点")).toBeInTheDocument());
    expect(screen.getByRole("alert")).toHaveTextContent("settings refresh failed");
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", undefined);
  });

  test("shows regenerate draft action on today page instead of LLM settings panel", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    expect(await screen.findByRole("button", { name: "重新整理" })).toBeInTheDocument();
    await openLlmSettingsFromNativeMenu();

    expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
    expect(
      within(screen.getByRole("region", { name: "LLM 配置面板" }))
        .queryByRole("button", { name: "重新整理草稿" })
    ).not.toBeInTheDocument();
  });

  test("regenerates draft from today page after confirmation", async () => {
    const fetchMock = createInitialFetchMock()
      .mockResolvedValueOnce(mockJsonResponse(reviewingToday))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings));
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const button = await screen.findByRole("button", { name: "重新整理" });
    fireEvent.click(button);
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();

    fireEvent.click(within(screen.getByRole("dialog", { name: "重新整理草稿" })).getByRole("button", { name: "确认重新整理" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ providerId: null })
    }));
  });

  test("resets regenerate confirmation after raw input changes", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const regenerateButton = await screen.findByRole("button", { name: "重新整理" });
    fireEvent.click(regenerateButton);
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("补充今天的自然语言输入"), {
      target: { value: "补充一点新上下文" }
    });

    expect(screen.queryByRole("dialog", { name: "重新整理草稿" })).not.toBeInTheDocument();

    fireEvent.click(regenerateButton);

    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", expect.anything());
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();
  });

  test("resets regenerate confirmation after toggling LLM panel", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const regenerateButton = await screen.findByRole("button", { name: "重新整理" });
    fireEvent.click(regenerateButton);
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();

    await openLlmSettingsFromNativeMenu();
    fireEvent.click(screen.getByRole("button", { name: "关闭" }));

    expect(screen.queryByRole("dialog", { name: "重新整理草稿" })).not.toBeInTheDocument();

    fireEvent.click(regenerateButton);

    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", expect.anything());
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();
  });

  test("editing journal content resets regenerate confirmation and blocks refresh while dirty", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const regenerateButton = await screen.findByRole("button", { name: "重新整理" });
    fireEvent.click(regenerateButton);
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "补充新的今日重点" }
    });

    expect(screen.queryByRole("dialog", { name: "重新整理草稿" })).not.toBeInTheDocument();
    expect(regenerateButton).toBeDisabled();

    fireEvent.click(regenerateButton);

    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", expect.anything());
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("补充新的今日重点");

    fireEvent.click(screen.getByRole("button", { name: "取消" }));

    expect(regenerateButton).toBeEnabled();
  });

  test("dirty inline block status disables formal save until cancel", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "还没保存的本地改动" }
    });

    expect(screen.getAllByText("有未保存修改").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "保存日记" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "取消" }));

    expect(screen.getAllByText("可保存").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "保存日记" })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  test("dirty inline block blocks raw input submit and regeneration refresh", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "还没保存的本地改动" }
    });
    fireEvent.change(screen.getByLabelText("补充今天的自然语言输入"), {
      target: { value: "这条 raw input 不能刷新掉本地编辑" }
    });

    expect(screen.getByRole("button", { name: "生成草稿" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "重新整理" })).toBeDisabled();

    const inputForm = screen.getByLabelText("补充今天的自然语言输入").closest("form");
    if (!inputForm) {
      throw new Error("Expected raw input form");
    }

    fireEvent.submit(inputForm);
    fireEvent.click(screen.getByRole("button", { name: "重新整理" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("先保存或取消当前编辑，再继续补充或重新整理。");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("还没保存的本地改动");
  });

  test("inserting optional block resets regenerate confirmation", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const regenerateButton = await screen.findByRole("button", { name: "重新整理" });
    fireEvent.click(regenerateButton);
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "添加 情绪感受" }));

    expect(screen.queryByRole("dialog", { name: "重新整理草稿" })).not.toBeInTheDocument();

    fireEvent.click(regenerateButton);

    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", expect.anything());
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();
  });

  test("blank raw input submit resets regenerate confirmation", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const regenerateButton = await screen.findByRole("button", { name: "重新整理" });
    fireEvent.click(regenerateButton);
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("请输入一段今天的自然语言内容。");
    expect(screen.queryByRole("dialog", { name: "重新整理草稿" })).not.toBeInTheDocument();

    fireEvent.click(regenerateButton);

    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", expect.anything());
    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();
  });

  test("submits raw input through harness run and refreshes from today editor state after SSE completion", async () => {
    const eventSource = createEventSourceMock();
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      {
        body: createEditorState({
          status: "empty",
          markdown: "",
          sections: [],
          availableOptionalSections: [],
          canConfirm: false,
          today: emptyToday
        })
      },
      { body: aiSettings },
      { body: { today: emptyToday, run: queuedHarnessRun } },
      { body: createEditorState() }
    ]);

    render(<App />);

    const input = await screen.findByLabelText("补充今天的自然语言输入");
    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    await waitFor(() =>
      expect(eventSource.EventSourceMock).toHaveBeenCalledWith("http://localhost:5057/journal/harness/runs/run-1/events")
    );
    eventSource.emit("run-completed", {
      type: "run-completed",
      runId: "run-1",
      status: "reviewing",
      message: "done"
    });

    await waitFor(() =>
      expect(screen.getAllByText("可保存").length).toBeGreaterThan(0)
    );
    expect(screen.getByRole("button", { name: "保存日记" })).toBeInTheDocument();
    expect(screen.getByText("推进 Phase 3")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/harness/runs", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ text: "今天完成 Phase 2 API 连接", source: "text" })
    });
    expect(fetchMock).toHaveBeenNthCalledWith(5, "http://localhost:5057/journal/today/editor", undefined);
  });

  test("disables input action while draft generation and editor refresh are pending", async () => {
    const eventSource = createEventSourceMock();
    const startHarnessDeferred = createDeferred<Response>();
    const refreshedEditorDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState({
        status: "empty",
        markdown: "",
        sections: [],
        availableOptionalSections: [],
        canConfirm: false,
        today: emptyToday
      })))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(startHarnessDeferred.promise)
      .mockReturnValueOnce(refreshedEditorDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const input = await screen.findByLabelText("补充今天的自然语言输入");
    const submitButton = screen.getByRole("button", { name: "生成草稿" });
    expect(submitButton).toBeEnabled();

    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(submitButton);

    expect(submitButton).toBeDisabled();

    startHarnessDeferred.resolve(mockJsonResponse({ today: emptyToday, run: queuedHarnessRun }));
    await waitFor(() =>
      expect(eventSource.EventSourceMock).toHaveBeenCalledWith("http://localhost:5057/journal/harness/runs/run-1/events")
    );
    expect(submitButton).toBeDisabled();

    eventSource.emit("run-completed", {
      type: "run-completed",
      runId: "run-1",
      status: "reviewing",
      message: "done"
    });
    refreshedEditorDeferred.resolve(mockJsonResponse(createEditorState()));

    await waitFor(() =>
      expect(screen.getAllByText("可保存").length).toBeGreaterThan(0)
    );
    expect(submitButton).toBeEnabled();
  });

  test("shows validation message for empty input", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState({
        status: "empty",
        markdown: "",
        sections: [],
        availableOptionalSections: [],
        canConfirm: false,
        today: emptyToday
      }) },
      { body: aiSettings }
    ]);

    render(<App />);

    await screen.findByLabelText("补充今天的自然语言输入");
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    expect(await screen.findByText("请输入一段今天的自然语言内容。")).toBeInTheDocument();
  });

  test("keeps api error visible when empty input validation fails", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState({
        status: "empty",
        markdown: "",
        sections: [],
        availableOptionalSections: [],
        canConfirm: false,
        today: emptyToday
      }) },
      { body: aiSettings },
      { ok: false, status: 500, body: { error: "submit failed" } }
    ]);

    render(<App />);

    const input = await screen.findByLabelText("补充今天的自然语言输入");
    fireEvent.change(input, { target: { value: "今天 API 会失败" } });
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    expect(await screen.findByText("submit failed")).toBeInTheDocument();

    fireEvent.change(input, { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    expect(screen.getByText("submit failed")).toBeInTheDocument();
    expect(screen.getByText("请输入一段今天的自然语言内容。")).toBeInTheDocument();
  });

  test("shows needs-attention productized state without confirm action", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState({
        status: "attention",
        validation: {
          isValid: false,
          issues: [
            {
              code: "missing-title",
              message: "title is required",
              repairHint: "补齐标题后再确认。"
            }
          ]
        },
        canConfirm: false,
        today: attentionToday
      }) },
      { body: aiSettings }
    ]);

    render(<App />);

    expect((await screen.findAllByText("需要处理")).length).toBeGreaterThan(0);
    expect(screen.getAllByText("正式日记没有被覆盖，原始表达仍然保留。").length).toBeGreaterThan(0);
    expect(screen.getAllByText("title is required").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "确认写入正式日记" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "保存日记" })).not.toBeInTheDocument();
  });

  test("keeps regenerate confirmation and action together in needs-attention state", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState({
        status: "attention",
        validation: {
          isValid: false,
          issues: [
            {
              code: "missing-title",
              message: "title is required",
              repairHint: "补齐标题后再确认。"
            }
          ]
        },
        canConfirm: false,
        today: attentionToday
      }) },
      { body: aiSettings }
    ]);

    render(<App />);

    expect((await screen.findAllByText("正式日记没有被覆盖，原始表达仍然保留。")).length).toBeGreaterThan(0);

    expect(screen.getAllByRole("button", { name: "重新整理" })).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: "重新整理" }));

    expect(screen.getByRole("dialog", { name: "重新整理草稿" })).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "重新整理" })).toHaveLength(1);
  });

  test("confirms draft and refreshes from today editor state", async () => {
    const entryPath = "C:\\Journal\\entries\\2026\\05\\2026-05-08.md";
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { body: processedToday(entryPath) },
      {
        body: createEditorState({
          status: "processed",
          canConfirm: false,
          today: processedToday(entryPath)
        })
      }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "保存日记" }));

    await waitFor(() =>
      expect(screen.getAllByText("已保存").length).toBeGreaterThan(0)
    );
    expect(screen.getByText(entryPath)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(
      4,
      "http://localhost:5057/journal/today/draft/confirm",
      { method: "POST" }
    );
    expect(fetchMock).toHaveBeenNthCalledWith(5, "http://localhost:5057/journal/today/editor", undefined);
  });

  test("disables confirm while draft confirmation and editor refresh are pending", async () => {
    const confirmDeferred = createDeferred<Response>();
    const refreshedEditorDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(confirmDeferred.promise)
      .mockReturnValueOnce(refreshedEditorDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const confirmButton = await screen.findByRole("button", { name: "保存日记" });
    expect(confirmButton).toBeEnabled();

    fireEvent.click(confirmButton);

    expect(confirmButton).toBeDisabled();

    const entryPath = "C:\\Journal\\entries\\2026\\05\\2026-05-08.md";
    confirmDeferred.resolve(mockJsonResponse(processedToday(entryPath)));
    await Promise.resolve();
    expect(confirmButton).toBeDisabled();

    refreshedEditorDeferred.resolve(mockJsonResponse(createEditorState({
      status: "processed",
      canConfirm: false,
      today: processedToday(entryPath)
    })));

    await waitFor(() =>
      expect(screen.getAllByText("已保存").length).toBeGreaterThan(0)
    );
    expect(screen.getByText(entryPath)).toBeInTheDocument();
  });

  test("saves block edits through editor endpoint and updates editor state", async () => {
    const updatedEditor = createEditorState({
      markdown: editorMarkdown.replace("推进 Phase 3", "保存后的区块内容"),
      sections: [
        {
          id: "today-focus",
          title: "今日重点",
          content: "保存后的区块内容",
          kind: "required",
          isEditableInBlockMode: true
        }
      ]
    });
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { body: updatedEditor }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    const focusEditor = screen.getByRole("textbox", { name: "编辑 今天想推进" });
    fireEvent.change(focusEditor, { target: { value: "保存后的区块内容" } });
    expect(screen.getAllByText("有未保存修改").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "保存日记" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "保存修改" }));

    await waitFor(() =>
      expect(screen.getAllByText("可保存").length).toBeGreaterThan(0)
    );
    expect(screen.getByText("保存后的区块内容")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "保存日记" })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/editor/blocks", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ sections: [{ id: "today-focus", content: "保存后的区块内容" }] })
    });
  });

  test("keeps failed block save inline dirty and unavailable for formal save", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { ok: false, status: 500, body: { error: "block save failed" } }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "保存失败时不要丢" }
    });
    fireEvent.click(screen.getByRole("button", { name: "保存修改" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("block save failed");
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("保存失败时不要丢");
    expect(screen.getAllByText("有未保存修改").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "保存日记" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "确认保存" })).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  test("disables block actions and confirm actions while selected block save is pending", async () => {
    const blockSaveDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(blockSaveDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    const saveButton = screen.getByRole("button", { name: "保存修改" });
    const focusEditor = screen.getByRole("textbox", { name: "编辑 今天想推进" });
    const confirmButton = screen.getByRole("button", { name: "保存日记" });

    fireEvent.click(saveButton);

    expect(saveButton).toBeDisabled();
    expect(focusEditor).toBeDisabled();
    expect(confirmButton).toBeDisabled();
    expect(screen.getByRole("button", { name: "添加 情绪感受" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: "编辑 今天想推进" })).not.toBeInTheDocument();

    blockSaveDeferred.resolve(mockJsonResponse(createEditorState()));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: "编辑 今天想推进" })).toBeEnabled()
    );
    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
    expect(confirmButton).toBeEnabled();
  });

  test("blocks raw input refresh while inline dirty so local block edits stay visible", async () => {
    const fetchMock = createInitialFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "编辑 今天想推进" }));
    const input = screen.getByLabelText("补充今天的自然语言输入");
    const inputForm = input.closest("form");
    if (!inputForm) {
      throw new Error("Expected raw input form");
    }

    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "准备保存的本地内容" }
    });
    fireEvent.change(input, { target: { value: "后发起的 raw input 刷新" } });
    fireEvent.submit(inputForm);

    expect(await screen.findByRole("alert")).toHaveTextContent("先保存或取消当前编辑，再继续补充或重新整理。");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("准备保存的本地内容");
  });

  test("restores mocked fetch between tests", () => {
    expect(fetch).not.toHaveProperty("mock");
  });
});

describe("JournalEditor", () => {
  test("reading-first preview shows product title and hides inline textarea by default", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    expect(screen.getByRole("heading", { name: "今天想推进" })).toBeInTheDocument();
    expect(screen.getByText("推进 Phase 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "编辑 今天想推进" })).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
  });

  test("reading preview collapses repeated blank lines from generated content", () => {
    const { container } = render(
      <JournalEditor
        editor={createEditorState({
          sections: [
            {
              id: "today-focus",
              title: "今日重点",
              content: "- 今天可能较早下班\n\n\n\n- 测试新整理的接口\n\n\n- 检查 DeepSeek bug",
              kind: "required",
              isEditableInBlockMode: true
            }
          ]
        })}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    const previewLines = [...container.querySelectorAll(".journal-block-readonly p")]
      .map(line => line.textContent);

    expect(previewLines).toEqual([
      "- 今天可能较早下班",
      "\u00a0",
      "- 测试新整理的接口",
      "\u00a0",
      "- 检查 DeepSeek bug"
    ]);
  });

  test("selected block save only submits the inline block being edited", () => {
    const onSaveBlocks = vi.fn();
    render(
      <JournalEditor
        editor={createEditorState({
          sections: [
            {
              id: "today-focus",
              title: "今日重点",
              content: "推进 Phase 3",
              kind: "required",
              isEditableInBlockMode: true
            },
            {
              id: "yesterday-review",
              title: "昨日回顾",
              content: "昨天完成 Phase 2",
              kind: "required",
              isEditableInBlockMode: true
            }
          ]
        })}
        isBusy={false}
        onSaveBlocks={onSaveBlocks}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    const focusEditor = screen.getByRole("textbox", { name: "编辑 今天想推进" });
    fireEvent.change(focusEditor, { target: { value: "完成前端编辑器组件" } });
    fireEvent.click(screen.getByRole("button", { name: "保存修改" }));

    expect(onSaveBlocks).toHaveBeenCalledTimes(1);
    expect(onSaveBlocks).toHaveBeenCalledWith([
      { id: "today-focus", content: "完成前端编辑器组件" }
    ]);
    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("完成前端编辑器组件");
  });

  test("switching sections keeps the dirty inline block active until it is saved or canceled", () => {
    render(
      <JournalEditor
        editor={createEditorState({
          sections: [
            {
              id: "today-focus",
              title: "今日重点",
              content: "推进 Phase 3",
              kind: "required",
              isEditableInBlockMode: true
            },
            {
              id: "yesterday-review",
              title: "昨日回顾",
              content: "昨天完成 Phase 2",
              kind: "required",
              isEditableInBlockMode: true
            }
          ]
        })}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "A 段未保存内容" }
    });

    const nextSectionEditButton = screen.getByRole("button", { name: "编辑 昨天回顾" });
    const insertButton = screen.getByRole("button", { name: "添加 情绪感受" });
    expect(nextSectionEditButton).toBeDisabled();
    expect(insertButton).toBeDisabled();

    fireEvent.click(nextSectionEditButton);
    fireEvent.click(insertButton);

    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("A 段未保存内容");
    expect(screen.queryByRole("textbox", { name: "编辑 昨天回顾" })).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 情绪感受" })).not.toBeInTheDocument();
    expect(screen.getByText("昨天完成 Phase 2")).toBeInTheDocument();
  });

  test("cancels inline block editing and restores preview text", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "临时改动，不保存" }
    });
    fireEvent.click(screen.getByRole("button", { name: "取消" }));

    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
    expect(screen.getByText("推进 Phase 3")).toBeInTheDocument();
    expect(screen.queryByText("临时改动，不保存")).not.toBeInTheDocument();
  });

  test("assistant-friendly system raw block is collapsed until expanded", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    expect(screen.getByRole("heading", { name: "今日材料" })).toBeInTheDocument();
    expect(screen.queryByText("今天要保留原始表达")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "编辑 今日材料" })).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 今日材料" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "展开 今日材料" })).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(screen.getByRole("button", { name: "展开 今日材料" }));

    expect(screen.getByText("今天要保留原始表达")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "收起 今日材料" })).toHaveAttribute("aria-expanded", "true");
  });

  test("shows 添加 情绪感受 for optional inserts and opens the new inline block", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "添加 情绪感受" }));

    expect(screen.getByRole("textbox", { name: "编辑 情绪感受" })).toBeInTheDocument();
  });

  test("canceling a temporary optional inline block removes it and restores insert action", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "添加 情绪感受" }));
    expect(screen.getByRole("textbox", { name: "编辑 情绪感受" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "取消" }));

    expect(screen.queryByRole("heading", { name: "情绪感受" })).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 情绪感受" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "添加 情绪感受" })).toBeInTheDocument();
  });

  test("keeps inserted optional blocks in catalog order after required sections", () => {
    render(
      <JournalEditor
        editor={createEditorState({
          sections: [
            {
              id: "raw-inputs",
              title: "原始输入",
              content: "今天要保留原始表达",
              kind: "system",
              isEditableInBlockMode: false
            },
            {
              id: "yesterday-review",
              title: "昨日回顾",
              content: "昨天完成 Phase 2",
              kind: "required",
              isEditableInBlockMode: true
            },
            {
              id: "today-focus",
              title: "今日重点",
              content: "推进 Phase 3",
              kind: "required",
              isEditableInBlockMode: true
            }
          ],
          availableOptionalSections: [
            {
              id: "work",
              title: "工作推进",
              order: 5,
              kind: "optionalSingleton",
              isEditableInBlockMode: true
            }
          ]
        })}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "添加 工作推进" }));

    expect(screen.getAllByRole("heading", { level: 2 }).map(heading => heading.textContent)).toEqual([
      "今日材料",
      "昨天回顾",
      "今天想推进",
      "工作推进"
    ]);
  });

  test("keeps optional insert action before the section list", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    const insertButton = screen.getByRole("button", { name: "添加 情绪感受" });
    const firstSection = screen.getByRole("region", { name: "今天想推进" });

    expect(insertButton.compareDocumentPosition(firstSection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  test("resets local block edits when the authoritative editor snapshot changes", () => {
    const initialEditor = createEditorState();
    const refreshedEditor = createEditorState({
      status: "processed",
      markdown: initialEditor.markdown,
      sections: initialEditor.sections,
      canConfirm: false,
      today: processedToday()
    });
    const { rerender } = render(
      <JournalEditor
        editor={initialEditor}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "未保存的本地区块内容" }
    });

    rerender(
      <JournalEditor
        editor={refreshedEditor}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
    expect(screen.getByText("推进 Phase 3")).toBeInTheDocument();
    expect(screen.queryByDisplayValue("未保存的本地区块内容")).not.toBeInTheDocument();
  });

  test("does not expose source editing in the daily editor", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    expect(screen.queryByRole("tablist")).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "区块模式" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "源码模式" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "展开高级源码" })).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑完整 JMF Markdown" })).not.toBeInTheDocument();
  });

  test("shows needs-attention explanation with validation issue message and repair hint", () => {
    render(
      <JournalEditor
        editor={createEditorState({
          status: "attention",
          validation: {
            isValid: false,
            issues: [
              {
                code: "missing-section",
                message: "缺少今日重点区块",
                repairHint: "请补回 today-focus 区块后再保存。"
              }
            ]
          }
        })}
        isBusy={false}
        onSaveBlocks={vi.fn()}
      />
    );

    expect(screen.getByRole("region", { name: "需要处理" })).toHaveClass(
      "attention-panel",
      "productized-attention-panel"
    );
    expect(screen.getByText("这篇草稿需要处理")).toBeInTheDocument();
    expect(screen.getByText("需要处理")).toBeInTheDocument();
    expect(screen.getByText("正式日记没有被覆盖，原始表达仍然保留。")).toBeInTheDocument();
    expect(screen.getByText("缺少今日重点区块")).toBeInTheDocument();
    expect(screen.getByText("请补回 today-focus 区块后再保存。")).toBeInTheDocument();
  });

  test("disables editable block actions while busy", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={true}
        onSaveBlocks={vi.fn()}
      />
    );

    expect(screen.getByRole("button", { name: "编辑 今天想推进" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "添加 情绪感受" })).toBeDisabled();
  });
});

describe("LlmSettingsPanel", () => {
  test("shows productized provider state and key status", () => {
    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={vi.fn()}
        onRevealApiKey={vi.fn()}
      />
    );

    expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
    expect(screen.getByText("本地备用")).toBeInTheDocument();
    expect(screen.getByText("默认预设")).toBeInTheDocument();
    expect(screen.getByText("无需 API Key")).toBeInTheDocument();
    expect(screen.queryByText("key ready")).not.toBeInTheDocument();
    expect(screen.queryByText("no key")).not.toBeInTheDocument();
  });

  test("shows file-backed key as masked preview and reveals on eye click", async () => {
    const fileSettings = {
      ...aiSettings,
      activeProviderId: "deepseek",
      providers: aiSettings.providers.map(provider =>
        provider.id === "deepseek"
          ? {
              ...provider,
              isActive: true,
              hasApiKey: true,
              source: "file",
              apiKeyPreview: "sk-••••••••••••••••4A7C",
              canRevealApiKey: true
            }
          : { ...provider, isActive: false }
      )
    };
    const onRevealApiKey = vi.fn().mockResolvedValue({
      providerId: "deepseek",
      source: "file",
      apiKey: "sk-file-backed-secret-4A7C"
    });

    render(
      <LlmSettingsPanel
        settings={fileSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={vi.fn()}
        onRevealApiKey={onRevealApiKey}
      />
    );

    expect(screen.getByPlaceholderText("sk-••••••••••••••••4A7C")).toHaveValue("");

    fireEvent.click(screen.getByRole("button", { name: "查看 API Key" }));

    expect(await screen.findByDisplayValue("sk-file-backed-secret-4A7C")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "隐藏 API Key" })).toBeInTheDocument();
    expect(onRevealApiKey).toHaveBeenCalledWith("deepseek");
  });

  test("shows environment key as loaded and not revealable", () => {
    const envSettings = {
      ...aiSettings,
      activeProviderId: "openai",
      providers: [
        ...aiSettings.providers.map(provider => ({ ...provider, isActive: false })),
        {
          id: "openai",
          type: "openai-compatible",
          displayName: "OpenAI",
          preset: "openai",
          baseUrl: "https://api.openai.com/v1",
          model: "gpt-4.1-mini",
          isEnabled: true,
          isActive: true,
          hasApiKey: true,
          apiKeyPreview: "",
          canRevealApiKey: false,
          source: "environment",
          timeoutSeconds: 45,
          temperature: 0.2,
          maxTokens: 1200,
          stylePreset: "faithful",
          lastTestStatus: "not-tested"
        }
      ]
    };

    render(
      <LlmSettingsPanel
        settings={envSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={vi.fn()}
        onRevealApiKey={vi.fn()}
      />
    );

    expect(screen.getByText("已从环境变量加载，不在界面显示")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "查看 API Key" })).not.toBeInTheDocument();
    expect(screen.getByTestId("environment-key-lock")).toBeInTheDocument();
  });

  test("shows file-backed key reveal when only non-key fields come from environment", async () => {
    const partialEnvironmentSettings = {
      ...aiSettings,
      activeProviderId: "openai",
      providers: [
        ...aiSettings.providers.map(provider => ({ ...provider, isActive: false })),
        {
          id: "openai",
          type: "openai-compatible",
          displayName: "OpenAI",
          preset: "openai",
          baseUrl: "https://api.openai.com/v1",
          model: "gpt-5.4-env",
          isEnabled: true,
          isActive: true,
          hasApiKey: true,
          apiKeyPreview: "sk-••••••••••••••••9D2A",
          canRevealApiKey: true,
          source: "environment",
          timeoutSeconds: 45,
          temperature: 0.2,
          maxTokens: 1200,
          stylePreset: "faithful",
          lastTestStatus: "not-tested"
        }
      ]
    };
    const onRevealApiKey = vi.fn().mockResolvedValue({
      providerId: "openai",
      source: "file",
      apiKey: "sk-file-secret-9D2A"
    });

    render(
      <LlmSettingsPanel
        settings={partialEnvironmentSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={vi.fn()}
        onRevealApiKey={onRevealApiKey}
      />
    );

    expect(screen.queryByText("已从环境变量加载，不在界面显示")).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText("sk-••••••••••••••••9D2A")).toHaveValue("");

    fireEvent.click(screen.getByRole("button", { name: "查看 API Key" }));

    expect(await screen.findByDisplayValue("sk-file-secret-9D2A")).toBeInTheDocument();
    expect(onRevealApiKey).toHaveBeenCalledWith("openai");
  });

  test("keeps masked api key preview out of editable value", async () => {
    const fileBackedDeepSeekSettings = {
      ...aiSettings,
      activeProviderId: "deepseek",
      providers: aiSettings.providers.map(provider =>
        provider.id === "deepseek"
          ? {
              ...provider,
              isEnabled: true,
              isActive: true,
              hasApiKey: true,
              source: "file",
              apiKeyPreview: "sk-••••••••••••••••4A7C",
              canRevealApiKey: true
            }
          : { ...provider, isActive: false }
      )
    };
    const onTest = vi.fn().mockResolvedValue({
      isSuccess: true,
      status: "success",
      safeResponseSnippet: "{\"ok\":true}",
      httpStatus: 200,
      latency: "00:00:00.1200000",
      error: null
    });

    render(
      <LlmSettingsPanel
        settings={fileBackedDeepSeekSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={onTest}
        onRevealApiKey={vi.fn()}
      />
    );

    expect(screen.getByPlaceholderText("sk-••••••••••••••••4A7C")).toHaveValue("");

    fireEvent.change(screen.getByLabelText("API Key"), { target: { value: "sk-new-secret-value" } });
    fireEvent.click(screen.getByRole("button", { name: "测试连接" }));

    await waitFor(() => expect(onTest).toHaveBeenCalled());
    const candidate = onTest.mock.calls[0][1];
    expect(candidate.providers.find((provider: AiProviderSaveRequest) => provider.id === "deepseek").apiKey).toBe("sk-new-secret-value");
  });

  test("editing revealed api key keeps displayed value and candidate in sync", async () => {
    const fileBackedDeepSeekSettings = {
      ...aiSettings,
      activeProviderId: "deepseek",
      providers: aiSettings.providers.map(provider =>
        provider.id === "deepseek"
          ? {
              ...provider,
              isEnabled: true,
              isActive: true,
              hasApiKey: true,
              source: "file",
              apiKeyPreview: "sk-••••••••••••••••4A7C",
              canRevealApiKey: true
            }
          : { ...provider, isActive: false }
      )
    };
    const onRevealApiKey = vi.fn().mockResolvedValue({
      providerId: "deepseek",
      source: "file",
      apiKey: "sk-file-backed-secret-4A7C"
    });
    const onTest = vi.fn().mockResolvedValue({
      isSuccess: true,
      status: "success",
      safeResponseSnippet: "{\"ok\":true}",
      httpStatus: 200,
      latency: "00:00:00.1200000",
      error: null
    });

    render(
      <LlmSettingsPanel
        settings={fileBackedDeepSeekSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={onTest}
        onRevealApiKey={onRevealApiKey}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "查看 API Key" }));

    const apiKeyInput = await screen.findByDisplayValue("sk-file-backed-secret-4A7C");
    fireEvent.change(apiKeyInput, { target: { value: "sk-revealed-edited-secret" } });

    expect(screen.getByDisplayValue("sk-revealed-edited-secret")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "测试连接" }));

    await waitFor(() => expect(onTest).toHaveBeenCalled());
    const candidate = onTest.mock.calls[0][1];
    expect(candidate.providers.find((provider: AiProviderSaveRequest) => provider.id === "deepseek").apiKey).toBe("sk-revealed-edited-secret");
  });

  test("tests current form with candidate settings and marks old result stale after edits", async () => {
    const onTest = vi.fn().mockResolvedValue({
      isSuccess: true,
      status: "success",
      safeResponseSnippet: "{\"ok\":true}",
      httpStatus: 200,
      latency: "00:00:00.1200000",
      error: null
    });

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={onTest}
        onRevealApiKey={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.change(screen.getByLabelText("模型"), { target: { value: "deepseek-next" } });
    fireEvent.click(screen.getByRole("button", { name: "测试连接" }));

    await waitFor(() => expect(onTest).toHaveBeenCalled());
    expect(onTest.mock.calls[0][0]).toBe("deepseek");
    expect(onTest.mock.calls[0][1].providers.find((provider: AiProviderSaveRequest) => provider.id === "deepseek").model).toBe("deepseek-next");
    expect(await screen.findByText("连接测试通过")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("模型"), { target: { value: "deepseek-latest" } });

    expect(screen.getByText("测试结果已过期")).toBeInTheDocument();
  });

  test("save and activate keeps panel open and does not switch provider on failed activation", async () => {
    const onActivate = vi.fn().mockResolvedValue({
      saved: false,
      settings: aiSettings,
      testResult: {
        isSuccess: false,
        status: "missing_api_key",
        safeResponseSnippet: "",
        httpStatus: null,
        latency: null,
        error: {
          stage: "configuration",
          code: "missing_api_key",
          message: "LLM API key is required.",
          technicalDetails: "Provider key is empty."
        }
      }
    });

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={onActivate}
        onTest={vi.fn()}
        onRevealApiKey={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

    expect(await screen.findByText("测试失败，配置没有保存")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "DeepSeek" })).toBeInTheDocument();
  });

  test("save and activate success shows today-page next step", async () => {
    const activatedSettings = {
      ...aiSettings,
      activeProviderId: "deepseek",
      providers: aiSettings.providers.map(provider =>
        provider.id === "deepseek"
          ? { ...provider, isActive: true, hasApiKey: true, source: "file", apiKeyPreview: "sk-••••••••••••••••4A7C", canRevealApiKey: true }
          : { ...provider, isActive: false }
      )
    };
    const onActivate = vi.fn().mockResolvedValue({
      saved: true,
      settings: activatedSettings,
      testResult: {
        isSuccess: true,
        status: "success",
        safeResponseSnippet: "{\"ok\":true}",
        httpStatus: 200,
        latency: "00:00:00.1200000",
        error: null
      }
    });

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={onActivate}
        onTest={vi.fn()}
        onRevealApiKey={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

    expect(await screen.findByText("可以回到今日页重新整理")).toBeInTheDocument();
  });

  test("save and activate clears manually entered api key after success", async () => {
    const fileBackedDeepSeekSettings = {
      ...aiSettings,
      activeProviderId: "deepseek",
      providers: aiSettings.providers.map(provider =>
        provider.id === "deepseek"
          ? {
              ...provider,
              isEnabled: true,
              isActive: true,
              hasApiKey: true,
              source: "file",
              apiKeyPreview: "sk-••••••••••••••••4A7C",
              canRevealApiKey: true
            }
          : { ...provider, isActive: false }
      )
    };
    const onActivate = vi.fn().mockResolvedValue({
      saved: true,
      settings: fileBackedDeepSeekSettings,
      testResult: {
        isSuccess: true,
        status: "success",
        safeResponseSnippet: "{\"ok\":true}",
        httpStatus: 200,
        latency: "00:00:00.1200000",
        error: null
      }
    });

    render(
      <LlmSettingsPanel
        settings={fileBackedDeepSeekSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={onActivate}
        onTest={vi.fn()}
        onRevealApiKey={vi.fn()}
      />
    );

    const apiKeyInput = screen.getByLabelText("API Key");
    fireEvent.change(apiKeyInput, { target: { value: "sk-new-secret-value" } });
    fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

    await screen.findByText("可以回到今日页重新整理");

    expect(screen.getByLabelText("API Key")).not.toHaveValue("sk-new-secret-value");
    expect(screen.getByPlaceholderText("sk-••••••••••••••••4A7C")).toHaveValue("");
  });

  test("shows diagnostics when api key reveal fails", async () => {
    const fileBackedDeepSeekSettings = {
      ...aiSettings,
      activeProviderId: "deepseek",
      providers: aiSettings.providers.map(provider =>
        provider.id === "deepseek"
          ? {
              ...provider,
              isEnabled: true,
              isActive: true,
              hasApiKey: true,
              source: "file",
              apiKeyPreview: "sk-••••••••••••••••4A7C",
              canRevealApiKey: true
            }
          : { ...provider, isActive: false }
      )
    };
    const onRevealApiKey = vi.fn().mockRejectedValue(new Error("reveal failed"));

    render(
      <LlmSettingsPanel
        settings={fileBackedDeepSeekSettings}
        isBusy={false}
        onClose={vi.fn()}
        onActivate={vi.fn()}
        onTest={vi.fn()}
        onRevealApiKey={onRevealApiKey}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "查看 API Key" }));

    await screen.findByText("测试失败，配置没有保存");

    expect(screen.queryByDisplayValue("sk-file-backed-secret-4A7C")).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText("sk-••••••••••••••••4A7C")).toHaveValue("");
    expect(screen.getByText("reveal failed")).toBeInTheDocument();
    expect(screen.getByText("request_failed")).toBeInTheDocument();
    expect(screen.getByText("技术详情")).toBeInTheDocument();
  });
});

describe("editor API client", () => {
  test("getAiSettings calls settings endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(aiSettings));
    vi.stubGlobal("fetch", fetchMock);

    await getAiSettings();

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", undefined);
  });

  test("saveAiSettings sends provider settings", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(aiSettings));
    vi.stubGlobal("fetch", fetchMock);

    await saveAiSettings({
      activeProviderId: "deepseek",
      providers: [
        {
          id: "deepseek",
          type: "openai-compatible",
          displayName: "DeepSeek",
          preset: "deepseek",
          baseUrl: "https://api.deepseek.com",
          model: "deepseek-v4-flash",
          apiKey: "",
          isEnabled: true,
          timeoutSeconds: 45,
          temperature: 0.2,
          maxTokens: 1200,
          stylePreset: "faithful"
        }
      ]
    });

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        activeProviderId: "deepseek",
        providers: [
          {
            id: "deepseek",
            type: "openai-compatible",
            displayName: "DeepSeek",
            preset: "deepseek",
            baseUrl: "https://api.deepseek.com",
            model: "deepseek-v4-flash",
            apiKey: "",
            isEnabled: true,
            timeoutSeconds: 45,
            temperature: 0.2,
            maxTokens: 1200,
            stylePreset: "faithful"
          }
        ]
      })
    });
  });

  test("testAiProvider sends provider id to settings test endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
      isSuccess: true,
      status: "success",
      safeResponseSnippet: "{\"ok\":true}",
      httpStatus: 200,
      latency: "00:00:00",
      error: null
    }));
    vi.stubGlobal("fetch", fetchMock);

    await testAiProvider("deepseek");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/test", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ providerId: "deepseek" })
    });
  });

  test("testAiProvider sends candidate settings when provided", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
      isSuccess: false,
      status: "missing_api_key",
      safeResponseSnippet: "",
      httpStatus: null,
      latency: null,
      error: null
    }));
    vi.stubGlobal("fetch", fetchMock);
    const candidate = {
      activeProviderId: "deepseek",
      providers: [
        {
          id: "deepseek",
          type: "openai-compatible",
          displayName: "DeepSeek",
          preset: "deepseek",
          baseUrl: "https://api.deepseek.com",
          model: "deepseek-v4-flash",
          apiKey: "",
          isEnabled: true,
          timeoutSeconds: 45,
          temperature: 0.2,
          maxTokens: 1200,
          stylePreset: "faithful"
        }
      ]
    };

    await testAiProvider("deepseek", candidate);

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/test", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ providerId: "deepseek", candidate })
    });
  });

  test("activateAiSettings posts protected activation request", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
      saved: true,
      settings: aiSettings,
      testResult: {
        isSuccess: true,
        status: "success",
        safeResponseSnippet: "{\"ok\":true}",
        httpStatus: 200,
        latency: "00:00:00.0100000",
        error: null
      }
    }));
    vi.stubGlobal("fetch", fetchMock);
    const request = { activeProviderId: "mock", providers: [] };

    await activateAiSettings(request);

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/activate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(request)
    });
  });

  test("revealAiProviderApiKey reads file-backed key endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
      providerId: "deepseek",
      source: "file",
      apiKey: "secret-value"
    }));
    vi.stubGlobal("fetch", fetchMock);

    await revealAiProviderApiKey("deepseek");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/deepseek/api-key", undefined);
  });

  test("regenerateTodayDraft sends optional provider override", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(reviewingToday));
    vi.stubGlobal("fetch", fetchMock);

    await regenerateTodayDraft("mock");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ providerId: "mock" })
    });
  });

  test("startHarnessRun posts text to harness run endpoint", async () => {
    const run = {
      id: "run-1",
      date: journalDate,
      createdAt: "2026-05-08T09:30:00+08:00",
      startedAt: null,
      completedAt: null,
      status: "queued",
      providerId: "mock",
      promptVersion: "journal-harness-v1",
      currentRawInputId: "raw-1",
      toolCalls: [],
      errors: [],
      summary: "Queued."
    };
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({ today: reviewingToday, run }));
    vi.stubGlobal("fetch", fetchMock);

    await startHarnessRun("今天继续整理 harness");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/harness/runs", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ text: "今天继续整理 harness", source: "text" })
    });
  });

  test("getJournalAudit reads audit records for selected date", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse([]));
    vi.stubGlobal("fetch", fetchMock);

    await getJournalAudit("2026-05-08");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/audit?date=2026-05-08", undefined);
  });

  test("openHarnessRunEvents opens SSE stream and parses known events", () => {
    const addEventListener = vi.fn();
    const eventSource = { addEventListener } as unknown as EventSource;
    const EventSourceMock = vi.fn(function () {
      return eventSource;
    });
    vi.stubGlobal("EventSource", EventSourceMock);
    const onEvent = vi.fn();

    const result = openHarnessRunEvents("run id", onEvent);

    expect(result).toBe(eventSource);
    expect(EventSourceMock).toHaveBeenCalledWith("http://localhost:5057/journal/harness/runs/run%20id/events");
    expect(addEventListener).toHaveBeenCalledWith("run-started", expect.any(Function));
    expect(addEventListener).toHaveBeenCalledWith("run-status", expect.any(Function));
    expect(addEventListener).toHaveBeenCalledWith("run-already-completed", expect.any(Function));
    expect(addEventListener).toHaveBeenCalledWith("run-completed", expect.any(Function));
    const completedListener = addEventListener.mock.calls.find(([name]) => name === "run-completed")?.[1];
    const event: JournalHarnessRunEvent = {
      type: "run-completed",
      runId: "run id",
      status: "reviewing",
      message: "done"
    };
    completedListener?.({ data: JSON.stringify(event) } as MessageEvent);

    expect(onEvent).toHaveBeenCalledWith(event);
  });

  test("openHarnessRunEvents reports invalid event JSON without throwing", () => {
    const addEventListener = vi.fn();
    const eventSource = { addEventListener } as unknown as EventSource;
    const EventSourceMock = vi.fn(function () {
      return eventSource;
    });
    vi.stubGlobal("EventSource", EventSourceMock);
    const onEvent = vi.fn();
    const onError = vi.fn();

    openHarnessRunEvents("run-1", onEvent, onError);

    const statusListener = addEventListener.mock.calls.find(([name]) => name === "run-status")?.[1];
    expect(() => statusListener?.({ data: "{not-json" } as MessageEvent)).not.toThrow();

    expect(onEvent).not.toHaveBeenCalled();
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  test("openHarnessRunEvents reports EventSource errors", () => {
    const addEventListener = vi.fn();
    const eventSource = { addEventListener } as unknown as EventSource;
    const EventSourceMock = vi.fn(function () {
      return eventSource;
    });
    vi.stubGlobal("EventSource", EventSourceMock);
    const onError = vi.fn();

    openHarnessRunEvents("run-1", vi.fn(), onError);

    const errorListener = addEventListener.mock.calls.find(([name]) => name === "error")?.[1];
    expect(errorListener).toBeDefined();
    errorListener?.(new Event("error"));

    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  test("getTodayEditor calls editor endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(createEditorState()));
    vi.stubGlobal("fetch", fetchMock);

    await getTodayEditor();

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/editor", undefined);
  });

  test("saveBlockDraft sends editable sections to editor blocks endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(createEditorState()));
    vi.stubGlobal("fetch", fetchMock);

    await saveBlockDraft([{ id: "today-focus", content: "更新重点" }]);

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/editor/blocks", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ sections: [{ id: "today-focus", content: "更新重点" }] })
    });
  });
});
