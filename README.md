# Journal

> 本地优先的 AI 日记桌面应用。你负责真实表达，AI 负责整理，Markdown 负责保存。

![GitHub release](https://img.shields.io/github/v/release/doomclouds/Journal?include_prereleases)
![Last commit](https://img.shields.io/github/last-commit/doomclouds/Journal)
![GitHub stars](https://img.shields.io/github/stars/doomclouds/Journal?style=social)

Journal 用来记录一个人的长期日记与记忆线索。它保存用户每天的原始表达，通过 OpenAI-compatible LLM / Harness Core 整理为结构化 JMF Markdown 草稿，再由用户审阅确认后写入本地 Markdown 文件。

项目当前已经发布 `v0.1.0` 和 `v0.1.1` 两个 Windows 本地版本。更完整的产品故事、记忆回廊愿景和项目活跃度展示见 [GitHub Wiki](https://github.com/doomclouds/Journal/wiki)。

## 当前能力

- 本地优先的今日自然语言输入和 raw input 持久化。
- OpenAI-compatible LLM Provider 配置、连接测试和受保护启用。
- Harness Core：LLM 只能生成受控工具计划，服务端执行并记录审计。
- JMF Markdown 草稿、块编辑、源码编辑、结构校验和用户确认。
- 正式日记写入前自动生成版本快照。
- Markdown 作为人类可读可信源，SQLite / FTS 只作为可重建索引。
- 历史搜索、版本查看和今日版本恢复为待确认草稿。
- 同日年轮：按 `MM-DD` 回看多年同日记录。
- 数据导出 / 导入、About、隐私与 AI 使用声明。
- Electron + `.NET` backend 的 Windows 安装包和 GitHub Actions Release 流水线。

仍未交付：云同步、自动更新、代码签名、应用内录音、语音转写、AI 追问式改写、多日期编辑、非今日版本恢复确认、item 级 provenance、draft diff 和完整 API Key 导入导出。

## 快速开始

环境要求：

- .NET SDK 10
- Node.js 24 或兼容版本
- npm 11 或兼容版本

常用命令：

```powershell
.\scripts\start-journal-dev.ps1
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

本地打包 Windows 安装包：

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.1
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.1
```

安装包和 SHA256 校验文件输出到 `artifacts/installer/dist/`。卸载默认保留 `%LocalAppData%/Journal` 下的本地日记、草稿、设置、审计和索引数据。

## 文档入口

- [项目愿景](./PROJECT_VISION.md)
- [Agent 项目上下文](./docs/agents/PROJECT_CONTEXT.md)
- [开发参考](./docs/agents/DEVELOPMENT_REFERENCE.md)
- [产品不变量](./docs/agents/PRODUCT_INVARIANTS.md)
- [交付归档索引](./docs/superpowers/archives/INDEX.md)
- [隐私声明](./docs/legal/PRIVACY.md)
- [数据安全声明](./docs/legal/DATA_SAFETY.md)
- [AI 使用声明](./docs/legal/AI_NOTICE.md)
- [免责声明](./docs/legal/DISCLAIMER.md)

## AI 生成内容声明

本仓库中部分产品叙事文案和 `docs/product/assets/journal-*.png` 视觉图由 OpenAI 提供的服务辅助生成，并由项目维护者审定后纳入仓库。
