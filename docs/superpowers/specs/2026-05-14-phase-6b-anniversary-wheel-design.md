# Phase 6B 同日年轮 MVP 设计

> 日期：2026-05-14
> 状态：待审查
> 对应方向：同日年轮、多年同日回看、历史工作台增强
> 原型：[同日年轮工作台原型](./2026-05-14-phase-6b-anniversary-wheel-prototype.html)

## 1. 背景

Journal 当前已经具备今日写作、Harness 受控整理、审计、正式 entry 覆盖前版本快照、SQLite/FTS 历史索引和历史工作台。现在的能力让用户能可靠地写今天、查历史、看版本，但还没有体现《晨间日记的奇迹》中最有辨识度的体验：**多年同一天的自己放在一起看**。

项目愿景里把同日年轮定义为：

- 以 `MM-DD` 维度聚合不同年份的日记。
- 展示不同年份同一天的关键词、情绪、主题变化。
- 让用户从“今天写一篇”走向“长期人生坐标系统”。

Phase 4A 已经在 SQLite `entries` 表中保存 `month_day`，也已经有 History Workbench 的三栏工作区。因此 Phase 6B 不需要新建一套独立产品结构，应该自然扩展现有历史工作台。

## 2. 目标

Phase 6B 首版目标是 **只读同日年轮 MVP**：

1. **日记纸面增加回廊入口**：默认打开今天的 `MM-DD`，入口包含顶部轻量按钮和日记正文右键菜单。
2. **History Workbench 增加 anniversary 模式**：沿用现有三栏布局，不创建新的顶层页面。
3. **按 `MM-DD` 聚合多年日记**：按年份倒序展示年份卡片摘要流。
4. **保留原始表达价值**：卡片和右侧详情显示 raw inputs 数量与轻量片段。
5. **支持月日选择器**：默认今天，也能切换生日、纪念日或任意合法月日。
6. **复用历史详情能力**：点击年份后可以查看完整历史日记和版本信息。

这一步的产品价值是让 Journal 第一次呈现“长期回看”的核心差异，而不是只继续加固写作链路。

## 3. 非目标

本阶段不做：

- AI 自动总结多年变化。
- AI 改写聊天或 follow-up UI。
- 多日期编辑、非今日确认或任何版本恢复入口。
- diff / rollback UI。
- item 级 provenance。
- 未来提醒 / Future Notes。
- 完整日历页、农历、节假日识别或纪念日管理。
- embedding / 语义搜索。
- 新的数据库事实源。

同日年轮首版是 **历史只读视图**，产品语义是“记忆回廊”。所有修改仍回到现有今日写作和普通历史工作台边界；同日年轮内部不提供恢复草稿、覆盖 entry 或进入编辑态的动作。

## 4. 用户入口

入口采用后续校正方案：**日记回廊菜单入口，打开 History Workbench 的同日年轮模式**。

今日页日记纸面新增低干扰入口：

- 顶部工具栏显示一个轻量 `日记回廊` 图标按钮。
- 日记正文区域支持右键菜单。
- 菜单项：`查看历史`、`同日年轮`。
- 点击 `同日年轮` 后：
  - `workspaceMode` 切到 `history`
  - `historyMode` 或等价状态切到 `anniversary`
  - 默认 `monthDay` 使用今天的 `MM-DD`
  - 触发同日年轮查询

入口不放在 Today Assistant 底部，不在今日材料区展开，也不新建顶部主导航。它是日记纸面上的回看入口，而不是今日写作流程的一部分。

## 5. 工作区布局

同日年轮复用 History Workbench 的三栏结构：

```text
左侧：月日选择 + 常看日期 + 年份索引
中间：同日年轮年份卡片摘要流
右侧：选中年份详情 + raw inputs 摘要 + 完整历史入口
```

### 5.1 左侧：月日选择

左侧顶部显示当前 `MM-DD`，例如 `05-14`。

月日选择器包含：

- 月份选择。
- 日期选择。
- 快速输入 `MM-DD`。
- `查看年轮`。
- `回到今天`。

校验规则：

- 必须是 `MM-DD`。
- 月份范围 `01` 到 `12`。
- 日期必须符合月份最大天数。
- `02-29` 允许，因为闰年历史可能存在；查询时只返回存在记录的年份。
- 非法输入不发请求，直接在左侧显示轻量错误。

首版不做完整日历网格。月日选择器只是 `MM-DD` 选择，不承担按年份选具体日期。

### 5.2 中间：年份卡片摘要流

中间是主体验。按年份倒序展示同一 `MM-DD` 的记录。

每张年份卡片包含：

- 年份和完整日期，例如 `2026-05-14`。
- 状态：`已保存` / `已更新` / `待确认` / `需处理` / `缺失`。
- 情绪或关键词。
- raw input 数量。
- 2 到 4 条核心摘要。
- 动作：`查看完整日记`，有版本时显示 `查看版本`。

核心摘要来源优先级：

1. `today-focus`
2. `yesterday-review`
3. `mood`
4. `work`
5. `learning`
6. `inspiration`
7. 其他可编辑 section
8. raw input hit / raw input snippet

摘要应短而可扫。卡片不是完整 Markdown 渲染，不应把多年日记整篇铺开。

### 5.3 右侧：选中年份详情

右侧显示当前选中的年份：

- 完整日期和状态。
- 年轮摘要统计：年份数、raw input 总数、版本数。
- 该年份 raw inputs 轻量片段。
- attention reason，如果该日期有结构问题。
- 完整历史入口：
  - `打开完整日记`：复用现有 `GET /journal/history/{date}` 和中间完整 Markdown 预览能力。
  - `查看版本`：复用现有版本列表与版本详情。

右侧不提供任何恢复按钮。版本只允许查看快照内容；即使选中的是今天的同日记录，也不能从同日年轮恢复为 draft。

## 6. 数据模型

首版尽量复用现有模型：

```csharp
public sealed record JournalHistoryEntrySummary(
    JournalDate Date,
    string Status,
    string? Mood,
    int RawInputCount,
    int VersionCount,
    IReadOnlyList<JournalHistoryHit> Hits,
    string? AttentionReason);
```

新增一个同日年轮响应模型：

```csharp
public sealed record JournalAnniversaryWheelResult(
    string MonthDay,
    int EntryCount,
    int RawInputCount,
    int VersionCount,
    IReadOnlyList<JournalHistoryEntrySummary> Items);
```

`Items` 里的 `Hits` 用于卡片摘要。首版不新增 AI 生成摘要字段，避免让模型参与历史解释。

## 7. API 设计

新增只读接口：

```text
GET /journal/history/anniversary/{monthDay}
```

示例：

```text
GET /journal/history/anniversary/05-14?limit=50
```

响应：

```json
{
  "monthDay": "05-14",
  "entryCount": 3,
  "rawInputCount": 12,
  "versionCount": 2,
  "items": [
    {
      "date": {
        "isoDate": "2026-05-14",
        "monthDay": "05-14"
      },
      "status": "updated",
      "mood": "开心且兴奋",
      "rawInputCount": 5,
      "versionCount": 1,
      "hits": [
        {
          "sourceType": "section",
          "sectionId": "today-focus",
          "title": "今日重点",
          "snippet": "继续看笔记软件，并处理 Harness 提示词边界问题。"
        }
      ],
      "attentionReason": null
    }
  ]
}
```

错误规则：

- `monthDay` 非 `MM-DD`：`400`，错误信息 `monthDay must use MM-dd`。
- 非法月日如 `02-30`：`400`，错误信息 `monthDay is invalid`。
- 没有记录：`200`，`items: []`。

打开年轮前应调用现有 indexing scan，和 `SearchAsync` 一样保证 SQLite cache 与 Markdown/raw inputs/version files 对齐。

## 8. 索引查询

SQLite 已有 `entries.month_day` 字段。新增查询应在 `JournalIndexStore` 内按 `month_day` 过滤。

查询原则：

- 只查 SQLite cache，不在 API 请求中全量扫 Markdown。
- `JournalHistoryService` 在读取前调用 `JournalIndexingService.ScanAsync`。
- 结果按 `date DESC`。
- limit 默认 50，最大值沿用历史搜索的限制策略。
- 每个日期最多返回有限条 `hits`，避免卡片过长。

hit 生成策略：

1. 优先从 `entry_sections` 读取核心 section。
2. 若某日期没有正式 entry section，但有 raw inputs，则返回 raw input snippet。
3. 对 `attention` / `missing` 状态保留 summary，但 snippet 可以为空或来自 raw inputs。

这能让“只有原始输入但还没有正式日记”的日期也进入年轮，而不是被完全隐藏。

## 9. 前端状态与组件

当前 `App.tsx` 已有 `workspaceMode: "today" | "audit" | "history"`。首版可以扩展 History Workbench 内部模式：

```ts
type HistoryWorkbenchMode = "search" | "anniversary";
```

推荐新增状态：

```ts
const [historyMode, setHistoryMode] = useState<HistoryWorkbenchMode>("search");
const [anniversaryMonthDay, setAnniversaryMonthDay] = useState(today.date.monthDay);
const [anniversaryResult, setAnniversaryResult] = useState<JournalAnniversaryWheelResult | null>(null);
const [anniversarySelectedDate, setAnniversarySelectedDate] = useState("");
```

`HistoryWorkbench` 可以继续作为一个组件，但根据 mode 切换左侧和中间内容：

- `search`：保持现有历史搜索与版本工作台。
- `anniversary`：显示同日年轮布局。

也可以拆出 `AnniversaryWheelWorkbench.tsx`，由 `HistoryWorkbench` 或 `App.tsx` 组合。推荐拆出一个子组件，避免 `HistoryWorkbench.tsx` 继续膨胀。

## 10. 交互流程

### 10.1 从今日页打开

```text
用户点击日记回廊菜单 / 同日年轮
  -> workspaceMode = history
  -> historyMode = anniversary
  -> monthDay = today.monthDay
  -> GET /journal/history/anniversary/{monthDay}
  -> 默认选中最新年份
```

### 10.2 切换月日

```text
用户修改月份 / 日期 / 快速输入
  -> 前端校验 MM-DD
  -> 合法则请求 anniversary API
  -> 清空旧选中详情
  -> 新结果返回后默认选中最新年份
```

需要防止旧请求晚返回覆盖新选择。处理方式和现有历史详情 stale selection 修复一致：以请求发起时的 `monthDay` 作为 guard。

### 10.3 查看完整日记

```text
用户点击某年卡片 / 查看完整日记
  -> selectedDate = yyyy-MM-dd
  -> GET /journal/history/{date}
  -> 中间或右侧显示完整 Markdown 预览
```

首版可以先在右侧提供入口，也可以点击后切回现有 history search/detail 预览态。实现时要避免两个模式抢同一份 stale detail。

## 11. UI 风格

原型已经确认方向可行：

- 继承当前 History Workbench 的三栏工具布局。
- 卡片半径保持 8px。
- 主区域是“年份卡片摘要流”，不是整篇日记瀑布。
- 色彩沿用暖纸面、sage、gold、muted，不引入新主色。
- 左侧月日选择器紧凑，不做大日历。
- 右侧用于详情和 raw inputs 轻量摘要。

原型文件：

[2026-05-14-phase-6b-anniversary-wheel-prototype.html](./2026-05-14-phase-6b-anniversary-wheel-prototype.html)

## 12. 测试策略

### 12.1 后端测试

新增或扩展：

- `JournalIndexStoreTests`
  - 按 `month_day` 查询多年份 entry，按日期倒序。
  - 同一 `MM-DD` 汇总 raw input count 和 version count。
  - 有 raw inputs 但 entry invalid / attention 时仍能返回轻量信息。
  - `02-29` 合法。

- `JournalHistoryServiceTests`
  - 查询 anniversary 前会 scan。
  - 没有结果返回空集合。

- `TodayJournalEndpointTests`
  - `GET /journal/history/anniversary/05-14` 返回结果。
  - 非法 `monthDay` 返回 400。

### 12.2 前端测试

新增或扩展：

- `api.ts`
  - `getAnniversaryWheel("05-14")` 请求正确 endpoint。

- `App.test.tsx`
  - 从日记回廊菜单打开同日年轮。
  - 默认请求今天的 `monthDay`。
  - 切换月日后请求新 `monthDay`。
  - 旧请求晚返回不会覆盖新结果。

- `HistoryWorkbench.test.tsx` 或新 `AnniversaryWheelWorkbench.test.tsx`
  - 渲染年份卡片摘要流。
  - 显示 raw input 数量。
  - 点击年份后触发查看完整日记。
  - 非法月日显示错误且不发请求。

## 13. 验收标准

1. 今日页能进入同日年轮模式。
2. 默认打开今天的 `MM-DD`。
3. 用户能选择任意合法 `MM-DD`。
4. 同日年轮按年份倒序展示多年同日记录。
5. 每张年份卡显示状态、raw input 数量和核心摘要。
6. 没有正式 entry 但有 raw inputs / attention 信息的日期不被静默丢弃。
7. 点击年份可以进入完整历史日记查看。
8. 非法月日不会请求后端，并给出明确提示。
9. 后端 API 不把 SQLite 当事实源；查询前 scan，SQLite 仍是可重建缓存。
10. 不引入跨日期编辑、AI 总结、diff/rollback 或未来提醒。

## 14. 后续扩展

Phase 6B MVP 完成后，可以继续演进：

- AI 年轮总结：让模型总结多年同日情绪、主题和长期变化。
- 纪念日管理：用户保存常看的 `MM-DD`。
- Future Notes：把未来提醒自然接入同日视角。
- 同日趋势：按 section、关键词、情绪做更清晰的长期变化分析。
- Phase 4B diff / rollback：让历史回看和版本恢复更安全。

首版先让用户 **看见多年同一天**。这件事做好，比一次性加很多智能分析更重要。

## 15. Spec 自审

- Placeholder scan：无 `TBD` / `TODO` / 空章节。
- Internal consistency：入口、API、UI、测试都围绕 `MM-DD` 只读聚合，没有跨日期编辑。
- Scope check：范围限定在同日年轮 MVP，不包含 AI 总结、Future Notes、diff/rollback。
- Ambiguity check：`02-29`、非法日期、raw-only/attention 日期、stale 请求处理均已明确。
