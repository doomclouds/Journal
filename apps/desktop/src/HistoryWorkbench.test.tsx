import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
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

const otherHistoryDate: JournalDate = {
  value: "2026-05-14",
  year: "2026",
  month: "05",
  isoDate: "2026-05-14",
  monthDay: "05-14",
  markdownFileName: "2026-05-14.md"
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
          markdown: "# 当前日记\n\n- Markdown 渲染出来的正式内容",
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
    const mainPreview = screen.getByRole("region", { name: "历史日记预览" });
    expect(within(mainPreview).getByRole("heading", { name: "当前日记" })).toBeInTheDocument();
    expect(within(mainPreview).getByText("Markdown 渲染出来的正式内容")).toBeInTheDocument();
    expect(within(mainPreview).queryByText("- 测试新整理的接口")).not.toBeInTheDocument();
    expect(screen.getByText("sha256:test")).toBeInTheDocument();
  });

  it("shows a quiet loading state while selected entry detail is loading", () => {
    render(
      <HistoryWorkbench
        isBusy={true}
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
            snippet: "摘要不应冒充正文"
          }]
        }]}
        detail={null}
        selectedDate="2026-05-13"
        versions={[]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    const mainPreview = screen.getByRole("region", { name: "历史日记预览" });
    expect(within(mainPreview).getByLabelText("历史日记读取中")).toBeInTheDocument();
    expect(within(mainPreview).getByText("正在铺开这一天的日记")).toBeInTheDocument();
    expect(within(mainPreview).queryByText("摘要不应冒充正文")).not.toBeInTheDocument();
  });

  it("shows a quiet loading state while history results are loading", () => {
    render(
      <HistoryWorkbench
        isBusy={true}
        query=""
        status=""
        entries={[]}
        detail={null}
        selectedDate=""
        versions={[]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    const mainPreview = screen.getByRole("region", { name: "历史日记预览" });
    expect(within(mainPreview).getByLabelText("历史日记读取中")).toBeInTheDocument();
    expect(within(mainPreview).getByText("正在铺开这一天的日记")).toBeInTheDocument();
    expect(within(screen.getByLabelText("历史结果列表")).getByText("正在检索历史...")).toBeInTheDocument();
    expect(within(mainPreview).queryByText("没有历史结果")).not.toBeInTheDocument();
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

    expect(onRestoreVersion).toHaveBeenCalledWith(expect.objectContaining({
      id: "version-2026-05-13T07-11-14+08-00",
      date: historyDate
    }));
  });

  it("requests version detail and renders selected version markdown in the main paper", () => {
    const onViewVersion = vi.fn();

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
          hits: []
        }]}
        detail={{
          date: historyDate,
          status: "processed",
          attentionReason: null,
          markdown: "正式 Markdown",
          sections: [{
            id: "today-focus",
            title: "今天想推进",
            content: "当前正式日记不应显示",
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
        selectedVersionDetail={{
          version: {
            id: "version-2026-05-13T07-11-14+08-00",
            date: historyDate,
            createdAt: "2026-05-13T07:11:14+08:00",
            reason: "confirm-draft",
            sourceEntryPath: "entries/2026/05/2026-05-13.md",
            markdownPath: ".journal/versions/2026/05/2026-05-13/version.md",
            metaPath: ".journal/versions/2026/05/2026-05-13/version.meta.json",
            contentHash: "sha256:test"
          },
          markdown: "# 旧版本\n\n- 指定版本内容"
        }}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onViewVersion={onViewVersion}
        onRestoreVersion={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "查看版本" }));

    expect(onViewVersion).toHaveBeenCalledWith(expect.objectContaining({
      id: "version-2026-05-13T07-11-14+08-00",
      date: historyDate
    }));
    const mainPreview = screen.getByRole("region", { name: "历史日记预览" });
    expect(within(mainPreview).getByRole("heading", { name: "旧版本" })).toBeInTheDocument();
    expect(within(mainPreview).getByText("指定版本内容")).toBeInTheDocument();
    expect(within(mainPreview).queryByText("当前正式日记不应显示")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("所选版本内容")).not.toBeInTheDocument();
  });

  it("returns the main paper from selected version to current entry detail", () => {
    const onClearVersion = vi.fn();

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
          hits: []
        }]}
        detail={{
          date: historyDate,
          status: "processed",
          attentionReason: null,
          markdown: "正式 Markdown",
          sections: [{
            id: "today-focus",
            title: "今天想推进",
            content: "当前正式日记内容",
            kind: "required",
            isEditableInBlockMode: true
          }],
          versions: []
        }}
        selectedDate="2026-05-13"
        versions={[]}
        selectedVersionDetail={{
          version: {
            id: "version-2026-05-13T07-11-14+08-00",
            date: historyDate,
            createdAt: "2026-05-13T07:11:14+08:00",
            reason: "confirm-draft",
            sourceEntryPath: "entries/2026/05/2026-05-13.md",
            markdownPath: ".journal/versions/2026/05/2026-05-13/version.md",
            metaPath: ".journal/versions/2026/05/2026-05-13/version.meta.json",
            contentHash: "sha256:test"
          },
          markdown: "# 旧版本\n\n- 指定版本内容"
        }}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onClearVersion={onClearVersion}
        onRestoreVersion={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "查看当前日记" }));

    expect(onClearVersion).toHaveBeenCalled();
  });

  it("does not render stale detail or restore actions from another selected date", () => {
    const onRestoreVersion = vi.fn();

    render(
      <HistoryWorkbench
        isBusy={false}
        query=""
        status=""
        entries={[{
          date: otherHistoryDate,
          status: "processed",
          mood: "专注",
          rawInputCount: 1,
          versionCount: 0,
          attentionReason: null,
          hits: [{
            sourceType: "section",
            sectionId: "today-focus",
            rawInputId: null,
            title: "今日重点",
            snippet: "新日期摘要"
          }]
        }]}
        detail={{
          date: historyDate,
          status: "processed",
          attentionReason: null,
          markdown: "旧日期 Markdown",
          sections: [{
            id: "today-focus",
            title: "今天想推进",
            content: "旧日期详情不应显示",
            kind: "required",
            isEditableInBlockMode: true
          }],
          versions: []
        }}
        selectedDate="2026-05-14"
        versions={[{
          id: "version-2026-05-13T07-11-14+08-00",
          date: historyDate,
          createdAt: "2026-05-13T07:11:14+08:00",
          reason: "confirm-draft",
          sourceEntryPath: "entries/2026/05/2026-05-13.md",
          markdownPath: ".journal/versions/2026/05/2026-05-13/version.md",
          metaPath: ".journal/versions/2026/05/2026-05-13/version.meta.json",
          contentHash: "sha256:old-version"
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

    expect(screen.queryByText("旧日期详情不应显示")).not.toBeInTheDocument();
    expect(screen.queryByText("sha256:old-version")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "恢复为草稿" })).not.toBeInTheDocument();
    expect(onRestoreVersion).not.toHaveBeenCalled();
  });

  it("requests reviewing history when the pending confirmation filter is selected", () => {
    const onStatusChange = vi.fn();

    render(
      <HistoryWorkbench
        isBusy={false}
        query=""
        status=""
        entries={[]}
        detail={null}
        selectedDate=""
        versions={[]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={onStatusChange}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "待确认" }));

    expect(onStatusChange).toHaveBeenCalledWith("reviewing");
  });
});
