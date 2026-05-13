import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { HistoryWorkbench } from "./HistoryWorkbench";
import type { JournalDate } from "./api";

const historyDate: JournalDate = {
  value: "2026-05-13",
  year: "2026",
  month: "05",
  isoDate: "2026-05-13",
  monthDay: "05-13",
  markdownFileName: "2026-05-13.md"
};

afterEach(() => {
  cleanup();
});

describe("HistoryWorkbench", () => {
  it("renders results, entry detail, and versions", () => {
    render(
      <HistoryWorkbench
        isBusy={false}
        query=""
        status=""
        entries={[{
          date: historyDate,
          status: "processed",
          mood: "平静",
          rawInputCount: 2,
          versionCount: 1,
          attentionReason: null,
          hits: [{
            sourceType: "section",
            sectionId: "today-focus",
            rawInputId: null,
            title: "今天想推进",
            snippet: "测试新整理的接口"
          }]
        }]}
        detail={{
          date: historyDate,
          status: "processed",
          attentionReason: null,
          markdown: "正式 Markdown",
          sections: [{
            id: "today-focus",
            title: "今天想推进",
            content: "- 测试新整理的接口",
            kind: "required",
            isEditableInBlockMode: true
          }],
          versions: []
        }}
        selectedDate="2026-05-13"
        versions={[{
          id: "version-2026-05-13T07-11-14+08-00",
          date: historyDate,
          createdAt: "2026-05-13T07:11:14+08:00",
          reason: "confirm-draft",
          sourceEntryPath: "entries/2026/05/2026-05-13.md",
          markdownPath: ".journal/versions/2026/05/2026-05-13/version.md",
          metaPath: ".journal/versions/2026/05/2026-05-13/version.meta.json",
          contentHash: "sha256:test"
        }]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    expect(screen.getByRole("heading", { name: "历史与版本" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /2026-05-13/ })).toBeInTheDocument();
    expect(screen.getByText("- 测试新整理的接口")).toBeInTheDocument();
    expect(screen.getByText("sha256:test")).toBeInTheDocument();
  });

  it("requests restore for selected version", async () => {
    const onRestoreVersion = vi.fn();

    render(
      <HistoryWorkbench
        isBusy={false}
        query=""
        status=""
        entries={[]}
        detail={null}
        selectedDate="2026-05-13"
        versions={[{
          id: "version-2026-05-13T07-11-14+08-00",
          date: historyDate,
          createdAt: "2026-05-13T07:11:14+08:00",
          reason: "confirm-draft",
          sourceEntryPath: "entries/2026/05/2026-05-13.md",
          markdownPath: ".journal/versions/2026/05/2026-05-13/version.md",
          metaPath: ".journal/versions/2026/05/2026-05-13/version.meta.json",
          contentHash: "sha256:test"
        }]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={onRestoreVersion}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "恢复为草稿" }));

    expect(onRestoreVersion).toHaveBeenCalledWith("version-2026-05-13T07-11-14+08-00");
  });
});
