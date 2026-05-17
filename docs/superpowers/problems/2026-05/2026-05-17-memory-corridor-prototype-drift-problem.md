# Memory Corridor Prototype Drift

- Date: `2026-05-17`
- Topic slug: `memory-corridor-prototype-drift`
- Status: `Captured`
- Scope: `UI`
- Tags: `memory-corridor`, `prototype`, `timeline`, `anniversary`, `testing`

## Symptom

Phase 8 同日记忆回廊已经有明确原型和设计文档，但落到桌面端后，中间区域变成普通竖向列表，左侧年份也变成摘要列表，而不是与中间时间轴节点绑定的年份导航。用户查看后连续指出“为什么不是按原型做成时间轴”“左侧年份选择跟原先不一样”“年份选择应该跟时间轴节点挂钩”。

## Trigger / Context

- Phase 8 原型要求中间使用同日多年时间轴，年份节点位于中轴线上，卡片左右交错并用连接线指向节点。
- Phase 8 设计要求左侧只做日期导航、已保存纪念日和年份定位，不承载正文阅读或摘要内容。
- 现有测试主要验证数据加载、阅读态和卡片文案出现，没有把“左侧年份节点导航”和“中间时间轴结构”作为可回归的 UI 合同。
- 用户人工验收发现实现虽然功能可用，但视觉结构和信息组织偏离原型。

## Root Cause

实现阶段把“多年同日卡片”理解成了普通年份摘要流，并复用了历史列表的组件语义；测试也沿用了“左侧年份列表展示卡片摘要”的旧断言，等于把偏离原型的结构固化成通过条件。

真正缺失的是原型结构合同：左侧年份应该是中间时间轴节点的导航镜像，节点需要通过稳定 id / `aria-controls` 绑定；中间卡片才承载简介、眼睛阅读按钮和正式日记入口。没有这层合同，功能测试通过也无法证明体验符合记忆回廊原型。

## Fix

- 将 `AnniversaryWheelWorkbench` 的中间区域改为 `memory-corridor-timeline`，使用中轴线、年份节点、左右交错卡片、连接线和眼睛阅读按钮。
- 将左侧年份改为 `anniversary-year-node-list`，每个年份按钮通过 `aria-controls="memory-entry-YYYY-MM-DD"` 绑定中间对应节点，点击时滚动定位，有记录的年份同步选中对应日期。
- 对闰日等非真实日期年份显示“无此日”，避免 2025-02-29 这类节点被误标成“去年”。
- 更新测试：验证时间轴结构、左右卡片 class、年份节点绑定、左侧不再显示卡片摘要，以及 App 层换月日后旧摘要不会残留。

## Why This Fix

只调整 CSS 让列表“看起来像时间轴”会继续保留错误的信息架构：左侧仍会混入摘要，中间也缺少可定位节点。重新建立节点导航和时间轴的结构绑定，能同时修复视觉、交互和测试合同，让未来改样式时不容易把记忆回廊退回普通列表。

## Recognition Clues

- 原型里有 timeline spine、year pin、左右卡片或 node navigation，但实现里只看到普通 list/card 流。
- 左侧导航区域出现正文摘要、卡片标题或 raw hit 文案。
- 测试断言只检查“文字出现”，没有检查稳定结构 class、节点 id、`aria-controls` 或阅读入口位置。
- 用户反馈集中在“看起来不像原型”“信息杂乱”“节点/线/卡片位置不对”。

## Applicability / Non-Applicability

### Applies When

- 有正式 UI 原型或设计文档，并且用户已经明确接受了某种信息架构。
- 页面价值来自空间关系、节点定位、左右分布、时间线或导航绑定，而不是单纯数据是否出现。
- 测试容易因为只断言文案存在而放过布局语义漂移。

### Does Not Apply When

- 需求只涉及文案、颜色、间距等轻量视觉修补，没有改变信息架构。
- 原型只是探索稿，用户尚未认可其结构，不应把探索稿直接写成硬合同。
- 页面本身就是普通列表，额外引入节点导航会增加无意义复杂度。

## Related Artifacts

- Spec: [2026-05-16-phase-8-memory-corridor-anniversary-design.md](../../specs/2026-05-16-phase-8-memory-corridor-anniversary-design.md)
- Plan: [2026-05-16-phase-8-memory-corridor-anniversary-implementation-plan.md](../../plans/2026-05-16-phase-8-memory-corridor-anniversary-implementation-plan.md)
- Archive: [2026-05-16-phase-8-memory-corridor-anniversary-archives.md](../../archives/2026-05/2026-05-16-phase-8-memory-corridor-anniversary-archives.md)
- Related Problems:
  - [Phase 7 Problem Gate Omission](./2026-05-15-phase-7-problem-gate-omission-problem.md)
- Code or Test:
  - [AnniversaryWheelWorkbench.tsx](../../../../apps/desktop/src/AnniversaryWheelWorkbench.tsx)
  - [AnniversaryWheelWorkbench.test.tsx](../../../../apps/desktop/src/AnniversaryWheelWorkbench.test.tsx)
  - [App.test.tsx](../../../../apps/desktop/src/App.test.tsx)
  - [styles.css](../../../../apps/desktop/src/styles.css)
