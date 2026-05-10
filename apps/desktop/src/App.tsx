import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import {
  addTodayInput,
  confirmTodayDraft,
  getAiSettings,
  getHealth,
  getTodayEditor,
  regenerateTodayDraft,
  saveAiSettings,
  saveBlockDraft,
  saveSourceDraft,
  testAiProvider,
  type AiProviderHealthResult,
  type AiSettingsView,
  type JournalBlockEditSection,
  type HealthResponse,
  type TodayEditorState
} from "./api";
import { JournalEditor } from "./JournalEditor";
import { LlmSettingsPanel } from "./LlmSettingsPanel";
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
  const settingsRequestIdRef = useRef(0);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [editor, setEditor] = useState<TodayEditorState | null>(null);
  const [aiSettings, setAiSettings] = useState<AiSettingsView | null>(null);
  const [isLlmPanelOpen, setIsLlmPanelOpen] = useState(false);
  const [input, setInput] = useState("");
  const [apiError, setApiError] = useState("");
  const [validationError, setValidationError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSettingsSubmitting, setIsSettingsSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;

    async function load() {
      try {
        const [healthResult, editorResult, aiSettingsResult] = await Promise.all([
          getHealth(),
          getTodayEditor(),
          getAiSettings()
        ]);
        if (!cancelled && requestId === requestIdRef.current) {
          setHealth(healthResult);
          setEditor(editorResult);
          setAiSettings(aiSettingsResult);
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

  const today = editor?.today ?? null;

  const title = useMemo(() => {
    return today ? `${today.date.isoDate} 晨间日记` : "今日晨间日记";
  }, [today]);

  const canConfirm = Boolean(editor?.canConfirm && today?.draft && today.status !== "attention");
  const statusLabel = today?.status ?? loadState;
  const activeProviderName = aiSettings?.providers.find(provider => provider.isActive)?.displayName
    ?? (aiSettings?.activeProviderId ? aiSettings.activeProviderId : "Mock");
  const inputCount = today?.rawInputs.length ?? 0;
  const isInitialLoading = loadState === "loading";
  const isBusy = isInitialLoading || isSubmitting;
  const attentionErrors = [
    ...(today?.errors ?? []),
    ...(today?.draft?.status === "attention" ? today.draft.errors : []),
    ...(editor?.validation.isValid === false ? editor.validation.issues.map(issue => issue.message) : [])
  ];
  const uniqueAttentionErrors = Array.from(new Set(attentionErrors));
  const hasEditableJournal = Boolean(editor && (editor.markdown.trim() || editor.sections.length > 0));

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
      await addTodayInput(trimmedInput);
      const next = await getTodayEditor();
      if (requestId === requestIdRef.current) {
        setEditor(next);
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
      await confirmTodayDraft();
      const next = await getTodayEditor();
      if (requestId === requestIdRef.current) {
        setEditor(next);
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

  async function handleSaveBlocks(sections: JournalBlockEditSection[]) {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setValidationError("");
    setIsSubmitting(true);
    try {
      const next = await saveBlockDraft(sections);
      if (requestId === requestIdRef.current) {
        setEditor(next);
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

  async function handleSaveSource(markdown: string) {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setValidationError("");
    setIsSubmitting(true);
    try {
      const next = await saveSourceDraft(markdown);
      if (requestId === requestIdRef.current) {
        setEditor(next);
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

  async function handleSaveAiSettings(request: Parameters<typeof saveAiSettings>[0]) {
    const settingsRequestId = settingsRequestIdRef.current + 1;
    settingsRequestIdRef.current = settingsRequestId;
    setIsSettingsSubmitting(true);
    try {
      const next = await saveAiSettings(request);
      if (settingsRequestId === settingsRequestIdRef.current) {
        setAiSettings(next);
        setApiError("");
      }
    } catch (caught) {
      if (settingsRequestId === settingsRequestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
    } finally {
      if (settingsRequestId === settingsRequestIdRef.current) {
        setIsSettingsSubmitting(false);
      }
    }
  }

  async function handleTestAiProvider(providerId: string): Promise<AiProviderHealthResult> {
    return await testAiProvider(providerId);
  }

  async function handleRegenerateDraft(providerId?: string) {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setValidationError("");
    setIsSubmitting(true);
    try {
      await regenerateTodayDraft(providerId);
      if (requestId === requestIdRef.current) {
        const nextEditor = await getTodayEditor();
        if (requestId !== requestIdRef.current) {
          return;
        }

        setEditor(nextEditor);
        setApiError("");
        setLoadState("ready");

        try {
          const nextAiSettings = await getAiSettings();
          if (requestId === requestIdRef.current) {
            setAiSettings(nextAiSettings);
          }
        } catch (caught) {
          if (requestId === requestIdRef.current) {
            setApiError(getErrorMessage(caught));
          }
        }
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
          <button type="button" className="llm-status-pill" aria-label={`LLM ${activeProviderName}`} onClick={() => setIsLlmPanelOpen(true)}>
            LLM {activeProviderName}
          </button>
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

        <section className="journal-stage" aria-label="JMF 日记编辑">
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
            {loadState === "error" && !hasEditableJournal ? <p className="empty-paper">还没有可编辑的 JMF 草稿</p> : null}
            {hasEditableJournal && editor ? (
              <JournalEditor
                editor={editor}
                isBusy={isBusy}
                onSaveBlocks={handleSaveBlocks}
                onSaveSource={handleSaveSource}
              />
            ) : null}
            {loadState !== "loading" && loadState !== "error" && !hasEditableJournal ? (
              <p className="empty-paper">还没有可编辑的 JMF 草稿</p>
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
                disabled={isBusy}
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
              <p>确认后更新当天正式 Markdown；当前版本不创建版本快照。</p>
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
      {isLlmPanelOpen && aiSettings ? (
        <LlmSettingsPanel
          settings={aiSettings}
          isBusy={isBusy || isSettingsSubmitting}
          onClose={() => setIsLlmPanelOpen(false)}
          onSave={handleSaveAiSettings}
          onTest={handleTestAiProvider}
          onRegenerate={handleRegenerateDraft}
        />
      ) : null}
    </main>
  );
}
