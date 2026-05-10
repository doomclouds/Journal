import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import {
  activateAiSettings,
  addTodayInput,
  confirmTodayDraft,
  getAiSettings,
  getHealth,
  getTodayEditor,
  regenerateTodayDraft,
  revealAiProviderApiKey,
  saveBlockDraft,
  testAiProvider,
  type AiSettingsActivationResult,
  type AiSettingsSaveRequest,
  type AiProviderHealthResult,
  type AiSettingsView,
  type JournalBlockEditSection,
  type HealthResponse,
  type TodayEditorState
} from "./api";
import { JournalEditor } from "./JournalEditor";
import { LlmSettingsPanel } from "./LlmSettingsPanel";
import { getProductJournalStatus, type ProductJournalStatusView } from "./todayWorkbenchView";
import "./styles.css";

type LoadState = "loading" | "ready" | "error";

function getErrorMessage(caught: unknown) {
  return caught instanceof Error ? caught.message : "unknown error";
}

function formatRawInputTime(value: string) {
  const time = value.match(/T(\d{2}:\d{2})/);
  return time?.[1] ?? value;
}

const localUnsavedChangeMessage = "先保存或取消当前编辑，再继续补充或重新整理。";

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
  const [pendingRegenerateDraft, setPendingRegenerateDraft] = useState(false);
  const [hasLocalUnsavedChanges, setHasLocalUnsavedChanges] = useState(false);

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

  useEffect(() => {
    setHasLocalUnsavedChanges(false);
  }, [editor]);

  useEffect(() => {
    if (!hasLocalUnsavedChanges && validationError === localUnsavedChangeMessage) {
      setValidationError("");
    }
  }, [hasLocalUnsavedChanges, validationError]);

  const today = editor?.today ?? null;

  const title = useMemo(() => {
    return today ? `${today.date.isoDate} 晨间日记` : "今日晨间日记";
  }, [today]);

  const canConfirm = Boolean(
    editor?.canConfirm
      && today?.draft
      && today.status !== "attention"
      && !hasLocalUnsavedChanges
  );
  const activeProvider = aiSettings?.providers.find(provider => provider.isActive);
  const activeProviderName = activeProvider?.displayName
    ?? (aiSettings?.activeProviderId ? aiSettings.activeProviderId : "Mock");
  const activeProviderStatus = activeProvider
    ? `${activeProvider.displayName} 可用`
    : aiSettings?.activeProviderId
      ? `${aiSettings.activeProviderId} 需要配置`
      : "Mock 可用";
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
  const productStatus: ProductJournalStatusView = editor
    ? getProductJournalStatus(editor, hasLocalUnsavedChanges)
    : {
        id: loadState === "error" ? "needs-attention" : "organizing",
        label: loadState === "error" ? "需要处理" : "整理中",
        tone: loadState === "error" ? "danger" : "neutral",
        nextStepTitle: loadState === "error" ? "检查连接状态" : "正在读取今天的状态",
        nextStepText: loadState === "error" ? "读取今日状态失败，请查看上方错误后重试。" : "正在加载今天的日记、草稿和整理配置。"
      };
  const titleDescription = inputCount > 0
    ? `已保留 ${inputCount} 条原始表达，当前整理稿可以继续确认或微调。`
    : "今天还未记录。先写一句自然语言，Journal 会保留原话。";
  const composeHint = hasLocalUnsavedChanges
    ? localUnsavedChangeMessage
    : pendingRegenerateDraft
      ? "这会覆盖当前草稿内容，但不会影响正式日记。"
      : "使用当前 LLM 重新整理当前草稿。";
  const structureStatusText = loadState === "loading"
    ? "正在读取今天的整理状态。"
    : editor?.validation.isValid
      ? "内容结构完整，可以保存。"
      : "草稿结构需要处理。";
  const saveTargetText = today?.entry
    ? "正式 Markdown 已更新。"
    : "保存后写入本地正式 Markdown。";

  function resetPendingRegenerateDraft() {
    setPendingRegenerateDraft(false);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    resetPendingRegenerateDraft();

    if (hasLocalUnsavedChanges) {
      setValidationError(localUnsavedChangeMessage);
      return;
    }

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
    resetPendingRegenerateDraft();
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
    resetPendingRegenerateDraft();
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

  async function handleActivateAiSettings(request: AiSettingsSaveRequest): Promise<AiSettingsActivationResult> {
    const settingsRequestId = settingsRequestIdRef.current + 1;
    settingsRequestIdRef.current = settingsRequestId;
    resetPendingRegenerateDraft();
    setIsSettingsSubmitting(true);
    try {
      const result = await activateAiSettings(request);
      if (settingsRequestId === settingsRequestIdRef.current) {
        setAiSettings(result.settings);
        setApiError("");
      }
      return result;
    } catch (caught) {
      if (settingsRequestId === settingsRequestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
      throw caught;
    } finally {
      if (settingsRequestId === settingsRequestIdRef.current) {
        setIsSettingsSubmitting(false);
      }
    }
  }

  async function handleTestAiProvider(
    providerId: string,
    candidate?: AiSettingsSaveRequest
  ): Promise<AiProviderHealthResult> {
    return await testAiProvider(providerId, candidate);
  }

  async function handleRevealAiProviderKey(providerId: string) {
    return await revealAiProviderApiKey(providerId);
  }

  async function handleRegenerateDraft(providerId?: string) {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    resetPendingRegenerateDraft();
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

  async function handleRegenerateCurrentDraft() {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    if (!pendingRegenerateDraft) {
      setPendingRegenerateDraft(true);
      return;
    }

    setPendingRegenerateDraft(false);
    await handleRegenerateDraft();
  }

  return (
    <main className="today-shell productized-today-shell">
      <header className="top-context">
        <div className="title-block productized-title-block">
          <span className="eyebrow">Journal</span>
          <h1>{title}</h1>
          <p>{titleDescription}</p>
        </div>
        <div className="status-strip" aria-label="今日状态">
          <span className={`status-pill product-status-${productStatus.tone}`}>{productStatus.label}</span>
          <span className="api-pill">API {health?.status ?? (loadState === "error" ? "error" : "checking")}</span>
          <button
            type="button"
            className="llm-status-pill"
            aria-label={`LLM ${activeProviderName}`}
            onClick={() => {
              resetPendingRegenerateDraft();
              setIsLlmPanelOpen(true);
            }}
          >
            {activeProviderStatus}
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

      <section className="workspace productized-workspace">
        <section className="journal-stage productized-journal-stage" aria-label="日记纸面">
          <article className="journal-paper productized-journal-paper">
            {loadState === "loading" ? <p className="empty-paper">正在读取今天的日记状态...</p> : null}
            {loadState === "error" && !hasEditableJournal ? (
              <section className="empty-paper productized-empty-paper">
                <h2>今天的状态暂时没读出来</h2>
                <p>右侧会显示错误信息，正式日记和原始表达不会被这里覆盖。</p>
              </section>
            ) : null}
            {hasEditableJournal && editor ? (
              <JournalEditor
                editor={editor}
                isBusy={isBusy}
                onSaveBlocks={handleSaveBlocks}
                onLocalInteraction={resetPendingRegenerateDraft}
                onDirtyChange={setHasLocalUnsavedChanges}
              />
            ) : null}
            {loadState !== "loading" && loadState !== "error" && !hasEditableJournal ? (
              <section className="empty-paper productized-empty-paper">
                <h2>今天先写一句</h2>
                <p>不用先想结构。写一段自然语言，Journal 会保留原话，再帮你整理成可确认的日记草稿。</p>
              </section>
            ) : null}
          </article>

          <section className="compose-bar" aria-label="底部输入和主操作">
            <form onSubmit={handleSubmit}>
              <label htmlFor="today-input">补充今天的自然语言输入</label>
              <textarea
                id="today-input"
                value={input}
                onChange={event => {
                  resetPendingRegenerateDraft();
                  setInput(event.target.value);
                }}
                placeholder={hasEditableJournal ? "补充一句今天的事，或者让 AI 基于当前材料重新整理这一版..." : "今天发生了什么？直接写原话..."}
                rows={3}
                disabled={isBusy}
              />
              <button type="submit" className="primary-action" disabled={isBusy || hasLocalUnsavedChanges}>
                生成草稿
              </button>
            </form>
            {hasEditableJournal ? (
              <div className="compose-secondary-actions">
                <p className="compose-hint">{composeHint}</p>
                <button type="button" className="secondary-action" onClick={handleRegenerateCurrentDraft} disabled={isBusy || hasLocalUnsavedChanges}>
                  重新整理
                </button>
              </div>
            ) : null}
            {canConfirm ? (
              <button type="button" className="primary-action" onClick={handleConfirm} disabled={isBusy}>
                保存日记
              </button>
            ) : null}
          </section>
        </section>

        <aside className="today-assistant" aria-label="今日助手">
          <section className={`assistant-panel assistant-panel-${productStatus.tone}`}>
            <div className="section-head">
              <h2>下一步</h2>
              <span>{productStatus.label}</span>
            </div>
            <strong>{productStatus.nextStepTitle}</strong>
            <p>{productStatus.nextStepText}</p>
          </section>

          <section className="assistant-panel">
            <div className="section-head">
              <h2>今日材料</h2>
              <span>{inputCount} 条</span>
            </div>
            {inputCount > 0 ? (
              <ol className="raw-list productized-raw-list">
                {today?.rawInputs.map(raw => (
                  <li key={raw.id}>
                    <strong>{formatRawInputTime(raw.createdAt)}</strong>
                    <p>{raw.text}</p>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="muted">还没有输入。这里之后会显示原始表达摘要。</p>
            )}
          </section>

          <section className="assistant-panel">
            <div className="section-head">
              <h2>整理状态</h2>
              <span>{activeProviderName}</span>
            </div>
            <dl className="today-status-list">
              <div>
                <dt>结构</dt>
                <dd>{structureStatusText}</dd>
              </div>
              <div>
                <dt>整理方式</dt>
                <dd>{activeProviderStatus}。</dd>
              </div>
              <div>
                <dt>保存目标</dt>
                <dd>{saveTargetText}</dd>
              </div>
            </dl>
          </section>

          {uniqueAttentionErrors.length > 0 ? (
            <section className="assistant-panel attention-panel productized-attention-panel" aria-label="需要处理">
              <div className="section-head">
                <h2>这篇草稿需要处理</h2>
                <span>需要处理</span>
              </div>
              <p>正式日记没有被覆盖，原始表达仍然保留。</p>
              <p>这通常不是你的输入丢了，而是整理结果没有通过结构检查。</p>
              <ul>
                {uniqueAttentionErrors.map(item => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </section>
          ) : null}

          {today?.entry ? (
            <section className="assistant-panel path-panel">
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
          onClose={() => {
            resetPendingRegenerateDraft();
            setIsLlmPanelOpen(false);
          }}
          onActivate={handleActivateAiSettings}
          onTest={handleTestAiProvider}
          onRevealApiKey={handleRevealAiProviderKey}
        />
      ) : null}
    </main>
  );
}
