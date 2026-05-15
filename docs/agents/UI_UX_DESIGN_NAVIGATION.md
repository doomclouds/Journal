# UI/UX Design Navigation

本文件是 Journal 后续 UI/UX 优化的导航入口。它不替代产品不变量，也不替代具体 feature spec；它负责告诉 agent：做 UI 相关任务时先读什么、怎么判断方向、怎么把设计落到现有代码。

## 读取顺序

处理任何 UI/UX、交互、视觉、布局、可访问性、前端体验优化任务时，按顺序读取：

1. `docs/agents/PROJECT_CONTEXT.md`
2. `docs/agents/PRODUCT_INVARIANTS.md`
3. `design-system/journal/MASTER.md`
4. 目标页面 override：
   - Today：`design-system/journal/pages/today-workbench.md`
   - History / Anniversary：`design-system/journal/pages/history-workbench.md`
   - Audit：`design-system/journal/pages/audit-workbench.md`
   - Settings / Data / About：`design-system/journal/pages/settings-and-data.md`
5. 相关历史 spec / prototype / archive：
   - `docs/superpowers/specs/`
   - `docs/superpowers/archives/INDEX.md`
6. 当前代码：
   - `apps/desktop/src/App.tsx`
   - `apps/desktop/src/styles.css`
   - 目标组件和对应 `*.test.tsx`

如果页面 override 不存在，先创建 override，再做大范围视觉或交互变更。

## 当前设计结论

Journal 已经进入 V1 / `0.1.0` Windows 本地发布阶段。接下来不是推翻重写，而是在现有 React/Electron 桌面工作台上做系统化优化。

主方向：

- 保留“纸张中心 + 工具侧栏 + 底部输入”的产品模型。
- 继续沿用纸感、墨色、鼠尾草绿、旧金色的视觉身份。
- 把 `ui-ux-pro-max` 的建议筛选后使用：采用 E-Ink/Paper、Swiss grid、微交互、数据密集工作台；拒绝 Marketplace、Portfolio、手写字体和通用蓝色 SaaS 仪表盘。
- 所有 UI 改动都必须服务日记写作、确认、审计、历史回看和本地数据可信度。

## 设计决策规则

做 UI 方案时先问三件事：

1. 这个改动是否让“写作和确认”更直接？
2. 这个改动是否让“源材料、草稿、正式条目、缓存、版本、审计”的边界更清楚？
3. 这个改动是否能贴着现有组件渐进实现？

如果答案是否定的，大概率是漂亮但不该做。

## 页面导航

| Surface | Primary file | Override | Role |
| --- | --- | --- | --- |
| Today Workbench | `apps/desktop/src/App.tsx`, `JournalEditor.tsx` | `design-system/journal/pages/today-workbench.md` | 写作、草稿、确认、助手 |
| History Search | `HistoryWorkbench.tsx` | `design-system/journal/pages/history-workbench.md` | 搜索、版本、恢复为草稿 |
| Same-Day Anniversary | `AnniversaryWheelWorkbench.tsx` | `design-system/journal/pages/history-workbench.md` | 只读同日回看 |
| Audit Workbench | `AuditWorkbench.tsx` | `design-system/journal/pages/audit-workbench.md` | Harness run 和 provenance 检查 |
| LLM Settings | `LlmSettingsPanel.tsx` | `design-system/journal/pages/settings-and-data.md` | Provider、Key 安全、测试、启用 |
| Backup / About | `App.tsx` modal sections | `design-system/journal/pages/settings-and-data.md` | 数据迁移、版本、法律与安全 |
| Global styling | `styles.css` | `design-system/journal/MASTER.md` | tokens、布局、组件基础 |

## 渐进优化路线

优先级建议：

1. **设计 token 收口**：把散落颜色、边框、阴影逐步收敛到 Master tokens。
2. **页面状态矩阵**：先补 Today、History、Audit、Settings 的 empty/loading/error/attention/success 状态一致性。
3. **命令层级梳理**：统一 primary、secondary、icon、danger、restore、confirm 的视觉和禁用规则。
4. **可访问性补强**：焦点、Tab 顺序、dialog focus restore、reduced motion、窄宽度文本溢出。
5. **信息密度微调**：History/Audit 可以更密，Today 应该更安静。
6. **视觉细节统一**：卡片半径、边框透明度、paper shadow、chip 状态色、hover/focus 反馈。

不要先做大面积改色。先收敛规则，再动具体页面。否则容易又回到“每个组件有自己的审美小宇宙”，这玩意后面会咬人。

## 代码落点

优先保持现有结构：

- 全局视觉和响应式：`apps/desktop/src/styles.css`
- 页面状态和顶层工作台：`apps/desktop/src/App.tsx`
- JMF 编辑：`JournalEditor.tsx`, `JournalBlockCard.tsx`, `InsertBlockMenu.tsx`, `ValidationPanel.tsx`
- LLM 设置：`LlmSettingsPanel.tsx`
- 审计：`AuditWorkbench.tsx`
- 历史：`HistoryWorkbench.tsx`, `AnniversaryWheelWorkbench.tsx`

除非一个组件已经明显承担过多职责，否则不要为了“设计系统化”先拆一堆抽象。先让 tokens、状态和交互一致。

## 验收门槛

UI/UX 改动完成前至少检查：

- 目标 page override 已读取，并且没有冲突。
- 不变量未被破坏：formal entry、raw input、draft、audit、index、API key 安全边界不变。
- 相关前端测试已更新或说明为什么无需更新。
- `npm test --prefix apps/desktop` 或目标 Vitest 已运行。
- 涉及布局/视觉时，至少人工或浏览器检查 375px、768px、1024px、1440px。
- 没有 emoji 图标、低对比文本、hover 位移、不可达按钮、横向溢出。

## 与 Superpowers 资产的关系

- 完成一个可验收 UI/UX requirement 后，归档到 `docs/superpowers/archives/`。
- 发现可复用失败模式，比如 stale selection、focus trap、状态错配、source material 边界误导，优先写入 `docs/superpowers/problems/` 或 inbox。
- 本文件只维护长期设计导航，不承载每个需求的完整过程记录。
