# Journal 隐私声明

本声明说明 Journal 在当前版本中的数据保存、网络传输和密钥可见性边界。除非用户另行配置或主动导出，Journal 以本地优先方式处理个人日记数据。

## 本地数据

默认情况下，Journal 将日记条目、原始输入、草稿、版本快照、AI 审计记录以及可重建的 SQLite 历史索引保存于当前 Windows 用户的本地应用数据目录：

```text
%LocalAppData%/Journal
```

当前版本不提供云同步功能，也不会在后台将本地日记数据同步至 Journal 自有云服务。

## 第三方 LLM provider

当用户启用真实 LLM provider 时，用户提交用于整理日记的文本可能会发送至当前启用的 provider。provider 的选择、配置、启用和停用均由用户控制。

第三方 provider 可能适用其独立的服务条款、隐私政策、日志策略和数据处理规则。启用真实 provider 前，用户应自行确认对应服务是否适合处理其提交内容。

## API Key

Journal 不会有意将完整 API Key 写入 Markdown 日记、版本快照、发布产物、GitHub Actions 日志或普通应用日志。

API Key 的可见性遵循以下边界：

- 环境变量来源的 API Key 不通过应用 API 或 UI reveal。
- 文件配置来源的 API Key 仅在用户明确触发查看等显式操作后，通过受保护 API/UI reveal。

## 用户责任

本地优先不等于自动备份。用户应根据自身需要保护本机账号、磁盘、备份介质和第三方 provider 凭据，并自行决定哪些内容适合提交给外部服务。
