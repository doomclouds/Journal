import type { JmfValidationResult, TodayEditorState } from "./api";

export type ProductJournalStatus =
  | "not-started"
  | "organizing"
  | "ready-to-save"
  | "dirty"
  | "needs-attention"
  | "saved";

export type ProductJournalStatusView = {
  id: ProductJournalStatus;
  label: string;
  tone: "neutral" | "good" | "warning" | "danger";
  nextStepTitle: string;
  nextStepText: string;
};

const productStatusViews: Record<ProductJournalStatus, ProductJournalStatusView> = {
  "not-started": {
    id: "not-started",
    label: "待开始",
    tone: "neutral",
    nextStepTitle: "写下今天的第一句",
    nextStepText: "先保留原始表达，稍后再整理成正式日记。"
  },
  organizing: {
    id: "organizing",
    label: "整理中",
    tone: "neutral",
    nextStepTitle: "继续整理草稿",
    nextStepText: "补充重点、调整结构，确认无误后再保存。"
  },
  "ready-to-save": {
    id: "ready-to-save",
    label: "可保存",
    tone: "good",
    nextStepTitle: "保存正式日记",
    nextStepText: "当前草稿已通过校验，可以写入正式 Markdown。"
  },
  dirty: {
    id: "dirty",
    label: "有未保存修改",
    tone: "warning",
    nextStepTitle: "先保存草稿",
    nextStepText: "当前编辑还停留在界面里，保存草稿后再确认。"
  },
  "needs-attention": {
    id: "needs-attention",
    label: "需要处理",
    tone: "danger",
    nextStepTitle: "修复校验问题",
    nextStepText: "根据诊断提示修复日记结构，再保存草稿。"
  },
  saved: {
    id: "saved",
    label: "已保存",
    tone: "good",
    nextStepTitle: "今天已归档",
    nextStepText: "正式 Markdown 已保存，后续补充会进入更新流程。"
  }
};

const sectionDisplayTitles: Record<string, string> = {
  "raw-inputs": "今日材料",
  "today-focus": "今天想推进",
  "yesterday-review": "昨天回顾",
  "future-notes": "未来提醒"
};

export function getProductJournalStatus(
  editor: Pick<TodayEditorState, "status" | "validation" | "canConfirm">,
  hasLocalUnsavedChanges = false
): ProductJournalStatusView {
  if (hasLocalUnsavedChanges) {
    return productStatusViews.dirty;
  }

  if (editor.status === "empty") {
    return productStatusViews["not-started"];
  }

  if (editor.status === "attention" || !editor.validation.isValid) {
    return productStatusViews["needs-attention"];
  }

  if (editor.status === "reviewing" && editor.canConfirm) {
    return productStatusViews["ready-to-save"];
  }

  if (editor.status === "processed" || editor.status === "updated") {
    return productStatusViews.saved;
  }

  return productStatusViews.organizing;
}

export function getSectionDisplayTitle(id: string, fallbackTitle: string): string {
  return sectionDisplayTitles[id] ?? fallbackTitle;
}

export function getSectionKindLabel(id: string, isEditableInBlockMode: boolean): string {
  if (id === "raw-inputs") {
    return "保留原话";
  }

  return isEditableInBlockMode ? "可编辑" : "只读";
}

export function getRawInputPreview(text: string, maxLength = 32): string {
  const normalized = text.replace(/\s+/g, " ").trim();
  if (normalized.length <= maxLength) {
    return normalized;
  }

  return `${normalized.slice(0, Math.max(0, maxLength - 4))}...`;
}

export type AssistantSummaryInput = {
  rawInputCount: number;
  editableSectionCount: number;
  dirtySectionCount: number;
};

export function getAssistantSummary(input: AssistantSummaryInput) {
  return {
    rawInputCount: String(input.rawInputCount),
    sectionCount: String(input.editableSectionCount),
    editedCount: String(input.dirtySectionCount)
  };
}

export function getStaticAiStyleLabel(): string {
  return "忠实整理";
}

export function hasSourceDiagnostics(validation: JmfValidationResult | null | undefined): boolean {
  return Boolean(validation?.issues.length);
}
