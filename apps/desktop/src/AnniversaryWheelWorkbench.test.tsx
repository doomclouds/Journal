import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import { AnniversaryWheelWorkbench } from "./AnniversaryWheelWorkbench";
import type { JournalAnniversaryWheelResult, JournalEntryVersion, JournalHistoryEntryDetail } from "./api";

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
  test("renders same-day year cards and selected markdown detail", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onViewVersion={vi.fn()}
        onClearVersion={vi.fn()}
      />
    );

    const preview = screen.getByRole("region", { name: "同日年轮预览" });
    expect(preview).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /2026/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: /2025/ })).toBeInTheDocument();
    expect(within(preview).getByText("打磨同日年轮")).toBeInTheDocument();
    expect(screen.getByText("去年今天只有原始材料")).toBeInTheDocument();
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
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onViewVersion={vi.fn()}
        onClearVersion={vi.fn()}
      />
    );

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
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={onMonthDayChange}
        onSelectDate={onSelectDate}
        onViewVersion={vi.fn()}
        onClearVersion={vi.fn()}
      />
    );

    const dateInput = screen.getByLabelText("选择同日年轮日期");
    expect(dateInput).toHaveAttribute("type", "date");

    fireEvent.change(dateInput, { target: { value: "2024-02-29" } });
    fireEvent.click(screen.getByRole("button", { name: /2025/ }));

    expect(onMonthDayChange).toHaveBeenCalledWith("02-29");
    expect(onSelectDate).toHaveBeenCalledWith("2025-05-14");
  });

  test("shows selected version preview and can return to current entry", () => {
    const onClearVersion = vi.fn();

    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        selectedVersionDetail={{ version, markdown: "# Snapshot\n\n历史版本内容" }}
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onViewVersion={vi.fn()}
        onClearVersion={onClearVersion}
      />
    );

    const preview = screen.getByRole("region", { name: "同日年轮预览" });
    expect(within(preview).getByText("历史版本内容")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /查看当前日记/ }));
    expect(onClearVersion).toHaveBeenCalledTimes(1);
  });

  test("keeps anniversary versions read-only without restore actions", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onViewVersion={vi.fn()}
        onClearVersion={vi.fn()}
      />
    );

    expect(screen.getByRole("button", { name: /查看版本/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /恢复版本/ })).not.toBeInTheDocument();
  });
});
