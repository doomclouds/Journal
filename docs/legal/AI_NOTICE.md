# Journal AI 使用声明

本声明说明 Journal 当前版本中 AI 功能的用途、边界和用户确认机制。

## AI 输出性质

Journal 中的 AI 输出用于整理、组织和辅助回顾个人日记内容。AI 输出不是事实源，不构成医疗、心理、法律、金融或其他专业建议，也不替代用户对个人记忆的判断。

## 草稿边界

原始用户输入会被保留。真实 LLM 或 Mock provider 生成的内容经服务端结构处理后，先进入草稿边界。

正式 Markdown 日记只有在用户确认当前草稿后才会更新。AI 输出、Harness Core 工具计划和编辑器保存行为均不应直接覆盖正式条目。

## 校验失败处理

如果 LLM JSON 或 JMF Markdown 校验失败，Journal 会创建 `attention` 草稿并保留修复信息，不覆盖正式日记。

## 第三方 provider

真实 LLM provider 由用户配置和启用。启用后，用户提交用于整理的文本可能会发送给所选 provider；是否使用、使用哪个 provider，由用户决定。第三方 provider 的服务条款、隐私政策和数据处理规则由对应 provider 负责。
