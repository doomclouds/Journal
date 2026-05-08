import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import {
  addTodayInput,
  confirmTodayDraft,
  getHealth,
  getToday,
  type HealthResponse,
  type TodayJournalState
} from "./api";
import { MarkdownPreview } from "./MarkdownPreview";
import "./styles.css";

type LoadState = "loading" | "ready" | "error";

function getErrorMessage(caught: unknown) {
  return caught instanceof Error ? caught.message : "unknown error";
}

function formatRawInputTime(value: string) {
  const time = value.match(/T(\d{2}:\d{2})/);
  return time?.[1] ?? value;
}

export default function App() {
  const requestIdRef = useRef(0);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [today, setToday] = useState<TodayJournalState | null>(null);
  const [input, setInput] = useState("");
  const [apiError, setApiError] = useState("");
  const [validationError, setValidationError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;

    async function load() {
      try {
        const [healthResult, todayResult] = await Promise.all([getHealth(), getToday()]);
        if (!cancelled && requestId === requestIdRef.current) {
          setHealth(healthResult);
          setToday(todayResult);
          setLoadState("ready");
          setApiError("");
        }
      } catch (caught) {
        if (!cancelled && requestId === requestIdRef.current) {
          setLoadState("error");
          setApiError(getErrorMessage(caught));
        }
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  const title = useMemo(() => {
    return today ? `${today.date.isoDate} 晨间日记` : "今日晨间日记";
  }, [today]);

  const canConfirm = today?.status === "reviewing" && today.draft !== null;
  const markdown = today?.draft?.markdown ?? today?.entry?.markdown ?? "";
  const statusLabel = today?.status ?? loadState;
  const inputCount = today?.rawInputs.length ?? 0;
  const isInitialLoading = loadState === "loading";
  const isBusy = isInitialLoading || isSubmitting;
  const attentionErrors = [
    ...(today?.errors ?? []),
    ...(today?.draft?.status === "attention" ? today.draft.errors : [])
  ];
  const uniqueAttentionErrors = Array.from(new Set(attentionErrors));

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedInput = input.trim();

    if (!trimmedInput) {
      setValidationError("请输入一段今天的自然语言内容。");
      return;
    }

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setValidationError("");
    setIsSubmitting(true);
    try {
      const next = await addTodayInput(trimmedInput);
      if (requestId === requestIdRef.current) {
        setToday(next);
        setInput("");
        setApiError("");
        setLoadState("ready");
      }
    } catch (caught) {
      if (requestId === requestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
    } finally {
      if (requestId === requestIdRef.current) {
        setIsSubmitting(false);
      }
    }
  }

  async function handleConfirm() {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setValidationError("");
    setIsSubmitting(true);
    try {
      const next = await confirmTodayDraft();
      if (requestId === requestIdRef.current) {
        setToday(next);
        setApiError("");
        setLoadState("ready");
      }
    } catch (caught) {
      if (requestId === requestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
    } finally {
      if (requestId === requestIdRef.current) {
        setIsSubmitting(false);
      }
    }
  }

  function focusInput() {
    document.getElementById("today-input")?.focus();
  }

  return (
    <main className="today-shell">
      <header className="top-context">
        <div className="title-block">
          <span className="eyebrow">Journal</span>
          <h1>{title}</h1>
        </div>
        <div className="status-strip" aria-label="运行状态">
          <span className={`status-pill status-${statusLabel}`}>{statusLabel}</span>
          <span className="api-pill">API {health?.status ?? (loadState === "error" ? "error" : "checking")}</span>
        </div>
      </header>

      {apiError ? (
        <p className="api-error" role="alert">
          {apiError}
        </p>
      ) : null}

      {validationError ? (
        <p className="validation-error" role="alert">
          {validationError}
        </p>
      ) : null}

      <section className="workspace">
        <aside className="context-rail" aria-label="今日上下文">
          <section className="rail-block date-block">
            <span className="rail-label">Today</span>
            <strong>{today?.date.monthDay ?? "--"}</strong>
            <p>{today?.date.markdownFileName ?? "读取中"}</p>
          </section>

          <section className="rail-block">
            <div className="section-head">
              <h2>Raw inputs</h2>
              <span>{inputCount} 条</span>
            </div>
            {inputCount > 0 ? (
              <ol className="raw-list">
                {today?.rawInputs.map(raw => (
                  <li key={raw.id}>
                    <strong>{formatRawInputTime(raw.createdAt)}</strong>
                    <p>{raw.text}</p>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="muted">今天还没有原始输入。</p>
            )}
          </section>
        </aside>

        <section className="journal-stage" aria-label="日记预览">
          <div className="compact-actions" aria-label="紧凑窗口操作">
            <button type="button" className="secondary-action" onClick={focusInput}>
              补充输入
            </button>
            {canConfirm ? (
              <button type="button" className="primary-action" onClick={handleConfirm} disabled={isBusy}>
                确认保存
              </button>
            ) : null}
          </div>

          <article className="journal-paper">
            {loadState === "loading" ? <p className="empty-paper">正在读取今天的日记状态...</p> : null}
            {loadState === "error" && !markdown ? <p className="empty-paper">还没有草稿</p> : null}
            {markdown ? <MarkdownPreview markdown={markdown} /> : null}
            {loadState !== "loading" && loadState !== "error" && !markdown ? (
              <p className="empty-paper">还没有草稿</p>
            ) : null}
          </article>
        </section>

        <aside className="input-dock" aria-label="输入与确认">
          <section className="dock-block input-block">
            <div className="section-head">
              <h2>补充今天</h2>
              <span>raw input</span>
            </div>
            <form onSubmit={handleSubmit}>
              <label htmlFor="today-input">补充今天的自然语言输入</label>
              <textarea
                id="today-input"
                value={input}
                onChange={event => setInput(event.target.value)}
                placeholder="例如：昨天把阶段 1 跑通了，今天准备做 JMF 主链路。"
                rows={8}
              />
              <button type="submit" className="primary-action" disabled={isBusy}>
                生成草稿
              </button>
            </form>
          </section>

          {uniqueAttentionErrors.length > 0 ? (
            <section className="dock-block attention-panel" aria-label="需要处理">
              <div className="section-head">
                <h2>需要处理</h2>
                <span>需处理</span>
              </div>
              <ul>
                {uniqueAttentionErrors.map(item => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </section>
          ) : null}

          {canConfirm ? (
            <section className="confirm-panel" aria-label="草稿确认">
              <strong>草稿可以确认</strong>
              <p>确认后更新当天正式 Markdown；阶段 2 不创建版本快照。</p>
              <button type="button" className="primary-action" onClick={handleConfirm} disabled={isBusy}>
                确认写入正式日记
              </button>
            </section>
          ) : null}

          {today?.entry ? (
            <section className="dock-block path-panel">
              <div className="section-head">
                <h2>正式文件</h2>
                <span>已写入</span>
              </div>
              <p>{today.entry.path}</p>
            </section>
          ) : null}
        </aside>
      </section>
    </main>
  );
}
