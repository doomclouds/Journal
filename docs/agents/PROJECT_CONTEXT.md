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
  -> history search / same-day anniversary / version restore workbench
```

V1 / `0.1.0` 已进入 Windows 本地发布闭环：安装版 Electron 加载打包前端，托管内置 `.NET` backend，并提供 About、法律声明、导入导出、Inno Setup 安装包和 GitHub Actions release workflow。

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
- Same-Day Anniversary Wheel 是只读记忆回廊：按 `MM-DD` 查看多年同日 entry、raw material snippets 和版本快照。
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
- Anniversary mode 只读，不暴露 restore/delete/diff/edit action。
- 当前 version restore 只允许恢复今天的版本为 `reviewing` draft。
- UI 风格应保持安静、工具化、可扫描，服务每日写作工作流，不做 marketing landing page。

## Source of Truth

- Product vision: `PROJECT_VISION.md`
- User-facing README: `README.md`
- Delivered history: `docs/superpowers/archives/INDEX.md`
- Specs: `docs/superpowers/specs/`
- Plans: `docs/superpowers/plans/`
- Problems: `docs/superpowers/problems/`
- Release notes: `docs/release/RELEASE_NOTES.md`
