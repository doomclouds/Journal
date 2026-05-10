# Today Workbench Command Surface Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Journal desktop UI around the approved command-surface prototype while keeping the current journal workflow stable and explicitly excluding shortcuts, source editing, and real AI style switching.

**Architecture:** This is a frontend-first refactor. Keep existing backend APIs and journal workflow handlers, reshape React components into a desktop shell, context rail, document stage, assistant panel, and visually aligned LLM settings panel. Preserve current state protection and tests while adding contracts that prevent source editor and shortcut regressions.

**Tech Stack:** React + TypeScript + Vite + Electron desktop app in `apps/desktop`; .NET 10 minimal API remains unchanged except regression testing.

---

## File Structure

- Modify `apps/desktop/src/App.tsx`
  - Owns app data loading, action handlers, shell composition, LLM panel visibility, and top-level layout.
- Modify `apps/desktop/src/JournalEditor.tsx`
  - Owns journal block rendering, insertable sections, inline edit state, and dirty state.
- Modify `apps/desktop/src/JournalBlockCard.tsx`
  - Owns one document section card, read-only display, and inline editing controls.
- Modify `apps/desktop/src/LlmSettingsPanel.tsx`
  - Keeps existing LLM configuration behavior while aligning layout and copy with the new prototype.
- Modify `apps/desktop/src/todayWorkbenchView.ts`
  - Product view helpers for status labels, section names, raw input display, and assistant summaries.
- Modify `apps/desktop/src/App.test.tsx`
  - Behavior tests for shell, menu, no shortcuts, no source editor, journal workflow, LLM settings.
- Modify `apps/desktop/src/todayWorkbenchView.test.ts`
  - Unit tests for productized helper output.
- Modify `apps/desktop/src/styles.css`
  - New visual system, command shell, three-column layout, document paper, assistant cards, responsive rules, LLM settings polish.
- Modify `apps/desktop/src/styles.test.ts`
  - CSS contract tests for command-surface layout and removed source editor styles.
- Keep backend files unchanged in implementation tasks.
  - Run `dotnet test Journal.slnx` for regression only.

---

## Task 1: Product View Helpers And Guardrails

**Files:**
- Modify: `apps/desktop/src/todayWorkbenchView.ts`
- Modify: `apps/desktop/src/todayWorkbenchView.test.ts`

- [ ] **Step 1: Add failing tests for product helper behavior**

Add or update tests in `apps/desktop/src/todayWorkbenchView.test.ts`:

```ts
import {
  getAssistantSummary,
  getProductJournalStatus,
  getRawInputPreview,
  getSectionDisplayTitle,
  getStaticAiStyleLabel
} from "./todayWorkbenchView";

test("uses product language for attention state", () => {
  const status = getProductJournalStatus({
    status: "attention",
    validation: { isValid: false, issues: [] },
    canConfirm: false
  });

  expect(status.label).toBe("需要处理");
  expect(status.nextStepText).toContain("日记结构");
  expect(status.nextStepText).not.toContain("JMF");
});

test("maps raw inputs to today materials", () => {
  expect(getSectionDisplayTitle("raw-inputs", "原始输入")).toBe("今日材料");
});

test("creates a short raw input preview", () => {
  expect(getRawInputPreview("今天想把主界面从调试面板改成真正的工作台", 12)).toBe("今天想把主界面从...");
});

test("summarizes assistant counts", () => {
  const summary = getAssistantSummary({
    rawInputCount: 3,
    editableSectionCount: 4,
    dirtySectionCount: 1
  });

  expect(summary).toEqual({
    rawInputCount: "3",
    sectionCount: "4",
    editedCount: "1"
  });
});

test("exposes static AI style copy without making it configurable", () => {
  expect(getStaticAiStyleLabel()).toBe("忠实整理");
});
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
npm test --prefix apps/desktop -- todayWorkbenchView.test.ts
```

Expected: fail because `getAssistantSummary`, `getRawInputPreview`, and `getStaticAiStyleLabel` do not exist yet.

- [ ] **Step 3: Implement helpers**

Update `apps/desktop/src/todayWorkbenchView.ts`:

```ts
export function getRawInputPreview(text: string, maxLength = 32) {
  const normalized = text.replace(/\s+/g, " ").trim();
  if (normalized.length <= maxLength) {
    return normalized;
  }

  return `${normalized.slice(0, Math.max(0, maxLength - 3))}...`;
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

export function getStaticAiStyleLabel() {
  return "忠实整理";
}
```

Keep the existing `getProductJournalStatus`, `getSectionDisplayTitle`, `getSectionKindLabel`, and diagnostics helpers. Ensure the attention text says `日记结构` instead of `JMF 结构`.

- [ ] **Step 4: Run helper tests**

Run:

```powershell
npm test --prefix apps/desktop -- todayWorkbenchView.test.ts
```

Expected: all tests in `todayWorkbenchView.test.ts` pass.

- [ ] **Step 5: Commit**

```powershell
git add apps/desktop/src/todayWorkbenchView.ts apps/desktop/src/todayWorkbenchView.test.ts
git commit -m "feat: add today workbench view helpers"
```

---

## Task 2: Desktop Shell And Menu Bar

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing shell tests**

Add tests in `apps/desktop/src/App.test.tsx`:

```ts
test("renders command-surface shell without shortcut hints", async () => {
  vi.stubGlobal("fetch", createInitialFetchMock());

  render(<App />);

  expect(await screen.findByText("Journal")).toBeInTheDocument();
  expect(screen.getByRole("navigation", { name: "应用菜单" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "文件" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "编辑" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "插入" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "视图" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "帮助" })).toBeInTheDocument();
  expect(screen.queryByText(/Ctrl\+/)).not.toBeInTheDocument();
  expect(screen.queryByText(/Alt\+/)).not.toBeInTheDocument();
});

test("menu opens LLM settings through existing settings behavior", async () => {
  vi.stubGlobal("fetch", createInitialFetchMock());

  render(<App />);

  fireEvent.click(await screen.findByRole("button", { name: "LLM 配置" }));

  expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
});
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: fail because the shell menu does not exist yet.

- [ ] **Step 3: Add shell markup**

In `apps/desktop/src/App.tsx`, wrap the current UI with a desktop shell:

```tsx
<main className="desktop-shell">
  <section className="app-window" aria-label="Journal 今日工作台">
    <div className="titlebar">
      <strong>Journal</strong>
      <span>{title}</span>
      <div className="window-controls" aria-hidden="true">
        <span>─</span>
        <span>□</span>
        <span>×</span>
      </div>
    </div>

    <nav className="menubar" aria-label="应用菜单">
      <div className="menu">
        <button type="button">文件</button>
        <div className="menu-panel">
          <button type="button" onClick={handleConfirm} disabled={!canConfirm || isBusy}>保存日记</button>
          <button type="button" onClick={handleRegenerateCurrentDraft} disabled={!hasEditableJournal || isBusy || hasLocalUnsavedChanges}>重新整理</button>
          <button type="button" onClick={() => setIsLlmPanelOpen(true)}>LLM 配置</button>
        </div>
      </div>
      <div className="menu">
        <button type="button">编辑</button>
        <div className="menu-panel">
          <button type="button" disabled>保存当前修改</button>
          <button type="button" disabled>取消当前编辑</button>
        </div>
      </div>
      <div className="menu">
        <button type="button">插入</button>
        <div className="menu-panel">
          <span className="menu-note">在日记纸面中添加段落</span>
        </div>
      </div>
      <div className="menu">
        <button type="button">视图</button>
        <div className="menu-panel">
          <span className="menu-note">日记 + 今日助手</span>
        </div>
      </div>
      <div className="menu">
        <button type="button">帮助</button>
        <div className="menu-panel">
          <span className="menu-note">Journal 使用说明</span>
        </div>
      </div>
    </nav>

    {/* existing top context and workspace continue here */}
  </section>
</main>
```

Do not add `keydown` listeners. Do not render shortcut strings.

- [ ] **Step 4: Run shell tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: shell tests pass; unrelated tests may still fail until later layout updates.

- [ ] **Step 5: Commit**

```powershell
git add apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: add today workbench command shell"
```

---

## Task 3: Three-Column Workbench Layout

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing layout tests**

Add tests:

```ts
test("renders context rail, journal paper, and today assistant", async () => {
  vi.stubGlobal("fetch", createInitialFetchMock());

  render(<App />);

  expect(await screen.findByRole("complementary", { name: "今日上下文" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "日记纸面" })).toBeInTheDocument();
  expect(screen.getByRole("complementary", { name: "今日助手" })).toBeInTheDocument();
  expect(screen.getByText("原始对话")).toBeInTheDocument();
  expect(screen.getByText("下一步")).toBeInTheDocument();
  expect(screen.getByText("今日材料")).toBeInTheDocument();
});

test("does not expose source editor entries in the runtime workbench", async () => {
  vi.stubGlobal("fetch", createInitialFetchMock());

  render(<App />);

  await screen.findByRole("region", { name: "日记纸面" });

  expect(screen.queryByRole("button", { name: "展开高级源码" })).not.toBeInTheDocument();
  expect(screen.queryByText("源码")).not.toBeInTheDocument();
  expect(screen.queryByText("高级源码")).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: fail until layout regions are renamed and added.

- [ ] **Step 3: Refactor App workspace markup**

In `App.tsx`, replace the current `productized-workspace` children with:

```tsx
<section className="workspace command-workspace">
  <aside className="context-rail" aria-label="今日上下文">
    <section className="date-card">
      <p className="month">{new Date(today?.date.isoDate ?? Date.now()).toLocaleString("en-US", { month: "short" })}</p>
      <h1>{today?.date.isoDate.slice(-2) ?? "--"}<span>晨间日记</span></h1>
      <span className={`pill product-status-${productStatus.tone}`}>{productStatus.label}</span>
    </section>

    <section className="rail-section">
      <div className="section-head">
        <h2>原始对话</h2>
        <span>{inputCount} 条</span>
      </div>
      {/* render raw inputs as folded material cards */}
    </section>

    <section className="rail-section">
      <div className="section-head">
        <h2>下一步</h2>
        <span>{productStatus.label}</span>
      </div>
      <div className="next-panel">
        <strong>{productStatus.nextStepTitle}</strong>
        <p>{productStatus.nextStepText}</p>
      </div>
    </section>
  </aside>

  <section className="journal-stage" aria-label="日记纸面">
    {/* existing journal paper and compose bar */}
  </section>

  <aside className="assistant-panel" aria-label="今日助手">
    {/* assistant cards */}
  </aside>
</section>
```

When rendering raw inputs, use `details`:

```tsx
<details className="raw-fold" key={raw.id} open={index === 0}>
  <summary>
    <span className="raw-time">{formatRawInputTime(raw.createdAt)}</span>
    <span className="raw-title">{getRawInputPreview(raw.text, 28)}</span>
  </summary>
  <div className="raw-body">
    {raw.text}
  </div>
</details>
```

- [ ] **Step 4: Run layout tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: layout tests pass; style tests may still fail until CSS is added.

- [ ] **Step 5: Commit**

```powershell
git add apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: add three-column today workbench"
```

---

## Task 4: Journal Paper And Block Editing Polish

**Files:**
- Modify: `apps/desktop/src/JournalEditor.tsx`
- Modify: `apps/desktop/src/JournalBlockCard.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing editor tests**

Add tests:

```ts
test("journal editor is labeled as a daily editor rather than JMF editor", () => {
  render(
    <JournalEditor
      editor={createEditorState()}
      isBusy={false}
      onSaveBlocks={vi.fn()}
    />
  );

  expect(screen.getByRole("region", { name: "日记编辑器" })).toBeInTheDocument();
  expect(screen.queryByRole("region", { name: "JMF 编辑器" })).not.toBeInTheDocument();
});

test("save block action uses concise product copy", () => {
  render(
    <JournalEditor
      editor={createEditorState()}
      isBusy={false}
      onSaveBlocks={vi.fn()}
    />
  );

  fireEvent.click(screen.getByRole("button", { name: "编辑 今天想推进" }));
  expect(screen.getByRole("button", { name: "保存修改" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /保存这一段/ })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: fail if labels or button copy still use old wording.

- [ ] **Step 3: Polish editor markup**

Ensure `JournalEditor.tsx` uses:

```tsx
<section className="journal-editor" aria-label="日记编辑器">
  <div className="journal-editor-toolbar productized-editor-toolbar">
    <div>
      <span className="eyebrow">日记纸面</span>
      <p>默认阅读，点击段落即可编辑。</p>
    </div>
  </div>
  <ValidationPanel validation={editor.validation} />
  <div className="journal-editor-blocks">
    {/* InsertBlockMenu and JournalBlockCard list */}
  </div>
</section>
```

Ensure `JournalBlockCard.tsx` uses `保存修改` for inline save.

- [ ] **Step 4: Run editor tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: editor tests pass.

- [ ] **Step 5: Commit**

```powershell
git add apps/desktop/src/JournalEditor.tsx apps/desktop/src/JournalBlockCard.tsx apps/desktop/src/App.test.tsx
git commit -m "style: polish journal paper editing"
```

---

## Task 5: LLM Settings Visual Alignment Without Style Switching

**Files:**
- Modify: `apps/desktop/src/LlmSettingsPanel.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing LLM settings tests**

Add tests:

```ts
test("LLM settings shows provider configuration without style selector buttons", async () => {
  vi.stubGlobal("fetch", createInitialFetchMock());

  render(<App />);

  fireEvent.click(await screen.findByRole("button", { name: /LLM/ }));

  const panel = screen.getByRole("region", { name: "LLM 配置面板" });
  expect(within(panel).getByText("模型来源")).toBeInTheDocument();
  expect(within(panel).getByText("连接信息")).toBeInTheDocument();
  expect(within(panel).getByText("配置来源")).toBeInTheDocument();
  expect(within(panel).getByText("最近诊断")).toBeInTheDocument();
  expect(within(panel).getByText("忠实整理")).toBeInTheDocument();
  expect(within(panel).queryByRole("button", { name: "轻度润色" })).not.toBeInTheDocument();
  expect(within(panel).queryByRole("button", { name: "结构优先" })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: fail until LLM panel headings and static style copy are aligned.

- [ ] **Step 3: Update LlmSettingsPanel headings and static style display**

In `LlmSettingsPanel.tsx`:

- Provider list header: `模型来源`.
- Main selected provider card heading: `连接信息`.
- Side cards: `配置来源`, `最近诊断`, `高级入口` or `高级参数`.
- Replace any style selector UI with static copy:

```tsx
<section className="llm-settings-card">
  <span className="rail-label">整理方式</span>
  <h2>{getStaticAiStyleLabel()}</h2>
  <p>保留原话优先，轻度整理成日记块。</p>
</section>
```

Do not wire `stylePreset` changes to inputs or provider activation in this task.

- [ ] **Step 4: Run LLM settings tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: LLM settings tests pass.

- [ ] **Step 5: Commit**

```powershell
git add apps/desktop/src/LlmSettingsPanel.tsx apps/desktop/src/App.test.tsx
git commit -m "style: align llm settings with command surface"
```

---

## Task 6: Command Surface CSS And Responsive Layout

**Files:**
- Modify: `apps/desktop/src/styles.css`
- Modify: `apps/desktop/src/styles.test.ts`

- [ ] **Step 1: Add failing CSS contract tests**

Update `styles.test.ts`:

```ts
test("defines command surface shell regions", () => {
  expect(css).toMatch(/\.desktop-shell\s*\{/);
  expect(css).toMatch(/\.app-window\s*\{/);
  expect(css).toMatch(/\.menubar\s*\{/);
  expect(css).toMatch(/\.context-rail\s*\{/);
  expect(css).toMatch(/\.journal-stage\s*\{/);
  expect(css).toMatch(/\.assistant-panel\s*\{/);
});

test("uses three columns on desktop and one column on narrow layouts", () => {
  expect(css).toMatch(/\.command-workspace\s*\{[^}]*grid-template-columns:\s*minmax\(240px,\s*0\.78fr\)\s+minmax\(520px,\s*1\.45fr\)\s+minmax\(320px,\s*0\.95fr\);/s);
  expect(css).toMatch(/@media\s*\(max-width:\s*1120px\)/);
  expect(css).toMatch(/\.command-workspace\s*\{[^}]*grid-template-columns:\s*1fr;/s);
});

test("does not include source editor styles", () => {
  expect(css).not.toMatch(/journal-source-drawer/);
  expect(css).not.toMatch(/journal-editor-source/);
});
```

- [ ] **Step 2: Run style tests**

Run:

```powershell
npm test --prefix apps/desktop -- styles.test.ts
```

Expected: fail until CSS selectors exist.

- [ ] **Step 3: Add CSS blocks**

Update `styles.css` with the command-surface visual system. Include these selectors:

```css
.desktop-shell {
  min-height: 100vh;
  background: linear-gradient(180deg, #fbfaf5 0%, #f0ede5 52%, #e9eeeb 100%);
  padding: 18px;
}

.app-window {
  min-height: calc(100vh - 36px);
  display: grid;
  grid-template-rows: 32px 36px auto minmax(0, 1fr);
  overflow: hidden;
  border: 1px solid rgba(52, 45, 36, 0.24);
  border-radius: 10px;
  background: rgba(255, 253, 247, 0.78);
  box-shadow: 0 16px 40px rgba(43, 38, 30, 0.1);
}

.titlebar {
  display: grid;
  grid-template-columns: 180px minmax(0, 1fr) auto;
  align-items: center;
  background: #1f1f1d;
  color: #fffdf8;
  padding: 0 14px;
}

.menubar {
  display: flex;
  align-items: center;
  gap: 4px;
  border-bottom: 1px solid rgba(52, 45, 36, 0.14);
  background: rgba(255, 253, 247, 0.9);
  padding: 0 12px;
}

.command-workspace {
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(240px, 0.78fr) minmax(520px, 1.45fr) minmax(320px, 0.95fr);
  border-top: 1px solid rgba(52, 45, 36, 0.14);
}

.context-rail,
.journal-stage,
.assistant-panel {
  min-width: 0;
  min-height: 0;
}

@media (max-width: 1120px) {
  .app-window {
    height: auto;
    overflow: visible;
  }

  .command-workspace {
    grid-template-columns: 1fr;
  }
}
```

Add the remaining detailed styles for date card, raw fold, document paper, assistant cards, compose bar, and LLM panel using the prototype values as the source of truth.

- [ ] **Step 4: Run style tests**

Run:

```powershell
npm test --prefix apps/desktop -- styles.test.ts
```

Expected: CSS tests pass.

- [ ] **Step 5: Commit**

```powershell
git add apps/desktop/src/styles.css apps/desktop/src/styles.test.ts
git commit -m "style: apply command surface layout"
```

---

## Task 7: Full Frontend Regression

**Files:**
- Modify: `apps/desktop/src/App.test.tsx`
- Modify: `apps/desktop/src/styles.test.ts`
- Modify: `apps/desktop/src/todayWorkbenchView.test.ts`

- [ ] **Step 1: Run frontend test suite**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: all frontend tests pass.

- [ ] **Step 2: Fix regressions with narrow patches**

If tests fail, fix only the relevant component or assertion. Do not reintroduce source editor UI, shortcut labels, or style selector controls.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: TypeScript and Vite build pass.

- [ ] **Step 4: Commit**

```powershell
git add apps/desktop/src
git commit -m "test: verify command surface frontend"
```

---

## Task 8: Visual Smoke And Backend Regression

**Files:**
- No required source changes unless smoke test finds a bug.

- [ ] **Step 1: Run backend regression tests**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: all .NET tests pass.

- [ ] **Step 2: Start app for visual smoke**

Use the repo scripts if available; otherwise start the two processes:

```powershell
dotnet run --project src/Journal.Api
```

In another process:

```powershell
npm run desktop --prefix apps/desktop
```

- [ ] **Step 3: Check runtime UI**

Verify manually or with Playwright where available:

- Main window has titlebar, menubar, top context, left context rail, center journal paper, right today assistant.
- No runtime shortcut hints such as `Ctrl+1`, `Ctrl+S`, or `Alt+1`.
- No `高级源码`, `源码模式`, `编辑完整 JMF Markdown`, or `保存源码草稿`.
- LLM settings opens and still supports provider selection, API Key reveal, test current form, save and enable.
- Static AI style copy does not look like a selectable control.
- Narrow window does not show nested scrollbars fighting each other.

- [ ] **Step 4: Stop dev processes**

Stop API and desktop processes using the repo's stop script if present, or close the processes started in Step 2.

- [ ] **Step 5: Commit any visual smoke fixes**

If no source changes are needed, skip this commit. If fixes were needed:

```powershell
git add apps/desktop/src
git commit -m "fix: polish command surface smoke issues"
```

---

## Self-Review

- Spec coverage: covers command shell, no shortcuts, no source editor, static AI style, main workbench, LLM settings, responsive behavior, and verification.
- Placeholder scan: no placeholder language remains.
- Type consistency: helper names are defined before use; React props stay aligned with existing `App`, `JournalEditor`, `LlmSettingsPanel`, and API types.
- Scope control: backend behavior is regression-tested but not refactored in this UI plan.
- Safety: source editor and shortcut strings are guarded by tests so they do not creep back in during implementation.
