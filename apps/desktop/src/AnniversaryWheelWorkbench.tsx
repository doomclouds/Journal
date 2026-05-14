import { ArrowLeft, CalendarDays, Eye, RefreshCw, RotateCcw } from "lucide-react";
import type {
  JournalAnniversaryWheelResult,
  JournalEntryVersion,
  JournalHistoryEntryDetail,
  JournalHistoryEntrySummary,
  JournalVersionDetail
} from "./api";
import { MarkdownPreview } from "./MarkdownPreview";

type AnniversaryWheelWorkbenchProps = {
  isBusy: boolean;
  monthDay: string;
  result: JournalAnniversaryWheelResult | null;
  selectedDate: string;
  detail: JournalHistoryEntryDetail | null;
  versions: JournalEntryVersion[];
  selectedVersionDetail?: JournalVersionDetail | null;
  error: string;
  onBack: () => void;
  onRefresh: () => void;
  onMonthDayChange: (monthDay: string) => void;
  onSelectDate: (date: string) => void;
  onViewVersion?: (version: JournalEntryVersion) => void;
  onClearVersion?: () => void;
  onRestoreVersion: (version: JournalEntryVersion) => void;
};

const quickMonthDays = ["01-01", "05-14", "10-01", "12-31"];

function getStatusLabel(status: string) {
  switch (status) {
    case "processed":
      return "已保存";
    case "updated":
      return "已更新";
    case "reviewing":
      return "待确认";
    case "attention":
      return "需处理";
    case "missing":
      return "缺失";
    case "raw-only":
      return "仅材料";
    default:
      return status;
  }
}

function formatHistoryTime(value: string) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function firstLine(item: JournalHistoryEntrySummary) {
  return item.hits[0]?.snippet?.replace(/^[-\s]+/, "") || item.mood || `${item.rawInputCount} 条材料`;
}

export function AnniversaryWheelWorkbench({
  isBusy,
  monthDay,
  result,
  selectedDate,
  detail,
  versions,
  selectedVersionDetail = null,
  error,
  onBack,
  onRefresh,
  onMonthDayChange,
  onSelectDate,
  onViewVersion,
  onClearVersion,
  onRestoreVersion
}: AnniversaryWheelWorkbenchProps) {
  const items = result?.items ?? [];
  const selected = items.find(item => item.date.isoDate === selectedDate) ?? items[0] ?? null;
  const matchingDetail = detail?.date.isoDate === selected?.date.isoDate ? detail : null;
  const matchingVersions = selected
    ? versions.filter(version => version.date.isoDate === selected.date.isoDate)
    : [];
  const matchingVersionDetail = selectedVersionDetail?.version.date.isoDate === selected?.date.isoDate
    ? selectedVersionDetail
    : null;
  const currentDetailMarkdown = matchingDetail?.markdown?.trim() ?? "";
  const isShowingVersion = matchingVersionDetail !== null;

  return (
    <>
      <aside className="context-rail history-rail anniversary-rail" aria-label="同日年轮日期">
        <section className="date-card history-date-card">
          <p className="month">Anniversary</p>
          <h1>{monthDay}<span>同日年轮</span></h1>
        </section>

        <section className="rail-section">
          <label className="anniversary-picker">
            <CalendarDays size={15} aria-hidden="true" />
            <input
              aria-label="选择同日年轮日期"
              type="text"
              inputMode="numeric"
              pattern="\d{2}-\d{2}"
              value={monthDay}
              onChange={event => onMonthDayChange(event.target.value)}
              placeholder="MM-DD"
            />
          </label>
          <div className="anniversary-quick-days" aria-label="快捷日期">
            {quickMonthDays.map(day => (
              <button
                key={day}
                type="button"
                className={monthDay === day ? "active" : ""}
                onClick={() => onMonthDayChange(day)}
              >
                {day}
              </button>
            ))}
          </div>
        </section>

        <section className="rail-section">
          <div className="section-head">
            <h2>年份</h2>
            <span>{items.length} 年</span>
          </div>
          <div className="history-result-list anniversary-year-list" aria-label="同日年份列表">
            {items.length > 0 ? items.map(item => (
              <button
                key={item.date.isoDate}
                type="button"
                className={`source-item history-result anniversary-year ${selected?.date.isoDate === item.date.isoDate ? "is-active" : ""}`}
                onClick={() => onSelectDate(item.date.isoDate)}
                aria-pressed={selected?.date.isoDate === item.date.isoDate}
              >
                <span className="source-meta">
                  <span>{item.date.year}</span>
                  <span>{getStatusLabel(item.status)}</span>
                </span>
                <strong>{item.hits[0]?.title ?? item.mood ?? "日记"}</strong>
                <p>{firstLine(item)}</p>
              </button>
            )) : (
              <p className="muted">这一天还没有可回看的历史。</p>
            )}
          </div>
        </section>
      </aside>

      <section className="journal-stage history-stage anniversary-stage" aria-label="同日年轮预览">
        <div className="stage-toolbar">
          <div className="stage-title">
            <p>同日年轮</p>
            <h2>{monthDay} 的历年回声</h2>
          </div>
          <div className="history-stage-actions">
            {isShowingVersion ? (
              <button type="button" className="secondary-action secondary" onClick={onClearVersion}>
                <Eye size={15} aria-hidden="true" />
                查看当前日记
              </button>
            ) : null}
            <button type="button" className="secondary-action secondary" onClick={onRefresh} disabled={isBusy}>
              <RefreshCw size={15} aria-hidden="true" />
              刷新
            </button>
            <button type="button" className="secondary-action secondary" onClick={onBack}>
              <ArrowLeft size={15} aria-hidden="true" />
              返回今日
            </button>
          </div>
        </div>

        <div className="document-scroll history-scroll">
          <article className="journal-paper history-paper anniversary-paper">
            {error ? <p className="api-error history-error" role="alert">{error}</p> : null}
            {selected ? (
              <>
                <header className="history-document-head">
                  <p className="kicker">{isShowingVersion ? "Version Snapshot" : "Anniversary Wheel"}</p>
                  <h1>{selected.date.isoDate}</h1>
                  <p>
                    {isShowingVersion ? (
                      <>
                        <span>历史版本</span>
                        <span>{formatHistoryTime(matchingVersionDetail.version.createdAt)}</span>
                        <span>{matchingVersionDetail.version.reason}</span>
                      </>
                    ) : (
                      <>
                        <span>{getStatusLabel(matchingDetail?.status ?? selected.status)}</span>
                        <span>{selected.rawInputCount} 条材料</span>
                        <span>{matchingVersions.length || selected.versionCount} 个版本</span>
                      </>
                    )}
                  </p>
                </header>

                {!isShowingVersion && (matchingDetail?.attentionReason ?? selected.attentionReason) ? (
                  <p className="attention-copy">{matchingDetail?.attentionReason ?? selected.attentionReason}</p>
                ) : null}

                {isShowingVersion ? (
                  <section className="history-version-main-preview" aria-label="同日版本内容">
                    <MarkdownPreview markdown={matchingVersionDetail.markdown} />
                  </section>
                ) : currentDetailMarkdown ? (
                  <section className="history-current-main-preview" aria-label="同日当前日记内容">
                    <MarkdownPreview markdown={currentDetailMarkdown} />
                  </section>
                ) : (
                  <div className="history-hit-list">
                    {selected.hits.map(hit => (
                      <section key={`${hit.sourceType}-${hit.sectionId ?? hit.rawInputId ?? hit.title}`} className="history-hit">
                        <span>{hit.sourceType === "raw-input" ? "原始材料" : hit.title}</span>
                        <p>{hit.snippet}</p>
                      </section>
                    ))}
                  </div>
                )}
              </>
            ) : (
              <section className="empty-paper audit-empty-state">
                <h2>没有同日记录</h2>
                <p>换一个日期看看。</p>
              </section>
            )}
          </article>
        </div>
      </section>

      <aside className="assistant-panel today-assistant history-inspector anniversary-inspector" aria-label="同日年轮详情">
        <div className="assistant-head">
          <div>
            <p className="assistant-eyebrow">Raw & Versions</p>
            <h2>材料与版本</h2>
          </div>
          <span className="assistant-time">{matchingVersions.length} 个版本</span>
        </div>

        <div className="assistant-body">
          {selected?.hits.map(hit => (
            <section className="assistant-card anniversary-raw-card" key={`${hit.sourceType}-${hit.sectionId ?? hit.rawInputId ?? hit.title}`}>
              <div className="assistant-card-head">
                <h3>{hit.sourceType === "raw-input" ? "原始材料" : hit.title}</h3>
                <span>{hit.sourceType === "raw-input" ? "Raw" : "Section"}</span>
              </div>
              <p>{hit.snippet}</p>
            </section>
          ))}

          {matchingVersions.length === 0 ? (
            <section className="assistant-card">
              <p className="muted">这一天还没有覆盖前快照。</p>
            </section>
          ) : matchingVersions.map(version => (
            <section className="assistant-card history-version-card" key={version.id}>
              <div className="assistant-card-head">
                <h3>{formatHistoryTime(version.createdAt)}</h3>
                <span>{version.reason}</span>
              </div>
              <p>{version.contentHash}</p>
              <button
                type="button"
                className="assistant-inline-action"
                aria-label={`查看版本 ${version.id}`}
                onClick={() => onViewVersion?.(version)}
                disabled={isBusy}
              >
                <Eye size={14} aria-hidden="true" />
                查看版本
              </button>
              <button
                type="button"
                className="assistant-inline-action"
                aria-label={`恢复版本 ${version.id} 为草稿`}
                onClick={() => onRestoreVersion(version)}
                disabled={isBusy}
              >
                <RotateCcw size={14} aria-hidden="true" />
                恢复为草稿
              </button>
            </section>
          ))}
        </div>
      </aside>
    </>
  );
}
