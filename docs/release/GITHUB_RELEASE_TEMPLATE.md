# Journal v<release-version>

> Windows 本地安装包发布。适用于 Windows x64，建议从上一版本平滑升级。

发布范围：`<previous-version>` → `v<release-version>`

发布日期：<release-date>

## 本次重点

<curated-notes>

## 下载与校验

| 文件 | 用途 |
| --- | --- |
| `Journal-Setup-<release-version>.exe` | Windows x64 安装程序 |
| `Journal-Setup-<release-version>.sha256` | 安装程序 SHA-256 校验值 |

```powershell
Get-FileHash -Algorithm SHA256 .\Journal-Setup-<release-version>.exe
Get-Content .\Journal-Setup-<release-version>.sha256
```

## 升级与数据

- 安装、升级和卸载默认保留 `%LocalAppData%/Journal` 下的本地日记数据。
- 导出包默认不包含完整 API Key；迁移 LLM 配置时请重新确认 provider 和密钥来源。
- 当前版本仍是本地优先的 Windows 单机发布，不包含云同步、自动更新或代码签名。

## 建议验证

- 打开 About，确认 Release / Frontend / Backend 版本显示为 `<release-version>`。
- 在 About 中查看 Privacy、Data Safety、AI Notice 等正式文件，确认内容在应用内阅读器中打开。
- 进行一次数据导出，确认生成的备份包可以在本机路径中找到。
- 如使用真实 LLM provider，升级后重新测试 provider 连接。

## 完整变更

<details>
<summary>查看从 <previous-version> 到 v<release-version> 的提交摘要</summary>

<commit-summary>

</details>

## 反馈

如果安装、启动、本地数据读取、About 声明阅读器或 AI 整理行为不符合预期，请附上版本号、安装路径、数据目录和复现步骤。
