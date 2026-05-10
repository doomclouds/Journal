import { describe, expect, test } from "vitest";
import type { TodayEditorState } from "./api";
import {
  getProductJournalStatus,
  getSectionDisplayTitle,
  getSectionKindLabel,
  hasSourceDiagnostics
} from "./todayWorkbenchView";

const baseEditor = {
  status: "empty",
  canConfirm: false,
  validation: {
    isValid: true,
    issues: []
  }
} as TodayEditorState;

function createEditor(overrides: Partial<TodayEditorState> = {}): TodayEditorState {
  return {
    ...baseEditor,
    ...overrides,
    validation: {
      ...baseEditor.validation,
      ...overrides.validation
    }
  };
}

describe("todayWorkbenchView", () => {
  test("maps backend editor status to product status labels", () => {
    expect(getProductJournalStatus(createEditor({ status: "empty" })).label).toBe("待开始");
    expect(getProductJournalStatus(createEditor({ status: "draft" })).label).toBe("整理中");
    expect(getProductJournalStatus(createEditor({ status: "reviewing", canConfirm: true })).label).toBe("可保存");
    expect(getProductJournalStatus(createEditor({ status: "processed" })).label).toBe("已保存");
    expect(getProductJournalStatus(createEditor({ status: "updated" })).label).toBe("已保存");
    expect(getProductJournalStatus(createEditor({ status: "attention" })).label).toBe("需要处理");
  });

  test("uses validation failure before reviewing readiness", () => {
    const status = getProductJournalStatus(createEditor({
      status: "reviewing",
      canConfirm: true,
      validation: {
        isValid: false,
        issues: [
          {
            code: "missing-section",
            message: "today-focus is required",
            repairHint: "补回 today-focus 区块"
          }
        ]
      }
    }));

    expect(status.id).toBe("needs-attention");
    expect(status.label).toBe("需要处理");
  });

  test("marks local unsaved changes as dirty", () => {
    const status = getProductJournalStatus(createEditor({ status: "reviewing", canConfirm: true }), true);

    expect(status.id).toBe("dirty");
  });

  test("maps section ids to product display titles", () => {
    expect(getSectionDisplayTitle("raw-inputs", "原始输入")).toBe("今日材料");
    expect(getSectionDisplayTitle("today-focus", "今日重点")).toBe("今天想推进");
    expect(getSectionDisplayTitle("gratitude", "感恩记录")).toBe("感恩记录");
  });

  test("maps section kind label by editability", () => {
    expect(getSectionKindLabel("raw-inputs", false)).toBe("保留原话");
    expect(getSectionKindLabel("today-focus", true)).toBe("可编辑");
    expect(getSectionKindLabel("metadata-note", false)).toBe("只读");
  });

  test("detects source diagnostics from validation issues", () => {
    expect(hasSourceDiagnostics({
      isValid: false,
      issues: [
        {
          code: "missing-section",
          message: "today-focus is required",
          repairHint: "补回 today-focus 区块"
        }
      ]
    })).toBe(true);

    expect(hasSourceDiagnostics({
      isValid: true,
      issues: []
    })).toBe(false);
  });
});
