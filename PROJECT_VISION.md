# Journal 项目愿景与设计蓝图

> 版本：v0.5
> 日期：2026-05-10
> 定位：基于《晨间日记的奇迹》思想的本地优先、AI 辅助 Markdown 桌面日记应用

## 1. 背景与灵感来源

《晨间日记的奇迹》是佐藤传关于晨间日记方法的书。公开书目信息显示，中文简体版由南海出版公司于 2009 年出版，共 143 页，内容围绕“为什么早上写日记”“写日记实现梦想”“电子晨间日记”“晨间日记作战法则”等主题展开。Google Books 的书目摘要将其概括为：每天早上用很短时间写日记，把过去经验、未来目标和当天行动连接起来，提高工作效率与实现目标的概率。

从公开资料和书评整理，这套方法的核心不是传统意义上的长篇情绪宣泄，而是一个低负担、可持续、可回看的人生记录系统：

- 早上写：隔了一晚后，情绪更稳定，更适合客观回看昨天。
- 时间短：强调几分钟内完成，降低习惯门槛。
- 分两层：一层记录昨天发生了什么，一层记录今天要做什么、未来想实现什么。
- 同日关联：把每年同一天的内容放在一起，形成“多年后的今天”的纵向视角。
- 电子化优势：搜索、分类、备份、附件、多年同日回看，比纸本更适合长期沉淀。
- 柔性结构：固定结构能降低空白压力，但结构过硬会压死灵感。

Journal 的产品启发是：它不应该只是“又一个 Markdown 日记本”，也不应该是“AI 帮用户写漂亮作文”。它应该是一套让用户自然表达、让 AI 负责整理、让 Markdown 长期保存的人生记录系统。

## 2. 产品愿景

Journal 是一款本地优先的桌面晨间日记应用，用来记录一个人长期的人生轨迹。

用户每天可以像聊天一样输入一段自然语言，内容可以来自键盘，也可以来自外部语音输入法。Journal 保存用户的原始表达，再通过可插拔 AI Provider 提炼出结构化 JSON，最后渲染成人类可读、可长期保存、可检索的 Markdown 日记。

一句话版本：

> Journal 要成为一个人每天早上 3 分钟打开的人生坐标系统：你负责真实表达，AI 负责整理，Markdown 负责保存。

## 3. 产品原则

### 3.1 原始表达不可丢

AI 整理后的文本只是阅读层和检索层。用户当天说过什么、怎么说的，才是日记的底片。每次输入的原始文字都必须保存，不能被 AI 摘要覆盖。

### 3.2 固定核心，弹性补充

日记必须有长期可读的稳定结构，但不能被九宫格式固定模板锁死。默认固定“昨日回顾”和“今日重点”，其他内容由 AI 根据当天表达自动补充，例如情绪、灵感、健康、关系、工作、长期目标线索等。

### 3.3 Markdown 是可信源

Markdown 文件是长期存档和人类可读的可信源。SQLite 索引库只是可重建缓存，不能成为唯一真相。用户即使脱离应用，也应该能直接打开 Markdown 文件查看自己的日记。

### 3.4 可增加、可更新、不可删除

日记记录用户的人生轨迹，不提供删除模型。应用可以追加输入、更新整理结果、生成新版本，但 UI 和 API 都不提供删除日记能力。外部手工删除文件时，系统应标记异常，而不是静默遗忘。

### 3.5 本地优先与隐私优先

第一版默认所有数据保存在本机。AI 调用只发送用户主动提交的文本，不把日记内容写入不必要的日志。模型供应商通过用户配置接入，应用不默认绑定云服务。

### 3.6 工具感优先

这是长期自用工具，不是营销网站。界面应安静、克制、可读、启动快、输入顺，不做花哨动效抢注意力。

### 3.7 结构由系统保护，内容保持自由

Journal 的 Markdown 不是完全无约束文本，而是可被解析的结构化 Markdown。普通用户在块编辑模式下只编辑内容，不接触机器标记；专业用户可以切换源码模式编辑完整 Markdown，但保存时必须通过结构校验。

## 3.8 当前实现快照

截至 2026-05-10，主线已交付到 Phase 5：

```text
自然语言输入
  -> Raw input 持久化
  -> Mock 或真实 OpenAI-compatible LLM 输出 JournalAiJson
  -> 服务端校验并渲染 JMF Markdown 草稿
  -> 块编辑 / 源码编辑与 JMF 结构校验
  -> 用户确认
  -> 正式 Markdown 文件
```

已经具备：今日输入、原始输入保存、Mock 兜底、OpenAI-compatible LLM 调用、Provider 配置、连接测试、受保护启用、API Key 掩码/查看、JMF 草稿预览、块编辑、源码编辑、结构校验和确认写入。

仍未具备：版本快照、SQLite 索引/搜索、同日年轮、多日期浏览、AI 改写聊天、自动保存、应用内录音/语音转写、安装包，以及生产模式下 Electron 自动托管 .NET 后端。

## 4. 核心产品模型

### 4.1 今日晨间卡

用户打开应用后看到当天主工作区。第一版的核心输入不是表单，而是一个自然语言输入区：

- 用户可以直接键盘输入。
- 用户可以使用外部 GLM / 智谱语音输入法等工具把语音转为文字后输入。
- 应用第一版不接入录音、语音识别、音频存储或转写服务。

提交后，系统将内容保存为当天的一次原始输入，并触发 AI 整理。

### 4.2 原始输入记录

同一天可以多次输入。每次输入都作为独立原始记录保存：

- 输入时间。
- 原始文字。
- 输入来源，例如 text、external-voice-ime。
- AI 整理批次。

主视图默认展示整洁日记。原始表达按长度自适应展示：短内容直接显示，长内容默认折叠，需要时展开查看。

### 4.3 AI 整理结果

AI 不直接输出 Markdown，而是输出结构化 JSON。后端负责：

1. 校验 JSON。
2. 归一化字段。
3. 渲染 Markdown 预览。
4. 等待用户确认或继续调整。
5. 用户确认后写入正式 Markdown。
6. 后续阶段再补版本快照和 SQLite 检索索引。

这样可以避免模型直接生成 Markdown 时格式漂移，也避免 AI 在用户确认前污染正式日记。

### 4.4 今日整洁版

当天主 Markdown 文件始终表示用户确认过的当前最新版，适合用户阅读、编辑器打开和前端渲染。AI 草稿、LLM 重生成和编辑保存都先落在 draft 边界内，不直接写入正式文件；历史版本目录是后续 Phase 4 能力。

### 4.5 同日年轮

围绕月日维度建立纵向关联。例如 5 月 7 日打开应用时，可以看到：

- 2026-05-07 的今日记录。
- 2025-05-07、2024-05-07 等历史同日记录。
- 同日记录中的关键词、情绪、主题变化。

这会形成“人生年轮”体验：用户不是只看昨天，而是在看不同年份的自己。

### 4.6 未来日记

未来日记不是普通待办清单，而是“给未来自己的锚点”：

- 某个未来日期想提醒自己的话。
- 某个长期梦想的阶段性推进。
- 某个纪念日、复盘日、承诺日。
- 一个现在还不能做，但不想忘记的方向。

第一版不实现完整未来提醒，但 Markdown、JSON 和索引结构需要为它预留扩展位置。

## 5. AI 辅助日记生成链路

### 5.1 输入边界

当前已交付输入链路只从“自然语言文本”开始：

```text
用户自然语言文本
  -> 保存原始输入
  -> 调用 Mock 或真实 LLM Provider
  -> AI 返回结构化 JSON
  -> 后端校验并渲染 Markdown 预览
  -> 用户通过块编辑或源码编辑调整草稿
  -> 用户确认后写入正式 Markdown
```

后续阶段再扩展：

```text
正式 Markdown
  -> 生成版本快照
  -> 更新 SQLite / FTS 索引
  -> 支持搜索、同日年轮和状态筛选
```

不纳入当前已交付范围：

- 应用内录音。
- 语音转文字。
- 音频文件管理。
- 语音模型调用。
- AI 改写聊天。
- 版本快照与 SQLite 索引。

### 5.2 JSON 中间结构

AI Provider 输出 JSON，供程序处理。JSON 使用稳定的块类型，而不是完全自由的标题。示例：

```json
{
  "date": "2026-05-07",
  "summaryTitle": "项目启动与产品蓝图整理",
  "rawInputIds": ["2026-05-07T08-12-30"],
  "yesterdayReview": [
    "完成了 Journal 项目愿景文档",
    "明确了 Markdown 本地存储方向"
  ],
  "todayFocus": [
    "确认 AI Provider 抽象设计",
    "确定 JSON 到 Markdown 的渲染链路"
  ],
  "optionalSections": [
    {
      "id": "inspiration",
      "title": "灵感",
      "items": [
        "日记不可删除，只能追加和更新",
        "原始表达是底片，AI 摘要是阅读层"
      ]
    }
  ],
  "tags": ["项目启动", "AI整理", "Markdown"],
  "topics": ["工作", "灵感"],
  "mood": "疲惫但有推进感",
  "futureNotes": []
}
```

### 5.3 Markdown 渲染结果

用户和前端主要面对 Markdown。Markdown 采用 `JMF v1` 格式：YAML front matter 存放元数据，HTML 注释存放机器可识别的块边界，标题和正文面向人类阅读。

```markdown
---
schema: journal-entry/v1
id: 2026-05-07
date: 2026-05-07
month_day: 05-07
status: reviewing
tags: [项目启动, AI整理, Markdown]
topics: [工作, 灵感]
mood: 疲惫但有推进感
version: 3
provider: deepseek
model: deepseek-v4-flash
prompt_version: journal-entry-v1
generated_at: 2026-05-07T08:15:20+08:00
---

# 2026-05-07 晨间日记

<!-- journal:section raw-inputs -->
## 原始输入

<details>
<summary>08:12 原始表达</summary>

昨天我有点累，但是把项目愿景写出来了……

</details>
<!-- /journal:section -->

<!-- journal:section yesterday-review -->
## 昨日回顾

- 完成了 Journal 项目愿景文档
- 明确了 Markdown 本地存储方向

<!-- /journal:section -->

<!-- journal:section today-focus -->
## 今日重点

- 确认 AI Provider 抽象设计
- 确定 JSON 到 Markdown 的渲染链路

<!-- /journal:section -->

<!-- journal:section inspiration -->
## 灵感

- 日记不可删除，只能追加和更新
- 原始表达是底片，AI 摘要是阅读层

<!-- /journal:section -->

<!-- journal:section keywords -->
## 关键词

`项目启动` `AI整理` `Markdown`

<!-- /journal:section -->
```

### 5.4 预览确认流

AI 生成的内容必须先进入预览状态，用户确认后才写入正式 Markdown：

```text
原始输入
  -> AI JSON 草稿
  -> Markdown 预览
  -> 用户确认
  -> 正式 Markdown
```

用户在预览页可以：

- 一键确认保存。
- 使用块编辑模式调整已知 JMF 区块。
- 使用源码模式编辑完整 Markdown，并在保存前通过结构校验。
- 重新选择可用 LLM 整理今日草稿。
- 保留本次原始输入，即使 LLM 生成失败也不丢失底稿。

后续再扩展 AI 对话式改写、显式放弃草稿、自动暂存和多日期草稿管理。

确认前，系统状态为 `reviewing`，正式日记文件不被覆盖。确认后，才进入 `processed` 或 `updated`。

### 5.5 JMF v1 块规则

`JMF v1` 是 Journal Markdown Format 的第一版。它的目标是保证 Markdown 可读，同时能和 JSON 稳定互转。

必需块，永远存在，不可删除：

- `raw-inputs`：原始输入。
- `yesterday-review`：昨日回顾。
- `today-focus`：今日重点。

可选单例块，每篇最多一个：

- `mood`：情绪状态。
- `inspiration`：灵感。
- `health`：健康与精力。
- `relationship`：关系与家庭。
- `work`：工作推进。
- `learning`：学习与思考。
- `money`：财务。
- `future-notes`：未来提醒。
- `gratitude`：感恩。

系统块，用户默认不可直接编辑：

- `keywords`：关键词。
- `metadata-note`：生成信息和模型信息。

块顺序在第一版固定。可选块如果没有内容就不渲染；一旦出现，系统按固定顺序插入，而不是按 AI 输出顺序或用户拖拽顺序插入。

固定顺序：

1. 原始输入
2. 情绪状态
3. 昨日回顾
4. 今日重点
5. 工作推进
6. 学习与思考
7. 健康与精力
8. 关系与家庭
9. 财务
10. 灵感
11. 未来提醒
12. 感恩
13. 关键词
14. 生成信息

### 5.6 编辑模式

Journal 提供两种编辑模式：

- 块编辑模式：默认模式。用户只看到“原始输入、昨日回顾、今日重点、灵感”等块，不看到 `<!-- journal:section ... -->` 这类机器标记。用户只能编辑块内容，新增块时只能从尚未出现的可选块类型中选择。
- 源码模式：高级模式。用户可以编辑完整 Markdown，包括 front matter 和 section marker。保存时必须通过 JMF 校验。

源码模式校验规则：

- front matter 必须存在。
- `schema: journal-entry/v1` 必须存在。
- 必需块必须存在。
- section marker 必须成对。
- 同一个 section id 不能重复。
- 未知 section id 进入 `attention` 状态。
- 如果用户修改标题但保留 marker，系统正常解析。
- 如果 marker 损坏，系统尝试弱解析；弱解析失败时不覆盖正式文件。

### 5.7 AI 行为约束

AI 整理时必须遵守：

- 不虚构用户没有表达的事实。
- 不为了凑结构强行生成空洞区块。
- 不把原始表达改写成失真的“鸡汤文”。
- 可以提炼目的、情绪、重点、关键词。
- 不确定的内容应弱化表达，不写成确定事实。

## 6. AI Provider 抽象

Journal 采用可插拔 AI Provider，不把业务绑定到单一供应商。

### 6.1 Provider 能力

当前已实现的通用能力：

- 文本生成：用于“自然表达 -> 日记 JSON”。
- 健康检查：用于验证 baseUrl、apiKey、model 是否可用。
- Provider 配置安全视图：设置页读取时不返回完整 API Key。
- Candidate 测试：用户可以先测试当前表单内容，不必先保存。
- 受保护启用：测试通过后才写入并启用 Provider。
- Mock 兜底：未配置真实 key 时仍可跑完整日记流程。

当前内置预设：

- `OpenAI`：默认模型 `gpt-5.4`。
- `DeepSeek`：默认模型 `deepseek-v4-flash`。
- `智谱 GLM`：默认模型 `glm-5.1`。
- `MockAiProvider`：本地假模型，用于开发、测试和无 Key 演示。
- `Custom`：OpenAI-compatible 高级配置，可自定义 baseUrl、model、超时、温度、max tokens 和整理风格。

暂未实现：模型列表拉取、streaming、embedding、Ollama、本地模型、语音转写和 Provider 能力声明。

### 6.2 当前 Provider 接口

```csharp
public interface IJournalAiProvider
{
    string ProviderId { get; }

    Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken);

    Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken);
}
```

当前持久化配置示例：

```json
{
  "activeProviderId": "deepseek",
  "providers": [
    {
      "id": "deepseek",
      "type": "openai-compatible",
      "displayName": "DeepSeek",
      "preset": "deepseek",
      "baseUrl": "https://api.deepseek.com",
      "model": "deepseek-v4-flash",
      "apiKey": "",
      "isEnabled": true,
      "timeoutSeconds": 45,
      "temperature": 0.2,
      "maxTokens": 1200,
      "stylePreset": "faithful"
    },
    {
      "id": "openai",
      "type": "openai-compatible",
      "displayName": "OpenAI",
      "preset": "openai",
      "baseUrl": "https://api.openai.com/v1",
      "model": "gpt-5.4"
    },
    {
      "id": "zhipu",
      "type": "openai-compatible",
      "displayName": "智谱 GLM",
      "preset": "zhipu",
      "baseUrl": "https://open.bigmodel.cn/api/paas/v4",
      "model": "glm-5.1"
    },
    {
      "id": "custom",
      "type": "openai-compatible",
      "displayName": "Custom",
      "preset": "custom",
      "baseUrl": "http://localhost:11434/v1",
      "model": "custom-model"
    },
    {
      "id": "mock",
      "type": "mock",
      "displayName": "Mock",
      "preset": "mock",
      "baseUrl": "local",
      "model": "mock-journal"
    }
  ]
}
```

配置读取顺序是环境变量优先、配置文件兜底。`GET /settings/ai` 只返回掩码和来源，不返回完整 key；只有文件配置的 key 可以在用户点击查看时通过 `GET /settings/ai/{providerId}/api-key` 返回，环境变量来源的 key 不可 reveal。API Key 不写入 Markdown、draft metadata、版本快照、普通日志或错误报告。

## 7. Markdown 存储与版本模型

### 7.1 主目录结构

一天一个 Markdown 文件，目录按年月分层：

```text
journal/
  entries/
    2026/
      05/
        2026-05-07.md
```

这种组织方式简单、可迁移、Git 友好，也方便按 `MM-DD` 扫描同日年轮。

### 7.2 隐藏工程目录

当前应用维护隐藏目录，用于原始输入、草稿、内部元数据和 LLM 配置：

```text
journal/
  .journal/
    raw-inputs/
      2026/
        05/
          2026-05-07.jsonl
    drafts/
      2026/
        05/
          2026-05-07.md
          2026-05-07.meta.json
    settings/
      ai-providers.json
```

`entries` 是人类主目录，应该保持干净。`.journal` 是应用维护目录，用户通常不需要手工编辑。

后续 Phase 4 再补版本和索引目录：

```text
journal/
  .journal/
    versions/
      2026/
        05/
          2026-05-07/
            2026-05-07T08-12-30.md
            2026-05-07T09-40-02.md
    index/
      journal.db
```

### 7.3 更新规则

当天可以多次输入。系统采用“底层可追溯，主视图干净”的策略：

- 每次输入都追加到 `raw-inputs`。
- AI 读取当天已有内容和本次新增输入，生成 JSON 草稿。
- 后端渲染出 Markdown 预览，进入 `reviewing` 状态。
- 用户确认前不覆盖 `entries` 下的正式 Markdown。
- 当前版本用户确认后直接更新正式 Markdown；Phase 4 再补“保存新 Markdown 前先将旧版写入 `versions`”。
- 当前文件始终保持用户确认过的最新版。

### 7.4 删除规则

日记不可删除：

- UI 不提供删除日记按钮。
- API 不提供 `DeleteEntry`。
- 版本快照不提供物理删除。
- 外部手工删除 Markdown 时，索引将该日记标记为 `missing` 或 `attention`，并提示用户处理。

后续如确实需要隐藏某段内容，只能做“隐藏/归档/加密”这类软处理，不做删除。

## 8. 检索与索引设计

### 8.1 基本原则

Markdown 是可信源，SQLite 索引库是可重建缓存。检索不能每次硬扫 Markdown 文件，否则数据变多后体验会变差。

### 8.2 Phase 4 检索范围

Phase 4 计划实现：

- 日期检索。
- 标签检索。
- 关键词检索。
- 全文搜索。
- 同日年轮。
- 按状态筛选，例如需要处理、已整理、已有草稿。

Phase 4 暂不实现：

- embedding 语义检索。
- 多模态检索。
- 云端索引。

### 8.3 SQLite 索引草案

核心索引表：

- `entries`：日期、文件路径、标题、情绪、标签、状态、版本号、文件 hash。
- `entry_sections`：section id、标题、固定顺序、原始表达、昨日回顾、今日重点、灵感等分段内容。
- `raw_inputs`：每次原始输入的时间、来源、文本 hash、关联日期。
- `entry_versions`：版本号、快照路径、创建时间、触发原因。
- `future_notes`：未来提醒、目标日期、来源文件。
- `entry_fts`：SQLite FTS5 全文索引。
- `entry_drafts`：AI 预览草稿、用户反馈、草稿状态、生成时间。

预留字段：

- `embedding_status`
- `embedding_model`
- `content_hash`
- `model_provider`

这些字段用于未来接入语义搜索，但第一版不生成向量。

### 8.4 重建机制

索引重建规则：

- 保存 Markdown 时同步更新索引。
- 应用启动时扫描文件 hash，只重建变更过的文件。
- 索引库损坏时，可以从 Markdown 全量重建。
- 用户手工修改 Markdown 后，下一次扫描能识别并更新索引。

## 9. 日记状态流

第一版使用轻量状态，不制造打卡压力：

- `empty`：未记录，当天还没有任何输入。
- `draft`：已保存原始输入，但尚未生成 AI 预览。
- `reviewing`：AI 预览已生成，等待用户确认。
- `processed`：用户已确认，正式 Markdown 已写入。
- `updated`：今日补充过，且新版已确认。
- `attention`：需要处理，AI 失败、JSON 校验失败、文件外部异常、索引异常等。
- `missing`：索引记录存在，但对应 Markdown 文件被外部删除或移动。

状态用途：

- 今日页展示当前处理状态。
- 日历用低调标记显示是否记录过。
- 搜索筛选可以找出异常或草稿。
- `reviewing` 记录可从今日页继续确认、继续对话或手动编辑预览。
- 不做连续打卡排名，不制造焦虑。

## 10. 技术蓝图

### 10.1 总体架构

Journal 采用 Electron + React + .NET 的前后端分离桌面架构：

- Electron：负责桌面壳、窗口管理、本地进程启动、系统集成。
- React：负责今日工作台、Markdown 预览、块编辑、源码编辑、状态呈现和 LLM 配置页面。
- .NET 后端：负责本地 API、AI Provider、JMF v1 渲染与解析、结构校验和文件存储。
- SQLite：作为后续本地索引库，不作为唯一数据源；当前尚未交付。
- Markdown 文件：作为用户日记可信源。

前端与后端通过本机 HTTP API 通信。开发阶段可以固定端口；生产打包时由 Electron 启动 .NET 后端进程，并在窗口关闭时做生命周期管理。

### 10.2 推荐技术栈

- 前端：Electron + React + TypeScript + Vite
- 后端：.NET 10 + ASP.NET Core Minimal API
- Markdown：后端负责 JMF v1 渲染、解析、校验，前端负责预览、块编辑和源码编辑
- 索引：SQLite + FTS5，Phase 4 交付
- 测试：xUnit / Vitest / Playwright 或 Electron 自动化测试
- 打包：Electron Builder 或 Forge，后续根据 Windows 安装包需求再选

### 10.3 目录规划草案

```text
Journal/
  apps/
    desktop/                    # Electron + React 前端
  src/
    Journal.Api/                # 本地 .NET API
    Journal.Domain/             # 日记、输入、版本、状态、AI 请求等领域模型
    Journal.Infrastructure/     # Markdown、SQLite、文件、AI Provider 实现
  tests/
    Journal.Tests/              # .NET 单元测试
  docs/
    architecture/               # 架构说明
    product/                    # 后续产品文档
  PROJECT_VISION.md
  README.md
```

## 11. 功能路线图

实现顺序说明：当前代码已先交付 Phase 5 的真实 LLM Provider 和设置体验，Phase 4 的版本快照与 SQLite 索引仍未交付。路线图编号保留产品能力的逻辑分层，不代表严格完成顺序。

### 阶段 0：愿景与设计蓝图

产物：

- 项目愿景文档。
- AI 辅助 Markdown 日记设计。
- 第一阶段边界。

验收：

- 能解释产品为什么存在。
- 能解释用户输入、AI 整理、Markdown 存储、检索索引、版本记录之间的关系。

### 阶段 1：应用框架骨架

目标是让 Electron 前端和 .NET 后端在本地跑起来，并建立可持续迭代的工程骨架。

> 执行策略：阶段 1 已确认采用 B 方案：工程化薄壳闭环。开发期先使用 .NET API 与 Electron/Vite 双进程联通，不在本阶段处理 Electron 托管 .NET 后端进程。

产物：

- Electron + React + TypeScript 前端应用。
- .NET API 解决方案。
- 本地 API 健康检查接口。
- 前端调用后端健康检查并显示运行状态。
- 基础目录、开发脚本、README 启动说明。
- 最小测试链路。

明确不做：

- 不做真实 AI 调用。
- 不做完整日记编辑器。
- 不做 SQLite 索引。
- 不做安装包。

### 阶段 2：JMF 生成确认 MVP

目标是打通“自然语言文本 -> Mock AI JSON -> JMF v1 Markdown 预览 -> 用户确认 -> 正式 Markdown 文件”的最小闭环。

产物：

- 今日输入框。
- MockAiProvider。
- JSON 校验。
- JMF v1 Markdown 渲染。
- 预览确认流。
- 一天一个 Markdown 文件。
- 原始输入保存。
- 用户确认后更新当前文件。
- 今日工作台。

明确不做：

- 块编辑模式。
- 源码模式。
- 版本快照。
- SQLite 索引。
- 真实 AI Provider。

这些能力从原阶段 2 拆出，避免第一条主链路被编辑器和长期可靠性复杂度拖胖。

### 阶段 3：JMF 编辑模式与结构校验

目标是让用户可以安全修改 JMF Markdown，同时保护机器可解析结构。

产物：

- 块编辑模式。
- 源码模式。
- JMF marker 成对校验。
- 必需块校验。
- 未知 section 的 `attention` 状态。
- 保存前结构保护。
- 结构修复提示。

### 阶段 4：版本与索引

目标是让日记可以安全更新，并具备基础检索能力。

产物：

- 版本快照。
- 不可删除约束。
- SQLite 索引库。
- FTS5 全文搜索。
- 文件 hash 扫描与索引重建。
- Markdown -> JSON 解析。
- JMF v1 结构异常检测。
- 状态流。

### 阶段 5：真实 AI Provider

目标是接入真实可配置模型。

状态：已交付基础可用版，并完成一轮 LLM 设置体验 polish。

产物：

- OpenAICompatibleProvider。
- DeepSeek、OpenAI、智谱 GLM 默认预设。
- 普通 Provider 配置页。
- 高级自定义 Provider 配置页。
- 健康检查。
- API Key 掩码、文件配置 key 的显式查看、环境变量 key 的不可 reveal 边界。
- Candidate 测试与受保护启用。
- AI 失败时的 attention 状态和重试。

后续增强：

- 模型列表拉取。
- Streaming。
- Provider 能力声明。
- 更丰富的整理风格编辑。

### 阶段 6：同日年轮与未来日记

目标是做出产品差异化体验。

产物：

- 历史同日记录。
- 标签、情绪、主题变化。
- FutureNote 基础模型。
- 今日应回看的未来提醒。

### 阶段 7：长期可靠性

目标是让用户敢把长期人生记录交给这个应用。

产物：

- 本地备份。
- 导出 Markdown / JSON。
- 数据迁移版本管理。
- 可选加密。
- 安装包。

## 12. 第一阶段实施建议

第一阶段推荐采用“薄壳先通”的策略：

1. 建立 .NET solution，拆出 API、Domain、Infrastructure。
2. 建立 Electron + React + TypeScript 前端。
3. Electron 开发模式下指向 Vite dev server。
4. .NET API 提供 `/health`。
5. React 首页显示 API 状态、当前日期、阶段占位。
6. README 写清楚启动步骤。
7. 增加最小测试，确保健康检查和前端基础渲染可验证。

第一阶段只追求一件事：后续所有功能都有地方长出来。

## 13. 风险与取舍

### 13.1 Electron + .NET 双进程复杂度

双进程桌面应用需要处理端口、启动顺序、关闭清理、日志和异常提示。第一阶段要尽早验证这条链路。

### 13.2 AI 输出不稳定

模型可能返回不合法 JSON、空洞内容或虚构事实。必须通过 JSON Schema、后端校验、失败状态、重试机制和 MockAiProvider 测试来控制风险。

### 13.3 Markdown 与索引一致性

用户可能手工编辑 Markdown。系统需要用文件 hash、更新时间和重建机制保证索引能跟上源文件变化。

### 13.4 JMF 结构被破坏

源码模式允许专业用户直接编辑 Markdown，但也可能破坏 section marker 或 front matter。系统保存前必须做 JMF 校验；校验失败时不覆盖正式文件，进入 `attention` 状态，并提供结构修复建议。

### 13.5 不可删除带来的敏感内容问题

不可删除是产品原则，但日记可能包含敏感内容。未来需要设计隐藏、加密、导出和备份策略。第一版先明确不做物理删除。

### 13.6 Provider 配置安全

API Key 不能进入 Markdown、draft metadata、普通日志、错误报告或未来版本快照。当前策略是环境变量优先、配置文件兜底；配置文件不做加密，安全边界靠本机用户目录权限和 UI 的默认隐藏/显式查看。后续如果引入多用户、同步或备份，再单独评估 Windows Credential Manager、DPAPI 或主密码方案。

## 14. 当前默认假设

- 目标平台先以 Windows 桌面为主。
- 第一版单机使用，不做登录和云同步。
- 后端使用 .NET 10，实际创建项目时以本机 SDK 可用版本为准。
- Markdown 是可信源，SQLite 是可重建索引。
- Markdown 使用 JMF v1；当前已实现生成、预览、确认、块编辑和源码编辑。
- 第一版不接入应用内语音。
- AI Provider 可插拔；当前已实现 Mock 与 OpenAI-compatible Provider，未配置真实 key 时使用 Mock 兜底。
- 语言默认中文，后续保留国际化可能性，但不在第一阶段实现。

## 15. 公开资料来源

- [Google Books：《晨间日记的奇迹》书目信息](https://books.google.com/books/about/%E6%99%A8%E9%97%B4%E6%97%A5%E8%AE%B0%E7%9A%84%E5%A5%87%E8%BF%B9.html?id=x_k9ygAACAAJ)
- [博客來：《晨間日記的奇蹟》内容简介](https://www.books.com.tw/products/0010317813)
- [豆瓣：《晨间日记的奇迹》书评与版本信息](https://book.douban.com/subject/3744041/)
- [博客园：晨间日记的奇迹，和将来的自己对话](https://www.cnblogs.com/me115/archive/2012/05/06/2485599.html)
- [S&S Life：佐藤傳的「晨間日記的奇蹟」阅读记录](https://sslife.tw/2006-12-03-590/)
