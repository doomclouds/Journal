# Superpowers Problem Index

## 2026-05

- [2026-05-13-harness-submit-not-wired-audit-empty-problem.md](./2026-05/2026-05-13-harness-submit-not-wired-audit-empty-problem.md): 记录 Phase 6 审计页已交付但主输入提交仍走旧接口，导致正常用户工作流不会产生 harness audit run 的接线缺口；现已改为 submit 走 harness run + SSE。
- [2026-05-12-harness-noop-draft-overwrite-problem.md](./2026-05/2026-05-12-harness-noop-draft-overwrite-problem.md): 记录 harness no-op/no-change 在 service 层仍写入或刷新 reviewing draft 的分层合同漂移问题。
- [2026-05-12-harness-provenance-attribute-pollution-problem.md](./2026-05/2026-05-12-harness-provenance-attribute-pollution-problem.md): 记录 LLM 工具参数中的 raw input id 未校验/转义就进入 JMF provenance marker，可能污染 section 属性的安全边界问题。
- [2026-05-12-harness-sse-run-lifecycle-problem.md](./2026-05/2026-05-12-harness-sse-run-lifecycle-problem.md): 记录 harness SSE endpoint 同时承担执行和进度推送时，断线、重连、并发首连可能导致取消或重复执行 run 的生命周期问题。
- [2026-05-11-electron-native-menu-command-bridge-problem.md](./2026-05/2026-05-11-electron-native-menu-command-bridge-problem.md): 记录 Electron 原生菜单命令只发送未被 React 可靠消费，导致 `文件 -> LLM 配置` 点击无效的桥接时序问题。
- [2026-05-10-llm-prompt-field-scope-too-narrow-problem.md](./2026-05/2026-05-10-llm-prompt-field-scope-too-narrow-problem.md): 记录真实 LLM 将日记事件只提取到 tags/topics/mood、正文块为空的 prompt 字段语义过窄问题。
- [2026-05-10-llm-user-environment-key-not-loaded-problem.md](./2026-05/2026-05-10-llm-user-environment-key-not-loaded-problem.md): 记录 Windows User 级 LLM API key 已配置但进程环境未继承，导致设置页显示无 key 的环境读取问题。
- [2026-05-10-llm-validator-contract-mismatch-problem.md](./2026-05/2026-05-10-llm-validator-contract-mismatch-problem.md): 记录真实 LLM 按提示词输出空结构化块时，被过严 AI JSON validator 拒绝导致草稿进入 `validation_failed` 的合同漂移问题。
- [2026-05-07-vite-loopback-cors-problem.md](./2026-05/2026-05-07-vite-loopback-cors-problem.md): 记录 Vite 在 `127.0.0.1` 与 Electron/文档使用 `localhost` 时触发的本地 CORS origin 漂移问题及精确白名单修法。
