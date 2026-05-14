# Journal 0.1.0 Release Notes

## Highlights

- Local-first morning journal workflow：晨间自然语言输入优先保存在本地。
- JMF draft / validation / confirmation：AI 或编辑器输出先进入草稿，经过 JMF 校验和用户确认后才写入正式 Markdown。
- OpenAI-compatible provider settings：支持用户配置 OpenAI-compatible LLM provider，并保留 Mock provider 作为默认安全路径。
- Harness Core audit trail：Harness Core 将 LLM 组织行为收束为受控工具计划，并保存可查看的审计记录。
- Local history search / version snapshots / same-day anniversary wheel：支持可重建 SQLite/FTS 历史索引、覆盖前版本快照、历史搜索和同日年轮。
- Windows installer release pipeline groundwork：为 Windows 安装包、发布资产、校验和与后续 release pipeline 建立基础。

## Data

Journal 默认把用户数据保存在：

```text
%LocalAppData%/Journal
```

卸载应用默认保留该目录，避免误删个人日记、原始输入、草稿、版本快照、审计记录和可重建索引。

## Known Limits

- No cloud sync：本版本没有云同步。
- No auto update/signing：本版本没有自动更新和代码签名。
- No full API Key export：导出数据包默认不包含完整 API Key。
- No non-today restore/confirm：当前恢复版本和确认写入仍以 today workflow 为中心，不支持非今日版本直接恢复/确认。
