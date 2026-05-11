# Today Workbench Command Surface Redesign Implementation Plan

> Corrected on 2026-05-11 after runtime review. The HTML prototype's outer desktop window, fake titlebar, fake window controls, and simulated menu are presentation-only. The real Electron app must use the native window chrome and native application menu. React owns only the content surface.

## Goal

把 Journal 主界面从调试面板式页面重构为接近已认可 HTML 原型的真实产品工作台：

- 顶部是轻量今日上下文和状态。
- 左侧是日期、原始对话和下一步。
- 中间是日记纸面、段落阅读/编辑和底部输入。
- 右侧是 Today Assistant、AI 整理摘要、今日材料、整理状态和快捷动作。
- 原生菜单中文化；React 内容区不再绘制菜单栏或假窗口。

## Explicit Non-Goals

- 不实现快捷键。
- 不实现 JMF 源码检查器、源码编辑器或高级源码入口。
- 不实现 AI 风格切换，当前只展示固定整理风格。
- 不把 HTML 原型里的 `.app-window`、`.titlebar`、`.window-controls`、模拟 `.menubar` 带入运行时 UI。
- 不新增后端 API。

## Task 1: Correct Runtime Shell Boundary

Files:

- `apps/desktop/electron/main.cjs`
- `apps/desktop/electron/menu.cjs`
- `apps/desktop/electron/preload.cjs`
- `apps/desktop/src/App.tsx`
- `apps/desktop/src/electronMenu.test.ts`
- `apps/desktop/src/App.test.tsx`

Steps:

- Add Electron native Chinese menu: `文件`、`编辑`、`视图`、`窗口`、`帮助`.
- Add `文件 -> LLM 配置` command and send it to renderer through preload bridge.
- Remove React-rendered app menu, fake titlebar, fake window controls, and nested window shell.
- Keep LLM settings opening from the top LLM status pill and native menu.
- Add tests that assert runtime content has no `.app-window` / `.titlebar` / `.window-controls` / `.menubar` / `.menu-panel`.

Acceptance:

- Native menu template exposes Chinese labels.
- Renderer can open LLM settings from native menu bridge.
- The app content root is `main.desktop-shell`.

## Task 2: Match The Approved Command Surface Content

Files:

- `apps/desktop/src/App.tsx`
- `apps/desktop/src/JournalEditor.tsx`
- `apps/desktop/src/JournalBlockCard.tsx`
- `apps/desktop/src/todayWorkbenchView.ts`
- `apps/desktop/src/todayWorkbenchView.test.ts`

Steps:

- Add product view helpers for display status, section titles, raw input preview, assistant statistics, and fixed AI style copy.
- Treat an empty editor as `待开始` even when blank Markdown has validation diagnostics.
- Map `raw-inputs` to `今日材料`, `today-focus` to `今天想推进`, `yesterday-review` to `昨天回顾`.
- Build the runtime content structure:
  - `command-top-context`
  - `context-rail`
  - `journal-stage`
  - `document-scroll`
  - `journal-paper document`
  - `assistant-panel today-assistant`
  - `compose-bar`
- Keep block editing inline on the paper surface, with compact `编辑` chip and `取消` / `保存修改`.
- Keep the bottom input and main actions visible as the primary workflow.
- Put raw input trace, tags, and material mapping in left rail and right assistant.
- Wire `只看日记 / 日记 + 助手` as a real stateful view switch; journal-only mode hides the assistant and expands the paper region.

Acceptance:

- Empty state is product language, not JMF validation/debug language.
- There is one primary `保存日记` action in the content flow.
- The main `重新整理` action remains distinguishable from right-side quick actions.
- Today Assistant shows status, counts, tags, materials, and save target without exposing backend enum names.
- The view switch never becomes decorative-only UI.

## Task 3: Align CSS With Prototype Layout

Files:

- `apps/desktop/src/styles.css`
- `apps/desktop/src/styles.test.ts`

Steps:

- Use prototype-inspired three-column layout:
  - left `260px`
  - center `minmax(520px, 1fr)`
  - right `minmax(360px, 0.72fr)`
- Use paper-like center document with constrained width, serif title, readable paragraphs, and no debug-card chrome.
- Use right assistant cards with compact stats, tags, material list, and quick actions.
- Use rounded quote accents for raw bodies and material items instead of plain straight border lines.
- Restore LLM provider/current-provider letter avatars as centered circular visual anchors.
- Keep feedback messages in a dedicated row above the workbench.
- Add responsive rules:
  - <= 1040px: rail + paper first, assistant below.
  - <= 820px: paper, rail, assistant stacked.
- Keep LLM settings responsive single-scroll behavior.

Acceptance:

- No nested fake window shell.
- No nested scroll trap at narrow sizes.
- `styles.test.ts` locks the command surface regions, responsive rules, and absence of source drawer styles.

## Task 4: Documentation And Visual Verification

Files:

- `docs/superpowers/specs/2026-05-10-today-workbench-command-surface-redesign-design.md`
- `docs/superpowers/archives/2026-05/2026-05-10-today-workbench-command-surface-redesign-archives.md`
- `output/playwright/today-workbench-command-surface-fixed-1440.png`
- `output/playwright/today-workbench-command-surface-fixed-960.png`

Steps:

- Update design and archive docs to state the runtime/prototype boundary.
- Capture current runtime screenshots at 1440x900 and 960x760.
- Compare against `docs/superpowers/specs/2026-05-10-today-workbench-command-surface-prototype.html`.

Acceptance:

- Runtime UI follows the accepted prototype content layout.
- Native app chrome/menu are not duplicated in React.
- Screenshots show the corrected product surface.

## Verification Commands

```powershell
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
dotnet test Journal.slnx
git diff --check
```

Visual verification:

```powershell
npx --yes playwright screenshot --channel msedge --viewport-size "1440,900" --wait-for-selector ".assistant-head" http://127.0.0.1:5173 output/playwright/today-workbench-command-surface-fixed-1440.png
npx --yes playwright screenshot --channel msedge --viewport-size "960,760" --wait-for-selector ".assistant-head" http://127.0.0.1:5173 output/playwright/today-workbench-command-surface-fixed-960.png
```
