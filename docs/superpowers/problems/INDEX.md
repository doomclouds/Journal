# Superpowers Problem Index

## 2026-05

- [2026-05-11-electron-native-menu-command-bridge-problem.md](./2026-05/2026-05-11-electron-native-menu-command-bridge-problem.md): 记录 Electron 原生菜单命令只发送未被 React 可靠消费，导致 `文件 -> LLM 配置` 点击无效的桥接时序问题。
- [2026-05-10-llm-prompt-field-scope-too-narrow-problem.md](./2026-05/2026-05-10-llm-prompt-field-scope-too-narrow-problem.md): 记录真实 LLM 将日记事件只提取到 tags/topics/mood、正文块为空的 prompt 字段语义过窄问题。
- [2026-05-10-llm-user-environment-key-not-loaded-problem.md](./2026-05/2026-05-10-llm-user-environment-key-not-loaded-problem.md): 记录 Windows User 级 LLM API key 已配置但进程环境未继承，导致设置页显示无 key 的环境读取问题。
- [2026-05-10-llm-validator-contract-mismatch-problem.md](./2026-05/2026-05-10-llm-validator-contract-mismatch-problem.md): 记录真实 LLM 按提示词输出空结构化块时，被过严 AI JSON validator 拒绝导致草稿进入 `validation_failed` 的合同漂移问题。
- [2026-05-07-vite-loopback-cors-problem.md](./2026-05/2026-05-07-vite-loopback-cors-problem.md): 记录 Vite 在 `127.0.0.1` 与 Electron/文档使用 `localhost` 时触发的本地 CORS origin 漂移问题及精确白名单修法。
