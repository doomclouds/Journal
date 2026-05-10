# Journal

一个晨间日记的桌面应用，用来记录每个人的一生的日记内容。

## 项目文档

- [项目愿景](./PROJECT_VISION.md)
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
- [Superpowers 交付归档索引](./docs/superpowers/archives/INDEX.md)
- [产品故事演示](./docs/product/journal-product-story.html)

## 当前状态

当前主线已经跑通到 Phase 5：

```text
自然语言输入 -> Raw input 持久化 -> Mock 或真实 LLM JSON -> JMF Markdown 草稿
  -> 块编辑 / 源码编辑与 JMF 校验
  -> 用户确认
  -> 正式 Markdown 文件
```

已交付能力包括：今日输入、原始输入保存、Mock 整理、OpenAI-compatible 真实 LLM 调用、JMF 草稿预览、块编辑、源码编辑、结构校验、确认写入正式 Markdown，以及可用的 LLM 参数配置界面。

仍未交付：版本快照、SQLite 索引/搜索、多日期浏览、AI 改写聊天、自动保存、应用内录音/语音转写、安装包、生产 Electron 托管 .NET 后端。

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

## 阶段 5：真实 LLM Provider 接入

当前已交付范围建立在阶段 3 的 JMF 草稿/编辑/确认链路之上，并补上真实 OpenAI-compatible LLM 配置与调用：

```text
自然语言输入 -> Raw input 持久化 -> Mock 或真实 LLM JSON -> JMF Markdown 草稿
  -> block/source edit with JMF validation
  -> 用户确认
  -> 正式 Markdown 文件
```

阶段 5 已交付：

- 后端支持 `Mock` 与真实 OpenAI-compatible LLM，当前通过环境变量和 `%LocalAppData%/Journal/.journal/settings/ai-providers.json` 共同决定 active LLM；环境变量优先，配置文件兜底。
- 默认预设：OpenAI `gpt-5.4`、DeepSeek `deepseek-v4-flash`、智谱 GLM `glm-5.1`，未配置 key 时默认使用 Mock。
- `GET /settings/ai` 返回安全视图，不返回完整 API Key；文件配置的 key 只显示掩码和可查看状态，环境变量 key 不可 reveal。
- `GET /settings/ai/{providerId}/api-key` 只用于用户点击“小眼睛”时查看文件配置的 key，不暴露环境变量来源的 key。
- `PUT /settings/ai` 保存配置草稿；`POST /settings/ai/test` 可以携带 candidate 测试当前表单内容，不必先写入配置文件。
- `POST /settings/ai/activate` 执行“测试通过后启用”，测试失败时不写入 active provider，避免把不可用模型设成当前 LLM。
- `POST /journal/today/draft/regenerate` 用于按指定 LLM 重新整理今日草稿；当前触发入口在今日工作台，不在设置页直接发起重生成。
- 真实 LLM 输出仍只能返回 `JournalAiJson`，并继续经过服务端校验、`raw-inputs` 保护和 JMF renderer 后才会进入 `reviewing` 或 `attention` draft。
- 顶部状态与配置面板统一使用 `LLM` 术语；设置页提供 Provider 状态、来源、连接测试、受保护启用、Key 隐藏/查看、错误提示和可操作下一步。
- 真实 LLM 失败会进入 `attention` draft，不会静默回退或覆盖正式 entry。

阶段 5 仍不包含版本快照、SQLite 索引、AI 改写聊天、自动保存、多日期浏览和安装包。

## 环境要求

- .NET SDK 10
- Node.js 24 或兼容版本
- npm 11 或兼容版本

## 启动 .NET API

```powershell
dotnet run --project src/Journal.Api
```

API 默认提供：

```text
GET http://localhost:5057/health
```

## 启动桌面前端

```powershell
npm install --prefix apps/desktop
npm run desktop --prefix apps/desktop
```

开发期采用双进程模式：先启动 .NET API，再启动 Electron/Vite 桌面前端。

## 验证

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```
