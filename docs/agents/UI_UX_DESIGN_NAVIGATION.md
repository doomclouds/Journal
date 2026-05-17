# Agent UI/UX Design Navigation

本文件记录 Journal 当前已经被用户认可的界面结构、视觉边界和后续 UI 工作入口。它不是营销设计稿索引；改前端体验前先读这里，再读对应 spec / prototype / archive。

## Current UI Philosophy

- Journal 是长期自用的本地桌面工具，不是 landing page。
- 信息密度可以高，但必须可扫描、层级清楚、交互路径明确。
- 日记正文是主角；面板、按钮、时间线和辅助信息都服务于阅读、整理和回看。
- 原型一旦被用户认可，后续实现和测试要锁住信息架构，不只检查文字是否出现。

## Main Surfaces

- Today Workbench：今日输入、正式日记纸面、今日材料、Today Assistant 和 JMF 编辑入口。
- LLM Settings：OpenAI-compatible provider 配置、连接测试、受保护启用和安全 API Key 展示。
- Audit Workbench：Harness Core run、tool plan、draft-only 执行和审计记录查看。
- History Workbench：历史搜索、日期详情、版本列表/详情和 today-only restore-to-draft。
- Same-Day Memory Corridor：多年同日回看、时间轴卡片、正式日记阅读态、纪念日资料和下一年提醒。
- Data Backup：本地数据概览、ZIP 导出、导入前备份和安装版内置文本阅读。

## Same-Day Memory Corridor Contract

同日记忆回廊已经从“年份列表”升级为三栏记忆空间：

- 左侧：日期选择、常看纪念日、年份节点导航。左侧年份是中间时间轴节点的导航镜像，不能承载正文摘要或 raw hit 内容。
- 中间：同日时间轴和正式日记阅读态。时间轴使用中轴线、年份节点、左右交错卡片和眼睛按钮；点击眼睛后中间区域切换为该日期最后正式日记，使用当前主体日记阅读方式。
- 右侧：纪念日意义观察、纪念日名称/类型/起点/说明，以及写给下一年同一天的提醒。

时间轴卡片是摘要，不是完整正文：

- 标题和每条预览行超过 30 字符时显示 `...`。
- 每张卡片最多显示 3 条预览行。
- 详细内容必须通过阅读态查看，不在时间轴卡片里展开。
- `hits` 是索引命中材料，不是产品级卡片文案；优先使用 formal entry section 派生的 `cardPreview`。

## Prototype And Asset Map

- Phase 8 design: `docs/superpowers/specs/2026-05-16-phase-8-memory-corridor-anniversary-design.md`
- Accepted prototype: `docs/superpowers/specs/2026-05-16-phase-6c-memory-corridor-prototype.html`
- Implementation plan: `docs/superpowers/plans/2026-05-16-phase-8-memory-corridor-anniversary-implementation-plan.md`
- Delivery archive: `docs/superpowers/archives/2026-05/2026-05-16-phase-8-memory-corridor-anniversary-archives.md`
- Drift problem: `docs/superpowers/problems/2026-05/2026-05-17-memory-corridor-prototype-drift-problem.md`

## Frontend Verification Expectations

When changing a mature UI surface:

1. Add or update focused Vitest coverage for the behavior or layout contract.
2. For prototype-sensitive surfaces, assert stable structure where possible: class, role, `aria-label`, `aria-controls`, selected state, or absence of misplaced content.
3. Run the focused component test first, then the nearby App-level flow.
4. Run `npm run build --prefix apps/desktop` before calling the UI work complete.

Useful focused command:

```powershell
npm test --prefix apps/desktop -- App.test.tsx AnniversaryWheelWorkbench.test.tsx HistoryWorkbench.test.tsx
```

## Next UI Work Candidates

- Memory Corridor insight layer：在不拥挤的前提下展示多年同日变化、起点、重要年份和纪念日意义。
- Month / Year Review：围绕成长、感恩、关系、能力、情绪稳定性和世界观变化做阶段复盘。
- Future Notes surface：把下一年提醒做成可管理、可回看、可采纳的未来锚点。
- Draft diff and confirmation clarity：确认写入前让用户看清楚 AI / Harness / 手工编辑带来的变化。
- Data safety UX：导入、导出、安装版路径、内置阅读器和本地数据说明继续收敛为更正式、更可信的产品体验。
