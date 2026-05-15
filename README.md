# Journal

一个晨间日记的桌面应用，用来记录每个人的一生的日记内容。

## 项目文档

- [项目愿景](./PROJECT_VISION.md)
- [Agent 项目上下文](./docs/agents/PROJECT_CONTEXT.md)
- [Agent 开发参考](./docs/agents/DEVELOPMENT_REFERENCE.md)
- [Agent 产品不变量](./docs/agents/PRODUCT_INVARIANTS.md)
- [阶段 1 设计](./docs/superpowers/specs/2026-05-07-phase-1-skeleton-design.md)
- [阶段 2 设计](./docs/superpowers/specs/2026-05-08-phase-2-jmf-generation-confirmation-design.md)
- [阶段 2 实施归档](./docs/superpowers/archives/2026-05/2026-05-08-phase-2-jmf-generation-confirmation-archives.md)
- [阶段 3 设计](./docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-design.md)
- [阶段 3 实施计划](./docs/superpowers/plans/2026-05-09-phase-3-jmf-editor-implementation-plan.md)
- [阶段 3 高保真原型](./docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-prototype.html)
- [阶段 3 实施归档](./docs/superpowers/archives/2026-05/2026-05-09-phase-3-jmf-editor-archives.md)
- [阶段 5 设计](./docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md)
- [阶段 5 实施归档](./docs/superpowers/archives/2026-05/2026-05-10-ai-provider-integration-archives.md)
- [LLM 设置体验设计](./docs/superpowers/specs/2026-05-10-llm-settings-ux-polish-design.md)
- [LLM 设置体验原型](./docs/superpowers/specs/2026-05-10-llm-settings-ux-polish-prototype.html)
- [LLM 设置体验实施归档](./docs/superpowers/archives/2026-05/2026-05-10-llm-settings-ux-polish-archives.md)
- [阶段 4A 本地历史与搜索设计](./docs/superpowers/specs/2026-05-13-phase-4a-local-history-search-design.md)
- [阶段 4A 历史搜索与版本恢复原型](./docs/superpowers/specs/2026-05-13-phase-4a-history-search-layout-prototype.html)
- [阶段 4A 实施计划](./docs/superpowers/plans/2026-05-13-phase-4a-local-history-search-implementation-plan.md)
- [阶段 4A 实施归档](./docs/superpowers/archives/2026-05/2026-05-13-phase-4a-local-history-search-archives.md)
- [阶段 6 Harness Core 设计](./docs/superpowers/specs/2026-05-12-journal-harness-core-design.md)
- [阶段 6 审计工作台原型](./docs/superpowers/specs/2026-05-12-journal-harness-audit-workbench-prototype.html)
- [阶段 6 实施计划](./docs/superpowers/plans/2026-05-12-journal-harness-core-implementation-plan.md)
- [阶段 6 实施归档](./docs/superpowers/archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- [阶段 6B 同日年轮设计](./docs/superpowers/specs/2026-05-14-phase-6b-anniversary-wheel-design.md)
- [阶段 6B 同日年轮原型](./docs/superpowers/specs/2026-05-14-phase-6b-anniversary-wheel-prototype.html)
- [阶段 6B 实施计划](./docs/superpowers/plans/2026-05-14-phase-6b-anniversary-wheel-implementation-plan.md)
- [阶段 6B 实施归档](./docs/superpowers/archives/2026-05/2026-05-14-phase-6b-anniversary-wheel-archives.md)
- [阶段 7 Windows 发布设计](./docs/superpowers/specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- [阶段 7 Windows 发布实施计划](./docs/superpowers/plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)
- [阶段 7 Windows 发布归档](./docs/superpowers/archives/2026-05/2026-05-14-phase-7-windows-release-pipeline-archives.md)
- [Superpowers 交付归档索引](./docs/superpowers/archives/INDEX.md)
- [产品故事演示](./docs/product/journal-product-story.html)

## 当前状态

当前主线已经进入 V1 / `0.1.0` Windows 本地发布阶段：

```text
自然语言输入 -> Raw input 持久化 -> Mock / 真实 LLM JSON 或 Harness Core 工具计划
  -> JMF Markdown 草稿 / reviewing draft / attention draft
  -> 块编辑 / 源码编辑 / Harness 审计
  -> 用户确认
  -> 覆盖前版本快照
  -> 正式 Markdown 文件
  -> 可重建 SQLite/FTS 历史索引
  -> 历史搜索 / 同日年轮 / 版本查看 / 恢复今日版本为草稿
  -> 数据导出/导入
  -> Windows 安装包
```

已交付能力包括：今日输入、原始输入保存、Mock 整理、OpenAI-compatible 真实 LLM 调用、JMF 草稿预览、块编辑、源码编辑、结构校验、确认写入正式 Markdown、可用的 LLM 参数配置界面、LLM Harness Core 的受控工具调用、草稿写入保护、section 级 provenance、按日期查看 harness run 的 AI 审计工作台、覆盖前版本快照、可重建 SQLite/FTS5 历史索引、历史搜索工作台、同日年轮工作台、今日版本恢复为待确认草稿、数据导出/导入、About/法律声明面板、安装版 Electron 托管 `.NET` backend、Inno Setup 安装包和 GitHub Actions release workflow。

- 数据导出默认不包含完整 API Key；导入会先备份当前 source material，再替换本地 Journal 数据。
- 数据备份面板通过 `GET /journal/data/summary` 固定展示当前 entry/raw input/version 计数，SQLite index 仍只是可重建缓存。
- Phase 6B adds the same-day anniversary wheel: the journal paper's corridor menu can open a read-only history workbench mode for a selected `MM-DD`, compare entries across years, inspect raw material snippets, and open version snapshots without changing the normal Today journal layout.

仍未交付：非今日版本直接恢复/确认、AI 改写聊天、自动保存、应用内录音/语音转写、删除流程、item 级 provenance、draft diff、entry rollback UI、云同步、自动更新、代码签名和完整 API Key 导入导出。

## Release Identity

应用图标资产提交在 `assets/app-icon/` 下。Release/CI 使用这些已提交的确定性 PNG/ICO 文件，不在发布构建过程中重新生成 AI 图像。

## Windows 安装包

本地构建并验证 Windows 安装包：

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
```

安装包和 SHA256 校验文件输出到 `artifacts/installer/dist/`。卸载默认保留 `%LocalAppData%/Journal` 下的本地日记、草稿、设置、审计和索引数据。

当前本地验证脚本只校验安装包 artifact 与 checksum，不执行真实安装或卸载。

## GitHub Actions 发布

首版完整 Windows 安装包不在每次 push 构建，避免把免费 GitHub Actions 额度烧在普通提交上。发布 workflow 已接入 `.github/workflows/release-windows.yml`：

- `workflow_dispatch` 手动输入 `release_version`，用于构建测试安装包 artifact。
- `v*` tag push 会构建安装包并创建 GitHub Release assets。
- Release version 应与 tag 对齐，例如 `v0.1.0` 对应 `0.1.0`，产物为 `Journal-Setup-0.1.0.exe` 和 `Journal-Setup-0.1.0.sha256`。

## 法律与数据声明

- [隐私声明 / Journal Privacy](./docs/legal/PRIVACY.md)
- [数据安全 / Journal Data Safety](./docs/legal/DATA_SAFETY.md)
- [AI 使用声明 / Journal AI Notice](./docs/legal/AI_NOTICE.md)
- [个人声明 / Personal Statement](./docs/legal/PERSONAL_STATEMENT.md)
- [免责声明 / Disclaimer](./docs/legal/DISCLAIMER.md)
- [Journal 0.1.0 Release Notes](./docs/release/RELEASE_NOTES.md)
- [Journal v0.1.0 GitHub Release Template](./docs/release/GITHUB_RELEASE_TEMPLATE.md)

## 阶段 1：应用框架骨架

阶段 1 只验证 Electron + React + .NET 本地 API 的工程链路。

不包含：

- 日记输入和保存
- 真实 LLM 接入
- Markdown/JMF 生成
- SQLite 索引
- 安装包

## 阶段 2：JMF 生成确认 MVP

阶段 2 只打通今日晨间日记主链路：

```text
自然语言输入 -> Mock AI JSON -> JMF Markdown 草稿 -> 用户确认 -> 正式 Markdown 文件
```

本阶段默认将开发期数据写入：

```text
%LocalAppData%/Journal/entries/yyyy/MM/yyyy-MM-dd.md
%LocalAppData%/Journal/.journal/raw-inputs/yyyy/MM/yyyy-MM-dd.jsonl
%LocalAppData%/Journal/.journal/drafts/yyyy/MM/yyyy-MM-dd.md
%LocalAppData%/Journal/.journal/drafts/yyyy/MM/yyyy-MM-dd.meta.json
```

阶段 2 API：

```text
GET http://localhost:5057/journal/today
POST http://localhost:5057/journal/today/inputs
POST http://localhost:5057/journal/today/draft/confirm
```

今日工作台仍然是只读 Markdown 预览，不提供块编辑和源码编辑。

阶段 2 不包含版本快照、SQLite 索引和真实 LLM 接入。这些能力按新路线图进入后续阶段。

## 阶段 3：JMF 编辑模式与结构校验

阶段 3 在阶段 2 的草稿确认链路上补上安全编辑层：

```text
读取今日 draft / entry Markdown
  -> 解析为 JMF document
  -> 块编辑或源码编辑
  -> JMF 结构校验
  -> 保存为 reviewing draft
  -> 用户确认
  -> 更新当天正式 Markdown
```

阶段 3 API：

```text
GET http://localhost:5057/journal/today/editor
PUT http://localhost:5057/journal/today/editor/blocks
PUT http://localhost:5057/journal/today/editor/source
```

编辑边界：

- 块编辑模式保护 `raw-inputs`，只展示原始表达，不允许直接改写。
- 新增块只允许从 JMF v1 已知可选单例块中选择，并按固定顺序插入。
- 源码模式可以编辑完整 Markdown，但保存前必须通过 JMF 结构校验。
- 块编辑和源码编辑成功后只保存为 `reviewing` draft。
- 校验失败会写入 `attention` draft 和修复提示，不覆盖正式 entry。
- 只有点击“确认写入正式日记”后才会更新 `entries/` 下的正式 Markdown。

阶段 3 仍不包含版本快照、SQLite 索引、真实 LLM 接入、AI 改写、自动保存和多日期浏览。

## 阶段 4A：本地历史、搜索与版本快照

阶段 4A 在正式 Markdown 写入链路上补上本地历史可靠性：

```text
确认 draft -> 覆盖前 snapshot -> 写正式 Markdown -> 更新 SQLite/FTS5 可重建索引
历史工作台 -> 搜索 entry section / raw inputs -> 查看版本 -> 恢复今日版本为 reviewing draft
```

阶段 4A 已交付：

- 正式 entry 被覆盖前会写入 `.journal/versions/yyyy/MM/yyyy-MM-dd/` 下的 Markdown 和 metadata 快照；首次写入不创建快照。
- `EntryWritePipeline` 统一正式写入顺序：snapshot old entry -> write Markdown -> update rebuildable index；snapshot 失败不覆盖旧 entry，index 失败不回滚正式 Markdown，但 warning 会返回到今日状态。
- `.journal/index/journal.db` 是可重建缓存，不是事实源；Markdown、raw-input jsonl 和 version 文件仍是可恢复源材料。
- SQLite schema 包含 entry summary、section、raw input、version metadata，并使用 FTS5 trigram 支持日记段落和原始材料搜索。
- 索引服务支持扫描、rebuild、schema/version 异常备份重建、invalid JMF 标记为 `attention`、缺失 entry 标记为 `missing`。
- History API 支持搜索、日期详情、版本列表、版本详情、今日版本恢复为草稿，以及手动 scan/rebuild。
- 桌面端新增历史与版本工作台：从 Today Assistant 进入，全工作区查看历史搜索、日期详情、版本快照，并可把今日版本恢复成 `reviewing` draft。

阶段 4A 边界：

- 恢复版本只写 draft，绝不直接覆盖 `entries/`。
- 由于当前编辑/确认链路仍以 today 为中心，恢复版本只允许恢复今天的版本；非今日版本恢复会被 API 拒绝。
- 不包含多日期编辑、非今日确认、draft diff / entry rollback UI、item 级 provenance 或删除流程。

## 阶段 5：真实 LLM Provider 接入

当前已交付范围建立在阶段 3 的 JMF 草稿/编辑/确认链路之上，并补上真实 OpenAI-compatible LLM 配置与调用：

```text
自然语言输入 -> Raw input 持久化 -> Mock 或真实 LLM JSON -> JMF Markdown 草稿
  -> block/source edit with JMF validation
  -> 用户确认
  -> 正式 Markdown 文件
```

阶段 5 已交付：

- 后端支持 `Mock` 与真实 OpenAI-compatible LLM，当前通过环境变量和 `%LocalAppData%/Journal/.journal/settings/ai-providers.json` 共同决定 active LLM；环境变量优先，配置文件兜底。Windows 下环境变量读取顺序为 Process -> User -> Machine。
- 默认预设：OpenAI `gpt-5.4`、DeepSeek `deepseek-v4-flash`、智谱 GLM `glm-5.1`，未配置 key 时默认使用 Mock。
- `GET /settings/ai` 返回安全视图，不返回完整 API Key；文件配置的 key 只显示掩码和可查看状态，环境变量 key 不可 reveal。
- `GET /settings/ai/{providerId}/api-key` 只用于用户点击“小眼睛”时查看文件配置的 key，不暴露环境变量来源的 key。
- `PUT /settings/ai` 保存配置草稿；`POST /settings/ai/test` 可以携带 candidate 测试当前表单内容，不必先写入配置文件。
- `POST /settings/ai/activate` 执行“测试通过后启用”，测试失败时不写入 active provider，避免把不可用模型设成当前 LLM。
- `POST /journal/today/draft/regenerate` 保留为旧的整篇草稿重生成兼容接口；今日工作台的用户输入和重新整理入口统一走 Harness Run。
- 真实 LLM 输出仍只能返回 `JournalAiJson`，并继续经过服务端校验、`raw-inputs` 保护和 JMF renderer 后才会进入 `reviewing` 或 `attention` draft。
- 顶部状态与配置面板统一使用 `LLM` 术语；设置页提供 Provider 状态、来源、连接测试、受保护启用、Key 隐藏/查看、错误提示和可操作下一步。
- 真实 LLM 失败会进入 `attention` draft，不会静默回退或覆盖正式 entry。

阶段 5 仍不包含版本快照、SQLite 索引、AI 改写聊天、自动保存、多日期浏览和安装包。

## 阶段 6：LLM Harness Core

Harness Core 将 LLM 从“整篇生成器”收束为受控工具调用：稳定的 Markdown system instructions 描述 planner 方法论和安全边界，动态 journal context 提供历史 raw inputs、section catalog 和工具约束；append 输入时额外提供当前 draft / 正式 entry 作为安全边界材料，当前用户输入只作为本次 user message 参与决策，模型不能直接写 Markdown 或正式 entry。

阶段 6 已交付：

- 今日工作台底部输入提交和“重新整理”都已接入 `POST /journal/today/harness/runs`，并通过 SSE 等待 harness run 完成后刷新日记纸面。
- `append-input` run 会持久化当前输入，供后续运行作为历史 raw input；但本次 planner prompt 中当前输入只出现在 user message，不会混入历史 raw inputs context。
- `reorganize-existing` run 不追加 raw input，而是使用固定服务端 user message；服务端只把已有 raw inputs、section catalog 和工具约束提供给 LLM，不提供当前 draft 或正式 entry。
- LLM 只能调用 append / upsert / revise AI section / no-op 工具，工具调用先被收集为计划，再由服务端执行。
- 用户内容只能被追加，不能被删除、清空或替换；`raw-inputs` 仍由服务端原始输入生成和保护。
- Harness Planner 的 section catalog 携带主题语义和避让规则；同一事实应进入一个最合适的 section，具体工作、学习、健康等主题不应重复堆进 `today-focus`。
- 今日页按钮“重新整理”是强结构重组模式：以历史 raw inputs 为唯一日记事实来源，放弃现有全部日记正文，不让 LLM 参考或继承当前 draft / confirmed entry，并重新规划整篇九宫格结构、合并重复、移动错分并压缩冗余。
- Harness Planner 要用 Markdown 语法标注重点内容，例如用 `**加粗**` 标出关键行动、关键风险和今日最重要目标，并按轻重缓急排序。
- Harness 执行器会把 AI 工具内容规范为 Markdown bullet list，并对本轮完全重复的工具内容做服务端去重。
- `appendJournalSection`、`upsertJournalSection`、`reviseAiGeneratedSection` 和 `noOp` 映射到 JMF operation executor，并经过 JMF validation。
- 工具执行只写 `reviewing` / `attention` draft，不直接写正式 entry；正式 Markdown 仍必须由用户确认。
- JMF section marker 支持 section 级 provenance，普通预览隐藏，审计和调试路径可见。
- AI 审计工作台支持按日期查看 harness run、工具调用、拒绝原因、验证状态和运行摘要。

阶段 6 自身仍不包含 item 级 provenance、用户授权删除/隐藏、draft diff、entry rollback UI 或多日期编辑；历史搜索和版本快照由 Phase 4A 提供。

## 阶段 6B：同日年轮

Phase 6B 在历史工作台中补上只读同日年轮模式：

- 日记纸面的 `日记回廊` 菜单新增 `同日年轮` 入口，默认打开今天的 `MM-DD`。
- History Workbench 可以按 `MM-DD` 查询多年同日记录，按年份倒序展示年份摘要卡片。
- 同日年轮复用可重建 SQLite/FTS5 历史索引，查询前仍通过历史索引扫描对齐 Markdown、raw-input jsonl 和 version 文件。
- 年份详情可以查看历史 Markdown、raw material snippets 和版本快照。
- 同日年轮是只读记忆回廊：版本可以查看，不能从年轮模式恢复为草稿；恢复动作只保留在普通历史工作台，且当前仍限制为今日版本恢复。

阶段 6B 不包含 AI 多年总结、纪念日管理、跨日期编辑、非今日确认、draft diff / entry rollback UI、Future Notes 或 item 级 provenance。

## 环境要求

- .NET SDK 10
- Node.js 24 或兼容版本
- npm 11 或兼容版本

## 一键启动开发环境

```powershell
.\scripts\start-journal-dev.ps1
```

脚本会复用已在线的 API/Vite；缺哪个启动哪个，然后打开 Electron 客户端。常用参数：

```powershell
.\scripts\start-journal-dev.ps1 -RestartApi -RestartVite
.\scripts\start-journal-dev.ps1 -NoElectron -OpenBrowser
.\scripts\start-journal-dev.ps1 -ShowLogs
```

停止后台开发进程：

```powershell
.\scripts\stop-journal-dev.ps1
.\scripts\stop-journal-dev.ps1 -Api
.\scripts\stop-journal-dev.ps1 -Vite
.\scripts\stop-journal-dev.ps1 -Electron
```

## 手动启动

开发期仍然是双进程模式；需要手动排查时可以分别启动：

```powershell
dotnet run --project src/Journal.Api
npm install --prefix apps/desktop
npm run desktop --prefix apps/desktop
```

API 默认提供：

```text
GET http://localhost:5057/health
```

## 验证

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```
