# Today Workbench Productized UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current debug-panel-like today journal screen with a productized daily writing workspace: journal paper first, bottom writing actions, right-side today assistant, inline paragraph editing, and dark advanced JMF source drawer.

**Architecture:** Keep the existing backend contracts and API client unchanged. The implementation is a frontend refactor of `App`, `JournalEditor`, `JournalBlockCard`, `InsertBlockMenu`, `ValidationPanel`, and `styles.css`, with one small view-model helper module for productized labels and status mapping.

**Tech Stack:** Electron + React + Vite + TypeScript, Vitest + Testing Library, existing .NET API contract.

---

## Source Materials

- Spec: `docs/superpowers/specs/2026-05-10-today-workbench-productized-ux-design.md`
- Prototype: `docs/superpowers/specs/2026-05-10-today-workbench-productized-ux-prototype.html`
- Existing frontend entry: `apps/desktop/src/App.tsx`
- Existing editor: `apps/desktop/src/JournalEditor.tsx`
- Existing styles: `apps/desktop/src/styles.css`

## File Structure

- Create `apps/desktop/src/todayWorkbenchView.ts`
  - Maps backend journal/editor state to productized UI labels.
  - Keeps user-facing status names out of JSX conditionals.
- Test `apps/desktop/src/todayWorkbenchView.test.ts`
  - Focused unit tests for status labels and section title normalization.
- Modify `apps/desktop/src/App.tsx`
  - Keeps data loading, API handlers, busy state, and LLM settings behavior.
  - Changes main layout language and structure to top context + paper + bottom compose + today assistant.
- Modify `apps/desktop/src/JournalEditor.tsx`
  - Changes default editor from all-textarea block form to reading-first paper sections.
  - Keeps section ordering and block save contract.
  - Moves source editor into an advanced drawer.
- Modify `apps/desktop/src/JournalBlockCard.tsx`
  - Changes each editable block to preview-by-default with inline editing when selected.
  - Keeps system blocks read-only.
- Modify `apps/desktop/src/InsertBlockMenu.tsx`
  - Changes language from `插入 X` to assistant-friendly `添加 X`.
- Modify `apps/desktop/src/ValidationPanel.tsx`
  - Changes visible language from JMF/attention-first to product explanation first.
- Modify `apps/desktop/src/styles.css`
  - Replaces the current rail/dock/form-heavy workbench styles with the productized paper layout.
  - Preserves LLM settings styles.
- Modify `apps/desktop/src/App.test.tsx`
  - Updates tests from internal labels to productized labels.
  - Adds inline editing and advanced source drawer coverage.
- Modify `apps/desktop/src/styles.test.ts`
  - Adds a CSS contract test for today workbench single-scroll narrow layout.

## Task 1: Add Productized Today View Helpers

**Files:**
- Create: `apps/desktop/src/todayWorkbenchView.ts`
- Create: `apps/desktop/src/todayWorkbenchView.test.ts`

- [ ] **Step 1: Write failing helper tests**

Create `apps/desktop/src/todayWorkbenchView.test.ts`:

```ts
import { describe, expect, test } from "vitest";
import type { JournalStatus, TodayEditorState } from "./api";
import {
  getProductJournalStatus,
  getSectionDisplayTitle,
  hasSourceDiagnostics
} from "./todayWorkbenchView";

function editor(status: JournalStatus, canConfirm: boolean, isValid = true): Pick<TodayEditorState, "status" | "canConfirm" | "validation"> {
  return {
    status,
    canConfirm,
    validation: {
      isValid,
      issues: isValid
        ? []
        : [
            {
              code: "missing-section",
              message: "缺少今日重点区块",
              repairHint: "补回 today-focus 区块后再保存。"
            }
          ]
    }
  };
}

describe("today workbench view helpers", () => {
  test.each([
    ["empty", false, true, "待开始"],
    ["draft", false, true, "整理中"],
    ["reviewing", true, true, "可保存"],
    ["processed", false, true, "已保存"],
    ["updated", false, true, "已保存"],
    ["attention", false, false, "需要处理"]
  ] satisfies Array<[JournalStatus, boolean, boolean, string]>)(
    "maps %s to product label %s",
    (status, canConfirm, isValid, label) => {
      expect(getProductJournalStatus(editor(status, canConfirm, isValid)).label).toBe(label);
    }
  );

  test("uses needs-attention label when validation fails even if status is reviewing", () => {
    expect(getProductJournalStatus(editor("reviewing", false, false)).label).toBe("需要处理");
  });

  test("translates technical section titles into product language", () => {
    expect(getSectionDisplayTitle("raw-inputs", "原始输入")).toBe("今日材料");
    expect(getSectionDisplayTitle("today-focus", "今日重点")).toBe("今天想推进");
    expect(getSectionDisplayTitle("gratitude", "感恩")).toBe("感恩");
  });

  test("detects source diagnostics when validation issues exist", () => {
    expect(hasSourceDiagnostics(editor("reviewing", false, false).validation)).toBe(true);
    expect(hasSourceDiagnostics(editor("reviewing", true, true).validation)).toBe(false);
  });
});
```

- [ ] **Step 2: Run helper tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- todayWorkbenchView.test.ts
```

Expected: fail because `todayWorkbenchView.ts` does not exist.

- [ ] **Step 3: Add helper implementation**

Create `apps/desktop/src/todayWorkbenchView.ts`:

```ts
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

const statusViews: Record<ProductJournalStatus, ProductJournalStatusView> = {
  "not-started": {
    id: "not-started",
    label: "待开始",
    tone: "neutral",
    nextStepTitle: "下一步",
    nextStepText: "写下第一段原始材料。Journal 会保留原话，再整理成可确认的草稿。"
  },
  organizing: {
    id: "organizing",
    label: "整理中",
    tone: "neutral",
    nextStepTitle: "正在整理",
    nextStepText: "正在根据今日材料生成日记草稿。"
  },
  "ready-to-save": {
    id: "ready-to-save",
    label: "可保存",
    tone: "good",
    nextStepTitle: "下一步",
    nextStepText: "这篇草稿可以保存。你也可以先点击纸面里的某一段微调。"
  },
  dirty: {
    id: "dirty",
    label: "有未保存修改",
    tone: "warning",
    nextStepTitle: "先处理修改",
    nextStepText: "保存当前段落或取消修改后，再确认正式日记。"
  },
  "needs-attention": {
    id: "needs-attention",
    label: "需要处理",
    tone: "danger",
    nextStepTitle: "为什么不能保存",
    nextStepText: "草稿还没有通过结构检查。正式日记没有被覆盖，原始表达仍然保留。"
  },
  saved: {
    id: "saved",
    label: "已保存",
    tone: "good",
    nextStepTitle: "已保存",
    nextStepText: "正式 Markdown 已更新。你仍然可以继续补充今天的材料。"
  }
};

export function getProductJournalStatus(
  editor: Pick<TodayEditorState, "status" | "canConfirm" | "validation">,
  hasLocalUnsavedChanges = false
): ProductJournalStatusView {
  if (hasLocalUnsavedChanges) {
    return statusViews.dirty;
  }

  if (!editor.validation.isValid || editor.status === "attention") {
    return statusViews["needs-attention"];
  }

  if (editor.canConfirm || editor.status === "reviewing") {
    return statusViews["ready-to-save"];
  }

  if (editor.status === "processed" || editor.status === "updated") {
    return statusViews.saved;
  }

  if (editor.status === "draft") {
    return statusViews.organizing;
  }

  return statusViews["not-started"];
}

export function getSectionDisplayTitle(id: string, fallbackTitle: string): string {
  switch (id) {
    case "raw-inputs":
      return "今日材料";
    case "today-focus":
      return "今天想推进";
    case "yesterday-review":
      return "昨天回顾";
    case "future-notes":
      return "未来提醒";
    default:
      return fallbackTitle;
  }
}

export function getSectionKindLabel(id: string, isEditableInBlockMode: boolean): string {
  if (id === "raw-inputs") {
    return "保留原话";
  }

  return isEditableInBlockMode ? "可编辑" : "只读";
}

export function hasSourceDiagnostics(validation: JmfValidationResult): boolean {
  return !validation.isValid && validation.issues.length > 0;
}
```

- [ ] **Step 4: Run helper tests and verify pass**

Run:

```powershell
npm test --prefix apps/desktop -- todayWorkbenchView.test.ts
```

Expected: pass.

- [ ] **Step 5: Commit helper task**

```powershell
git add apps/desktop/src/todayWorkbenchView.ts apps/desktop/src/todayWorkbenchView.test.ts
git commit -m "feat: add today workbench view helpers"
```

## Task 2: Productize App Shell, Status, Bottom Compose, and Today Assistant

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`
- Modify: `apps/desktop/src/styles.css`

- [ ] **Step 1: Write failing App shell tests**

In `apps/desktop/src/App.test.tsx`, add these tests inside `describe("App", () => { ... })` after the initial render tests:

```tsx
  test("shows productized today workbench language instead of internal status labels", async () => {
    mockFetchSequence([
      { body: healthResponse },
      { body: createEditorState() },
      { body: deepSeekAiSettings }
    ]);

    render(<App />);

    expect(await screen.findByText("可保存")).toBeInTheDocument();
    expect(screen.getByText("今日材料")).toBeInTheDocument();
    expect(screen.getByText("整理状态")).toBeInTheDocument();
    expect(screen.getByText("下一步")).toBeInTheDocument();
    expect(screen.getByText("DeepSeek 可用")).toBeInTheDocument();
    expect(screen.queryByText("reviewing")).not.toBeInTheDocument();
    expect(screen.queryByText("Raw inputs")).not.toBeInTheDocument();
  });

  test("shows friendly empty state when today has no material", async () => {
    mockFetchSequence([
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

    expect(await screen.findByText("今天先写一句")).toBeInTheDocument();
    expect(screen.getByText("不用先想结构。写一段自然语言，Journal 会保留原话，再帮你整理成可确认的日记草稿。")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "生成草稿" })).toBeInTheDocument();
    expect(screen.queryByText("还没有可编辑的 JMF 草稿")).not.toBeInTheDocument();
  });

  test("shows productized needs-attention state without confirm action", async () => {
    mockFetchSequence([
      { body: healthResponse },
      {
        body: createEditorState({
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
        })
      },
      { body: aiSettings }
    ]);

    render(<App />);

    expect(await screen.findByText("需要处理")).toBeInTheDocument();
    expect(screen.getByText("正式日记没有被覆盖，原始表达仍然保留。")).toBeInTheDocument();
    expect(screen.getByText("title is required")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "确认写入正式日记" })).not.toBeInTheDocument();
  });
```

- [ ] **Step 2: Run App tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "productized|friendly empty|needs-attention"
```

Expected: fail because current UI still shows internal labels and old empty-state language.

- [ ] **Step 3: Import helper and derive product status in App**

In `apps/desktop/src/App.tsx`, add:

```tsx
import { getProductJournalStatus } from "./todayWorkbenchView";
```

Near existing derived values, after `const statusLabel = today?.status ?? loadState;`, add:

```tsx
  const productStatus = editor
    ? getProductJournalStatus(editor)
    : {
        id: loadState === "error" ? "needs-attention" : "organizing",
        label: loadState === "error" ? "需要处理" : "整理中",
        tone: loadState === "error" ? "danger" : "neutral",
        nextStepTitle: loadState === "error" ? "为什么不能保存" : "正在整理",
        nextStepText: loadState === "error" ? "读取今日状态失败，请稍后重试。" : "正在读取今天的日记状态。"
      };
  const activeProviderStatus = activeProviderName === "Mock" ? "Mock 可用" : `${activeProviderName} 可用`;
```

- [ ] **Step 4: Replace App return shell structure**

In `apps/desktop/src/App.tsx`, replace the current `<main className="today-shell">...</main>` JSX body with this structure while keeping all existing handlers and `LlmSettingsPanel` at the bottom:

```tsx
    <main className="today-shell productized-today-shell">
      <header className="top-context">
        <div className="title-block productized-title-block">
          <span className="date-mark">{today?.date.monthDay.slice(3) ?? "--"}</span>
          <div>
            <span className="eyebrow">Journal</span>
            <h1>{title}</h1>
            <p>{inputCount > 0 ? `已保留 ${inputCount} 条原始表达。AI 整理稿待确认保存。` : "今天还没有记录，先随便写一句就可以开始。"}</p>
          </div>
        </div>
        <div className="status-strip" aria-label="今日状态">
          <span className={`status-pill product-status-${productStatus.tone}`}>{productStatus.label}</span>
          <span className="api-pill">API {health?.status ?? (loadState === "error" ? "error" : "checking")}</span>
          <button
            type="button"
            className="llm-status-pill"
            aria-label={`LLM ${activeProviderName}`}
            onClick={() => {
              resetPendingRegenerateDraft();
              setIsLlmPanelOpen(true);
            }}
          >
            {activeProviderStatus}
          </button>
        </div>
      </header>

      {apiError ? (
        <p className="api-error" role="alert">
          {apiError}
        </p>
      ) : null}

      {validationError ? (
        <p className="validation-error" role="alert">
          {validationError}
        </p>
      ) : null}

      <section className="workspace productized-workspace">
        <section className="journal-stage productized-journal-stage" aria-label="日记纸面">
          <article className="journal-paper productized-journal-paper">
            {loadState === "loading" ? <p className="empty-paper">正在读取今天的日记状态...</p> : null}
            {hasEditableJournal && editor ? (
              <JournalEditor
                editor={editor}
                isBusy={isBusy}
                onSaveBlocks={handleSaveBlocks}
                onSaveSource={handleSaveSource}
                onLocalInteraction={resetPendingRegenerateDraft}
              />
            ) : null}
            {loadState !== "loading" && !hasEditableJournal ? (
              <section className="empty-paper productized-empty-paper">
                <h2>今天先写一句</h2>
                <p>不用先想结构。写一段自然语言，Journal 会保留原话，再帮你整理成可确认的日记草稿。</p>
              </section>
            ) : null}
          </article>

          <section className="compose-bar" aria-label="底部输入和主操作">
            <form onSubmit={handleSubmit}>
              <label htmlFor="today-input">补充今天</label>
              <textarea
                id="today-input"
                value={input}
                onChange={event => {
                  resetPendingRegenerateDraft();
                  setInput(event.target.value);
                }}
                placeholder={hasEditableJournal ? "补充一句今天的事，或者让 AI 基于当前材料重新整理这一版..." : "今天发生了什么？直接写原话..."}
                rows={3}
                disabled={isBusy}
              />
              <button type="submit" className="primary-action" disabled={isBusy}>
                生成草稿
              </button>
            </form>
            {hasEditableJournal ? (
              <button type="button" className="secondary-action" onClick={handleRegenerateCurrentDraft} disabled={isBusy}>
                重新整理
              </button>
            ) : null}
            {canConfirm ? (
              <button type="button" className="primary-action" onClick={handleConfirm} disabled={isBusy}>
                保存日记
              </button>
            ) : null}
          </section>
        </section>

        <aside className="today-assistant" aria-label="今日助手">
          <section className={`assistant-panel assistant-panel-${productStatus.tone}`}>
            <h2>{productStatus.nextStepTitle}</h2>
            <p>{productStatus.nextStepText}</p>
            {canConfirm ? (
              <button type="button" className="primary-action" onClick={handleConfirm} disabled={isBusy}>
                确认保存
              </button>
            ) : null}
          </section>

          <section className="assistant-panel">
            <div className="section-head">
              <h2>今日材料</h2>
              <span>{inputCount} 条</span>
            </div>
            {inputCount > 0 ? (
              <ol className="raw-list productized-raw-list">
                {today?.rawInputs.map(raw => (
                  <li key={raw.id}>
                    <strong>{formatRawInputTime(raw.createdAt)}</strong>
                    <p>{raw.text}</p>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="muted">还没有输入。这里之后会显示原始表达摘要。</p>
            )}
          </section>

          <section className="assistant-panel">
            <h2>整理状态</h2>
            <dl className="today-status-list">
              <div>
                <dt>结构</dt>
                <dd>{editor?.validation.isValid ? "内容结构完整，可以保存。" : "草稿结构需要处理。"}</dd>
              </div>
              <div>
                <dt>LLM</dt>
                <dd>{activeProviderName} 整理。</dd>
              </div>
              <div>
                <dt>保存目标</dt>
                <dd>{today?.entry ? "正式 Markdown 已更新。" : "保存后写入本地正式 Markdown。"}</dd>
              </div>
            </dl>
          </section>

          {uniqueAttentionErrors.length > 0 ? (
            <section className="assistant-panel assistant-panel-danger" aria-label="技术详情">
              <h2>技术详情</h2>
              <p>正式日记没有被覆盖，原始表达仍然保留。</p>
              <ul>
                {uniqueAttentionErrors.map(item => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </section>
          ) : null}
        </aside>
      </section>
      {isLlmPanelOpen && aiSettings ? (
        <LlmSettingsPanel
          settings={aiSettings}
          isBusy={isBusy || isSettingsSubmitting}
          onClose={() => {
            resetPendingRegenerateDraft();
            setIsLlmPanelOpen(false);
          }}
          onSettingsChanged={handleAiSettingsChanged}
        />
      ) : null}
    </main>
```

- [ ] **Step 5: Run App tests and verify targeted tests pass**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "productized|friendly empty|needs-attention"
```

Expected: pass for the new tests. Existing old tests may fail because their labels still expect old language; later tasks update them.

- [ ] **Step 6: Commit App shell task**

```powershell
git add apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: productize today workbench shell"
```

## Task 3: Change JournalEditor to Reading-First Inline Editing

**Files:**
- Modify: `apps/desktop/src/JournalEditor.tsx`
- Modify: `apps/desktop/src/JournalBlockCard.tsx`
- Modify: `apps/desktop/src/InsertBlockMenu.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Replace editor component tests with reading-first tests**

In `apps/desktop/src/App.test.tsx`, replace these old `JournalEditor` tests:

- `shows editable today focus textarea in default block mode`
- `inserts an available optional block into the page`
- `keeps block save action before the section list`
- `saves current editable block sections`
- `disables editable block and source textareas while busy`

with:

```tsx
  test("renders editable sections as reading-first blocks", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.getByRole("region", { name: "今天想推进" })).toHaveTextContent("推进 Phase 3");
    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
  });

  test("opens only the selected block as an inline editor", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));

    expect(screen.getByRole("textbox", { name: "编辑 今天想推进" })).toHaveValue("推进 Phase 3");
    expect(screen.queryByRole("textbox", { name: "编辑 今日材料" })).not.toBeInTheDocument();
  });

  test("saves only the selected inline block", () => {
    const onSaveBlocks = vi.fn();
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={onSaveBlocks}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "完成产品化主界面" }
    });
    fireEvent.click(screen.getByRole("button", { name: "保存这一段" }));

    expect(onSaveBlocks).toHaveBeenCalledWith([{ id: "today-focus", content: "完成产品化主界面" }]);
  });

  test("cancels inline edit and restores preview content", () => {
    const onSaveBlocks = vi.fn();
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={onSaveBlocks}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑 今天想推进" }), {
      target: { value: "不保存的内容" }
    });
    fireEvent.click(screen.getByRole("button", { name: "取消编辑 今天想推进" }));

    expect(onSaveBlocks).not.toHaveBeenCalled();
    expect(screen.queryByRole("textbox", { name: "编辑 今天想推进" })).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "今天想推进" })).toHaveTextContent("推进 Phase 3");
  });

  test("adds optional sections through assistant-friendly add action", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "添加 情绪感受" }));

    expect(screen.getByRole("region", { name: "情绪感受" })).toBeInTheDocument();
  });
```

- [ ] **Step 2: Run editor tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "reading-first|selected block|inline block|cancels inline|assistant-friendly"
```

Expected: fail because current editor renders textareas by default and uses `插入`.

- [ ] **Step 3: Update JournalBlockCard props and implementation**

Replace `apps/desktop/src/JournalBlockCard.tsx` with:

```tsx
import type { JmfSection } from "./api";
import { getSectionDisplayTitle, getSectionKindLabel } from "./todayWorkbenchView";

type JournalBlockCardProps = {
  section: JmfSection;
  value: string;
  disabled: boolean;
  isEditing: boolean;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onChange: (content: string) => void;
  onSave: () => void;
};

function renderPreview(section: JmfSection, value: string) {
  const lines = value.trim() ? value.split(/\r?\n/) : ["这一段还没有内容。"];

  if (lines.length > 1 || lines.some(line => line.trim().startsWith("- "))) {
    return (
      <ul className="journal-block-preview-list">
        {lines.map((line, index) => (
          <li key={`${section.id}-${index}`}>{line.replace(/^- /, "") || "\u00a0"}</li>
        ))}
      </ul>
    );
  }

  return <p>{lines[0]}</p>;
}

export function JournalBlockCard({
  section,
  value,
  disabled,
  isEditing,
  onStartEdit,
  onCancelEdit,
  onChange,
  onSave
}: JournalBlockCardProps) {
  const title = getSectionDisplayTitle(section.id, section.title);
  const kindLabel = getSectionKindLabel(section.id, section.isEditableInBlockMode);

  return (
    <section
      className={`journal-block-card ${isEditing ? "editing" : "preview"}`}
      aria-labelledby={`journal-block-${section.id}`}
      aria-label={title}
    >
      <div className="journal-block-heading">
        <div>
          <h2 id={`journal-block-${section.id}`}>{title}</h2>
          <span>{kindLabel}</span>
        </div>
        {section.isEditableInBlockMode && !isEditing ? (
          <button type="button" className="secondary-action compact-action" onClick={onStartEdit} disabled={disabled}>
            编辑 {title}
          </button>
        ) : null}
      </div>

      {section.isEditableInBlockMode && isEditing ? (
        <div className="inline-block-editor">
          <textarea
            aria-label={`编辑 ${title}`}
            value={value}
            disabled={disabled}
            onChange={event => onChange(event.target.value)}
            rows={5}
          />
          <p>段落编辑只影响当前草稿。保存后会重新校验 JMF 结构。</p>
          <div className="journal-block-actions">
            <button type="button" className="secondary-action" onClick={onCancelEdit} disabled={disabled}>
              取消编辑 {title}
            </button>
            <button type="button" className="primary-action" onClick={onSave} disabled={disabled}>
              保存这一段
            </button>
          </div>
        </div>
      ) : (
        <div className={section.isEditableInBlockMode ? "journal-block-preview" : "journal-block-readonly"}>
          {renderPreview(section, value)}
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Update JournalEditor block state and save logic**

In `apps/desktop/src/JournalEditor.tsx`:

1. Import helper:

```tsx
import { getSectionDisplayTitle } from "./todayWorkbenchView";
```

2. Add state after existing `sections` state:

```tsx
  const [editingSectionId, setEditingSectionId] = useState<string | null>(null);
```

3. Reset selected section in the existing `useEffect`:

```tsx
    setEditingSectionId(null);
```

4. Replace `saveBlocks()` with:

```tsx
  function saveSection(id: string) {
    const section = sections.find(item => item.id === id);
    if (!section || !section.isEditableInBlockMode) {
      return;
    }

    onSaveBlocks([{ id: section.id, content: section.content }]);
  }
```

5. Replace the block render loop with:

```tsx
          {sections.map(section => (
            <JournalBlockCard
              key={section.id}
              section={section}
              value={section.content}
              disabled={isBusy}
              isEditing={editingSectionId === section.id}
              onStartEdit={() => {
                onLocalInteraction?.();
                setEditingSectionId(section.id);
              }}
              onCancelEdit={() => {
                setSections(editor.sections);
                setEditingSectionId(null);
              }}
              onChange={content => updateSectionContent(section.id, content)}
              onSave={() => saveSection(section.id)}
            />
          ))}
```

6. Remove the old `保存块编辑草稿` toolbar button when `mode === "blocks"`. The toolbar should keep only source drawer controls in Task 4.

- [ ] **Step 5: Update InsertBlockMenu language**

Replace the button text in `apps/desktop/src/InsertBlockMenu.tsx`:

```tsx
          添加 {section.title}
```

- [ ] **Step 6: Run editor tests and verify pass**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "reading-first|selected block|inline block|cancels inline|assistant-friendly"
```

Expected: pass.

- [ ] **Step 7: Update affected App tests that still query textareas by default**

Replace default queries like:

```tsx
screen.getByRole("textbox", { name: "编辑 今日重点" })
```

with:

```tsx
fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
screen.getByRole("textbox", { name: "编辑 今天想推进" });
```

Use this in tests for saving block edits, stale responses, and disabled block editing.

- [ ] **Step 8: Commit inline editing task**

```powershell
git add apps/desktop/src/JournalEditor.tsx apps/desktop/src/JournalBlockCard.tsx apps/desktop/src/InsertBlockMenu.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: add reading-first journal block editing"
```

## Task 4: Move Source Editing Into a Dark Advanced Drawer

**Files:**
- Modify: `apps/desktop/src/JournalEditor.tsx`
- Modify: `apps/desktop/src/ValidationPanel.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Write failing source drawer tests**

In `apps/desktop/src/App.test.tsx`, replace source-mode tests with:

```tsx
  test("keeps JMF source hidden until advanced drawer is opened", () => {
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={vi.fn()}
      />
    );

    expect(screen.queryByRole("textbox", { name: "编辑完整 JMF Markdown" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "展开高级源码" }));
    expect(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" })).toHaveValue(editorMarkdown);
  });

  test("saves full markdown from advanced source drawer", () => {
    const onSaveSource = vi.fn();
    render(
      <JournalEditor
        editor={createEditorState()}
        isBusy={false}
        onSaveBlocks={vi.fn()}
        onSaveSource={onSaveSource}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "展开高级源码" }));
    fireEvent.change(screen.getByRole("textbox", { name: "编辑完整 JMF Markdown" }), {
      target: { value: "# 2026-05-08\n\n更新后的源码" }
    });
    fireEvent.click(screen.getByRole("button", { name: "保存源码草稿" }));

    expect(onSaveSource).toHaveBeenCalledWith("# 2026-05-08\n\n更新后的源码");
  });

  test("shows validation panel as productized needs-attention explanation", () => {
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

    expect(screen.getByText("这篇草稿需要处理")).toBeInTheDocument();
    expect(screen.getByText("正式日记没有被覆盖，原始表达仍然保留。")).toBeInTheDocument();
    expect(screen.getByText("缺少今日重点区块")).toBeInTheDocument();
  });
```

- [ ] **Step 2: Run source drawer tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "advanced drawer|advanced source|needs-attention explanation"
```

Expected: fail because source mode is still a tab and validation language is old.

- [ ] **Step 3: Replace tablist with advanced drawer state**

In `apps/desktop/src/JournalEditor.tsx`:

1. Remove `type EditorMode = "blocks" | "source";`.

2. Replace `const [mode, setMode] = useState<EditorMode>("blocks");` with:

```tsx
  const [isSourceOpen, setIsSourceOpen] = useState(false);
```

3. Reset source drawer in `useEffect`:

```tsx
    setIsSourceOpen(false);
```

4. Replace the toolbar JSX with:

```tsx
      <div className="journal-editor-toolbar productized-editor-toolbar">
        <div>
          <span className="eyebrow">日记纸面</span>
          <p>默认阅读，点击段落即可编辑。</p>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={() => {
            onLocalInteraction?.();
            setIsSourceOpen(current => !current);
          }}
        >
          {isSourceOpen ? "收起高级源码" : "展开高级源码"}
        </button>
      </div>
```

5. Always render block list. After block list, render the source drawer only when open:

```tsx
      {isSourceOpen ? (
        <section className="journal-source-drawer" aria-label="高级 JMF 源码">
          <div className="source-drawer-head">
            <div>
              <h2>高级：JMF 源码</h2>
              <p>这里显示 front matter、JMF marker 和技术细节。</p>
            </div>
          </div>
          <textarea
            aria-label="编辑完整 JMF Markdown"
            value={sourceMarkdown}
            disabled={isBusy}
            onChange={event => {
              onLocalInteraction?.();
              setSourceMarkdown(event.target.value);
            }}
            rows={14}
          />
          <div className="source-actions">
            <button
              type="button"
              className="editor-save-action"
              onClick={() => onSaveSource(sourceMarkdown)}
              disabled={isBusy}
            >
              保存源码草稿
            </button>
          </div>
        </section>
      ) : null}
```

- [ ] **Step 4: Productize ValidationPanel**

Replace `apps/desktop/src/ValidationPanel.tsx` with:

```tsx
import type { JmfValidationResult } from "./api";

type ValidationPanelProps = {
  validation: JmfValidationResult;
};

export function ValidationPanel({ validation }: ValidationPanelProps) {
  if (validation.isValid || validation.issues.length === 0) {
    return null;
  }

  return (
    <section className="attention-panel productized-attention-panel" aria-label="需要处理">
      <div className="section-head">
        <h2>这篇草稿需要处理</h2>
        <span>需要处理</span>
      </div>
      <p>正式日记没有被覆盖，原始表达仍然保留。</p>
      <ul>
        {validation.issues.map(issue => (
          <li key={`${issue.code}-${issue.message}`}>
            <strong>{issue.message}</strong>
            <p>{issue.repairHint}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
```

- [ ] **Step 5: Run source drawer tests and verify pass**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "advanced drawer|advanced source|needs-attention explanation"
```

Expected: pass.

- [ ] **Step 6: Commit source drawer task**

```powershell
git add apps/desktop/src/JournalEditor.tsx apps/desktop/src/ValidationPanel.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: move jmf source into advanced drawer"
```

## Task 5: Apply Productized Visual Styling and Responsive Contract

**Files:**
- Modify: `apps/desktop/src/styles.css`
- Modify: `apps/desktop/src/styles.test.ts`

- [ ] **Step 1: Add failing CSS contract tests**

Extend `apps/desktop/src/styles.test.ts` with:

```ts
describe("today workbench responsive styles", () => {
  const css = readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), "styles.css"), "utf8");

  test("uses productized today workbench layout classes", () => {
    expect(css).toMatch(/\.productized-workspace\s*\{/);
    expect(css).toMatch(/\.today-assistant\s*\{/);
    expect(css).toMatch(/\.compose-bar\s*\{/);
    expect(css).toMatch(/\.journal-source-drawer\s*\{/);
  });

  test("keeps today workbench to one primary scroll in narrow layout", () => {
    expect(css).toMatch(/@media\s*\(max-width:\s*1180px\)/);
    expect(css).toMatch(/\.productized-workspace\s*\{[^}]*grid-template-columns:\s*1fr;/s);
    expect(css).toMatch(/\.today-assistant\s*\{[^}]*overflow:\s*visible;/s);
  });
});
```

- [ ] **Step 2: Run style tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- styles.test.ts
```

Expected: fail because new classes and responsive rules do not exist yet.

- [ ] **Step 3: Add productized workbench styles**

In `apps/desktop/src/styles.css`, keep existing root/button/LLM settings styles. Replace or override the current main workbench styles by adding this block before `.llm-settings-overlay`:

```css
.productized-today-shell {
  background: #f3f0e8;
}

.productized-title-block {
  display: flex;
  align-items: baseline;
  gap: 14px;
}

.productized-title-block .date-mark {
  color: #2f6d62;
  font-size: 54px;
  font-weight: 900;
  line-height: 0.95;
}

.productized-title-block p {
  margin: 6px 0 0;
  color: #756e63;
  line-height: 1.5;
}

.product-status-good {
  background: #e1eee8;
  color: #2f6d62;
  border-color: #b8d4ca;
}

.product-status-warning {
  background: #f6ead6;
  color: #946523;
  border-color: #e0c596;
}

.product-status-danger {
  background: #f7e3df;
  color: #a84d3f;
  border-color: #e3bdb5;
}

.productized-workspace {
  grid-template-columns: minmax(0, 1fr) 360px;
  grid-template-areas: "paper dock";
  align-items: start;
}

.productized-journal-stage {
  gap: 14px;
}

.productized-journal-paper {
  border-radius: 12px;
  background: #fffdf8;
  box-shadow: 0 10px 30px rgba(44, 38, 28, 0.06);
}

.productized-empty-paper {
  display: block;
  min-height: 360px;
}

.productized-empty-paper h2 {
  margin: 0 0 10px;
  color: #211e1a;
  font-size: 34px;
}

.productized-empty-paper p {
  margin: 0;
  color: #756e63;
  line-height: 1.7;
}

.compose-bar {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  gap: 10px;
  border: 1px solid #ddd5c7;
  border-radius: 12px;
  background: #fffdf8;
  padding: 10px;
  box-shadow: 0 8px 24px rgba(44, 38, 28, 0.06);
}

.compose-bar form {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
}

.compose-bar label {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}

.compose-bar textarea {
  min-height: 52px;
  resize: vertical;
}

.today-assistant {
  grid-area: dock;
  display: grid;
  gap: 12px;
  min-width: 0;
}

.assistant-panel {
  border: 1px solid #ddd5c7;
  border-radius: 12px;
  background: #fffdf8;
  padding: 15px;
  box-shadow: 0 8px 22px rgba(44, 38, 28, 0.04);
}

.assistant-panel h2 {
  margin: 0 0 8px;
  color: #2f2a24;
  font-size: 16px;
}

.assistant-panel p,
.assistant-panel dd {
  color: #756e63;
  line-height: 1.6;
}

.assistant-panel-good {
  border-color: #b8d4ca;
  background: #e1eee8;
}

.assistant-panel-warning {
  border-color: #e0c596;
  background: #f6ead6;
}

.assistant-panel-danger {
  border-color: #e3bdb5;
  background: #f7e3df;
}

.today-status-list {
  display: grid;
  gap: 9px;
  margin: 12px 0 0;
}

.today-status-list div {
  border-top: 1px solid #e6dfd3;
  padding-top: 9px;
}

.today-status-list dt {
  color: #302b25;
  font-size: 12px;
  font-weight: 900;
}

.today-status-list dd {
  margin: 3px 0 0;
  font-size: 12px;
}

.productized-raw-list {
  max-height: none;
}

.journal-block-heading {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.journal-block-heading h2 {
  margin: 0;
}

.journal-block-heading span {
  display: block;
  margin-top: 4px;
  color: #756e63;
  font-size: 12px;
  font-weight: 800;
}

.compact-action {
  min-height: 34px;
}

.journal-block-card.preview {
  border-top: 1px solid #e5ded1;
  border-radius: 0;
  background: transparent;
  padding: 22px 0 0;
}

.journal-block-card.editing {
  border: 1px solid #aed0c6;
  border-radius: 12px;
  background: #f7fbf8;
  padding: 16px;
}

.journal-block-preview,
.journal-block-readonly {
  border: 0;
  background: transparent;
  padding: 0;
}

.inline-block-editor p {
  margin: 8px 0 0;
  color: #5d7f76;
  font-size: 12px;
}

.journal-block-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 10px;
}

.journal-source-drawer {
  border: 1px solid #5b4d3f;
  border-radius: 12px;
  background: #2b251f;
  color: #f3e8da;
  padding: 15px;
}

.journal-source-drawer textarea {
  background: #382f27;
  color: #f3e8da;
  border-color: #5b4d3f;
  font-family: "Cascadia Code", Consolas, "Microsoft YaHei", monospace;
}

.source-drawer-head h2,
.source-drawer-head p {
  color: #f3e8da;
}
```

In the existing `@media (max-width: 1180px)` block, add:

```css
  .productized-workspace {
    grid-template-columns: 1fr;
    grid-template-areas:
      "paper"
      "dock";
    grid-template-rows: auto auto;
  }

  .today-assistant {
    overflow: visible;
  }

  .compose-bar,
  .compose-bar form {
    grid-template-columns: 1fr;
  }
```

- [ ] **Step 4: Run style tests and verify pass**

Run:

```powershell
npm test --prefix apps/desktop -- styles.test.ts
```

Expected: pass.

- [ ] **Step 5: Commit styling task**

```powershell
git add apps/desktop/src/styles.css apps/desktop/src/styles.test.ts
git commit -m "style: apply productized today workbench layout"
```

## Task 6: Full Regression and Visual Verification

**Files:**
- Modify if needed: `apps/desktop/src/App.test.tsx`
- Modify if needed: `apps/desktop/src/styles.css`

- [ ] **Step 1: Run all frontend tests**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: all tests pass. If tests still reference old labels, update them to the new product language without reducing coverage.

- [ ] **Step 2: Build desktop frontend**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: TypeScript and Vite build pass.

- [ ] **Step 3: Run backend tests**

Run:

```powershell
dotnet test Journal.slnx --artifacts-path $env:TEMP\journal-test-artifacts
```

Expected: backend tests pass. This UI refactor should not require backend changes.

- [ ] **Step 4: Start app with dev script**

Run:

```powershell
.\scripts\start-journal-dev.ps1 -RestartApi -RestartVite
```

Expected:

- API health endpoint returns ok.
- Vite starts at `http://127.0.0.1:5173`.
- Electron process launches.

- [ ] **Step 5: Verify productized language in running app**

Use Playwright or manual browser inspection against `http://127.0.0.1:5173`.

Check:

- Top status shows `待开始`, `可保存`, `需要处理`, or `已保存`, not raw backend enum text.
- Main screen has `今日材料`, `整理状态`, and `下一步`.
- `Raw inputs` is not visible.
- JMF source is hidden until `展开高级源码` is clicked.
- Advanced source drawer uses dark background.

- [ ] **Step 6: Verify narrow layout**

Use Playwright viewport checks at `960x640` and `1180x780`.

Expected:

- Paper remains first.
- Bottom compose appears directly under paper.
- Today assistant stacks below.
- No nested independent side/dock scrollbar competes with the page.
- Text does not overlap.

- [ ] **Step 7: Stop dev processes**

Run:

```powershell
.\scripts\stop-journal-dev.ps1
```

Expected: API, Vite, and Electron processes launched by the dev script are stopped.

- [ ] **Step 8: Commit final verification fixes**

If verification required any additional fixes:

```powershell
git add apps/desktop/src/App.test.tsx apps/desktop/src/styles.css apps/desktop/src/JournalEditor.tsx apps/desktop/src/JournalBlockCard.tsx apps/desktop/src/ValidationPanel.tsx
git commit -m "fix: polish today workbench ux verification"
```

If no additional fixes were needed, do not create an empty commit.

## Task 7: Update Delivery Archive

**Files:**
- Create: `docs/superpowers/archives/2026-05/2026-05-10-today-workbench-productized-ux-archives.md`
- Modify: `docs/superpowers/archives/INDEX.md`

- [ ] **Step 1: Create archive after implementation is verified**

Create `docs/superpowers/archives/2026-05/2026-05-10-today-workbench-productized-ux-archives.md`:

```md
# Today Workbench Productized UX

- Date: 2026-05-10
- Status: delivered
- Spec: [2026-05-10-today-workbench-productized-ux-design.md](../../specs/2026-05-10-today-workbench-productized-ux-design.md)
- Prototype: [2026-05-10-today-workbench-productized-ux-prototype.html](../../specs/2026-05-10-today-workbench-productized-ux-prototype.html)

## Summary

The today journal screen was productized from a debug-panel-like workspace into a daily writing interface. The main screen now prioritizes the journal paper, bottom writing actions, productized status labels, a right-side today assistant, inline section editing, and a dark advanced JMF source drawer.

## Delivered

- Productized status mapping for today journal states.
- Empty, ready-to-save, saved, and needs-attention language.
- Reading-first journal paper with inline paragraph editing.
- Bottom compose area for daily writing and main actions.
- Today assistant for next step, material summary, status, and technical details.
- Dark advanced JMF source drawer.
- Responsive rules that avoid competing nested scroll areas.

## Verification

- `npm test --prefix apps/desktop`
- `npm run build --prefix apps/desktop`
- `dotnet test Journal.slnx --artifacts-path $env:TEMP\journal-test-artifacts`
- Visual check at default desktop and narrow viewport.
```

- [ ] **Step 2: Add archive index entry**

Add near the top of `docs/superpowers/archives/INDEX.md`:

```md
- [2026-05-10-today-workbench-productized-ux-archives.md](./2026-05/2026-05-10-today-workbench-productized-ux-archives.md): 归档 Journal 今日工作台从调试面板式界面调整为日记纸面优先、底部输入、今日助手和暗色高级源码抽屉的产品化体验优化。
```

- [ ] **Step 3: Commit archive**

```powershell
git add docs/superpowers/archives/2026-05/2026-05-10-today-workbench-productized-ux-archives.md docs/superpowers/archives/INDEX.md
git commit -m "docs: archive today workbench ux polish"
```

## Self-Review

- Spec coverage:
  - Productized status language: Task 1 and Task 2.
  - Journal paper first layout: Task 2 and Task 5.
  - Bottom compose and main actions: Task 2 and Task 5.
  - Today assistant: Task 2 and Task 5.
  - Reading-first inline editing: Task 3.
  - Dark advanced source drawer: Task 4 and Task 5.
  - Empty and needs-attention states: Task 2 and Task 4.
  - Responsive single-scroll contract: Task 5 and Task 6.
  - Archive after delivery: Task 7.
- Placeholder scan:
  - No unresolved placeholder markers or future-detail gaps are left in this plan.
- Type consistency:
  - New helper uses existing `JournalStatus`, `TodayEditorState`, `JmfValidationResult`.
  - Existing API functions remain unchanged.
  - New productized labels are frontend-only and do not change backend contracts.
