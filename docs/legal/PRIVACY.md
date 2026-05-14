# Journal Privacy

Journal 是一个 local-first 的晨间日记应用。默认情况下，日记条目、原始输入、草稿、版本快照、AI 审计记录，以及可重建的 SQLite 历史索引，都存储在当前 Windows 用户的本地应用数据目录下：

```text
%LocalAppData%/Journal
```

本版本不提供云同步。Journal 不会在后台把你的日记数据同步到云端。

当你启用真实 LLM provider 时，你提交用于整理日记的文本可能会发送给你配置的 provider。provider 由用户选择和配置；是否发送、发送到哪个服务，取决于当前启用的 LLM 设置。

Journal 不会有意把完整 API Key 写入 Markdown 日记、版本快照、发布产物、GitHub Actions 日志或普通应用日志。

API Key 的可见性遵循现有应用边界：

- 环境变量来源的 API Key 不会通过 API 或 UI reveal。
- 文件配置来源的 API Key 只能在用户明确点击查看等显式操作后，通过受保护 API/UI reveal。

请注意，第三方 LLM provider 可能有自己的隐私政策和数据处理规则。启用真实 provider 前，建议先确认对应服务的条款。
