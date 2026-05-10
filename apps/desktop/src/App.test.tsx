import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";
import {
  getAiSettings,
  getTodayEditor,
  regenerateTodayDraft,
  saveAiSettings,
  saveBlockDraft,
  saveSourceDraft,
  testAiProvider,
  type AiSettingsSaveRequest,
  type JournalDraft,
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

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body
  } as Response;
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("App", () => {
  test("prevents stale initial load race by disabling submit until editor state resolves", async () => {
    const healthDeferred = createDeferred<Response>();
    const editorDeferred = createDeferred<Response>();
    const aiSettingsDeferred = createDeferred<Response>();
    const postInputDeferred = createDeferred<Response>();
    const refreshedEditorDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockReturnValueOnce(healthDeferred.promise)
      .mockReturnValueOnce(editorDeferred.promise)
      .mockReturnValueOnce(aiSettingsDeferred.promise)
      .mockReturnValueOnce(postInputDeferred.promise)
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

    postInputDeferred.resolve(mockJsonResponse(emptyToday));
    refreshedEditorDeferred.resolve(mockJsonResponse(createEditorState()));

    expect(await screen.findByText("reviewing")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("推进 Phase 3");
  });

  test("loads health and today editor state on initial render", async () => {
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
      { body: aiSettings }
    ]);

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "2026-05-08 晨间日记" })
    ).toBeInTheDocument();
    expect(screen.getByText("empty")).toBeInTheDocument();
    expect(screen.getByLabelText("补充今天的自然语言输入")).toBeInTheDocument();
    expect(screen.getByText("还没有可编辑的 JMF 草稿")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(1, "http://localhost:5057/health", undefined);
    expect(fetchMock).toHaveBeenNthCalledWith(2, "http://localhost:5057/journal/today/editor", undefined);
    expect(fetchMock).toHaveBeenNthCalledWith(3, "http://localhost:5057/settings/ai", undefined);
    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today", undefined);
  });

  test("shows current LLM provider in top status strip", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    expect(await screen.findByRole("button", { name: "LLM Mock" })).toBeInTheDocument();
  });

  test("opens LLM settings panel from top status strip", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));

    expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /DeepSeek/ })).toBeInTheDocument();
    expect(screen.getByText("最小 JSON 请求")).toBeInTheDocument();
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
            message: "AI provider rejected the API key.",
            technicalDetails: "httpStatus: 401 authorization: [redacted]"
          }
        }
      }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "测试已保存配置" }));

    expect(await screen.findByText("unauthorized")).toBeInTheDocument();
    expect(screen.getByText("安全技术详情")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/settings/ai/test", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ providerId: "deepseek" })
    });
  });

  test("shows api error when saving LLM settings fails", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { ok: false, status: 500, body: { error: "settings save failed" } }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "启用 Provider" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("settings save failed");
    expect(screen.getByRole("button", { name: "启用 Provider" })).toBeEnabled();
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/settings/ai", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: expect.stringContaining('"activeProviderId":"deepseek"')
    });
  });

  test("ignores stale LLM settings save response when a later save wins", async () => {
    const firstSaveDeferred = createDeferred<Response>();
    const secondSaveDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(firstSaveDeferred.promise)
      .mockReturnValueOnce(secondSaveDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
    const providerList = within(screen.getByRole("navigation", { name: "Provider 列表" }));
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "启用 Provider" }));

    fireEvent.click(providerList.getByRole("button", { name: /Mock/ }));
    fireEvent.submit(screen.getByRole("button", { name: "启用 Provider" }).closest("form")!);

    secondSaveDeferred.resolve(mockJsonResponse(aiSettings));
    await waitFor(() => expect(screen.getByRole("button", { name: "LLM Mock" })).toBeInTheDocument());

    firstSaveDeferred.resolve(mockJsonResponse(deepSeekAiSettings));
    await Promise.resolve();

    expect(screen.getByRole("button", { name: "LLM Mock" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "LLM DeepSeek" })).not.toBeInTheDocument();
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

    fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));
    expect(screen.getByText("这会覆盖当前草稿内容，但不会影响正式日记。")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));

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

    fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));
    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));

    await waitFor(() =>
      expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("重新生成后的重点")
    );
    expect(screen.getByRole("alert")).toHaveTextContent("settings refresh failed");
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", undefined);
  });

  test("submits raw input and refreshes from today editor state", async () => {
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
      { body: emptyToday },
      { body: createEditorState() }
    ]);

    render(<App />);

    const input = await screen.findByLabelText("补充今天的自然语言输入");
    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    expect(await screen.findByText("reviewing")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "确认写入正式日记" })).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("推进 Phase 3");
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/inputs", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ text: "今天完成 Phase 2 API 连接", source: "text" })
    });
    expect(fetchMock).toHaveBeenNthCalledWith(5, "http://localhost:5057/journal/today/editor", undefined);
  });

  test("disables input action while draft generation and editor refresh are pending", async () => {
    const postInputDeferred = createDeferred<Response>();
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
      .mockReturnValueOnce(postInputDeferred.promise)
      .mockReturnValueOnce(refreshedEditorDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const input = await screen.findByLabelText("补充今天的自然语言输入");
    const submitButton = screen.getByRole("button", { name: "生成草稿" });
    expect(submitButton).toBeEnabled();

    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(submitButton);

    expect(submitButton).toBeDisabled();

    postInputDeferred.resolve(mockJsonResponse(emptyToday));
    await Promise.resolve();
    expect(submitButton).toBeDisabled();

    refreshedEditorDeferred.resolve(mockJsonResponse(createEditorState()));

    expect(await screen.findByText("reviewing")).toBeInTheDocument();
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

  test("shows attention errors without confirm action", async () => {
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

    expect((await screen.findAllByText("attention")).length).toBeGreaterThan(0);
    expect(screen.getAllByText("title is required").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "确认写入正式日记" })).not.toBeInTheDocument();
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

    fireEvent.click(await screen.findByRole("button", { name: "确认写入正式日记" }));

    await waitFor(() => expect(screen.getByText("processed")).toBeInTheDocument());
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

    const confirmButton = await screen.findByRole("button", { name: "确认写入正式日记" });
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

    await waitFor(() => expect(screen.getByText("processed")).toBeInTheDocument());
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

    const focusEditor = await screen.findByRole("textbox", { name: "编辑 今日重点" });
    fireEvent.change(focusEditor, { target: { value: "保存后的区块内容" } });
    fireEvent.click(screen.getByRole("button", { name: "保存块编辑草稿" }));

    await waitFor(() =>
      expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("保存后的区块内容")
    );
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/editor/blocks", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ sections: [{ id: "today-focus", content: "保存后的区块内容" }] })
    });
  });

  test("saves source edits through editor endpoint and updates editor state", async () => {
    const updatedMarkdown = "# 2026-05-08\n\n源码保存后的内容";
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: aiSettings },
      { body: createEditorState({
        markdown: updatedMarkdown,
        sections: [
          {
            id: "today-focus",
            title: "今日重点",
            content: "源码保存后的内容",
            kind: "required",
            isEditableInBlockMode: true
          }
        ]
      }) }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("tab", { name: "源码模式" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" }), {
      target: { value: updatedMarkdown }
    });
    fireEvent.click(screen.getByRole("button", { name: "保存源码草稿" }));

    await waitFor(() =>
      expect(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" })).toHaveValue(updatedMarkdown)
    );
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/editor/source", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ markdown: updatedMarkdown })
    });
  });

  test("disables editor save and confirm actions while block save is pending", async () => {
    const blockSaveDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(blockSaveDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const saveButton = await screen.findByRole("button", { name: "保存块编辑草稿" });
    const confirmButton = screen.getByRole("button", { name: "确认写入正式日记" });

    fireEvent.click(saveButton);

    expect(saveButton).toBeDisabled();
    expect(confirmButton).toBeDisabled();
    expect(screen.getByRole("button", { name: "插入 情绪感受" })).toBeDisabled();
    expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toBeDisabled();

    blockSaveDeferred.resolve(mockJsonResponse(createEditorState()));

    await waitFor(() => expect(saveButton).toBeEnabled());
    expect(confirmButton).toBeEnabled();
  });

  test("disables source editing while source save is pending", async () => {
    const sourceSaveDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(sourceSaveDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    fireEvent.click(await screen.findByRole("tab", { name: "源码模式" }));
    const sourceEditor = screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" });
    fireEvent.click(screen.getByRole("button", { name: "保存源码草稿" }));

    expect(sourceEditor).toBeDisabled();

    sourceSaveDeferred.resolve(mockJsonResponse(createEditorState()));

    await waitFor(() => expect(sourceEditor).toBeEnabled());
  });

  test("ignores stale block save response when a later raw input refresh wins", async () => {
    const blockSaveDeferred = createDeferred<Response>();
    const postInputDeferred = createDeferred<Response>();
    const inputRefreshDeferred = createDeferred<Response>();
    const staleEditor = createEditorState({
      status: "reviewing",
      markdown: editorMarkdown.replace("推进 Phase 3", "旧保存响应"),
      sections: [
        {
          id: "today-focus",
          title: "今日重点",
          content: "旧保存响应",
          kind: "required",
          isEditableInBlockMode: true
        }
      ]
    });
    const latestEditor = createEditorState({
      status: "processed",
      canConfirm: false,
      today: processedToday(),
      markdown: editorMarkdown.replace("推进 Phase 3", "最新输入刷新"),
      sections: [
        {
          id: "today-focus",
          title: "今日重点",
          content: "最新输入刷新",
          kind: "required",
          isEditableInBlockMode: true
        }
      ]
    });
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
      .mockResolvedValueOnce(mockJsonResponse(aiSettings))
      .mockReturnValueOnce(blockSaveDeferred.promise)
      .mockReturnValueOnce(postInputDeferred.promise)
      .mockReturnValueOnce(inputRefreshDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const focusEditor = await screen.findByRole("textbox", { name: "编辑 今日重点" });
    const input = screen.getByLabelText("补充今天的自然语言输入");
    const inputForm = input.closest("form");
    if (!inputForm) {
      throw new Error("Expected raw input form");
    }

    fireEvent.change(input, { target: { value: "后发起的 raw input 刷新" } });
    fireEvent.change(focusEditor, { target: { value: "准备保存但会变旧" } });
    fireEvent.click(screen.getByRole("button", { name: "保存块编辑草稿" }));
    fireEvent.submit(inputForm);

    postInputDeferred.resolve(mockJsonResponse(reviewingToday));
    await Promise.resolve();
    inputRefreshDeferred.resolve(mockJsonResponse(latestEditor));

    await waitFor(() =>
      expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("最新输入刷新")
    );

    blockSaveDeferred.resolve(mockJsonResponse(staleEditor));

    await Promise.resolve();
    await waitFor(() =>
      expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("最新输入刷新")
    );
    expect(screen.queryByDisplayValue("旧保存响应")).not.toBeInTheDocument();
  });

  test("restores mocked fetch between tests", () => {
    expect(fetch).not.toHaveProperty("mock");
  });
});

describe("JournalEditor", () => {
  test("shows editable today focus textarea in default block mode", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("推进 Phase 3");
  });

  test("shows raw inputs without exposing an editable control", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByText("今天要保留原始表达")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "编辑 原始输入" })).not.toBeInTheDocument();
  });

  test("inserts an available optional block into the page", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "插入 情绪感受" }));

    expect(screen.getByRole("textbox", { name: "编辑 情绪感受" })).toBeInTheDocument();
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
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "插入 工作推进" }));

    expect(screen.getAllByRole("heading", { level: 2 }).map(heading => heading.textContent)).toEqual([
      "原始输入",
      "昨日回顾",
      "今日重点",
      "工作推进"
    ]);
  });

  test("keeps block save action before the section list", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    const saveButton = screen.getByRole("button", { name: "保存块编辑草稿" });
    const firstSection = screen.getByRole("region", { name: "原始输入" });

    expect(saveButton.compareDocumentPosition(firstSection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  test("saves current editable block sections", () => {
    const onSaveBlocks = vi.fn();
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={onSaveBlocks}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今日重点" }), {
      target: { value: "完成前端编辑器组件" }
    });
    fireEvent.click(screen.getByRole("button", { name: "保存块编辑草稿" }));

    expect(onSaveBlocks).toHaveBeenCalledWith([
      { id: "today-focus", content: "完成前端编辑器组件" }
    ]);
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
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今日重点" }), {
      target: { value: "未保存的本地区块内容" }
    });

    rerender(
      <JournalEditor
        editor={refreshedEditor}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("推进 Phase 3");
    expect(screen.queryByDisplayValue("未保存的本地区块内容")).not.toBeInTheDocument();
  });

  test("saves full markdown in source mode", () => {
    const onSaveSource = vi.fn();
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={onSaveSource}
      />
    );

    fireEvent.click(screen.getByRole("tab", { name: "源码模式" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" }), {
      target: { value: "# 2026-05-08\n\n更新后的源码" }
    });
    fireEvent.click(screen.getByRole("button", { name: "保存源码草稿" }));

    expect(onSaveSource).toHaveBeenCalledWith("# 2026-05-08\n\n更新后的源码");
  });

  test("resets local source edits when the authoritative editor snapshot changes", () => {
    const initialEditor = createEditorState();
    const refreshedEditor = createEditorState({
      status: "attention",
      markdown: initialEditor.markdown,
      sections: initialEditor.sections,
      validation: {
        isValid: false,
        issues: [
          {
            code: "unknown-section",
            message: "存在未知区块",
            repairHint: "删除未知区块后再保存。"
          }
        ]
      },
      canConfirm: false
    });
    const { rerender } = render(
      <JournalEditor
        editor={initialEditor}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("tab", { name: "源码模式" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" }), {
      target: { value: "# 未保存的本地源码" }
    });

    rerender(
      <JournalEditor
        editor={refreshedEditor}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" })).toHaveValue(editorMarkdown);
    expect(screen.queryByDisplayValue("# 未保存的本地源码")).not.toBeInTheDocument();
  });

  test("shows attention validation issue message and repair hint", () => {
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
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByText("缺少今日重点区块")).toBeInTheDocument();
    expect(screen.getByText("请补回 today-focus 区块后再保存。")).toBeInTheDocument();
  });

  test("disables editable block and source textareas while busy", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={true}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toBeDisabled();

    fireEvent.click(screen.getByRole("tab", { name: "源码模式" }));

    expect(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" })).toBeDisabled();
  });
});

describe("LlmSettingsPanel", () => {
  test("shows LLM provider list and safe technical defaults", () => {
    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onTest={vi.fn()}
        onRegenerate={vi.fn()}
      />
    );

    expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
    const providerList = within(screen.getByRole("navigation", { name: "Provider 列表" }));
    expect(providerList.getByRole("button", { name: /Mock/ })).toHaveAttribute("aria-pressed", "true");
    expect(providerList.getByRole("button", { name: /DeepSeek/ })).toBeInTheDocument();
    expect(screen.getByText("会使用已保存的 Provider 配置，不会测试当前未保存草稿。")).toBeInTheDocument();
    expect(screen.getByLabelText("超时")).toHaveAttribute("type", "number");
    expect(screen.getByLabelText("API Key 已加载，值不显示")).toHaveValue("");
  });

  test("tests selected provider and shows safe technical details", async () => {
    const onTest = vi.fn().mockResolvedValue({
      isSuccess: false,
      status: "unauthorized",
      safeResponseSnippet: "",
      httpStatus: 401,
      latency: "00:00:00.1200000",
      error: {
        stage: "provider-call",
        code: "unauthorized",
        message: "AI provider rejected the API key.",
        technicalDetails: "httpStatus: 401 authorization: [redacted]"
      }
    });

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onTest={onTest}
        onRegenerate={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "测试已保存配置" }));

    expect(await screen.findByText("unauthorized")).toBeInTheDocument();
    expect(screen.getByText("安全技术详情")).toBeInTheDocument();
    expect(screen.getByText("httpStatus: 401 authorization: [redacted]")).toBeInTheDocument();
    expect(onTest).toHaveBeenCalledWith("deepseek");
  });

  test("clears stale connection result when editing unsaved provider fields", async () => {
    const onTest = vi.fn().mockResolvedValue({
      isSuccess: false,
      status: "unauthorized",
      safeResponseSnippet: "",
      httpStatus: 401,
      latency: "00:00:00.1200000",
      error: {
        stage: "provider-call",
        code: "unauthorized",
        message: "AI provider rejected the API key.",
        technicalDetails: "httpStatus: 401 authorization: [redacted]"
      }
    });

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onTest={onTest}
        onRegenerate={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "测试已保存配置" }));

    expect(await screen.findByText("unauthorized")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("模型"), { target: { value: "deepseek-next" } });

    expect(screen.queryByText("unauthorized")).not.toBeInTheDocument();
    expect(screen.queryByText("安全技术详情")).not.toBeInTheDocument();
  });

  test("ignores stale connection result after switching provider", async () => {
    const testDeferred = createDeferred<{
      isSuccess: boolean;
      status: string;
      safeResponseSnippet: string;
      httpStatus: number;
      latency: string;
      error: {
        stage: string;
        code: string;
        message: string;
        technicalDetails: string;
      };
    }>();
    const onTest = vi.fn().mockReturnValue(testDeferred.promise);

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onTest={onTest}
        onRegenerate={vi.fn()}
      />
    );

    const providerList = within(screen.getByRole("navigation", { name: "Provider 列表" }));
    fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
    fireEvent.click(screen.getByRole("button", { name: "测试已保存配置" }));
    fireEvent.click(providerList.getByRole("button", { name: /Mock/ }));

    testDeferred.resolve({
      isSuccess: false,
      status: "unauthorized",
      safeResponseSnippet: "",
      httpStatus: 401,
      latency: "00:00:00.1200000",
      error: {
        stage: "provider-call",
        code: "unauthorized",
        message: "AI provider rejected the API key.",
        technicalDetails: "httpStatus: 401 authorization: [redacted]"
      }
    });

    await waitFor(() => expect(onTest).toHaveBeenCalledWith("deepseek"));
    expect(screen.queryByText("unauthorized")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Mock" })).toBeInTheDocument();
  });

  test("keeps numeric setting when a number input is cleared", async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={onSave}
        onTest={vi.fn()}
        onRegenerate={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText("超时"), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "启用 Provider" }));

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const request = onSave.mock.calls[0][0] as AiSettingsSaveRequest;
    expect(request.providers.find(provider => provider.id === "mock")?.timeoutSeconds).toBe(1);
  });

  test("requires confirmation before regenerating draft", async () => {
    const onRegenerate = vi.fn().mockResolvedValue(undefined);

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onTest={vi.fn()}
        onRegenerate={onRegenerate}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));

    expect(screen.getByText("这会覆盖当前草稿内容，但不会影响正式日记。")).toBeInTheDocument();
    expect(onRegenerate).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));

    await waitFor(() => expect(onRegenerate).toHaveBeenCalledWith(undefined));
  });

  test("does not reuse mock regenerate confirmation for current provider regenerate", async () => {
    const onRegenerate = vi.fn().mockResolvedValue(undefined);

    render(
      <LlmSettingsPanel
        settings={aiSettings}
        isBusy={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onTest={vi.fn()}
        onRegenerate={onRegenerate}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "用 Mock 生成一次" }));
    expect(screen.getByText("这会覆盖当前草稿内容，但不会影响正式日记。")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));
    expect(onRegenerate).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));
    await waitFor(() => expect(onRegenerate).toHaveBeenCalledWith(undefined));
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

  test("saveSourceDraft sends markdown to editor source endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(createEditorState()));
    vi.stubGlobal("fetch", fetchMock);

    await saveSourceDraft("# 2026-05-08\n\n更新源码");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/editor/source", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ markdown: "# 2026-05-08\n\n更新源码" })
    });
  });
});
