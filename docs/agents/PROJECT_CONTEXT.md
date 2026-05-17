# Agent Project Context

本文件承接根目录 `AGENTS.md` 中过长的项目背景。Agent 需要理解当前产品状态、阶段边界和不可过度声称的能力时，优先读这里，再读 `docs/superpowers/archives/INDEX.md` 查证交付历史。

## Product Summary

Journal 是本地优先的晨间日记桌面应用。核心产品链路是：

```text
自然语言输入
  -> raw input 本地持久化
  -> Mock / OpenAI-compatible LLM / Harness Core tool plan
  -> JMF Markdown draft
  -> block/source edit 或 harness execution
  -> JMF validation
  -> user confirmation
  -> version snapshot when overwriting
  -> formal Markdown entry
  -> rebuildable SQLite/FTS history index
  -> history search / same-day memory corridor / version restore workbench
```

Journal 已发布 `v0.1.0` 和 `v0.1.1` 两个 Windows 本地版本。主线在 Windows 本地发布闭环之上继续推进 Phase 8 同日记忆回廊：安装版 Electron 加载打包前端，托管内置 `.NET` backend，并提供 About、法律声明、导入导出、Inno Setup 安装包和 GitHub Actions release workflow。

## Delivered V1 Scope

- 今日自然语言输入和 raw input 持久化。
- Mock AI、OpenAI-compatible LLM provider、候选配置测试、受保护启用和安全 API Key 视图。
- JMF Markdown 草稿、block edit、source edit、JMF validation、attention draft 和用户确认写入正式 Markdown。
- Harness Core：LLM 只能生成 side-effect-free tool plan，服务端执行 append/upsert/revise/no-op，执行结果 draft-only，并记录审计。
- Harness 统一 Today compose submit 和 reorganize existing；重新整理只基于已有 raw inputs，不继承当前 draft 或 confirmed entry。
- Section-level provenance 存在于 JMF marker，普通 preview 隐藏。
- Formal entry overwrite 通过 `EntryWritePipeline`：snapshot old entry -> write Markdown -> update rebuildable SQLite index。
- `.journal/versions/` 保存覆盖前 Markdown 和 metadata；首次写入不创建 snapshot。
- `.journal/index/journal.db` 是可重建缓存，不是事实源。
- History Workbench 支持搜索、日期详情、版本列表/详情、索引 scan/rebuild 和今日版本 restore-to-draft。
- Same-Day Memory Corridor 支持按 `MM-DD` 查看多年同日 timeline cards、formal-entry reading mode、saved anniversaries 和 next-year notes；持久数据写入 `.journal/anniversaries/anniversaries.json`。
- 同日记忆回廊 UI 已采用三栏结构：左侧只做日期、常看纪念日和年份节点导航；中间显示左右交错时间轴卡片和正式日记阅读态；右侧管理纪念日意义、起点日期和下一年提醒。时间轴卡片只显示摘要，单条文本超过 30 字符显示省略号，最多显示 3 条预览。
- Data backup UX：`GET /journal/data/summary` 展示当前 entry/raw input/version 计数；导出生成 ZIP；导入前备份当前 source material。
- Windows release：本地 build/verify installer 脚本和 `.github/workflows/release-windows.yml` 已接入。

## Not Delivered Unless Code Says Otherwise

不要默认声称以下能力已经存在：

- 非今日 restore/confirm。
- AI rewrite follow-up chat。
- Autosave。
- Rich text / WYSIWYG editing。
- In-app recording or speech-to-text。
- Delete flows。
- Item-level provenance。
- Draft diff。
- Entry rollback UI。
- Cloud sync。
- Auto update / signing。
- Full API Key export/import。

## Current Product Boundaries

- Raw user input 是源材料，不得被 summary 覆盖。
- Formal Markdown 是耐久、人类可读的事实源。
- SQLite / FTS index 只是缓存，必须可由 Markdown、raw-input jsonl 和 version files 重建。
- Editor save 永远是 draft write；formal entry 只能由用户确认写入。
- Invalid source/block edit 应形成 `attention` draft 和修复信息，不得部分覆盖 formal entry。
- Same-Day Memory Corridor 不暴露 restore/delete/diff/edit action；saved anniversaries 和 next-year notes 只写入 anniversary store，不改写 formal entry。
- 当前 version restore 只允许恢复今天的版本为 `reviewing` draft。
- UI 风格应保持安静、工具化、可扫描，服务每日写作工作流，不做 marketing landing page。

## Next Development Candidates

下一阶段优先从“让记忆回廊真正有长期价值”继续推进，而不是堆更多入口：

1. 同日记忆回廊增强：多年同日的变化洞察、用户手动标记的纪念日类型、起点/阶段变化说明、重要年份高亮。
2. 周期复盘：月复盘、年复盘、周年回看，把历史材料转成阶段性成长、感恩、关系、能力和情绪稳定性观察。
3. Future Notes：把“写给下一年同一天”的提醒扩展成可回看、可采纳、可忽略、可追踪的未来锚点系统。
4. AI follow-up chat：在 draft 边界内提供追问式改写和澄清，不直接改写 formal entry。
5. Draft diff / entry rollback UI：让用户在确认前清楚看到变更，并把版本恢复能力从 today-only 谨慎扩展出去。
6. 语音输入增强：优先支持外部语音输入法工作流说明，再评估应用内录音和 speech-to-text。
7. 长期可靠性：自动更新、代码签名、可选加密、导出包校验和更清晰的数据迁移策略。

## JMF Section Boundary

当前 active 新内容分类：

- `mood`：状态与情绪。
- `work`：工作与学习。
- `relationship`：生活与关系。
- `health`：健康与精力。
- `money`：财务。
- `inspiration`：灵感与未来提醒。

基础和系统 section：

- `raw-inputs`：原始输入。
- `yesterday-review`：昨日回顾。
- `today-focus`：今日重点。
- `keywords`：关键词。
- `metadata-note`：生成信息。

legacy section 只做旧日记兼容，不作为新内容目标：

- `learning`：学习与思考，合并到 `work`。
- `future-notes`：未来提醒，合并到 `inspiration`。
- `gratitude`：感恩，合并到 `relationship`。

新增块菜单、今日编辑器可新增块列表、Harness prompt catalog 和 Harness operation executor 都应只面向 active sections。`reorganize-existing` 是用户选择式单篇转换：基于 raw inputs 按当前 active 分类重新整理草稿，确认后才覆盖正式 entry。

## Source of Truth

- Product vision: `PROJECT_VISION.md`
- User-facing README: `README.md`
- Delivered history: `docs/superpowers/archives/INDEX.md`
- Specs: `docs/superpowers/specs/`
- Plans: `docs/superpowers/plans/`
- Problems: `docs/superpowers/problems/`
- Release notes: `docs/release/RELEASE_NOTES.md`
