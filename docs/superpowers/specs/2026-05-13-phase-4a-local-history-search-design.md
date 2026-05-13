# Phase 4A Local History & Search Foundation 设计

> 日期：2026-05-13
> 状态：待审查
> 对应方向：版本快照、索引地基、历史搜索与版本恢复
> 原型：[历史搜索与版本恢复工作台原型](./2026-05-13-phase-4a-history-search-layout-prototype.html)

## 1. 背景

Journal 当前主线已交付到 Phase 6：

```text
自然语言输入
  -> Raw input 持久化
  -> LLM Harness Core 受控工具计划
  -> reviewing / attention draft
  -> 块编辑 / 源码编辑 / 审计
  -> 用户确认
  -> 正式 Markdown entry
```

这条链路已经把 AI 写入收束到 draft 边界内，但正式 entry 仍缺少长期可靠性底座：

- 确认写入正式 Markdown 前没有版本快照。
- 没有 SQLite 索引与 FTS 搜索。
- 无法稳定列出历史日期、状态和正文命中。
- 外部手工修改或删除 Markdown 后，应用只能靠当日读取路径感知，缺少全局状态检测。
- 有审计 run，但没有面向日记历史的版本查看与恢复入口。

按照项目愿景，Journal 不是“今天能写就行”的工具，而是长期人生记录系统。Phase 4A 先补可靠性地基，再进入同日年轮、未来日记和更丰富 AI 改写。

## 2. 目标

Phase 4A 采用 **可靠性优先**：

1. **正式 entry 覆盖前创建版本快照**：保护用户已确认过的 Markdown。
2. **索引可重建**：SQLite 是缓存，Markdown 仍是可信源。
3. **外部变化可同步**：文件缺失要标记，文件 hash 漂移时以 Markdown 为可信源重新解析，合法则更新索引，不合法则进入 attention。
4. **历史可浏览可搜索**：提供正式历史工作台，支持日期列表、状态筛选和全文搜索。
5. **版本可恢复到 draft**：恢复动作不直接覆盖正式 entry，仍走 reviewing draft -> 用户确认边界。

## 3. 非目标

本阶段不做：

- item 级 provenance。
- 同日年轮完整体验。
- future notes / 未来提醒完整模型。
- 自动合并外部修改。
- diff 视图和直接 rollback。
- 物理删除、清空、隐藏或加密。
- embedding / 语义搜索 / 向量库。
- 云同步、备份策略和安装包。
- 生产 Electron 托管 .NET 后端。

版本恢复只恢复为 draft，不提供“立刻覆盖正式 entry”。

## 4. 核心方案

采用 **Local History & Search Foundation**：

```text
Entry write pipeline
  -> snapshot old formal entry
  -> write new formal entry
  -> update file hash metadata
  -> update SQLite index + FTS

Startup / refresh scan
  -> compare known entries with filesystem
  -> detect missing / changed
  -> reparse valid changed Markdown
  -> mark invalid or missing status

History workbench
  -> query indexed entries
  -> preview selected entry
  -> list versions
  -> restore selected version as reviewing draft
```

### 4.1 快照策略

首版只在正式 entry 被覆盖前创建快照。

触发点：

- 用户确认当前 draft，且当天 `entries/yyyy/MM/yyyy-MM-dd.md` 已存在。
- 后续任何正式 entry 写入路径，只要会覆盖已有正式 Markdown，都必须先快照。

不触发快照：

- 新日期第一次写入正式 entry。
- raw input 追加。
- harness run 写 reviewing / attention draft。
- block editor / source editor 保存 draft。
- audit run 写入。

原因：

- 当前真正不可逆风险是正式 Markdown 被覆盖。
- draft 仍处在确认边界内，不需要在 Phase 4A 做完整 draft history。
- 快照模型越简单，越容易验证“不会丢正式内容”。

### 4.2 版本路径

版本快照写入隐藏目录：

```text
%LocalAppData%/Journal/.journal/versions/yyyy/MM/yyyy-MM-dd/
  2026-05-13T07-11-14+08-00.md
  2026-05-13T07-11-14+08-00.meta.json
```

`meta.json` 建议包含：

```json
{
  "id": "version-2026-05-13-071114",
  "date": "2026-05-13",
  "createdAt": "2026-05-13T07:11:14+08:00",
  "reason": "confirm-draft",
  "sourceEntryPath": "entries/2026/05/2026-05-13.md",
  "contentHash": "sha256:...",
  "previousStatus": "processed"
}
```

`reason` 首版推荐值：

- `confirm-draft`
- `restore-version-confirm`
- `external-snapshot`

`external-snapshot` 只作为预留，不在 Phase 4A 主流程主动使用。

### 4.3 外部变更检测与同步

索引记录正式 entry 的：

- `content_hash`
- `last_write_time_utc`
- `file_size`
- `indexed_at`
- `status`
- `attention_reason`

打开历史工作台或执行显式扫描时：

- 文件不存在：标记 `missing`。
- 文件存在但 hash 与索引不一致：重新解析 Markdown。
- JMF valid：更新 `entries`、`entry_sections`、FTS 和 hash metadata，使索引反映当前 Markdown。
- JMF invalid：不更新正文索引，标记 `attention`，`attention_reason=invalid_jmf`。
- 文件存在且 hash 一致：保持当前索引状态。

这不是自动合并；系统不把旧索引内容和外部修改内容混合，也不尝试修复用户手写 Markdown。规则只有一个：Markdown 是可信源，SQLite 是可重建缓存。外部修改如果仍是合法 JMF，就应该进入索引；如果结构被破坏，就进入 attention。

### 4.4 SQLite 索引模型

SQLite 是可重建缓存，不是可信源。

首版采用 **分段索引**，而不是每篇日记聚合成一条大文档。这样搜索结果能区分命中来自正式整理段落还是原始表达，也为后续同日年轮和 section 级分析保留结构。

首版表：

```text
journal_meta
entries
entry_sections
entry_versions
raw_inputs
section_fts
raw_input_fts
```

#### journal_meta

保存索引库元信息：

- `key`
- `value`

首版必须包含：

- `schema_version=1`
- `rebuilt_at_utc`

打开历史页或执行扫描时检查 `schema_version`。如果版本不兼容，自动把旧 db 移到 `.journal/index/backups/`，再从 Markdown、raw inputs 和 versions 重建。

#### entries

记录每个正式 entry 的概要：

- `date`
- `month_day`
- `entry_path`
- `status`
- `mood`
- `tags_json`
- `topics_json`
- `content_hash`
- `last_write_time_utc`
- `file_size`
- `indexed_at_utc`
- `attention_reason`

#### entry_sections

记录可检索 section：

- `date`
- `section_id`
- `title`
- `display_order`
- `content`

#### entry_versions

记录快照：

- `id`
- `date`
- `version_path`
- `created_at_utc`
- `reason`
- `content_hash`
- `source_entry_path`

#### raw_inputs

从现有 jsonl 同步索引：

- `id`
- `date`
- `created_at_utc`
- `source`
- `text`

#### section_fts

FTS5 trigram 虚表，索引正式 entry 的 section 文本：

- 日期
- `section_id`
- `title`
- `content`
- tags / topics / mood 的归一化文本

#### raw_input_fts

FTS5 trigram 虚表，索引 raw input 文本：

- `raw_input_id`
- 日期
- `created_at_utc`
- `source`
- `text`

版本快照不进入默认 FTS。否则搜索结果会把旧版本内容和当前正式日记混在一起。用户需要查看旧版本时，通过版本列表进入版本详情。

### 4.5 FTS 与搜索结果

中文搜索首版使用 **SQLite FTS5 trigram**。

原因：

- Journal 主要是中文自然语言日记。
- `unicode61` 对中文词级搜索体验弱。
- 本地单人日记数据量可控，trigram 的索引体积可以接受。
- 比 `LIKE` 更适合后续高亮、分页和多日期搜索。

搜索结果采用两层结构：

```text
左栏：按 entry/date 聚合的结果列表
中间：选中 entry 的正式日记预览和命中高亮
右栏：版本列表、文件状态和恢复动作
```

API 返回时应保留命中来源：

- `sourceType=section`
- `sourceType=raw-input`
- `sectionId`
- `rawInputId`
- `snippet`

左栏仍显示“日记日期”，不要退化成一堆孤立命中片段。选中某天后，再展示该日命中的 section/raw input 摘要。

首版不建 `future_notes` 和 `entry_drafts` 表，避免 Phase 4A 被未来日记和 draft history 拖胖。

### 4.6 索引更新规则

正式 entry 写入成功后：

1. 解析 JMF Markdown。
2. 校验结构。
3. 更新 `entries`。
4. 更新 `entry_sections`。
5. 同步 `section_fts`。
6. 同步 `entry_versions`。

打开历史工作台时：

1. 读取索引。
2. 扫描 `entries/` 目录与 `.journal/raw-inputs/`。
3. 对比 hash / 文件存在性。
4. 对 changed Markdown 执行 JMF parse / validate。
5. valid 时更新索引和 FTS。
6. invalid 时标记 `attention/invalid_jmf`。
7. missing 时标记 `missing`。
8. 同步 raw input jsonl 到 `raw_inputs` 和 `raw_input_fts`。

今日页启动不做全量索引扫描，避免拖慢写日记主流程。正式 entry 写入后仍必须同步增量更新索引，保证新确认内容能立刻被历史工作台搜到。

如果 SQLite 文件损坏或 schema version 不兼容，系统自动把旧 db 备份到 `.journal/index/backups/`，然后从 Markdown、raw inputs 和 versions 全量重建。用户只看到重建完成或重建失败的 warning，不需要先理解 SQLite 内部细节。

## 5. 历史工作台 UI

原型采用 **独立历史工作台**，参考审计工作台的整页切换方式。

入口建议：

- 不在顶部增加 mode tab。当前实际界面顶部只有品牌、日期和 API 健康状态，不承担页面导航。
- 在 `Today Assistant` 里增加 `查看历史` 入口，位置优先靠近「正式文件」或新增「历史与版本」卡片。
- 点击后复用当前 `workspaceMode` 的整页切换方式，类似 `查看审计` 打开 `AuditWorkbench`。

点击后整个工作区从 `today` 切到 `history`，而不是把历史塞进今日左栏或弹层。

### 5.1 布局

复用当前三栏 shell：

```text
左栏：搜索、状态筛选、日期列表
中间：选中日期的正式 entry 预览 / 搜索命中
右栏：版本列表、hash 状态、恢复为 draft
```

这和审计工作台一致：

```text
左栏：日期 / run 列表
中间：时间线 / 主内容
右栏：详情 / provenance / 拒绝原因
```

好处：

- 不污染今日写作主界面。
- 搜索结果、版本列表和恢复操作有足够空间。
- 后续同日年轮可以自然扩展到历史工作台，而不是另开一个产品结构。

历史工作台的 `返回今日` 放在中间 `stage-toolbar`，对齐当前 `AuditWorkbench` 的真实交互，不放在顶部全局栏。

### 5.2 历史左栏

左栏包含：

- 搜索框。
- 状态筛选：全部、processed、updated、reviewing、attention、missing。
- 日期列表，按日期倒序。
- 每条显示日期、状态、raw input 数、版本数、命中摘要。

Phase 4A 不做日历/月视图。日历属于后续浏览体验增强。

### 5.3 中间预览区

中间展示选中 entry：

- 日期与状态。
- 结构化 Markdown 预览。
- 搜索命中高亮。
- `attention` / `missing` 时显示修复说明，不假装内容正常。

如果 entry missing：

- 不展示旧缓存正文为当前真相。
- 可以展示索引摘要，但必须明确标记“文件缺失，内容来自旧索引缓存，仅供定位”。

### 5.4 右侧版本区

右侧展示：

- 当前 entry hash 状态。
- 历史版本列表。
- 快照创建时间、原因、hash。
- `恢复所选版本为草稿`。

恢复动作：

```text
version snapshot
  -> copy content to draft/yyyy/MM/yyyy-MM-dd.md
  -> draft status = reviewing
  -> sourceRawInputIds 保持当前可推导值或空列表
  -> errors = []
  -> user reviews draft
  -> confirm writes formal entry
```

恢复不会直接覆盖正式 entry。确认恢复后的 draft 时，会再次触发正式 entry 覆盖前快照。

## 6. 后端 API 方向

接口名称可在实现计划中微调，但建议形成以下能力。

### 6.1 历史查询

```text
GET /journal/history?query=&status=&from=&to=&limit=&cursor=
```

返回按 entry/date 聚合的结果列表和命中摘要。命中摘要需要标记来源类型：section 或 raw input。

### 6.2 历史详情

```text
GET /journal/history/{date}
```

返回：

- entry metadata。
- rendered markdown / sections。
- status。
- attention reason。
- versions summary。

### 6.3 版本列表

```text
GET /journal/history/{date}/versions
```

返回当天版本列表。

### 6.4 版本详情

```text
GET /journal/history/{date}/versions/{versionId}
```

返回快照 Markdown 预览和 metadata。

### 6.5 恢复版本为草稿

```text
POST /journal/history/{date}/versions/{versionId}/restore-draft
```

写 reviewing draft，不写正式 entry。

### 6.6 索引维护

```text
POST /journal/index/rebuild
POST /journal/index/scan
```

`scan` 在打开历史工作台时触发或由历史服务内部触发。首版 UI 可以不暴露全量重建按钮，但后端需要测试覆盖，方便诊断和后续接设置页。

## 7. 服务边界

建议新增或拆分以下基础设施服务：

- `JournalVersionStore`：负责版本路径、快照写入、版本读取。
- `JournalIndexStore`：负责 SQLite schema、`journal_meta`、query、upsert 和 db 备份重建。
- `JournalIndexingService`：负责从 JMF Markdown / raw inputs / versions 更新分段索引和 FTS。
- `JournalHistoryService`：负责历史 API 的组合查询。
- `EntryWritePipeline` 或等价封装：负责正式 entry 写入前快照、写入后索引更新。

不要把版本和索引逻辑继续塞进 Today service。Today service 可以调用写入管线，但不拥有长期历史模型。

## 8. 错误处理

### 8.1 快照失败

如果正式 entry 已存在，但创建版本快照失败：

- 不覆盖正式 entry。
- 返回错误。
- draft 保持原状态。
- UI 提示“版本快照失败，未写入正式日记”。

这是硬边界。

### 8.2 索引更新失败

如果正式 entry 写入成功，但索引更新失败：

- 正式 Markdown 保留。
- 返回 `attention` 或附带 index warning。
- 后续可通过 `scan/rebuild` 修复索引；如果 db 损坏，先备份旧 db 再重建。

Markdown 是可信源，不能因为索引失败回滚已经成功写入的正式文件。

### 8.3 恢复版本失败

恢复为 draft 失败时：

- 不修改正式 entry。
- 不删除版本快照。
- 返回可读错误。

### 8.4 外部文件缺失或修改

索引发现 missing：

- 历史列表显示 `missing`。
- 不从旧索引缓存静默恢复文件。
- 不自动删除索引记录。

索引发现 hash 漂移：

- 重新解析当前 Markdown。
- JMF valid：更新索引，让历史搜索反映当前文件。
- JMF invalid：标记 `attention/invalid_jmf`，不把损坏结构写入 section 索引。

## 9. 测试策略

后端测试：

- 已有 entry 覆盖前创建版本快照。
- 新 entry 首次写入不创建快照。
- 快照失败时不覆盖正式 entry。
- 正式 entry 写入成功后更新 `entries` / `entry_sections` / `section_fts`。
- raw input jsonl 同步到 `raw_inputs` / `raw_input_fts`。
- FTS5 trigram 可以命中中文子串。
- 搜索结果按 entry 聚合，并包含 section/raw input 命中摘要。
- index scan 标记 missing。
- index scan 对外部修改的 valid JMF 自动更新索引。
- index scan 对外部修改的 invalid JMF 标记 `attention/invalid_jmf`。
- 版本恢复只写 reviewing draft，不写正式 entry。
- 恢复后的 draft 确认时再次创建快照。
- schema version 不兼容或 SQLite 损坏时，备份旧 db 后自动重建。
- SQLite 删除后可从 Markdown / raw inputs / versions 重建。

前端测试：

- 历史工作台可以加载日期列表。
- 搜索和状态筛选调用正确 API。
- 点击历史 entry 显示预览与版本列表。
- 选中 entry 后显示 section/raw input 命中片段。
- 点击恢复版本进入 reviewing draft 状态。
- missing / invalid_jmf 有明确提示。
- 从历史工作台返回今日不丢失今日输入状态。

## 10. 验收标准

Phase 4A 完成后，应满足：

1. 覆盖已有正式 entry 前一定有版本快照。
2. 快照失败不会覆盖正式 entry。
3. 历史工作台能搜索、筛选、打开历史 entry。
4. 用户能看到某天的版本列表。
5. 用户能把某个版本恢复成 reviewing draft。
6. 外部删除正式 entry 后能标记 `missing`。
7. 外部修改正式 entry 后，如果 JMF valid，索引自动更新为当前 Markdown 内容。
8. 外部修改正式 entry 后，如果 JMF invalid，索引标记 `attention/invalid_jmf`。
9. 搜索使用 FTS5 trigram，并能区分 section 命中和 raw input 命中。
10. 搜索结果左栏按 entry 聚合，选中后展示命中片段。
11. SQLite 索引可以从 Markdown、raw inputs 和 versions 重建。

## 11. 后续衔接

Phase 4A 完成后，后续可以自然进入：

- Phase 4B：diff / rollback / 显式重新索引与外部修改处理 UI。
- Phase 6B：同日年轮，在历史工作台中按 `MM-DD` 聚合多年同日。
- Future Notes：未来提醒和长期锚点。
- AI 改写聊天：基于版本快照和恢复边界更安全地做对话式修改。

本阶段先把“长期敢用”的地基钉稳。
