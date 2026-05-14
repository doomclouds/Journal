# Journal Data Safety

Journal 把本地文件作为长期数据的事实源：

- Markdown 日记条目是正式日记的 source material。
- raw-input jsonl 文件保存原始输入，是整理和恢复的重要 source material。
- version snapshot 文件保存覆盖前版本，也是可恢复 source material。

SQLite 历史索引只是可重建缓存。它用于搜索、历史浏览和同日年轮等体验；如果索引损坏或缺失，可以从 Markdown 条目、raw-input jsonl 文件和版本快照重新构建。

默认情况下，Journal 的用户数据位于：

```text
%LocalAppData%/Journal
```

卸载应用时应默认保留 `%LocalAppData%/Journal`，避免误删个人日记。后续安装、升级或卸载流程都应把这个目录视为用户数据目录，而不是一次性安装产物。

导入数据包时，Journal 应先备份当前数据，再替换 source material。source 文件恢复完成后，应重建 SQLite 索引，确保历史搜索和同日年轮重新对齐本地文件。

导出数据包默认不包含完整 API Key。需要迁移 LLM 配置时，请重新确认 provider 和密钥来源。

Journal 尽量保护本地数据边界，但本地磁盘、系统账号、备份介质和第三方 LLM provider 不在应用完全控制范围内。重要日记请自行保留可靠备份。
