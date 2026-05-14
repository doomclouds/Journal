# Journal AI Notice

AI 输出是整理辅助，不是事实源。Journal 的设计目标是帮助你把晨间自然表达组织成可读结构，而不是替你决定真实记忆。

原始用户输入会被保留。真实 LLM 或 Mock provider 生成的内容需要经过服务端结构处理后，先写入草稿边界。

正式 Markdown 日记只有在用户确认当前草稿后才会更新。AI 输出、Harness Core 工具计划和编辑器保存都不应直接覆盖正式条目。

如果 LLM JSON 或 JMF Markdown 校验失败，Journal 会创建 `attention` 草稿并保留修复信息，不会覆盖正式日记。

真实 LLM provider 由用户配置和启用。启用后，用户提交用于整理的文本可能会发送给所选 provider；是否使用、使用哪个 provider，由用户决定。
