# Journal

一个晨间日记的桌面应用，用来记录每个人的一生的日记内容。

## 项目文档

- [项目愿景](./PROJECT_VISION.md)
- [阶段 1 设计](./docs/superpowers/specs/2026-05-07-phase-1-skeleton-design.md)
- [阶段 2 设计](./docs/superpowers/specs/2026-05-08-phase-2-jmf-generation-confirmation-design.md)
- [阶段 3 设计](./docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-design.md)
- [阶段 3 实施计划](./docs/superpowers/plans/2026-05-09-phase-3-jmf-editor-implementation-plan.md)
- [阶段 3 高保真原型](./docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-prototype.html)
- [产品故事演示](./docs/product/journal-product-story.html)

## 阶段 1：应用框架骨架

阶段 1 只验证 Electron + React + .NET 本地 API 的工程链路。

不包含：

- 日记输入和保存
- AI Provider
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

阶段 2 不包含版本快照、SQLite 索引和真实 AI Provider。这些能力按新路线图进入后续阶段。

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

阶段 3 仍不包含版本快照、SQLite 索引、真实 AI Provider、AI 改写、自动保存和多日期浏览。

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
