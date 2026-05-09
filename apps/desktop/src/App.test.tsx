import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";
import {
  getTodayEditor,
  saveBlockDraft,
  saveSourceDraft,
  type JournalDraft,
  type TodayJournalState,
  type TodayEditorState
} from "./api";
import { JournalEditor } from "./JournalEditor";

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
    const postInputDeferred = createDeferred<Response>();
    const refreshedEditorDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockReturnValueOnce(healthDeferred.promise)
      .mockReturnValueOnce(editorDeferred.promise)
      .mockReturnValueOnce(postInputDeferred.promise)
      .mockReturnValueOnce(refreshedEditorDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const input = screen.getByLabelText("补充今天的自然语言输入");
    const submitButton = screen.getByRole("button", { name: "生成草稿" });

    fireEvent.change(input, { target: { value: "初始加载未完成时不能提交" } });
    fireEvent.click(submitButton);

    expect(submitButton).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(2);

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

    await waitFor(() => expect(submitButton).toBeEnabled());

    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(submitButton);

    expect(submitButton).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(3);

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
      }
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
    expect(fetchMock).not.toHaveBeenCalledWith("http://localhost:5057/journal/today", undefined);
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
    expect(fetchMock).toHaveBeenNthCalledWith(3, "http://localhost:5057/journal/today/inputs", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ text: "今天完成 Phase 2 API 连接", source: "text" })
    });
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/editor", undefined);
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
      }) }
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
      }) }
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
      3,
      "http://localhost:5057/journal/today/draft/confirm",
      { method: "POST" }
    );
    expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/journal/today/editor", undefined);
  });

  test("disables confirm while draft confirmation and editor refresh are pending", async () => {
    const confirmDeferred = createDeferred<Response>();
    const refreshedEditorDeferred = createDeferred<Response>();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(mockJsonResponse(healthResponse))
      .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
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
      { body: updatedEditor }
    ]);

    render(<App />);

    const focusEditor = await screen.findByRole("textbox", { name: "编辑 今日重点" });
    fireEvent.change(focusEditor, { target: { value: "保存后的区块内容" } });
    fireEvent.click(screen.getByRole("button", { name: "保存块编辑草稿" }));

    await waitFor(() =>
      expect(screen.getByRole("textbox", { name: "编辑 今日重点" })).toHaveValue("保存后的区块内容")
    );
    expect(fetchMock).toHaveBeenNthCalledWith(3, "http://localhost:5057/journal/today/editor/blocks", {
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
    expect(fetchMock).toHaveBeenNthCalledWith(3, "http://localhost:5057/journal/today/editor/source", {
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
      .mockReturnValueOnce(blockSaveDeferred.promise);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    const saveButton = await screen.findByRole("button", { name: "保存块编辑草稿" });
    const confirmButton = screen.getByRole("button", { name: "确认写入正式日记" });

    fireEvent.click(saveButton);

    expect(saveButton).toBeDisabled();
    expect(confirmButton).toBeDisabled();
    expect(screen.getByRole("button", { name: "插入 情绪感受" })).toBeDisabled();

    blockSaveDeferred.resolve(mockJsonResponse(createEditorState()));

    await waitFor(() => expect(saveButton).toBeEnabled());
    expect(confirmButton).toBeEnabled();
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
});

describe("editor API client", () => {
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
