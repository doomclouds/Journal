# Journal Release Notes

## 0.1.1

### Highlights

- Unified app versioning：前端、后端和 GitHub Release 统一以 tag 版本号作为发布版本。
- Polished packaged app UX：补齐 About、隐私/AI/免责声明、本地文档阅读和安装版路径处理。
- Release workflow improvements：GitHub Release 模板开始承载版本更新说明，安装包构建和校验脚本继续作为发布前检查入口。
- Same-day memory corridor foundation：主线继续推进同日记忆回廊，围绕多年同日、纪念日和下一年提醒形成下一阶段产品方向。

### Known Limits

- `0.1.1` 仍然是本地优先 Windows 桌面版本，不包含云同步、自动更新、代码签名或应用内语音转写。
- 后续 main 上的 Phase 8 记忆回廊增强如果尚未打 tag，不应被误认为已经包含在已发布安装包中。

## 0.1.0

## Highlights

- Local-first morning journal workflow：晨间自然语言输入优先保存在本地。
- JMF draft / validation / confirmation：AI 或编辑器输出先进入草稿，经过 JMF 校验和用户确认后才写入正式 Markdown。
- OpenAI-compatible provider settings：支持用户配置 OpenAI-compatible LLM provider，并保留 Mock provider 作为默认安全路径。
- Harness Core audit trail：Harness Core 将 LLM 组织行为收束为受控工具计划，并保存可查看的审计记录。
- Local history search / version snapshots / same-day anniversary wheel：支持可重建 SQLite/FTS 历史索引、覆盖前版本快照、历史搜索和同日年轮。
- Data export / import：支持本地 ZIP 数据包导出、导入前备份当前 source material，并在数据备份面板固定展示当前本地数据概览。
- Windows installer：提供 Windows x64 Inno Setup 安装包、发布资产和 SHA-256 校验文件。

## Data

Journal 默认把用户数据保存在：

```text
%LocalAppData%/Journal
```

卸载应用默认保留该目录，避免误删个人日记、原始输入、草稿、版本快照、审计记录和可重建索引。

## Known Limits

- No cloud sync：本版本不提供云同步、跨设备同步或远端备份。
- No auto update/signing：本版本没有自动更新和代码签名。
- No full API Key export：导出数据包默认不包含完整 API Key。
- No non-today restore/confirm：当前恢复版本和确认写入仍以 today workflow 为中心，不支持非今日版本直接恢复/确认。
