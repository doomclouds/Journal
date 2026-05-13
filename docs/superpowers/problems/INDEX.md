# Superpowers Problem Index

## 2026-05

- [2026-05-14-harness-section-boundary-duplication-problem.md](./2026-05/2026-05-14-harness-section-boundary-duplication-problem.md): 记录 Harness Planner 缺少九宫格 section 语义边界时，同一事实会被重复写入 `today-focus` 与 `work` 等相近主题的分配问题。
- [2026-05-13-deepseek-thinking-tool-call-reasoning-content-problem.md](./2026-05/2026-05-13-deepseek-thinking-tool-call-reasoning-content-problem.md): 记录 DeepSeek V4 thinking mode 在 Agent Framework tool call 后必须回传 `reasoning_content`，而 OpenAI-compatible 适配层未写回该扩展字段导致 400 的运行时兼容问题。
- [2026-05-13-generated-draft-missing-ai-provenance-problem.md](./2026-05/2026-05-13-generated-draft-missing-ai-provenance-problem.md): 记录 AI 草稿生成入口漏写 section provenance，导致 harness 误把 AI 生成段落视为 unknown 并拒绝 revise 的来源边界问题。
- [2026-05-13-harness-current-input-context-leak-problem.md](./2026-05/2026-05-13-harness-current-input-context-leak-problem.md): 记录 append-input 当前输入虽从 raw input 列表排除，却经派生 currentDraftMarkdown 泄漏回 protected context 的 Prompt 分层问题。
- [2026-05-13-harness-submit-not-wired-audit-empty-problem.md](./2026-05/2026-05-13-harness-submit-not-wired-audit-empty-problem.md): 记录 Phase 6 审计页已交付但主输入提交仍走旧接口，导致正常用户工作流不会产生 harness audit run 的接线缺口；现已改为 submit 走 harness run + SSE。
- [2026-05-13-history-index-cache-stale-sidecars-problem.md](./2026-05/2026-05-13-history-index-cache-stale-sidecars-problem.md): 记录可重建 SQLite/FTS 历史索引在 invalid JMF、raw-only 日期、rebuild sidecar 和非规范路径下容易保留旧缓存或漏索引的失败模式。
- [2026-05-13-history-workbench-stale-selection-restore-mismatch-problem.md](./2026-05/2026-05-13-history-workbench-stale-selection-restore-mismatch-problem.md): 记录历史工作台 selected date 与旧 detail/version state 错配，可能拼出新日期加旧 versionId 恢复请求的前端竞态问题。
- [2026-05-13-journal-generated-content-blank-line-inflation-problem.md](./2026-05/2026-05-13-journal-generated-content-blank-line-inflation-problem.md): 记录 AI 生成内容多余空行被后端原样写入、前端按段落放大，且今日材料默认展开过长导致正文阅读被遮挡的格式化体验问题。
- [2026-05-13-journal-test-workspace-cleanup-file-lock-problem.md](./2026-05/2026-05-13-journal-test-workspace-cleanup-file-lock-problem.md): 记录 Windows 全量测试中 SQLite/WAL/SSE 后台句柄释放晚于 `TempWorkspace.Dispose`，导致临时目录删除偶发失败的测试稳定性问题。
- [2026-05-12-harness-noop-draft-overwrite-problem.md](./2026-05/2026-05-12-harness-noop-draft-overwrite-problem.md): 记录 harness no-op/no-change 在 service 层仍写入或刷新 reviewing draft 的分层合同漂移问题。
- [2026-05-12-harness-provenance-attribute-pollution-problem.md](./2026-05/2026-05-12-harness-provenance-attribute-pollution-problem.md): 记录 LLM 工具参数中的 raw input id 未校验/转义就进入 JMF provenance marker，可能污染 section 属性的安全边界问题。
- [2026-05-12-harness-sse-run-lifecycle-problem.md](./2026-05/2026-05-12-harness-sse-run-lifecycle-problem.md): 记录 harness SSE endpoint 同时承担执行和进度推送时，断线、重连、并发首连可能导致取消或重复执行 run 的生命周期问题。
- [2026-05-11-electron-native-menu-command-bridge-problem.md](./2026-05/2026-05-11-electron-native-menu-command-bridge-problem.md): 记录 Electron 原生菜单命令只发送未被 React 可靠消费，导致 `文件 -> LLM 配置` 点击无效的桥接时序问题。
- [2026-05-10-llm-prompt-field-scope-too-narrow-problem.md](./2026-05/2026-05-10-llm-prompt-field-scope-too-narrow-problem.md): 记录真实 LLM 将日记事件只提取到 tags/topics/mood、正文块为空的 prompt 字段语义过窄问题。
- [2026-05-10-llm-user-environment-key-not-loaded-problem.md](./2026-05/2026-05-10-llm-user-environment-key-not-loaded-problem.md): 记录 Windows User 级 LLM API key 已配置但进程环境未继承，导致设置页显示无 key 的环境读取问题。
- [2026-05-10-llm-validator-contract-mismatch-problem.md](./2026-05/2026-05-10-llm-validator-contract-mismatch-problem.md): 记录真实 LLM 按提示词输出空结构化块时，被过严 AI JSON validator 拒绝导致草稿进入 `validation_failed` 的合同漂移问题。
- [2026-05-07-vite-loopback-cors-problem.md](./2026-05/2026-05-07-vite-loopback-cors-problem.md): 记录 Vite 在 `127.0.0.1` 与 Electron/文档使用 `localhost` 时触发的本地 CORS origin 漂移问题及精确白名单修法。
