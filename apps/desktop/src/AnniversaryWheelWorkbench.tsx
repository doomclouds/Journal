import { ArrowLeft, Eye, RefreshCw } from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";
import type {
  JournalAnniversaryItem,
  JournalAnniversarySaveRequest,
  JournalAnniversaryWheelResult,
  JournalEntryVersion,
  JournalHistoryEntryDetail,
  JournalHistoryEntrySummary,
  JournalNextYearNoteStatus
} from "./api";
import { DatePickerField } from "./DatePickerField";
import { JournalPaperLoading } from "./JournalPaperLoading";
import { MarkdownPreview } from "./MarkdownPreview";

type AnniversaryWheelWorkbenchProps = {
  isBusy: boolean;
  isAnniversarySaving?: boolean;
  isNextYearNoteSaving?: boolean;
  monthDay: string;
  result: JournalAnniversaryWheelResult | null;
  anniversaries: JournalAnniversaryItem[];
  anniversaryError: string;
  selectedDate: string;
  detail: JournalHistoryEntryDetail | null;
  versions: JournalEntryVersion[];
  error: string;
  onBack: () => void;
  onRefresh: () => void;
  onMonthDayChange: (monthDay: string) => void;
  onSelectDate: (date: string) => void;
  onSaveAnniversary: (id: string | null, request: JournalAnniversarySaveRequest) => void | Promise<void>;
  onAddNextYearNote: (anniversaryId: string, text: string) => void | Promise<void>;
  onAdoptNextYearNote?: (anniversaryId: string, noteId: string) => void | Promise<void>;
  onDismissNextYearNote?: (anniversaryId: string, noteId: string) => void | Promise<void>;
};

const quickMonthDays = ["01-01", "05-14", "10-01", "12-31"];
const anniversaryTypeOptions = [
  { value: "project-milestone", label: "项目里程碑" },
  { value: "growth", label: "成长" },
  { value: "relationship", label: "关系" },
  { value: "gratitude", label: "感恩" },
  { value: "self-reminder", label: "自我提醒" }
];
const nextYearNoteStatusLabels: Record<JournalNextYearNoteStatus, string> = {
  pending: "待处理",
  adopted: "已采纳",
  dismissed: "已忽略"
};
const timelineCardTextMaxLength = 30;
const timelineCardPreviewLineLimit = 3;

function getLocalTodayIsoDate() {
  const today = new Date();
  const year = today.getFullYear();
  const month = String(today.getMonth() + 1).padStart(2, "0");
  const day = String(today.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function isNextYearNoteDue(targetDate: string) {
  return targetDate <= getLocalTodayIsoDate();
}

function isValidDateInputValue(value: string, monthDay: string) {
  const parsed = new Date(`${value}T00:00:00`);
  return !Number.isNaN(parsed.getTime()) && value.slice(5) === monthDay;
}

function toDateInputValue(monthDay: string) {
  if (!/^\d{2}-\d{2}$/.test(monthDay)) {
    return "";
  }

  const currentYear = new Date().getFullYear();
  const currentYearValue = `${currentYear}-${monthDay}`;
  if (isValidDateInputValue(currentYearValue, monthDay)) {
    return currentYearValue;
  }

  for (let offset = 1; offset <= 4; offset += 1) {
    const previousYearValue = `${currentYear - offset}-${monthDay}`;
    if (isValidDateInputValue(previousYearValue, monthDay)) {
      return previousYearValue;
    }

    const nextYearValue = `${currentYear + offset}-${monthDay}`;
    if (isValidDateInputValue(nextYearValue, monthDay)) {
      return nextYearValue;
    }
  }

  return "";
}

function toMonthDay(dateValue: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateValue);
  return match ? `${match[2]}-${match[3]}` : "";
}

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

function getLocalErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "操作失败";
}

function getAnniversaryTypeValue(value: string | null | undefined): string {
  if (value && anniversaryTypeOptions.some(option => option.value === value)) {
    return value;
  }

  return "self-reminder";
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

function getCardPreview(item: JournalHistoryEntrySummary) {
  const title = item.cardPreview?.title ?? item.hits[0]?.title ?? item.mood ?? "日记";
  const lines = item.cardPreview?.lines?.length ? item.cardPreview.lines : [firstLine(item)];
  return { title, lines };
}

function truncateTimelineCardText(value: string) {
  const normalized = value.trim();
  return normalized.length > timelineCardTextMaxLength
    ? `${normalized.slice(0, timelineCardTextMaxLength)}...`
    : normalized;
}

function getTimelineCardPreview(item: JournalHistoryEntrySummary) {
  const preview = getCardPreview(item);
  return {
    title: truncateTimelineCardText(preview.title),
    lines: preview.lines
      .slice(0, timelineCardPreviewLineLimit)
      .map(line => truncateTimelineCardText(line))
  };
}

function isRealDate(year: number, monthDay: string) {
  const month = Number(monthDay.slice(0, 2));
  const day = Number(monthDay.slice(3));
  if (!Number.isInteger(month) || !Number.isInteger(day)) {
    return false;
  }

  return month >= 1
    && month <= 12
    && day >= 1
    && day <= new Date(year, month, 0).getDate();
}

function getAnchorYear(items: JournalHistoryEntrySummary[]) {
  const firstYear = Number(items[0]?.date.year);
  return Number.isInteger(firstYear) ? firstYear : new Date().getFullYear();
}

function getOriginYear(anniversary: JournalAnniversaryItem | null) {
  const value = anniversary?.originDate?.slice(0, 4);
  const year = value ? Number(value) : Number.NaN;
  return Number.isInteger(year) ? year : null;
}

function getRelativeYearLabel(
  year: number,
  anchorYear: number,
  hasEntry: boolean,
  isOrigin: boolean,
  isValidCalendarDate: boolean
) {
  if (!isValidCalendarDate) {
    return "无此日";
  }

  if (isOrigin) {
    return "起点";
  }

  if (year === anchorYear) {
    return "今年";
  }

  if (year === anchorYear - 1) {
    return "去年";
  }

  return hasEntry ? "有记录" : "无记录";
}

function getTimelineEntryId(year: number, monthDay: string) {
  return `memory-entry-${year}-${monthDay}`;
}

export function AnniversaryWheelWorkbench({
  isBusy,
  monthDay,
  result,
  isAnniversarySaving = false,
  isNextYearNoteSaving = false,
  anniversaries,
  anniversaryError,
  selectedDate,
  detail,
  versions,
  error,
  onBack,
  onRefresh,
  onMonthDayChange,
  onSelectDate,
  onSaveAnniversary,
  onAddNextYearNote,
  onAdoptNextYearNote,
  onDismissNextYearNote
}: AnniversaryWheelWorkbenchProps) {
  const [isReading, setIsReading] = useState(false);
  const [selectedAnniversaryId, setSelectedAnniversaryId] = useState<string | null>(null);
  const [anniversaryTitle, setAnniversaryTitle] = useState("");
  const [anniversaryType, setAnniversaryType] = useState("self-reminder");
  const [anniversaryOriginDate, setAnniversaryOriginDate] = useState("");
  const [anniversaryDescription, setAnniversaryDescription] = useState("");
  const [nextYearNoteText, setNextYearNoteText] = useState("");
  const [nextYearNoteError, setNextYearNoteError] = useState("");
  const items = result?.items ?? [];
  const selected = items.find(item => item.date.isoDate === selectedDate) ?? items[0] ?? null;
  const pinnedAnniversaries = useMemo(
    () => anniversaries.filter(item => item.pinned),
    [anniversaries]
  );
  const selectedAnniversaryById = anniversaries.find(item =>
    item.id === selectedAnniversaryId && item.monthDay === monthDay
  ) ?? null;
  const selectedAnniversary = selectedAnniversaryById
    ?? pinnedAnniversaries.find(item => item.monthDay === monthDay)
    ?? anniversaries.find(item => item.monthDay === monthDay)
    ?? null;
  const matchingDetail = detail?.date.isoDate === selected?.date.isoDate ? detail : null;
  const matchingVersions = selected
    ? versions.filter(version => version.date.isoDate === selected.date.isoDate)
    : [];
  const currentDetailMarkdown = matchingDetail?.markdown?.trim() ?? "";
  const expectsEntryDetail = selected !== null && selected.status !== "missing";
  const isEntryDetailLoading = expectsEntryDetail && matchingDetail === null;
  const dateInputValue = toDateInputValue(monthDay);
  const anchorYear = getAnchorYear(items);
  const originYear = getOriginYear(selectedAnniversary);
  const itemsByYear = useMemo(
    () => new Map(items.map(item => [Number(item.date.year), item])),
    [items]
  );
  const timelineEntries = useMemo(
    () => Array.from({ length: 10 }, (_, index) => {
      const year = anchorYear - index;
      const item = itemsByYear.get(year) ?? null;
      const isOrigin = originYear === year;
      const isValidCalendarDate = isRealDate(year, monthDay);
      return {
        year,
        item,
        id: getTimelineEntryId(year, monthDay),
        side: index % 2 === 0 ? "is-left" : "is-right",
        isOrigin,
        isRealDate: isValidCalendarDate,
        label: getRelativeYearLabel(year, anchorYear, item !== null, isOrigin, isValidCalendarDate)
      };
    }),
    [anchorYear, itemsByYear, monthDay, originYear]
  );
  const meaningObservation = useMemo(() => {
    const entryCount = items.length;
    const originText = originYear ? `起点 ${originYear}` : "尚未设置起点";
    const description = selectedAnniversary?.description?.trim();
    const base = `${entryCount} 年记录，${originText}。`;
    return description ? `${base}${description}` : base;
  }, [items.length, originYear, selectedAnniversary?.description]);

  useEffect(() => {
    setAnniversaryTitle(selectedAnniversary?.title ?? "");
    setAnniversaryType(getAnniversaryTypeValue(selectedAnniversary?.type));
    setAnniversaryOriginDate(selectedAnniversary?.originDate ?? selected?.date.isoDate ?? "");
    setAnniversaryDescription(selectedAnniversary?.description ?? "");
    setNextYearNoteError("");
  }, [selectedAnniversary?.id, monthDay]);

  useEffect(() => {
    setNextYearNoteText("");
  }, [selectedAnniversary?.id, monthDay]);

  function openReading(date: string) {
    setIsReading(true);
    onSelectDate(date);
  }

  function returnToTimeline() {
    setIsReading(false);
  }

  function selectYear(date: string) {
    if (isReading) {
      setIsReading(false);
    }
    onSelectDate(date);
  }

  function jumpToTimelineEntry(entry: (typeof timelineEntries)[number]) {
    const node = document.getElementById(entry.id);
    if (typeof node?.scrollIntoView === "function") {
      node.scrollIntoView({ block: "center", behavior: "smooth" });
    }
    if (entry.item) {
      selectYear(entry.item.date.isoDate);
    }
  }

  function changeMonthDay(nextMonthDay: string, anniversaryId: string | null = null) {
    setSelectedAnniversaryId(anniversaryId);
    setIsReading(false);
    onMonthDayChange(nextMonthDay);
  }

  function handleSaveAnniversary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isAnniversarySaving) {
      return;
    }

    const request: JournalAnniversarySaveRequest = {
      monthDay,
      type: getAnniversaryTypeValue(anniversaryType),
      title: anniversaryTitle.trim(),
      description: anniversaryDescription.trim(),
      originDate: anniversaryOriginDate || selected?.date.isoDate || null,
      pinned: true
    };
    void onSaveAnniversary(selectedAnniversary?.id ?? null, request);
  }

  async function handleSaveNextYearNote(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isNextYearNoteSaving) {
      return;
    }

    const text = nextYearNoteText.trim();
    if (!selectedAnniversary || !text) {
      return;
    }

    setNextYearNoteError("");
    try {
      await onAddNextYearNote(selectedAnniversary.id, text);
      setNextYearNoteText("");
    } catch (caught) {
      setNextYearNoteError(getLocalErrorMessage(caught));
    }
  }

  async function handleAdoptNextYearNote(noteId: string) {
    if (!selectedAnniversary || isNextYearNoteSaving || !onAdoptNextYearNote) {
      return;
    }

    setNextYearNoteError("");
    try {
      await onAdoptNextYearNote(selectedAnniversary.id, noteId);
    } catch (caught) {
      setNextYearNoteError(getLocalErrorMessage(caught));
    }
  }

  async function handleDismissNextYearNote(noteId: string) {
    if (!selectedAnniversary || isNextYearNoteSaving || !onDismissNextYearNote) {
      return;
    }

    setNextYearNoteError("");
    try {
      await onDismissNextYearNote(selectedAnniversary.id, noteId);
    } catch (caught) {
      setNextYearNoteError(getLocalErrorMessage(caught));
    }
  }

  return (
    <>
      <aside className="context-rail history-rail anniversary-rail" aria-label="同日年轮日期">
        <section className="date-card history-date-card">
          <p className="month">Anniversary</p>
          <h1>{monthDay}<span>同日年轮</span></h1>
        </section>

        <section className="rail-section">
          <DatePickerField
            label="同日日期"
            ariaLabel="选择同日年轮日期"
            value={dateInputValue}
            onChange={value => {
              const nextMonthDay = toMonthDay(value);
              if (nextMonthDay) {
                changeMonthDay(nextMonthDay);
              }
            }}
          />
          <div className="anniversary-quick-days" aria-label="快捷日期">
            {quickMonthDays.map(day => (
              <button
                key={day}
                type="button"
                className={monthDay === day ? "active" : ""}
                onClick={() => changeMonthDay(day)}
              >
                {day}
              </button>
            ))}
          </div>
        </section>

        <section className="rail-section">
          <div className="section-head">
            <h2>常看日期</h2>
            <span>{pinnedAnniversaries.length} 个</span>
          </div>
          <div className="history-result-list anniversary-saved-list" aria-label="常看纪念日列表">
            {pinnedAnniversaries.length > 0 ? pinnedAnniversaries.map(item => (
              <button
                key={item.id}
                type="button"
                className={`source-item history-result anniversary-saved ${item.monthDay === monthDay ? "is-active" : ""}`}
                onClick={() => changeMonthDay(item.monthDay, item.id)}
                aria-pressed={selectedAnniversary?.id === item.id}
              >
                <span className="source-meta">
                  <span>{item.monthDay}</span>
                  <span>{item.type}</span>
                </span>
                <strong>{item.title}</strong>
                {item.description ? <p>{item.description}</p> : null}
              </button>
            )) : (
              <p className="muted">还没有保存常看日期。</p>
            )}
          </div>
        </section>

        <section className="rail-section">
          <div className="section-head">
            <h2>年份</h2>
            <span>{timelineEntries.length} 年</span>
          </div>
          <div className="anniversary-year-node-list" aria-label="同日年份列表">
            {timelineEntries.map(entry => (
              <button
                key={entry.id}
                type="button"
                className={`anniversary-year-node ${entry.item ? "has-entry" : "is-empty"} ${entry.isOrigin ? "is-origin" : ""} ${selected?.date.year === String(entry.year) ? "is-active" : ""}`}
                onClick={() => jumpToTimelineEntry(entry)}
                aria-controls={entry.id}
                aria-pressed={selected?.date.year === String(entry.year)}
                aria-current={selected?.date.year === String(entry.year) ? "true" : undefined}
              >
                <i className="anniversary-year-node-dot" aria-hidden="true" />
                <strong>{entry.year}</strong>
                <span>{entry.label}</span>
              </button>
            ))}
          </div>
        </section>
      </aside>

      <section className="journal-stage history-stage anniversary-stage" aria-label="同日年轮预览">
        <div className="stage-toolbar">
          <div className="stage-title">
            <p>{isReading ? "同日阅读" : "同日年轮"}</p>
            <h2>{isReading && selected ? selected.date.isoDate : `${monthDay} 的历年回声`}</h2>
          </div>
          <div className="history-stage-actions">
            {isReading ? (
              <button type="button" className="secondary-action secondary" onClick={returnToTimeline}>
                <ArrowLeft size={15} aria-hidden="true" />
                返回年轮
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
              isReading ? (
                <>
                  <header className="history-document-head">
                    <p className="kicker">Reading</p>
                    <h1>{selected.date.isoDate}</h1>
                    <p>
                      <span>{getStatusLabel(matchingDetail?.status ?? selected.status)}</span>
                      <span>{selected.rawInputCount} 条材料</span>
                      <span>{matchingVersions.length || selected.versionCount} 个版本</span>
                      {selected.entryUpdatedAt ? <span>最后写入 {formatHistoryTime(selected.entryUpdatedAt)}</span> : null}
                    </p>
                  </header>
                  {isEntryDetailLoading ? (
                    <JournalPaperLoading label="同日当前日记读取中" />
                  ) : currentDetailMarkdown ? (
                    <section className="anniversary-reading-paper" aria-label="同日当前日记内容">
                      <MarkdownPreview markdown={currentDetailMarkdown} />
                    </section>
                  ) : (
                    <section className="empty-paper audit-empty-state">
                      <h2>没有可阅读的当前日记</h2>
                      <p>这一天可能只有原始材料或记录缺失。</p>
                    </section>
                  )}
                </>
              ) : (
                <>
                  <header className="history-document-head">
                    <p className="kicker">Anniversary Wheel</p>
                    <h1>{monthDay}</h1>
                    <p>
                      <span>{items.length} 年记录</span>
                      <span>{pinnedAnniversaries.length} 个常看日期</span>
                    </p>
                  </header>
                  <div className="memory-corridor-timeline" aria-label="同日年轮时间线">
                    <div className="memory-corridor-spine" aria-hidden="true" />
                    {timelineEntries.map(entry => {
                      const item = entry.item;
                      const preview = item ? getTimelineCardPreview(item) : null;
                      return (
                        <section
                          id={entry.id}
                          className={`memory-entry ${entry.side} ${entry.isOrigin ? "is-origin" : ""} ${item ? "" : "is-empty"}`}
                          data-year={entry.year}
                          key={entry.id}
                        >
                          <div className="year-pin">
                            <strong>{entry.year}</strong>
                            <span>{entry.label}</span>
                            <i aria-hidden="true" />
                          </div>
                          <article
                            className={`memory-card ${item && selected?.date.isoDate === item.date.isoDate ? "is-active" : ""}`}
                          >
                            <div className="memory-card-head">
                              <div className="memory-title">
                                <span className="memory-meta">{entry.year} · {entry.label}</span>
                                <h3>{preview?.title ?? (entry.isRealDate ? `这一年没有留下 ${monthDay}` : `${monthDay} 在这一年不存在`)}</h3>
                              </div>
                              <div className="card-actions">
                                <span className="count-pill">
                                  {item ? `${item.rawInputCount} 条材料` : "无记录"}
                                </span>
                                {item ? (
                                  <button
                                    type="button"
                                    className="eye-button"
                                    aria-label={`阅读 ${item.date.isoDate} 日记`}
                                    title="阅读日记"
                                    onClick={() => openReading(item.date.isoDate)}
                                  >
                                    <Eye size={15} aria-hidden="true" />
                                  </button>
                                ) : null}
                              </div>
                            </div>
                            {item && preview ? (
                              <>
                                <div className="anniversary-card-preview">
                                  {preview.lines.map(line => <p key={`${item.date.isoDate}-${line}`}>{line}</p>)}
                                </div>
                                {item.entryUpdatedAt ? (
                                  <span className="quote-line">最后写入 {formatHistoryTime(item.entryUpdatedAt)}</span>
                                ) : null}
                              </>
                            ) : (
                              <p>{entry.isRealDate ? "空白也是时间的一部分，它让记录习惯的形成过程变得可见。" : "这个月日只在真实存在的年份里承载记录。"}</p>
                            )}
                          </article>
                        </section>
                      );
                    })}
                  </div>
                </>
              )
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
            <p className="assistant-eyebrow">Anniversary</p>
            <h2>纪念日资料</h2>
          </div>
          <span className="assistant-time">{monthDay}</span>
        </div>

        <div className="assistant-body">
          {anniversaryError ? <p className="api-error history-error" role="alert">{anniversaryError}</p> : null}
          {nextYearNoteError ? <p className="api-error history-error" role="alert">{nextYearNoteError}</p> : null}

          <section className="assistant-card anniversary-meaning-card">
            <div className="assistant-card-head">
              <h3>意义观察</h3>
              <span>{items.length} 年记录</span>
            </div>
            <p>{meaningObservation}</p>
            <div className="anniversary-source-years" aria-label="相关年份">
              {timelineEntries
                .filter(entry => entry.item || entry.isOrigin)
                .slice(0, 5)
                .map(entry => (
                  <span
                    key={entry.id}
                    className={entry.isOrigin ? "is-origin" : ""}
                  >
                    {entry.year}
                  </span>
                ))}
            </div>
          </section>

          <form className="assistant-card anniversary-form" onSubmit={handleSaveAnniversary}>
            <label>
              <span>纪念日名称</span>
              <input
                value={anniversaryTitle}
                onChange={event => setAnniversaryTitle(event.target.value)}
                placeholder="给这一天起个名字"
              />
            </label>
            <label>
              <span>纪念日类型</span>
              <select value={anniversaryType} onChange={event => setAnniversaryType(event.target.value)}>
                {anniversaryTypeOptions.map(option => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>
              <span>起点日期</span>
              <input
                type="date"
                value={anniversaryOriginDate}
                onChange={event => setAnniversaryOriginDate(event.target.value)}
              />
            </label>
            <label>
              <span>说明</span>
              <textarea
                rows={3}
                value={anniversaryDescription}
                onChange={event => setAnniversaryDescription(event.target.value)}
                placeholder="这一天为什么值得常看"
              />
            </label>
            <button
              type="submit"
              className="primary-action"
              disabled={isBusy || isAnniversarySaving || !anniversaryTitle.trim()}
            >
              保存纪念日
            </button>
          </form>

          <form className="assistant-card anniversary-form" onSubmit={handleSaveNextYearNote}>
            <label>
              <span>写给下一年同一天</span>
              <textarea
                rows={3}
                value={nextYearNoteText}
                onChange={event => setNextYearNoteText(event.target.value)}
                placeholder="给明年的自己留一句提醒"
              />
            </label>
            <button
              type="submit"
              className="secondary-action"
              disabled={isBusy || isNextYearNoteSaving || !selectedAnniversary || !nextYearNoteText.trim()}
            >
              保存下一年提醒
            </button>
          </form>

          {selectedAnniversary?.nextYearNotes.length ? (
            <section className="assistant-card">
              <div className="assistant-card-head">
                <h3>下一年提醒</h3>
                <span>{selectedAnniversary.nextYearNotes.length} 条</span>
              </div>
              {selectedAnniversary.nextYearNotes.map(note => (
                <div className="anniversary-next-year-note" key={note.id}>
                  <p>{note.text}</p>
                  <span className="source-meta">{nextYearNoteStatusLabels[note.status]}</span>
                  {note.status === "pending" ? (
                    <div className="anniversary-note-actions">
                      {isNextYearNoteDue(note.targetDate) ? (
                        <button
                          type="button"
                          className="assistant-inline-action"
                          onClick={() => void handleAdoptNextYearNote(note.id)}
                          disabled={isBusy || isNextYearNoteSaving}
                        >
                          采纳提醒
                        </button>
                      ) : null}
                      <button
                        type="button"
                        className="assistant-inline-action"
                        onClick={() => void handleDismissNextYearNote(note.id)}
                        disabled={isBusy || isNextYearNoteSaving}
                      >
                        忽略提醒
                      </button>
                    </div>
                  ) : null}
                </div>
              ))}
            </section>
          ) : null}

        </div>
      </aside>
    </>
  );
}
