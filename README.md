# Journal

一个晨间日记的桌面应用，用来记录每个人的一生的日记内容。

## 项目文档

- [项目愿景](./PROJECT_VISION.md)
- [阶段 1 设计](./docs/superpowers/specs/2026-05-07-phase-1-skeleton-design.md)
- [产品故事演示](./docs/product/journal-product-story.html)

## 阶段 1：应用框架骨架

阶段 1 只验证 Electron + React + .NET 本地 API 的工程链路。

不包含：

- 日记输入和保存
- AI Provider
- Markdown/JMF 生成
- SQLite 索引
- 安装包

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
