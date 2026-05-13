import { ArrowLeft, RefreshCw, RotateCcw, Search } from "lucide-react";
import type { JournalEntryVersion, JournalHistoryEntryDetail, JournalHistoryEntrySummary } from "./api";

type HistoryWorkbenchProps = {
  isBusy: boolean;
  query: string;
  status: string;
  entries: JournalHistoryEntrySummary[];
  detail: JournalHistoryEntryDetail | null;
  selectedDate: string;
  versions: JournalEntryVersion[];
  error: string;
  onBack: () => void;
  onQueryChange: (value: string) => void;
  onStatusChange: (value: string) => void;
  onSelectDate: (date: string) => void;
  onRefresh: () => void;
  onRestoreVersion: (versionId: string) => void;
};

const statusOptions = [
  { value: "", label: "全部" },
  { value: "processed", label: "已保存" },
  { value: "updated", label: "已更新" },
  { value: "attention", label: "需处理" },
  { value: "missing", label: "缺失" }
];

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

function getStatusLabel(status: string) {
  switch (status) {
    case "processed":
      return "已保存";
    case "updated":
      return "已更新";
    case "attention":
      return "需处理";
    case "missing":
      return "缺失";
    default:
      return status;
  }
}

export function HistoryWorkbench({
  isBusy,
  query,
  status,
  entries,
  detail,
  selectedDate,
  versions,
  error,
  onBack,
  onQueryChange,
  onStatusChange,
  onSelectDate,
  onRefresh,
  onRestoreVersion
}: HistoryWorkbenchProps) {
  const selected = entries.find(entry => entry.date.isoDate === selectedDate) ?? entries[0] ?? null;
  const previewSections = detail?.sections ?? [];

  return (
    <>
      <aside className="context-rail history-rail" aria-label="历史搜索">
        <section className="date-card history-date-card">
          <p className="month">History</p>
          <h1>{selected?.date.monthDay ?? "-- --"}<span>本地历史与版本</span></h1>
        </section>

        <section className="rail-section">
          <label className="history-search">
            <Search size={15} aria-hidden="true" />
            <input
              aria-label="搜索历史日记"
              value={query}
              onChange={event => onQueryChange(event.target.value)}
              placeholder="搜索日记或原始材料"
            />
          </label>
          <div className="history-filter" aria-label="状态筛选">
            {statusOptions.map(option => (
              <button
                key={option.value || "all"}
                type="button"
                className={status === option.value ? "active" : ""}
                onClick={() => onStatusChange(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>
        </section>

        <section className="rail-section">
          <div className="section-head">
            <h2>结果</h2>
            <span>{entries.length} 篇</span>
          </div>
          <div className="history-result-list" aria-label="历史结果列表">
            {entries.length > 0 ? entries.map(entry => (
              <button
                key={entry.date.isoDate}
                type="button"
                className={`source-item history-result ${selected?.date.isoDate === entry.date.isoDate ? "is-active" : ""}`}
                onClick={() => onSelectDate(entry.date.isoDate)}
                aria-pressed={selected?.date.isoDate === entry.date.isoDate}
              >
                <span className="source-meta">
                  <span>{entry.date.isoDate}</span>
                  <span>{getStatusLabel(entry.status)}</span>
                </span>
                <strong>{entry.hits[0]?.title ?? entry.mood ?? "日记"}</strong>
                <p>{entry.hits[0]?.snippet ?? `${entry.rawInputCount} 条材料 / ${entry.versionCount} 个版本`}</p>
              </button>
            )) : (
              <p className="muted">没有匹配的历史日记。</p>
            )}
          </div>
        </section>
      </aside>

      <section className="journal-stage history-stage" aria-label="历史日记预览">
        <div className="stage-toolbar">
          <div className="stage-title">
            <p>历史工作台</p>
            <h2>历史与版本</h2>
          </div>
          <div className="history-stage-actions">
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
          <article className="journal-paper history-paper">
            {error ? <p className="api-error history-error" role="alert">{error}</p> : null}
            {selected ? (
              <>
                <header className="history-document-head">
                  <p className="kicker">Local History</p>
                  <h1>{selected.date.isoDate}</h1>
                  <p>
                    <span>{getStatusLabel(detail?.status ?? selected.status)}</span>
                    <span>{selected.rawInputCount} 条材料</span>
                    <span>{versions.length || selected.versionCount} 个版本</span>
                  </p>
                </header>

                {detail?.attentionReason ?? selected.attentionReason ? (
                  <p className="attention-copy">{detail?.attentionReason ?? selected.attentionReason}</p>
                ) : null}

                <div className="history-hit-list">
                  {previewSections.length > 0 ? previewSections.map(section => (
                    <section key={section.id} className="history-hit">
                      <span>{section.title}</span>
                      <p>{section.content}</p>
                    </section>
                  )) : selected.hits.map(hit => (
                    <section key={`${hit.sourceType}-${hit.sectionId ?? hit.rawInputId ?? hit.title}`} className="history-hit">
                      <span>{hit.sourceType === "raw-input" ? "原始材料" : hit.title}</span>
                      <p>{hit.snippet}</p>
                    </section>
                  ))}
                </div>
              </>
            ) : (
              <section className="empty-paper audit-empty-state">
                <h2>没有历史结果</h2>
                <p>调整搜索词或状态筛选后再看。</p>
              </section>
            )}
          </article>
        </div>
      </section>

      <aside className="assistant-panel today-assistant history-inspector" aria-label="版本详情">
        <div className="assistant-head">
          <div>
            <p className="assistant-eyebrow">Versions</p>
            <h2>版本快照</h2>
          </div>
          <span className="assistant-time">{versions.length} 个</span>
        </div>

        <div className="assistant-body">
          {versions.length === 0 ? (
            <section className="assistant-card">
              <p className="muted">这一天还没有覆盖前快照。</p>
            </section>
          ) : versions.map(version => (
            <section className="assistant-card history-version-card" key={version.id}>
              <div className="assistant-card-head">
                <h3>{formatHistoryTime(version.createdAt)}</h3>
                <span>{version.reason}</span>
              </div>
              <p>{version.contentHash}</p>
              <button
                type="button"
                className="assistant-inline-action"
                onClick={() => onRestoreVersion(version.id)}
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
