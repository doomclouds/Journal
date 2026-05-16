import { ArrowLeft, BookOpen, RefreshCw } from "lucide-react";
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
            <span>{items.length} 年</span>
          </div>
          <div className="history-result-list anniversary-year-list" aria-label="同日年份列表">
            {items.length > 0 ? items.map(item => {
              const preview = getCardPreview(item);
              return (
                <button
                  key={item.date.isoDate}
                  type="button"
                  className={`source-item history-result anniversary-year ${selected?.date.isoDate === item.date.isoDate ? "is-active" : ""}`}
                  onClick={() => selectYear(item.date.isoDate)}
                  aria-pressed={selected?.date.isoDate === item.date.isoDate}
                >
                  <span className="source-meta">
                    <span>{item.date.year}</span>
                    <span>{getStatusLabel(item.status)}</span>
                  </span>
                  <strong>{preview.title}</strong>
                  <p>{preview.lines.join(" / ")}</p>
                </button>
              );
            }) : (
              <p className="muted">这一天还没有可回看的历史。</p>
            )}
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
                  <div className="anniversary-timeline" aria-label="同日年轮时间线">
                    {items.map(item => {
                      const preview = getCardPreview(item);
                      return (
                        <article className="anniversary-timeline-card" key={item.date.isoDate}>
                          <div className="anniversary-timeline-card-head">
                            <div>
                              <span>{item.date.year}</span>
                              <strong>{preview.title}</strong>
                            </div>
                            <span>{getStatusLabel(item.status)}</span>
                          </div>
                          <div className="anniversary-card-preview">
                            {preview.lines.map(line => <p key={`${item.date.isoDate}-${line}`}>{line}</p>)}
                          </div>
                          <button
                            type="button"
                            className="assistant-inline-action"
                            aria-label={`阅读 ${item.date.isoDate} 日记`}
                            onClick={() => openReading(item.date.isoDate)}
                          >
                            <BookOpen size={14} aria-hidden="true" />
                            阅读
                          </button>
                        </article>
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
