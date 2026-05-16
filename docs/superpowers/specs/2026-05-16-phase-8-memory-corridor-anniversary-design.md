# Phase 8 同日年轮增强与纪念日数据域设计

> 日期：2026-05-16
> 状态：设计方向已确认，待实现
> 对应方向：同日年轮增强、纪念日管理、同日正式日记阅读
> 原型：[同日年轮记忆回廊原型](./2026-05-16-phase-6c-memory-corridor-prototype.html)

## 1. 背景

Phase 6B 已经交付只读同日年轮：按 `MM-DD` 查询多年同日 entry，复用历史详情、版本快照和 SQLite/FTS 索引。这个 MVP 证明了“多年同一天放在一起看”的价值，但当前体验仍偏历史列表：

- 年份卡片摘要来自 `hits`，更像搜索命中片段，不像日记重点。
- 中间区域仍像历史工作台纸面，没有“时间线 -> 当年日记正文”的清晰切换。
- 常看日期、纪念日、下一年一句话都停留在原型概念，没有本地数据模型。
- 同日年轮内部不应产生编辑、恢复、删除等副作用，但纪念日保存又确实需要新的长期数据域。

Phase 8 把同日年轮升级为 **记忆回廊**：用时间线展示多年同日变化，用正式日记阅读承接细节，用纪念日数据域保存用户主动确认的意义。

## 2. 目标

1. **同日时间线**：中间主区域按年份倒序展示同一 `MM-DD` 的多年记录，卡片只展示轻量重点。
2. **日记阅读态**：点击年份卡片的眼睛按钮后，中间主区域切换为该日期最后正式日记正文；通过 `返回年轮` 回到时间线。
3. **返回路径清晰**：同日年轮主工具栏保留真实位置的 `刷新` 与 `返回今日`；内部阅读态只新增 `返回年轮`。
4. **左侧只做导航**：日期选择、已保存纪念日/常看日期、年份定位，不承载正文阅读。
5. **右侧只做纪念日设置**：保存/编辑当前 `MM-DD` 的纪念日信息和下一年锚点。
6. **纪念日成为独立本地数据域**：新增可导出、可备份、可重建索引的 JSON 源数据。
7. **不自动消费用户提醒**：下一年一句话只作为提醒；用户手动采纳后才进入 raw input / draft context。

## 3. 非目标

本阶段不做：

- AI 自动总结多年变化。
- AI follow-up chat。
- 自动生成常看日期。
- 自动把下一年提醒写入未来正式日记。
- 非今日 restore / confirm。
- 同日年轮内编辑正式 entry。
- 删除日记、删除版本、版本 diff 或 rollback UI。
- item 级 provenance。
- embedding / 语义搜索。
- 云同步、多设备同步、在线账号。

纪念日可以新增和编辑；删除/归档可后续设计，不在本阶段主流程内。

## 4. 现有数据来源

### 4.1 同日年轮摘要

现有接口：

```http
GET /journal/history/anniversary/{monthDay}?limit=50
```

返回 `JournalAnniversaryWheelResult`：

- `monthDay`
- `items[]`
  - `date`
  - `status`
  - `mood`
  - `rawInputCount`
  - `versionCount`
  - `hits[]`
  - `attentionReason`

`hits` 的定位是 **索引命中材料**，不是产品级卡片文案。它可以解释搜索或索引命中来源，也可以作为缺失数据时的兜底，但不能直接等同于同日年轮卡片简介。

### 4.2 当年正式日记阅读

现有接口：

```http
GET /journal/history/{yyyy-MM-dd}
```

返回：

- `markdown`
- `sections`
- `versions`
- `status`
- `attentionReason`

阅读态只展示当前正式 Markdown，也就是该日期最后确认写入的正式日记。若用户需要看旧版本，仍使用已有历史/版本能力；同日年轮阅读态不再提供额外“进入完整日记”按钮。

为显示“最后版本日期”，需要在 history detail 或 anniversary item 中补充当前正式 entry 的最后写入时间：

- 推荐字段：`entryUpdatedAt`
- 来源：SQLite index 中已有的 `entries.last_write_time_utc`，必要时由 scan 从 Markdown 文件 `LastWriteTimeUtc` 重建。
- 含义：当前正式 Markdown 的最后写入时间，不等同于 `.journal/versions/` 里的历史 snapshot 创建时间。

### 4.3 版本数据

现有接口：

```http
GET /journal/history/{yyyy-MM-dd}/versions
GET /journal/history/{yyyy-MM-dd}/versions/{versionId}
```

本阶段同日年轮卡片可以显示版本数量，但不把版本恢复、diff、删除放进同日年轮主流程。

## 5. 新增数据域：纪念日

纪念日不是 SQLite 索引事实源，也不是同日年轮查询结果的附属字段。它是用户主动保存的长期记忆数据。

源文件：

```text
.journal/anniversaries/anniversaries.json
```

推荐结构：

```json
{
  "schema": "journal-anniversaries/v1",
  "items": [
    {
      "id": "anniv-20260516-journal-stage",
      "monthDay": "05-16",
      "title": "Journal 阶段日",
      "type": "project-milestone",
      "originDate": "2024-05-16",
      "description": "从记录习惯逐渐走向个人记忆核心。",
      "pinned": true,
      "createdAt": "2026-05-16T23:42:00+08:00",
      "updatedAt": "2026-05-16T23:42:00+08:00",
      "nextYearNotes": [
        {
          "id": "note-20260516-001",
          "targetDate": "2027-05-16",
          "text": "明年回来看，Journal 是否真的成为了日常记忆核心。",
          "status": "pending",
          "createdAt": "2026-05-16T23:45:00+08:00",
          "adoptedAt": null,
          "rawInputId": null
        }
      ]
    }
  ]
}
```

### 5.1 字段说明

- `monthDay`：纪念日归属月日，格式 `MM-DD`。
- `originDate`：纪念日起点日期，可为空；存在时必须是实际存在的 `yyyy-MM-dd`。
- `type`：初版使用固定枚举，如 `project-milestone`、`growth`、`relationship`、`gratitude`、`self-reminder`。
- `pinned`：是否出现在左侧“纪念日 / 常看日期”列表。
- `nextYearNotes.status`：
  - `pending`：目标日期未到或尚未处理。
  - `adopted`：用户已采纳进今日材料。
  - `dismissed`：用户已忽略，不再提醒。

### 5.2 闰日规则

`02-29` 的下一年锚点使用“下一次真实存在的同月同日”。例如 2026-02-29 不存在，保存下一年提醒时目标日期应落到 2028-02-29，而不是自动改成 02-28 或 03-01。

## 6. 新增接口

### 6.1 读取纪念日

```http
GET /journal/anniversaries
GET /journal/anniversaries/{monthDay}
```

用途：

- 左侧纪念日列表。
- 右侧当前日期纪念日设置。
- 到达目标日期时读取待处理的下一年提醒。

### 6.2 保存或编辑纪念日

```http
POST /journal/anniversaries
PUT /journal/anniversaries/{id}
```

保存内容：

- `monthDay`
- `title`
- `type`
- `originDate`
- `description`
- `pinned`

同一个 `monthDay` 允许多个纪念日，但左侧列表默认按 `pinned`、`updatedAt` 排序。

### 6.3 下一年一句话

```http
POST /journal/anniversaries/{id}/next-year-notes
POST /journal/anniversaries/{id}/next-year-notes/{noteId}/adopt
POST /journal/anniversaries/{id}/next-year-notes/{noteId}/dismiss
```

采纳规则：

1. 目标日期到来时，今日主界面和同日年轮可以显示提醒。
2. 用户点击采纳后，服务端将提醒文本写入当日 raw input。
3. 写入 raw input 后返回 `rawInputId`，并把 note 状态改为 `adopted`。
4. AI 整理时它只是当日材料之一，不自动覆盖 draft 或 formal entry。

## 7. UI 设计

### 7.1 工作区结构

```text
左侧：日期导航 / 纪念日列表 / 年份定位
中间：同日时间线 或 当年正式日记阅读
右侧：当前 MM-DD 的纪念日设置 / 下一年锚点
```

同日年轮仍从现有日记回廊入口进入，不新增顶层主导航。

### 7.2 左侧导航

左侧只做导航：

- 日期选择控件，默认今天。
- 纪念日 / 常看日期列表，数据来自 `anniversaries.json` 中 `pinned = true` 的纪念日。
- 年份定位列表，数据来自 `/journal/history/anniversary/{monthDay}`。

没有保存过纪念日时，纪念日列表显示空状态，不展示原型假数据。

### 7.3 中间时间线

时间线卡片包含：

- 年份和日期。
- 状态。
- raw input 数量。
- version 数量。
- 一段从日记重点 section 派生的简介。
- 眼睛按钮：在中间主区域打开该日期正式日记。

卡片简介规则：

1. 优先从正式日记 section 中提取重点内容。
2. section 优先级：
   - `today-focus`
   - `work`
   - `relationship`
   - `mood`
   - `yesterday-review`
   - `inspiration`
   - 其他可编辑 section
3. 跳过 `raw-inputs`、`metadata-note`、`keywords`。
4. 每张卡片展示 1 到 3 条重点，超长内容截断。
5. 若没有可用 section，再兜底使用 `mood` 或 `${rawInputCount} 条材料`。
6. `hits` 只作为兜底材料，不再直接作为卡片首选简介。

示例：`2026-05-16` 的卡片可以展示：

```text
发布晨间日记软件 v0.1.1 修正版本
优化 AI 系统提示，修复小 bug，提高使用体验
周末带家人出去逛逛
```

### 7.4 中间日记阅读态

点击眼睛按钮后：

- 中间主区域切换为正式日记阅读态。
- 标题区显示该日期和最后版本时间。
- 正文直接渲染 `GET /journal/history/{date}` 返回的 `markdown`。
- 不显示“进入完整日记”或“查看版本”之类额外跳转按钮。
- 顶部显示 `返回年轮`，回到时间线状态。

### 7.5 返回按钮位置

同日年轮主工具栏保持真实应用位置：

- `刷新`
- `返回今日`

其中 `返回今日` 从同日年轮回到主体日记界面。

`返回年轮` 只在内部阅读态出现，用于从某一年正式日记回到同日时间线。

### 7.6 右侧纪念日设置

右侧只处理当前 `MM-DD` 的纪念日数据：

- 意义观察：基于当前同日年轮年份与保存的纪念日说明显示。
- 保存为纪念日：标题、类型、起点年、说明、置顶。
- 下一年锚点：写给下一次同月同日的一句话。

保存后写入 `anniversaries.json`。它不修改正式日记，不触发 AI 整理。

## 8. 数据流

### 8.1 打开同日年轮

```text
用户从日记回廊入口进入
  -> monthDay = 今天 MM-DD
  -> GET /journal/history/anniversary/{monthDay}
  -> GET /journal/anniversaries/{monthDay}
  -> 渲染左侧年份、纪念日状态、中间时间线、右侧设置
```

### 8.2 生成卡片简介

推荐实现：后端在 anniversary 查询时基于已索引 sections 派生卡片重点。

```text
entry_sections
  -> 按 section 优先级选重点
  -> 去掉 Markdown list 前缀
  -> 截断为 1 到 3 条
  -> 返回给前端作为 card preview
```

如果短期不扩返回模型，前端也可以用现有 `hits` 做兜底，但不能把 raw input 当首选简介。

推荐返回模型扩展：

```json
{
  "date": { "isoDate": "2026-05-16" },
  "entryUpdatedAt": "2026-05-16T01:37:50Z",
  "cardPreview": {
    "title": "今日重点",
    "lines": [
      "发布晨间日记软件 v0.1.1 修正版本",
      "优化 AI 系统提示，修复小 bug，提高使用体验"
    ]
  }
}
```

`cardPreview` 是从现有 Markdown sections / indexed sections 派生的展示数据，不是 AI 新生成内容。

### 8.3 打开某一年日记

```text
点击年份卡眼睛按钮
  -> GET /journal/history/{date}
  -> 中间区域切换阅读态
  -> 渲染正式 Markdown
```

### 8.4 保存纪念日

```text
用户填写右侧纪念日设置
  -> POST/PUT /journal/anniversaries
  -> 写 anniversaries.json
  -> 刷新左侧纪念日列表和右侧状态
```

### 8.5 采纳下一年提醒

```text
目标日期到来
  -> 读取 pending nextYearNotes
  -> 用户点击采纳
  -> 写入当日 raw input
  -> note.status = adopted
  -> note.rawInputId = 新 raw input id
  -> 用户后续正常整理/确认
```

## 9. 空状态与错误

- 没有同日历史：中间显示空时间线，右侧仍可保存纪念日。
- 没有纪念日：左侧纪念日区域显示空状态，不生成假常看日期。
- 日期非法：不发 history / anniversary 请求，清空旧结果并显示错误。
- 纪念日 JSON 损坏：读取失败时不影响正式日记和历史索引；界面显示错误，并提示备份/修复。
- history index 损坏：继续遵守现有规则，SQLite 可重建。
- `02-29`：只返回实际存在年份；下一年锚点指向下一次真实闰日。

## 10. 数据安全与备份

- `anniversaries.json` 是长期数据材料，纳入数据导出。
- 数据导入时随整体数据包恢复；本阶段不做复杂合并策略。
- SQLite 可以索引纪念日以便查询，但索引永远不是事实源。
- 保存纪念日和下一年提醒不得覆盖正式 Markdown。
- 采纳下一年提醒只写 raw input，不直接写 draft 或 formal entry。

## 11. 测试清单

后端测试：

- 保存、更新、读取纪念日。
- `MM-DD` 校验和 `02-29` 下一次目标日期计算。
- 下一年提醒 `pending -> adopted -> dismissed` 状态变化。
- 采纳提醒后写入当日 raw input，并记录 `rawInputId`。
- 数据导出包含 `anniversaries.json`，导入可恢复。
- anniversary 查询能派生重点 section 预览，并跳过 `raw-inputs`。

前端测试：

- 打开同日年轮默认选择今天。
- 左侧纪念日列表只显示真实保存/置顶数据。
- 无纪念日时显示空状态。
- 时间线卡片展示重点 section 摘要，不直接展示 raw input 长句。
- 点击眼睛切换到日记阅读态。
- 阅读态渲染正式 Markdown，并只显示最后版本日期。
- `返回年轮` 回到时间线。
- `返回今日` 位于同日年轮主工具栏，回到主体日记界面。
- 保存纪念日后左侧和右侧状态同步刷新。
- 采纳下一年提醒后生成 raw input，不自动确认正式日记。

## 12. 实现落点

后端：

- 新增 `JournalAnniversaryStore`，读写 `.journal/anniversaries/anniversaries.json`。
- 新增 `JournalAnniversaryService`，处理保存、更新、下一年提醒和采纳。
- 新增 Minimal API endpoints：`/journal/anniversaries*`。
- 扩展数据导出/导入服务纳入 anniversaries。
- 扩展 history / anniversary 查询，返回 `entryUpdatedAt` 和卡片重点内容。
- `anniversaries.json` 写入使用临时文件 + 原子替换，避免中途崩溃写坏源数据。

前端：

- 重构 `AnniversaryWheelWorkbench` 为时间线态 / 阅读态。
- 左侧日期控件复用统一 `DatePickerField`。
- 左侧纪念日列表接入真实 `/journal/anniversaries`。
- 右侧纪念日设置接入新增保存接口。
- 卡片摘要改为重点 section 派生内容。
- 删除原型中不存在于真实界面的顶栏假按钮。

## 13. 验收标准

1. 用户进入同日年轮后，可以清楚看到多年同日时间线。
2. 年份卡片展示的是正式日记重点，不是 raw input 副本或搜索命中噪音。
3. 点击卡片眼睛后，中间区域直接展示该日期最后正式日记。
4. `返回年轮` 和 `返回今日` 各自语义清楚、位置符合真实界面。
5. 用户能保存当前 `MM-DD` 为纪念日，并在左侧看到它。
6. 用户能写给下一次同月同日一句话；到期后必须手动采纳才进入 raw input。
7. 所有新增纪念日数据可导出、可导入，不依赖 SQLite 作为事实源。
