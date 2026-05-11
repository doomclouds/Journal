import { FormEvent, useEffect, useRef, useState } from "react";
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
import {
  getAssistantSummary,
  getProductJournalStatus,
  getRawInputPreview,
  getSectionDisplayTitle,
  getStaticAiStyleLabel,
  type ProductJournalStatusView
} from "./todayWorkbenchView";
import "./styles.css";

type LoadState = "loading" | "ready" | "error";
type NativeMenuCommand = "open-llm-settings";

declare global {
  interface Window {
    journalDesktop?: {
      platform?: string;
      onNativeMenuCommand?: (handler: (command: NativeMenuCommand) => void) => () => void;
    };
  }
}

function getErrorMessage(caught: unknown) {
  return caught instanceof Error ? caught.message : "unknown error";
}

function formatRawInputTime(value: string) {
  const time = value.match(/T(\d{2}:\d{2})/);
  return time?.[1] ?? value;
}

function getRawInputTags(text: string): string[] {
  const matches = text.match(/#[^\s#，。,.；;！!？?、]+/g) ?? [];
  return Array.from(new Set(matches));
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
  const [workbenchView, setWorkbenchView] = useState<"journal" | "assistant">("assistant");

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

  useEffect(() => {
    return window.journalDesktop?.onNativeMenuCommand?.(command => {
      if (command === "open-llm-settings") {
        resetPendingRegenerateDraft();
        setIsLlmPanelOpen(true);
      }
    });
  }, []);

  const today = editor?.today ?? null;

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
  const editableSectionCount = editor?.sections.filter(section => section.isEditableInBlockMode).length ?? 0;
  const assistantSummary = getAssistantSummary({
    rawInputCount: inputCount,
    editableSectionCount,
    dirtySectionCount: hasLocalUnsavedChanges ? 1 : 0
  });
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
  const monthLabel = today
    ? new Date(`${today.date.isoDate}T00:00:00`).toLocaleString("en-US", { month: "short" })
    : "Today";
  const dayLabel = today?.date.isoDate.slice(-2) ?? "--";
  const dateValue = today ? new Date(`${today.date.isoDate}T00:00:00`) : null;
  const weekdayLabel = dateValue?.toLocaleDateString("zh-CN", { weekday: "long" }) ?? "今天";
  const zhDateLabel = dateValue
    ? dateValue.toLocaleDateString("zh-CN", { year: "numeric", month: "long", day: "numeric" })
    : "今天";
  const latestRawInput = today?.rawInputs[today.rawInputs.length - 1];
  const latestRawInputTime = latestRawInput?.createdAt
    ? formatRawInputTime(latestRawInput.createdAt)
    : "--:--";
  const sectionTargets = editor?.sections
    .filter(section => section.id !== "raw-inputs")
    .map(section => getSectionDisplayTitle(section.id, section.title)) ?? [];
  const rawInputViews = today?.rawInputs.map((raw, index) => ({
    raw,
    tags: getRawInputTags(raw.text),
    target: sectionTargets[index % Math.max(sectionTargets.length, 1)] ?? "今日材料"
  })) ?? [];
  const todayTags = Array.from(new Set(rawInputViews.flatMap(item => item.tags)));
  const visibleTodayTags = todayTags.length > 0 ? todayTags : ["#今日材料"];
  const documentTitle = hasEditableJournal ? "把今天收好" : "今天先写一句";
  const documentSubtitle = inputCount > 0
    ? "今天的原始表达已经保留。先确认日记段落，再把它保存成本地 Markdown。"
    : "不用先想结构。写一段自然语言，Journal 会保留原话，再帮你整理成可确认的日记草稿。";
  const dateStatusText = productStatus.id === "ready-to-save"
    ? "AI 整理稿待确认"
    : productStatus.label;

  function resetPendingRegenerateDraft() {
    setPendingRegenerateDraft(false);
  }

  function focusWorkbenchTarget(selector: string) {
    requestAnimationFrame(() => {
      document.querySelector<HTMLElement>(selector)?.focus();
    });
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
    <main className="desktop-shell" aria-label="Journal 今日工作台">
      <header className="top-context command-top-context">
        <div className="brand">
          <strong>Journal</strong>
          <span>本地优先晨间日记</span>
        </div>
        <div className="today-line">
          <span className="status-dot" aria-hidden="true"></span>
          <span><strong>{zhDateLabel}</strong> · {weekdayLabel} · {inputCount > 0 ? "原始表达已保留" : "等待第一句原始表达"}</span>
        </div>
        <div className="status-pills" aria-label="今日状态">
          <span className={`pill product-status-${productStatus.tone}`}>{productStatus.label}</span>
          <span className="pill neutral">API {health?.status ?? (loadState === "error" ? "error" : "checking")}</span>
          <button
            type="button"
            className="pill neutral llm-status-pill"
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

      <section className="feedback-row" aria-label="提示信息">
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
      </section>

      <section className={`workspace command-workspace ${workbenchView === "journal" ? "journal-only" : ""}`}>
        <aside className="context-rail" aria-label="今日上下文">
          <section className="date-card">
            <p className="month">{monthLabel}</p>
            <h1>{dayLabel}<span>{weekdayLabel} · 晨间日记</span></h1>
            <div className="date-status-row">
              <span className={`pill date-status-pill product-status-${productStatus.tone}`}>{dateStatusText}</span>
            </div>
          </section>

          <section className="rail-section">
            <div className="section-head">
              <h2>原始对话</h2>
              <span>{inputCount} 条</span>
            </div>
            <div className="raw-stack">
              {rawInputViews.length > 0 ? rawInputViews.map(({ raw, tags, target }, index) => (
                <details className="raw-fold" key={raw.id} open={index === 0}>
                  <summary>
                    <span>
                      <span className="raw-time">{formatRawInputTime(raw.createdAt)}</span>
                      <span className="raw-title">{getRawInputPreview(raw.text, 28)}</span>
                      {tags.length > 0 ? (
                        <span className="raw-tags">
                          {tags.map(tag => <span key={`${raw.id}-${tag}`}>{tag}</span>)}
                        </span>
                      ) : null}
                    </span>
                  </summary>
                  <div className="raw-body">
                    {raw.text}
                    <div className="raw-map">
                      <span>已用于：{target}</span>
                      {tags[0] ? <span>已提取：{tags[0].replace(/^#/, "")}</span> : null}
                    </div>
                  </div>
                </details>
              )) : (
                <p className="muted">还没有原始对话。先在下方写一句今天的事。</p>
              )}
            </div>
          </section>

          <section className="rail-section">
            <div className="section-head">
              <h2>下一步</h2>
              <span>{productStatus.label}</span>
            </div>
            <div className="next-panel">
              <strong>{productStatus.nextStepTitle}</strong>
              <p>{productStatus.nextStepText}</p>
            </div>
          </section>
        </aside>

        <section className="journal-stage productized-journal-stage" aria-label="日记纸面" tabIndex={-1}>
          <div className="stage-toolbar">
            <div className="stage-title">
              <p>日记纸面</p>
              <h2>默认阅读，点击段落才编辑</h2>
            </div>
            <div className="view-switch" aria-label="视图切换">
              <button
                type="button"
                aria-pressed={workbenchView === "journal"}
                onClick={() => setWorkbenchView("journal")}
              >
                只看日记
              </button>
              <button
                type="button"
                aria-pressed={workbenchView === "assistant"}
                onClick={() => setWorkbenchView("assistant")}
              >
                日记 + 助手
              </button>
            </div>
          </div>

          <div className="document-scroll">
            <article className="journal-paper document">
              <header className="document-header">
                <p className="kicker">Morning Journal</p>
                <h1>{documentTitle}</h1>
                <p className="subtitle">{documentSubtitle}</p>
              </header>

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
          </div>

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
                placeholder={hasEditableJournal ? "继续写一句今天的事，Journal 会保留原话，再更新草稿..." : "今天发生了什么？直接写原话..."}
                rows={3}
                disabled={isBusy}
              />
              <button type="submit" className="primary-action primary" disabled={isBusy || hasLocalUnsavedChanges}>
                生成草稿
              </button>
            </form>
            {hasEditableJournal ? (
              <div className="compose-secondary-actions">
                <p className="compose-hint">{composeHint}</p>
                <button type="button" className="secondary-action secondary" onClick={handleRegenerateCurrentDraft} disabled={isBusy || hasLocalUnsavedChanges}>
                  重新整理
                </button>
              </div>
            ) : null}
            {canConfirm ? (
              <button type="button" className="primary-action primary" onClick={handleConfirm} disabled={isBusy}>
                保存日记
              </button>
            ) : null}
          </section>
        </section>

        {workbenchView === "assistant" ? (
        <aside className="assistant-panel today-assistant" aria-label="今日助手">
          <div className="assistant-head">
            <div>
              <p className="assistant-eyebrow">Today Assistant</p>
              <h2>把今天收好</h2>
              <div className="assistant-meta">
                <span>{productStatus.label}</span>
                {visibleTodayTags.slice(0, 2).map(tag => <span key={`assistant-meta-${tag}`}>{tag}</span>)}
                <span>{activeProviderName}</span>
              </div>
            </div>
            <span className="assistant-time">{latestRawInputTime}</span>
          </div>

          <div className="assistant-body">
            <section className={`assistant-card next-step-card assistant-card-${productStatus.tone}`}>
              <div className="next-step-title">
                <span className="status-dot" aria-hidden="true"></span>
                <div>
                  <strong>下一步：{productStatus.nextStepTitle}</strong>
                  <p>{productStatus.nextStepText}</p>
                </div>
              </div>
              <div className="next-actions">
                <button className="secondary" type="button" onClick={() => focusWorkbenchTarget(".journal-stage")} disabled={isBusy}>
                  回到日记
                </button>
              </div>
            </section>

            <section className="assistant-card">
              <div className="assistant-card-head">
                <h3>AI 整理</h3>
                <span>{loadState === "loading" ? "读取中" : "刚刚更新"}</span>
              </div>
              <p>从 {assistantSummary.rawInputCount} 条原始输入中整理出 {assistantSummary.sectionCount} 个日记段落，保留原话优先，轻度整理成可编辑的日记内容。</p>
              <div className="assistant-stat-grid" aria-label="AI 整理统计">
                <div className="assistant-stat">
                  <strong>{assistantSummary.rawInputCount}</strong>
                  <span>原始输入</span>
                </div>
                <div className="assistant-stat">
                  <strong>{assistantSummary.sectionCount}</strong>
                  <span>日记段落</span>
                </div>
                <div className="assistant-stat">
                  <strong>{assistantSummary.editedCount}</strong>
                  <span>手动编辑</span>
                </div>
              </div>
              <div className="tag-row" aria-label="今日标签">
                <strong>今日标签</strong>
                {visibleTodayTags.map(tag => <span key={`tag-${tag}`}>{tag}</span>)}
              </div>
              <div className="insight-tags" aria-label="识别主题">
                {sectionTargets.slice(0, 3).map(target => <span key={`insight-${target}`}>{target}</span>)}
              </div>
            </section>

            <section className="assistant-card">
              <div className="assistant-card-head">
                <h3>今日材料</h3>
                <span>{inputCount > 0 ? "可追溯" : "待输入"}</span>
              </div>
              {rawInputViews.length > 0 ? (
                <div className="material-list">
                  {rawInputViews.map(({ raw, tags, target }) => (
                    <div className="material-item" key={raw.id}>
                      <div className="material-meta">
                        <span>来源 {formatRawInputTime(raw.createdAt)}</span>
                        {tags[0] ? <span>{tags[0]}</span> : null}
                        <span>写入 {target}</span>
                      </div>
                      {getRawInputPreview(raw.text, 48)}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted">还没有输入。这里之后会显示原始表达摘要。</p>
              )}
            </section>

            <section className="assistant-card">
              <div className="assistant-card-head">
                <h3>整理状态</h3>
                <span>{activeProviderName}</span>
              </div>
              <dl className="today-status-list">
                <div>
                  <dt>结构</dt>
                  <dd>{structureStatusText}</dd>
                </div>
                <div>
                  <dt>整理方式</dt>
                  <dd>{activeProviderStatus} · {getStaticAiStyleLabel()}。</dd>
                </div>
                <div>
                  <dt>保存目标</dt>
                  <dd>{saveTargetText}</dd>
                </div>
              </dl>
            </section>

            <section className="assistant-card">
              <div className="assistant-card-head">
                <h3>快捷动作</h3>
                <span>命令层</span>
              </div>
              <div className="quick-actions">
                <button type="button" aria-label="快捷动作 插入段落" onClick={() => focusWorkbenchTarget(".insert-block-menu button")}>插入段落</button>
                <button type="button" aria-label="快捷动作 重新整理" onClick={handleRegenerateCurrentDraft} disabled={!hasEditableJournal || isBusy || hasLocalUnsavedChanges}>重新整理</button>
                <button type="button" aria-label="快捷动作 LLM 配置" onClick={() => setIsLlmPanelOpen(true)}>LLM 配置</button>
                <button type="button" aria-label="快捷动作 查看材料" onClick={() => focusWorkbenchTarget(".raw-fold summary")}>查看材料</button>
              </div>
            </section>

            {uniqueAttentionErrors.length > 0 ? (
              <section className="assistant-card attention-panel productized-attention-panel" aria-label="需要处理">
                <div className="assistant-card-head">
                  <h3>这篇草稿需要处理</h3>
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
              <section className="assistant-card path-panel">
                <div className="assistant-card-head">
                  <h3>正式文件</h3>
                  <span>已写入</span>
                </div>
                <p>{today.entry.path}</p>
              </section>
            ) : null}
          </div>
        </aside>
        ) : null}
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
