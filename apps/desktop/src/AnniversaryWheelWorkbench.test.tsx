import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import { AnniversaryWheelWorkbench } from "./AnniversaryWheelWorkbench";
import type {
  JournalAnniversaryItem,
  JournalAnniversaryWheelResult,
  JournalEntryVersion,
  JournalHistoryEntryDetail
} from "./api";

const date2026 = {
  value: "2026-05-14",
  year: "2026",
  month: "05",
  isoDate: "2026-05-14",
  monthDay: "05-14",
  markdownFileName: "2026-05-14.md"
};

const date2025 = {
  ...date2026,
  value: "2025-05-14",
  year: "2025",
  isoDate: "2025-05-14",
  markdownFileName: "2025-05-14.md"
};

const result: JournalAnniversaryWheelResult = {
  monthDay: "05-14",
  items: [
    {
      date: date2026,
      status: "processed",
      mood: "期待",
      rawInputCount: 2,
      versionCount: 1,
      entryUpdatedAt: "2026-05-14T08:30:00+08:00",
      cardPreview: {
        title: "年轮卡片标题",
        lines: ["卡片第一行", "卡片第二行"]
      },
      attentionReason: null,
      hits: [{
        sourceType: "section",
        sectionId: "today-focus",
        rawInputId: null,
        title: "今天想推进",
        snippet: "- 打磨同日年轮"
      }]
    },
    {
      date: date2025,
      status: "missing",
      mood: null,
      rawInputCount: 1,
      versionCount: 0,
      entryUpdatedAt: null,
      cardPreview: {
        title: "去年卡片",
        lines: ["去年卡片摘要"]
      },
      attentionReason: null,
      hits: [{
        sourceType: "raw-input",
        sectionId: null,
        rawInputId: "raw-1",
        title: "text",
        snippet: "去年今天只有原始材料"
      }]
    }
  ]
};

const savedAnniversary: JournalAnniversaryItem = {
  id: "anniversary-1",
  monthDay: "05-14",
  type: "self-reminder",
  title: "常看的这一天",
  description: "每年都回来看看",
  originDate: "2025-05-14",
  pinned: true,
  createdAt: "2026-05-14T09:00:00+08:00",
  updatedAt: "2026-05-14T09:00:00+08:00",
  nextYearNotes: [{
    id: "note-1",
    targetDate: "2027-05-14",
    text: "明年提醒",
    status: "pending",
    createdAt: "2026-05-14T09:05:00+08:00",
    adoptedAt: null,
    rawInputId: null
  }]
};

const yearEndAnniversary: JournalAnniversaryItem = {
  ...savedAnniversary,
  id: "anniversary-12-31",
  monthDay: "12-31",
  type: "gratitude",
  title: "年末常看日",
  description: "年末回来复盘",
  originDate: "2025-12-31",
  nextYearNotes: []
};

const dueAnniversary: JournalAnniversaryItem = {
  ...savedAnniversary,
  nextYearNotes: savedAnniversary.nextYearNotes.map(note => ({
    ...note,
    targetDate: "2026-05-01"
  }))
};

const secondSameDayAnniversary: JournalAnniversaryItem = {
  ...savedAnniversary,
  id: "anniversary-second",
  type: "growth",
  title: "第二个同日纪念日",
  description: "编辑第二个",
  originDate: "2024-05-14",
  updatedAt: "2026-05-14T09:30:00+08:00",
  nextYearNotes: [{
    id: "note-second",
    targetDate: "2026-05-01",
    text: "第二个提醒",
    status: "pending",
    createdAt: "2026-05-14T09:35:00+08:00",
    adoptedAt: null,
    rawInputId: null
  }]
};

const detail: JournalHistoryEntryDetail = {
  date: date2026,
  status: "processed",
  attentionReason: null,
  markdown: "# 2026-05-14\n\n## 今天想推进\n\n- 打磨同日年轮",
  sections: [],
  versions: []
};

const version: JournalEntryVersion = {
  id: "version-1",
  date: date2026,
  createdAt: "2026-05-14T08:00:00+08:00",
  reason: "confirm-draft",
  sourceEntryPath: "entries/2026/05/2026-05-14.md",
  markdownPath: ".journal/versions/2026/05/2026-05-14/version-1.md",
  metaPath: ".journal/versions/2026/05/2026-05-14/version-1.meta.json",
  contentHash: "sha256:abc"
};

afterEach(() => {
  cleanup();
});

describe("AnniversaryWheelWorkbench", () => {
  test("renders same-day year cards and timeline previews", () => {
    const { container } = render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const preview = screen.getByRole("region", { name: "同日年轮预览" });
    const years = screen.getByLabelText("同日年份列表");
    const stageTimeline = within(preview).getByLabelText("同日年轮时间线");
    expect(preview).toBeInTheDocument();
    expect(within(years).getByRole("button", { name: /2026/ })).toHaveAttribute("aria-pressed", "true");
    expect(within(years).getByRole("button", { name: /2025/ })).toBeInTheDocument();
    expect(stageTimeline).toHaveClass("memory-corridor-timeline");
    expect(container.querySelector(".memory-corridor-spine")).not.toBeNull();
    expect(container.querySelector("#memory-entry-2026-05-14")).toHaveClass("memory-entry", "is-left");
    expect(container.querySelector("#memory-entry-2025-05-14")).toHaveClass("memory-entry", "is-right");
    expect(container.querySelector("#memory-entry-2024-05-14")).toHaveClass("memory-entry", "is-empty");
    expect(container.querySelector("#memory-entry-2026-05-14 .year-pin")).not.toBeNull();
    expect(within(preview).getByText("年轮卡片标题")).toBeInTheDocument();
    expect(within(preview).getByText("卡片第一行")).toBeInTheDocument();
    expect(screen.getAllByText("去年卡片摘要").length).toBeGreaterThan(0);
  });

  test("renders left years as node navigation linked to timeline entries", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const years = screen.getByLabelText("同日年份列表");
    const year2025 = within(years).getByRole("button", { name: /2025/ });

    expect(years).toHaveClass("anniversary-year-node-list");
    expect(year2025).toHaveAttribute("aria-controls", "memory-entry-2025-05-14");
    expect(year2025.querySelector(".anniversary-year-node-dot")).not.toBeNull();
    expect(within(years).queryByText("去年卡片摘要")).not.toBeInTheDocument();
  });

  test("renders meaning observation in the anniversary side panel", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const inspector = screen.getByRole("complementary", { name: "同日年轮详情" });
    expect(within(inspector).getByText("意义观察")).toBeInTheDocument();
    expect(within(inspector).getAllByText(/2 年记录/).length).toBeGreaterThan(0);
    expect(within(inspector).getByText(/起点 2025/)).toBeInTheDocument();
    expect(within(inspector).getByText("2 年记录，起点 2025。每年都回来看看")).toBeInTheDocument();
  });

  test("waits for selected entry detail instead of flashing summary hits", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={null}
        versions={[]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "阅读 2026-05-14 日记" }));

    const preview = screen.getByRole("region", { name: "同日年轮预览" });
    expect(within(preview).getByLabelText("同日当前日记读取中")).toBeInTheDocument();
    expect(within(preview).getByText("正在铺开这一天的日记")).toBeInTheDocument();
    expect(within(preview).queryByText("- 打磨同日年轮")).not.toBeInTheDocument();
  });

  test("emits month-day changes and selected date changes", () => {
    const onMonthDayChange = vi.fn();
    const onSelectDate = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={onMonthDayChange}
        onSelectDate={onSelectDate}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const dateInput = screen.getByLabelText("选择同日年轮日期");
    expect(dateInput).toHaveAttribute("type", "date");
    expect(dateInput).toHaveValue(`${new Date().getFullYear()}-05-14`);

    fireEvent.change(dateInput, { target: { value: "2024-02-29" } });
    fireEvent.click(within(screen.getByLabelText("同日年份列表")).getByRole("button", { name: /2025/ }));

    expect(onMonthDayChange).toHaveBeenCalledWith("02-29");
    expect(onSelectDate).toHaveBeenCalledWith("2025-05-14");
  });

  test("does not render version snapshot actions in the anniversary inspector", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const inspector = screen.getByRole("complementary", { name: "同日年轮详情" });
    expect(within(inspector).queryByRole("button", { name: /查看版本/ })).not.toBeInTheDocument();
    expect(within(inspector).queryByText("这一天还没有覆盖前快照。")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /恢复版本/ })).not.toBeInTheDocument();
  });

  test("renders card preview lines instead of raw hit snippets", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const yearNavigation = screen.getByLabelText("同日年份列表");
    const stageTimeline = screen.getByLabelText("同日年轮时间线");
    expect(within(yearNavigation).queryByText("年轮卡片标题")).not.toBeInTheDocument();
    expect(within(yearNavigation).queryByText("卡片第一行 / 卡片第二行")).not.toBeInTheDocument();
    expect(within(stageTimeline).getByText("年轮卡片标题")).toBeInTheDocument();
    expect(within(stageTimeline).getByText("卡片第一行")).toBeInTheDocument();
    expect(within(stageTimeline).getByText("卡片第二行")).toBeInTheDocument();
    expect(within(stageTimeline).queryByText("- 打磨同日年轮")).not.toBeInTheDocument();
  });

  test("truncates long timeline card preview text", () => {
    const longTitle = "这是一段特别长的同日年轮卡片标题，用来模拟真实日记里很啰嗦的一句话";
    const longLine = "这是一段特别长的同日年轮卡片摘要，用来确认外层时间线不会把整段内容全部铺出来";
    const longPreviewResult: JournalAnniversaryWheelResult = {
      ...result,
      items: [{
        ...result.items[0],
        cardPreview: {
          title: longTitle,
          lines: [longLine]
        }
      }]
    };

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={longPreviewResult}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const stageTimeline = screen.getByLabelText("同日年轮时间线");
    expect(within(stageTimeline).queryByText(longTitle)).not.toBeInTheDocument();
    expect(within(stageTimeline).queryByText(longLine)).not.toBeInTheDocument();
    expect(within(stageTimeline).getByText("这是一段特别长的同日年轮卡片标题，用来模拟真实日记里很啰嗦的...")).toBeInTheDocument();
    expect(within(stageTimeline).getByText("这是一段特别长的同日年轮卡片摘要，用来确认外层时间线不会把整...")).toBeInTheDocument();
  });

  test("limits timeline card preview lines to three items", () => {
    const manyLinesResult: JournalAnniversaryWheelResult = {
      ...result,
      items: [{
        ...result.items[0],
        cardPreview: {
          title: "多条摘要卡片",
          lines: ["第一条摘要", "第二条摘要", "第三条摘要", "第四条不应显示"]
        }
      }]
    };

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={manyLinesResult}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    const stageTimeline = screen.getByLabelText("同日年轮时间线");
    expect(within(stageTimeline).getByText("第一条摘要")).toBeInTheDocument();
    expect(within(stageTimeline).getByText("第二条摘要")).toBeInTheDocument();
    expect(within(stageTimeline).getByText("第三条摘要")).toBeInTheDocument();
    expect(within(stageTimeline).queryByText("第四条不应显示")).not.toBeInTheDocument();
  });

  test("opens selected date markdown in reading mode and returns to timeline", () => {
    const onSelectDate = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={onSelectDate}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "阅读 2026-05-14 日记" }));

    const preview = screen.getByRole("region", { name: "同日年轮预览" });
    expect(onSelectDate).toHaveBeenCalledWith("2026-05-14");
    expect(within(preview).getByLabelText("同日当前日记内容")).toBeInTheDocument();
    expect(within(preview).getByText("打磨同日年轮")).toBeInTheDocument();
    expect(within(preview).queryByText("年轮卡片标题")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "返回年轮" }));

    expect(within(preview).getByText("年轮卡片标题")).toBeInTheDocument();
  });

  test("shows entry updated time in reading mode", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "阅读 2026-05-14 日记" }));

    expect(screen.getByText("最后写入 05/14 08:30")).toBeInTheDocument();
  });

  test("returns to timeline when changing month day from reading mode", () => {
    const onMonthDayChange = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary, yearEndAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={onMonthDayChange}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "阅读 2026-05-14 日记" }));
    expect(screen.getByLabelText("同日当前日记内容")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("选择同日年轮日期"), { target: { value: "2026-12-31" } });

    expect(onMonthDayChange).toHaveBeenCalledWith("12-31");
    expect(screen.getByLabelText("同日年轮时间线")).toBeInTheDocument();
    expect(screen.queryByLabelText("同日当前日记内容")).not.toBeInTheDocument();
  });

  test("returns to timeline when opening a saved anniversary from reading mode", () => {
    const onMonthDayChange = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary, yearEndAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={onMonthDayChange}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "阅读 2026-05-14 日记" }));
    expect(screen.getByLabelText("同日当前日记内容")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /年末常看日/ }));

    expect(onMonthDayChange).toHaveBeenCalledWith("12-31");
    expect(screen.getByLabelText("同日年轮时间线")).toBeInTheDocument();
    expect(screen.queryByLabelText("同日当前日记内容")).not.toBeInTheDocument();
  });

  test("shows empty anniversary list until user saves a pinned anniversary", () => {
    const onSaveAnniversary = vi.fn();

    const { rerender } = render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={onSaveAnniversary}
        onAddNextYearNote={vi.fn()}
      />
    );

    expect(screen.getByText("还没有保存常看日期。")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("纪念日名称"), { target: { value: "常看的这一天" } });
    fireEvent.click(screen.getByRole("button", { name: "保存纪念日" }));

    expect(onSaveAnniversary).toHaveBeenCalledWith(null, expect.objectContaining({
      monthDay: "05-14",
      title: "常看的这一天",
      type: "self-reminder",
      originDate: "2026-05-14",
      pinned: true
    }));

    rerender(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={onSaveAnniversary}
        onAddNextYearNote={vi.fn()}
      />
    );

    expect(screen.queryByText("还没有保存常看日期。")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /常看的这一天/ })).toBeInTheDocument();
  });

  test("keeps anniversary form drafts when selecting another year", () => {
    const { rerender } = render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    const titleInput = screen.getByLabelText("纪念日名称");
    const noteInput = screen.getByLabelText("写给下一年同一天");
    fireEvent.change(titleInput, { target: { value: "未保存标题" } });
    fireEvent.change(noteInput, { target: { value: "未保存提醒" } });
    fireEvent.click(within(screen.getByLabelText("同日年份列表")).getByRole("button", { name: /2025/ }));

    rerender(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2025-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    expect(titleInput).toHaveValue("未保存标题");
    expect(noteInput).toHaveValue("未保存提醒");
  });

  test("clears next-year note only after a successful save", async () => {
    const onAddNextYearNote = vi.fn().mockResolvedValue(undefined);

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={onAddNextYearNote}
      />
    );

    const noteInput = screen.getByLabelText("写给下一年同一天");
    fireEvent.change(noteInput, { target: { value: "明年继续复盘" } });
    fireEvent.click(screen.getByRole("button", { name: "保存下一年提醒" }));

    expect(onAddNextYearNote).toHaveBeenCalledWith("anniversary-1", "明年继续复盘");
    await waitFor(() => expect(noteInput).toHaveValue(""));
  });

  test("does not treat an in-flight next-year note submission as success", () => {
    const onAddNextYearNote = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        isNextYearNoteSaving={true}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={onAddNextYearNote}
      />
    );

    const noteInput = screen.getByLabelText("写给下一年同一天");
    fireEvent.change(noteInput, { target: { value: "提交中别清空" } });
    fireEvent.submit(noteInput.closest("form") as HTMLFormElement);

    expect(onAddNextYearNote).not.toHaveBeenCalled();
    expect(noteInput).toHaveValue("提交中别清空");
  });

  test("renders adopt action only for due pending next-year notes", () => {
    const onAdoptNextYearNote = vi.fn();
    const onDismissNextYearNote = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[dueAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={onAdoptNextYearNote}
        onDismissNextYearNote={onDismissNextYearNote}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "采纳提醒" }));
    fireEvent.click(screen.getByRole("button", { name: "忽略提醒" }));

    expect(onAdoptNextYearNote).toHaveBeenCalledWith("anniversary-1", "note-1");
    expect(onDismissNextYearNote).toHaveBeenCalledWith("anniversary-1", "note-1");
  });

  test("hides adopt action for future pending next-year notes", () => {
    const onAdoptNextYearNote = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={onAdoptNextYearNote}
        onDismissNextYearNote={vi.fn()}
      />
    );

    expect(screen.getByText("待处理")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "采纳提醒" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "忽略提醒" })).toBeInTheDocument();
  });

  test("selects a specific saved anniversary when multiple share the same month day", () => {
    const onSaveAnniversary = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary, secondSameDayAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={onSaveAnniversary}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /第二个同日纪念日/ }));

    expect(screen.getByLabelText("纪念日名称")).toHaveValue("第二个同日纪念日");
    expect(screen.getByLabelText("说明")).toHaveValue("编辑第二个");
    expect(screen.getByText("第二个提醒")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "保存纪念日" }));

    expect(onSaveAnniversary).toHaveBeenCalledWith("anniversary-second", expect.objectContaining({
      title: "第二个同日纪念日",
      type: "growth"
    }));
  });

  test("keeps anniversary form draft when selected anniversary refreshes", () => {
    const refreshedAnniversary: JournalAnniversaryItem = {
      ...savedAnniversary,
      title: "服务端刷新后的标题",
      description: "服务端刷新后的说明",
      updatedAt: "2026-05-14T10:00:00+08:00"
    };

    const { rerender } = render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    const titleInput = screen.getByLabelText("纪念日名称");
    const descriptionInput = screen.getByLabelText("说明");
    fireEvent.change(titleInput, { target: { value: "未保存资料标题" } });
    fireEvent.change(descriptionInput, { target: { value: "未保存资料说明" } });

    rerender(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[refreshedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    expect(titleInput).toHaveValue("未保存资料标题");
    expect(descriptionInput).toHaveValue("未保存资料说明");
  });

  test("keeps next-year note draft when note actions refresh the selected anniversary", () => {
    const refreshedAnniversary: JournalAnniversaryItem = {
      ...savedAnniversary,
      updatedAt: "2026-05-14T10:00:00+08:00",
      nextYearNotes: savedAnniversary.nextYearNotes.map(note => ({
        ...note,
        status: "adopted",
        adoptedAt: "2026-05-14T10:00:00+08:00",
        rawInputId: "raw-adopted"
      }))
    };

    const { rerender } = render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    const noteInput = screen.getByLabelText("写给下一年同一天");
    fireEvent.change(noteInput, { target: { value: "新的未保存提醒" } });

    rerender(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[refreshedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    expect(noteInput).toHaveValue("新的未保存提醒");
  });

  test("renders next-year note statuses in Chinese", () => {
    const anniversaryWithStatuses: JournalAnniversaryItem = {
      ...savedAnniversary,
      nextYearNotes: [
        savedAnniversary.nextYearNotes[0],
        {
          ...savedAnniversary.nextYearNotes[0],
          id: "note-adopted",
          text: "已经采纳",
          status: "adopted",
          adoptedAt: "2026-05-14T10:00:00+08:00",
          rawInputId: "raw-adopted"
        },
        {
          ...savedAnniversary.nextYearNotes[0],
          id: "note-dismissed",
          text: "已经忽略",
          status: "dismissed"
        }
      ]
    };

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[anniversaryWithStatuses]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={vi.fn()}
        onAdoptNextYearNote={vi.fn()}
        onDismissNextYearNote={vi.fn()}
      />
    );

    expect(screen.getByText("待处理")).toBeInTheDocument();
    expect(screen.getByText("已采纳")).toBeInTheDocument();
    expect(screen.getByText("已忽略")).toBeInTheDocument();
  });

  test("keeps next-year note text and shows an error when save fails", async () => {
    const onAddNextYearNote = vi.fn().mockRejectedValue(new Error("保存提醒失败"));

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        anniversaries={[savedAnniversary]}
        anniversaryError=""
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onSaveAnniversary={vi.fn()}
        onAddNextYearNote={onAddNextYearNote}
      />
    );

    const noteInput = screen.getByLabelText("写给下一年同一天");
    fireEvent.change(noteInput, { target: { value: "失败也不能丢" } });
    fireEvent.click(screen.getByRole("button", { name: "保存下一年提醒" }));

    await screen.findByRole("alert");
    expect(screen.getByText("保存提醒失败")).toBeInTheDocument();
    expect(noteInput).toHaveValue("失败也不能丢");
  });
});
