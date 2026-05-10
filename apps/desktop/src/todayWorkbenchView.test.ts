import { describe, expect, test } from "vitest";
import type { TodayEditorState } from "./api";
import {
  getProductJournalStatus,
  getSectionDisplayTitle,
  getSectionKindLabel,
  hasSourceDiagnostics
} from "./todayWorkbenchView";

type EditorStatusInput = Pick<TodayEditorState, "status" | "canConfirm" | "validation">;

const baseEditor: EditorStatusInput = {
  status: "empty",
  canConfirm: false,
  validation: {
    isValid: true,
    issues: []
  }
};

function createEditor(overrides: Partial<EditorStatusInput> = {}): EditorStatusInput {
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

  test("uses product tone names that match the CSS contract", () => {
    expect(getProductJournalStatus(createEditor({ status: "empty" })).tone).toBe("neutral");
    expect(getProductJournalStatus(createEditor({ status: "draft" })).tone).toBe("neutral");
    expect(getProductJournalStatus(createEditor({ status: "reviewing", canConfirm: true })).tone).toBe("good");
    expect(getProductJournalStatus(createEditor({ status: "processed" })).tone).toBe("good");
    expect(getProductJournalStatus(createEditor({ status: "updated" })).tone).toBe("good");
    expect(getProductJournalStatus(createEditor({ status: "attention" })).tone).toBe("danger");
    expect(getProductJournalStatus(createEditor({ status: "reviewing", canConfirm: true }), true).tone).toBe("warning");
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

  test("uses validation invalid flag before reviewing readiness even without display issues", () => {
    const invalidWithoutIssues = {
      isValid: false,
      issues: []
    };
    const status = getProductJournalStatus(createEditor({
      status: "reviewing",
      canConfirm: true,
      validation: invalidWithoutIssues
    }));

    expect(status.id).toBe("needs-attention");
    expect(hasSourceDiagnostics(invalidWithoutIssues)).toBe(false);
  });

  test("marks local unsaved changes as dirty", () => {
    const status = getProductJournalStatus(createEditor({ status: "reviewing", canConfirm: true }), true);

    expect(status.id).toBe("dirty");
  });

  test("maps section ids to product display titles", () => {
    expect(getSectionDisplayTitle("raw-inputs", "原始输入")).toBe("今日材料");
    expect(getSectionDisplayTitle("today-focus", "今日重点")).toBe("今天想推进");
    expect(getSectionDisplayTitle("yesterday-review", "昨日回顾")).toBe("昨天回顾");
    expect(getSectionDisplayTitle("future-notes", "未来备注")).toBe("未来提醒");
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

    expect(hasSourceDiagnostics({
      isValid: false,
      issues: []
    })).toBe(false);
  });
});
