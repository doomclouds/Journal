# History Workbench Stale Selection Restore Mismatch

- Date: `2026-05-13`
- Topic slug: `history-workbench-stale-selection-restore-mismatch`
- Status: `Captured`
- Scope: `UI`
- Tags: `history`, `frontend`, `race`, `restore`, `state`, `anniversary`

## Symptom

历史工作台切换日期、刷新、搜索或筛选时，左侧选中的日期已经变化，但中间详情和右侧版本列表短时间内仍显示旧日期的数据；用户如果此时点击“恢复为草稿”，前端可能拼出“新日期 + 旧 versionId”的 restore 请求。

后续用户还发现历史版本只能在列表里看到 metadata，不能打开指定版本查看 Markdown 内容；恢复操作也只传 `versionId`，仍依赖外层 selected date 作为请求 date，容易让“指定版本”这个 action target 和当前选择状态绑得太松。

Phase 6B 同日年轮复用这套 History Workbench 状态后，同类问题再次出现：version detail 请求晚返回会在刷新、切换或“查看当前日记”之后重新显示旧 snapshot；非法 `MM-DD` 输入如果只做正则校验，还会把旧年份卡片和旧摘要挂到新的非法日期标题下。

## Trigger / Context

- `App.tsx` 用 `historySelectedDate`、`historyDetail`、`historyVersions` 三份独立 state 管理历史工作台。
- 日期详情和版本列表是异步加载；请求期间 UI 仍可交互。
- restore API path 使用当前 `historySelectedDate` 和点击按钮传入的 `versionId` 组合。
- 后端已有 `GET /journal/history/{date}/versions/{versionId}`，但前端历史工作台没有调用它展示指定版本内容。
- 版本卡片的 restore 回调只传 `versionId`，丢失了版本自身携带的 `date`。
- 同日年轮新增 `anniversaryMonthDay`、`anniversaryResult` 和共享的 `historyDetail` / `historyVersions` / `historyVersionDetail`，输入日期和刷新请求也是异步的。
- version detail 请求和 entry detail 请求不是同一个生命周期；清空 version detail 如果不推进对应 request generation，pending response 仍可能晚回写。

## Root Cause

selected date 和 detail/version state 没有同生命周期失效。初版只在请求返回后更新详情和版本，切换日期或刷新期间没有立即清空旧 detail/versions；组件也没有按 `selectedDate` 校验 detail/versions 的归属日期。于是 UI 会把旧版本按钮挂在新日期上下文里。

这类问题不是普通视觉延迟，而是 state source 不一致导致的错对象操作风险。

版本查看缺口的根因是 Phase 4A 只接了版本列表和恢复动作，漏接了版本详情 API。恢复动作的根因是 action target 没有作为完整对象传递，导致前端必须重新从 selected state 取 date。

同日年轮暴露的新增根因是：只给 entry/detail 请求加 request id 不够，version detail 自己也需要独立的 generation guard；同时，用户输入 invalid month-day 时如果只“不发新成功结果”而不主动失效旧结果，UI 会继续把旧 result 当成当前 month-day 的内容。

## Fix

- `handleHistorySelectDate` 在发起 detail/version 请求前立即清空 `historyDetail` 和 `historyVersions`。
- `refreshHistory` 在递增 request id 后、发起 `getJournalHistory()` 前立即清空当前 detail/versions，覆盖刷新、搜索词变化和状态筛选变化。
- `HistoryWorkbench` 渲染前按 `selectedDate` 过滤：只有 `detail.date.isoDate === selectedDate` 才显示 detail，只有 `version.date.isoDate === selectedDate` 的版本才显示恢复按钮。
- 历史工作台接入 `getJournalHistoryVersion(date, versionId)`，版本卡新增“查看版本”，右栏显示“所选版本内容”。
- 版本卡的查看和恢复回调都传完整 `JournalEntryVersion`，恢复请求使用 `version.date.isoDate + version.id`，不再用外层 `historySelectedDate` 拼副作用请求。
- App 级测试覆盖日期切换、刷新挂起期间旧 detail/旧恢复按钮不可见，并断言不会发出错误 restore 请求；组件级测试覆盖 mismatched detail/version 不渲染。
- App/组件测试覆盖指定版本详情请求、版本 Markdown 展示和恢复回调传完整版本对象。
- Phase 6B 增加 `historyVersionRequestIdRef`，查看版本、刷新、切换日期和清空版本预览都会让旧 version detail response 失效。
- 同日年轮的 month-day 输入校验对齐后端规则：必须 `MM-DD`，月份 01-12，大小月正确，`02-29` 合法；非法输入会清空 anniversary result、selected date、detail、versions 和 version detail，并显示错误而不发请求。
- App 级测试覆盖同日年轮 detail/version 乱序、刷新后旧 version detail 晚返回、查看当前日记后 pending version detail 不复活，以及非法 month-day 不请求且不显示旧年份卡。

## Why This Fix

只靠 request id 防旧响应覆盖不够，因为风险窗口发生在新请求尚未返回之前。立即清空旧 state 可以消除窗口；组件内按日期过滤是第二道防线，即使上层 state 未来再次错配，也不会把旧版本按钮暴露给用户。

把 restore 请求改成 version 自身 date 能降低错配风险，但仍无法解决中间详情显示旧内容的问题，所以第一轮选择“失效旧 state + 渲染归属校验”的组合。后续补上“完整 version 对象作为 action target”，让恢复动作不再依赖 selected state；补上版本详情查看，则让用户在恢复前能确认指定版本内容。

对 version detail 这种二级详情，单纯复用 entry request id 不够，因为用户可以只关闭版本预览而不触发 entry 刷新；因此需要独立的 version request generation。对 month-day 输入，前端直接校验并清空旧 result 比“等后端 400 后只显示错误”更稳，因为它消除了错误标题下挂旧结果的用户可见窗口。

## Recognition Clues

- UI 左侧 active 日期已经变成 B，但中间还显示 A 的 section 内容。
- 右侧版本列表显示 A 的 `version-*`，而 header/selected date 是 B。
- App 测试需要 deferred promise 才能复现；所有请求立即 resolve 时不容易暴露。
- 代码里 restore path 由 selected state 和 version id 两个来源拼接。
- `api.ts` 已有 `getJournalHistoryVersion`，但 `App.tsx`/`HistoryWorkbench.tsx` 没有引用。
- 版本卡只有“恢复为草稿”，没有“查看版本”或“所选版本内容”区域。
- 同日年轮标题已切到新的 `MM-DD`，但左侧年份卡片或中间摘要仍是上一日期。
- “查看当前日记”后，之前挂起的版本详情请求晚返回，又把版本 snapshot 显示回来。
- 测试只在请求立即 resolve 时通过；加入 deferred promise 或连续跑全量前端测试时出现偶发失败。

## Applicability / Non-Applicability

### Applies When

- 主列表选择项、详情数据和 action target 分别存在独立 state。
- 详情/版本请求异步加载，且加载期间仍显示上一条数据。
- 用户操作会把 selected id/date 与 detail/version id 组合成写入、恢复、删除等有副作用请求。

### Does Not Apply When

- 详情数据和 action target 来自同一个不可分割对象，无法错配。
- 操作是纯读请求，没有恢复、写入或删除副作用。
- UI 明确支持 stale-while-revalidate，并且 action 按数据自身 id/date 发请求，不依赖外部 selected state。
- 输入控件只是过滤展示，不会驱动远端请求或切换详情上下文。

## Related Artifacts

- Spec: [2026-05-13-phase-4a-local-history-search-design.md](../../specs/2026-05-13-phase-4a-local-history-search-design.md)
- Plan: [2026-05-13-phase-4a-local-history-search-implementation-plan.md](../../plans/2026-05-13-phase-4a-local-history-search-implementation-plan.md)
- Archive: [2026-05-13-phase-4a-local-history-search-archives.md](../../archives/2026-05/2026-05-13-phase-4a-local-history-search-archives.md)
  - [2026-05-14-phase-6b-anniversary-wheel-archives.md](../../archives/2026-05/2026-05-14-phase-6b-anniversary-wheel-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [App.tsx](../../../../apps/desktop/src/App.tsx)
  - [HistoryWorkbench.tsx](../../../../apps/desktop/src/HistoryWorkbench.tsx)
  - [AnniversaryWheelWorkbench.tsx](../../../../apps/desktop/src/AnniversaryWheelWorkbench.tsx)
  - [App.test.tsx](../../../../apps/desktop/src/App.test.tsx)
  - [HistoryWorkbench.test.tsx](../../../../apps/desktop/src/HistoryWorkbench.test.tsx)
