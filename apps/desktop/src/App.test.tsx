import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";

type MockResponse = {
  ok?: boolean;
  status?: number;
  body: unknown;
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

const emptyToday = {
  date: journalDate,
  status: "empty",
  rawInputs: [],
  draft: null,
  entry: null,
  errors: []
};

const reviewingToday = {
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
  draft: {
    date: journalDate,
    status: "reviewing",
    markdown: "# 2026-05-08\n\n## 晨间记录\n\n今天完成 Phase 2 API 连接",
    sourceRawInputIds: ["raw-1"],
    errors: [],
    updatedAt: "2026-05-08T08:05:00+08:00"
  }
};

function processedToday(entryPath = "C:\\Journal\\entries\\2026\\05\\2026-05-08.md") {
  return {
    ...reviewingToday,
    status: "processed",
    draft: {
      ...reviewingToday.draft,
      status: "processed"
    },
    entry: {
      date: journalDate,
      markdown: reviewingToday.draft.markdown,
      path: entryPath,
      updatedAt: "2026-05-08T08:06:00+08:00"
    }
  };
}

const attentionToday = {
  ...reviewingToday,
  status: "attention",
  draft: {
    ...reviewingToday.draft,
    status: "attention",
    markdown: "# AI JSON validation failed\n\n## Errors\n\n- title is required",
    errors: ["title is required"]
  },
  errors: ["title is required"]
};

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

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("App", () => {
  test("renders empty today workbench", async () => {
    const fetchMock = mockFetchSequence([
      { body: healthResponse },
      { body: emptyToday }
    ]);

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "2026-05-08 晨间日记" })
    ).toBeInTheDocument();
    expect(screen.getByText("empty")).toBeInTheDocument();
    expect(screen.getByLabelText("补充今天的自然语言输入")).toBeInTheDocument();
    expect(screen.getByText("还没有草稿")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(1, "http://localhost:5057/health");
    expect(fetchMock).toHaveBeenNthCalledWith(2, "http://localhost:5057/journal/today");
  });

  test("shows reviewing draft after submitting input", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: emptyToday },
      { body: reviewingToday }
    ]);

    render(<App />);

    const input = await screen.findByLabelText("补充今天的自然语言输入");
    fireEvent.change(input, { target: { value: "今天完成 Phase 2 API 连接" } });
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    expect(await screen.findByText("reviewing")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "确认写入日记" })).toBeInTheDocument();
    expect(screen.getByTestId("markdown-preview")).toHaveTextContent("今天完成 Phase 2 API 连接");
  });

  test("shows attention errors without confirm action", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: attentionToday }
    ]);

    render(<App />);

    expect(await screen.findByText("attention")).toBeInTheDocument();
    expect(screen.getAllByText("title is required").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "确认写入日记" })).not.toBeInTheDocument();
  });

  test("shows processed entry path after confirming reviewing draft", async () => {
    const entryPath = "C:\\Journal\\entries\\2026\\05\\2026-05-08.md";
    mockFetchSequence([
      { body: healthResponse },
      { body: reviewingToday },
      { body: processedToday(entryPath) }
    ]);

    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "确认写入日记" }));

    await waitFor(() => expect(screen.getByText("processed")).toBeInTheDocument());
    expect(screen.getByText(entryPath)).toBeInTheDocument();
  });

  test("restores mocked fetch between tests", () => {
    expect(fetch).not.toHaveProperty("mock");
  });
});
