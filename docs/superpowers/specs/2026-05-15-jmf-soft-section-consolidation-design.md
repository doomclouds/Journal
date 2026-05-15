# JMF Soft Section Consolidation 设计

## 1. 背景

当前 JMF v1 分类较细：

- `mood` 情绪状态
- `work` 工作推进
- `learning` 学习与思考
- `health` 健康与精力
- `relationship` 关系与家庭
- `money` 财务
- `inspiration` 灵感
- `future-notes` 未来提醒
- `gratitude` 感恩

实际使用中，`today-focus` 与 `work` / `learning` / `health` 容易重复，`relationship` 与 `gratitude` 容易重复，`inspiration` 与 `future-notes` 也容易边界模糊。项目已有问题资产记录过 Harness section boundary duplication，说明这是稳定的产品语义问题，不只是一次 prompt 小毛病。

本设计目标是减少 AI 新生成内容的分类数量，降低重复填空倾向，同时不强制改写用户已有日记。

## 2. 核心决策

采用软合并，不做批量数据迁移：

1. 旧 Markdown entry、draft、version snapshot 原样保留。
2. 旧 section id 继续可解析、可查看，避免历史文件变成 invalid。
3. 新的 AI 整理、Harness section catalog 和新增块入口只暴露合并后的活跃分类。
4. 用户如果想把某天旧日记转换为新结构，可以手动点击重新整理，由已有 raw inputs 重新生成新分类草稿，再确认覆盖。
5. 覆盖正式 entry 时继续走现有 `EntryWritePipeline`，旧正式 entry 会先进入 `.journal/versions/`。

这个方案把迁移权交给用户，而不是应用启动时静默重写历史日记。

## 3. 新活跃分类

保留必需区块：

- `raw-inputs` 原始输入：只读源材料。
- `yesterday-review` 昨日回顾：昨天发生的事、完成情况、复盘和回看。
- `today-focus` 今日重点：今天最高优先级、关键行动或日程重心。

活跃可选区块调整为：

- `mood` 状态与情绪：情绪、压力、期待、疲惫、心理状态和状态变化。
- `work` 工作与学习：工作项目、开发、会议、排障、交付、读书、课程、方法论和技能成长。
- `relationship` 生活与关系：家庭、朋友、人际、生活事件、庆幸、珍惜和值得感谢的人事物。
- `health` 健康与精力：睡眠、身体状态、运动、饮食、作息和精力管理。
- `money` 财务：消费、收入、预算、理财和金钱意识。
- `inspiration` 灵感与未来提醒：突然想到的点子、长期观察、未来某天要提醒自己的事和非今日执行事项。

系统区块保留：

- `keywords` 关键词。
- `metadata-note` 生成信息。

## 4. Legacy 兼容分类

以下旧分类进入 legacy 状态：

- `learning` 学习与思考：合并到 `work` 工作与学习。
- `future-notes` 未来提醒：合并到 `inspiration` 灵感与未来提醒。
- `gratitude` 感恩：合并到 `relationship` 生活与关系。

Legacy section 的行为：

1. Parser 和 validator 继续识别 legacy section，不把旧日记标记为 unknown section。
2. Composer 继续能输出 legacy section，用于保留旧文档、旧草稿或从旧 version 查看内容。
3. Block editor 可以显示和编辑已经存在的 legacy section，避免用户打开旧日记时内容突然不可操作。
4. 新增块菜单不再提供 legacy section。
5. Harness Planner 和普通 AI 整理不再把 legacy section 作为可写目标。
6. Reorganize-existing 只生成活跃分类，因此用户确认后自然完成单篇日记的用户选择式迁移。

## 5. Prompt 分类规范

系统提示词应像编辑规范，而不是鼓励自由发挥。

总原则：

- 原始输入是事实源，不虚构、不美化成鸡汤。
- 只整理用户已经表达的内容。
- 不为了填满分类而硬写。
- 同一事实只放进一个最合适的 section。
- 内容很少时，宁可少生成几个分类。
- `today-focus` 最多 1-3 条，只做当天导航，不承载细节分类。

分类规则：

- `yesterday-review`：昨天发生了什么、完成了什么、有什么复盘。
- `today-focus`：今天最重要的 1-3 个优先事项，只放总重心。
- `mood`：心情、压力、期待、疲惫、精神状态。
- `work`：项目、开发、会议、排障、读书、课程、方法论、技能成长。
- `relationship`：家庭、朋友、人际、生活事件、值得珍惜或感谢的人事物。
- `health`：睡眠、身体状态、运动、饮食、作息。
- `money`：消费、收入、预算、理财、金钱意识。
- `inspiration`：点子、长期观察、未来某天要提醒自己的事。

冲突裁决：

- 如果一件事既像 `today-focus` 又像 `work`，细节放 `work`，`today-focus` 只保留抽象重心。
- 如果一件事是今天跑步、睡眠、身体状态，放 `health`，不要重复放 `today-focus`。
- 如果一件事是家人、朋友、母亲节、感谢某人或生活事件，放 `relationship`，不要拆到 `mood`。
- 如果一件事是以后要做、未来提醒或长期观察，放 `inspiration`，不要放 `today-focus`。
- 情绪区只记录状态，不承载事件详情；事件详情放对应主题区。
- 不要把同一句或同一个事实用近义句重复写入多个 section。

## 6. 数据与安全边界

本设计不执行自动批量迁移，因此：

- 不扫描并改写所有历史 Markdown。
- 不修改 `.journal/versions/` 中的历史快照。
- 不在启动时静默重写用户数据。
- 不改变 raw input 的事实源地位。

单篇用户选择式迁移依赖现有重新整理链路：

```text
existing raw inputs
  -> reorganize-existing Harness run
  -> active section catalog only
  -> reviewing draft
  -> user confirmation
  -> snapshot old formal entry
  -> write new formal entry
  -> rebuild/update index
```

## 7. UI 行为

- 块编辑的新增块菜单只显示活跃可选分类。
- 旧日记里已经存在的 legacy section 继续显示。
- 如果用户点击重新整理，界面不需要额外宣传“迁移”；其产品语义是“按当前分类规则重新整理”。
- 后续可以在数据备份或历史工作台增加提示，但本次不要求做全局迁移入口。

## 8. 测试要求

后续实现必须覆盖：

- `JmfSectionCatalog` 区分 active optional 与 legacy optional。
- 旧 `learning` / `future-notes` / `gratitude` section 仍然通过 parse/validate。
- `GetAvailableOptionalSections` 不返回 legacy section。
- Harness prompt 的 section catalog 不包含 legacy section。
- Reorganize-existing 只能面向活跃分类。
- Composer 对旧 legacy section 保持可输出。
- 前端新增块菜单不出现 legacy section。

## 9. 非目标

- 不做自动全量迁移。
- 不做 schema version bump。
- 不删除旧 Markdown 中的 legacy section。
- 不改写 version snapshot。
- 不引入复杂的用户自定义分类系统。
- 不实现 Future Notes 完整提醒系统。

## 10. Spec 自审

- Placeholder scan：无 TODO、TBD 或空章节。
- Internal consistency：软合并、legacy 兼容、用户选择式重新整理三者一致。
- Scope check：范围集中在 section catalog、prompt、编辑入口和测试，不包含全量迁移或 Future Notes 完整功能。
- Ambiguity check：明确 legacy 可解析但不再作为 AI / 新增块目标；明确不做自动批量迁移。
