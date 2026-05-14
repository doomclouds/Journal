import { FormEvent, useEffect, useRef, useState } from "react";
import { History, RefreshCw, Save, SendHorizontal } from "lucide-react";
import {
  activateAiSettings,
  confirmTodayDraft,
  frontendBuildInfo,
  getAiSettings,
  getAppInfo,
  getHealth,
  initializeApiBaseUrlFromDesktop,
  getJournalAudit,
  getJournalAnniversaryWheel,
  getJournalHistory,
  getJournalHistoryEntry,
  getJournalHistoryVersion,
  getJournalHistoryVersions,
  getTodayEditor,
  openHarnessRunEvents,
  revealAiProviderApiKey,
  restoreJournalHistoryVersionDraft,
  saveBlockDraft,
  startAppendHarnessRun,
  startReorganizeHarnessRun,
  testAiProvider,
  type AppInfo,
  type AiSettingsActivationResult,
  type AiSettingsSaveRequest,
  type AiProviderHealthResult,
  type AiSettingsView,
  type JournalBlockEditSection,
  type JournalEntryVersion,
  type JournalHarnessAuditRun,
  type JournalHarnessRunEvent,
  type JournalAnniversaryWheelResult,
  type JournalHistoryEntryDetail,
  type JournalHistoryEntrySummary,
  type JournalVersionDetail,
  type HealthResponse,
  type StartHarnessRunResponse,
  type TodayEditorState
} from "./api";
import { AuditWorkbench } from "./AuditWorkbench";
import { AnniversaryWheelWorkbench } from "./AnniversaryWheelWorkbench";
import { HistoryWorkbench } from "./HistoryWorkbench";
import { JournalEditor } from "./JournalEditor";
import { LlmSettingsPanel } from "./LlmSettingsPanel";
import {
  getLocalServiceStatusLabel,
  type LocalServiceState
} from "./serviceStatus";
import {
  getAssistantSummary,
  getProductJournalStatus,
  getRawInputPreview,
  getSectionDisplayTitle,
  type ProductJournalStatusView
} from "./todayWorkbenchView";
import "./styles.css";

type LoadState = "loading" | "ready" | "error";
type NativeMenuCommand = "open-llm-settings" | "open-about";
const nativeMenuDomEventName = "journal:native-menu-command";

declare global {
  interface Window {
    journalDesktop?: {
      platform?: string;
      getLocalServiceStatus?: () => Promise<LocalServiceState>;
      getApiBaseUrl?: () => Promise<string | null>;
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
const terminalHarnessStatuses = new Set(["reviewing", "attention", "no-change", "failed", "interrupted"]);
const daysInMonthByNumber = new Map([
  [1, 31],
  [2, 29],
  [3, 31],
  [4, 30],
  [5, 31],
  [6, 30],
  [7, 31],
  [8, 31],
  [9, 30],
  [10, 31],
  [11, 30],
  [12, 31]
]);

function isValidAnniversaryMonthDay(monthDay: string) {
  const match = /^(\d{2})-(\d{2})$/.exec(monthDay);
  if (!match) {
    return false;
  }

  const month = Number(match[1]);
  const day = Number(match[2]);
  const maxDay = daysInMonthByNumber.get(month);
  return maxDay !== undefined && day >= 1 && day <= maxDay;
}

function isTerminalHarnessEvent(event: JournalHarnessRunEvent) {
  return terminalHarnessStatuses.has(event.status)
    || event.type === "run-completed"
    || event.type === "run-failed"
    || event.type === "run-already-completed"
    || event.type === "run-interrupted";
}

function isTrustedLocalServiceState(state: LocalServiceState | null) {
  return state?.status === "connected" || state?.status === "reused";
}

function getBlockedLocalServiceMessage(state: LocalServiceState | null) {
  if (state?.reason) {
    return state.reason;
  }

  return "本地服务启动失败，暂时无法读取今天的状态。";
}

export default function App() {
  const requestIdRef = useRef(0);
  const settingsRequestIdRef = useRef(0);
  const auditRequestIdRef = useRef(0);
  const historyRequestIdRef = useRef(0);
  const historyVersionRequestIdRef = useRef(0);
  const aboutRequestIdRef = useRef(0);
  const aboutInFlightRef = useRef<Promise<AppInfo> | null>(null);
  const aboutCloseButtonRef = useRef<HTMLButtonElement | null>(null);
  const aboutDialogRef = useRef<HTMLElement | null>(null);
  const aboutPreviousFocusRef = useRef<HTMLElement | null>(null);
  const harnessEventsRef = useRef<EventSource | null>(null);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [editor, setEditor] = useState<TodayEditorState | null>(null);
  const [aiSettings, setAiSettings] = useState<AiSettingsView | null>(null);
  const [isLlmPanelOpen, setIsLlmPanelOpen] = useState(false);
  const [isAboutOpen, setIsAboutOpen] = useState(false);
  const [appInfo, setAppInfo] = useState<AppInfo | null>(null);
  const [aboutError, setAboutError] = useState("");
  const [input, setInput] = useState("");
  const [apiError, setApiError] = useState("");
  const [validationError, setValidationError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSettingsSubmitting, setIsSettingsSubmitting] = useState(false);
  const [isRegenerateConfirmOpen, setIsRegenerateConfirmOpen] = useState(false);
  const [hasLocalUnsavedChanges, setHasLocalUnsavedChanges] = useState(false);
  const [workbenchView, setWorkbenchView] = useState<"journal" | "assistant">("assistant");
  const [journalCorridorMenu, setJournalCorridorMenu] = useState<{ x: number; y: number } | null>(null);
  const [workspaceMode, setWorkspaceMode] = useState<"today" | "audit" | "history">("today");
  const [auditDate, setAuditDate] = useState("");
  const [auditRuns, setAuditRuns] = useState<JournalHarnessAuditRun[]>([]);
  const [historyViewMode, setHistoryViewMode] = useState<"search" | "anniversary">("search");
  const [historyQuery, setHistoryQuery] = useState("");
  const [historyStatus, setHistoryStatus] = useState("");
  const [historyEntries, setHistoryEntries] = useState<JournalHistoryEntrySummary[]>([]);
  const [anniversaryMonthDay, setAnniversaryMonthDay] = useState("");
  const [anniversaryResult, setAnniversaryResult] = useState<JournalAnniversaryWheelResult | null>(null);
  const [historyDetail, setHistoryDetail] = useState<JournalHistoryEntryDetail | null>(null);
  const [historySelectedDate, setHistorySelectedDate] = useState("");
  const [historyVersions, setHistoryVersions] = useState<JournalEntryVersion[]>([]);
  const [historyVersionDetail, setHistoryVersionDetail] = useState<JournalVersionDetail | null>(null);
  const [historyError, setHistoryError] = useState("");
  const [localServiceState, setLocalServiceState] = useState<LocalServiceState | null>(null);

  useEffect(() => {
    let cancelled = false;
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;

    async function load() {
      try {
        const hasDesktopBridge = Boolean(window.journalDesktop);
        const getDesktopServiceState = window.journalDesktop?.getLocalServiceStatus;
        let desktopServiceState: LocalServiceState | null = null;
        if (getDesktopServiceState) {
          desktopServiceState = await getDesktopServiceState().catch(() => null);
          if (!cancelled && requestId === requestIdRef.current && desktopServiceState) {
            setLocalServiceState(desktopServiceState);
          }
        }

        if (hasDesktopBridge) {
          if (desktopServiceState && !isTrustedLocalServiceState(desktopServiceState)) {
            if (!cancelled && requestId === requestIdRef.current) {
              setLoadState("error");
              setApiError(getBlockedLocalServiceMessage(desktopServiceState));
            }
            return;
          }

          const trustedApiBaseUrl = await initializeApiBaseUrlFromDesktop();
          if (!trustedApiBaseUrl) {
            if (!cancelled && requestId === requestIdRef.current) {
              setLoadState("error");
              setApiError(getBlockedLocalServiceMessage(desktopServiceState));
            }
            return;
          }
        } else {
          await initializeApiBaseUrlFromDesktop();
        }

        if (cancelled || requestId !== requestIdRef.current) {
          return;
        }

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
    return () => {
      harnessEventsRef.current?.close();
    };
  }, []);

  useEffect(() => {
    const getDesktopServiceState = window.journalDesktop?.getLocalServiceStatus;
    if (!getDesktopServiceState) {
      return undefined;
    }

    const refreshLocalServiceState = () => {
      void getDesktopServiceState()
        .then(setLocalServiceState)
        .catch(() => undefined);
    };
    const interval = window.setInterval(refreshLocalServiceState, 5000);

    return () => {
      window.clearInterval(interval);
    };
  }, []);

  useEffect(() => {
    if (!isAboutOpen) {
      return undefined;
    }

    aboutPreviousFocusRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;

    function handleAboutKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        closeAboutPanel();
      }
    }

    document.addEventListener("keydown", handleAboutKeyDown);
    (aboutCloseButtonRef.current ?? aboutDialogRef.current)?.focus();

    return () => {
      document.removeEventListener("keydown", handleAboutKeyDown);
      const previousFocus = aboutPreviousFocusRef.current;
      aboutPreviousFocusRef.current = null;
      if (previousFocus && document.contains(previousFocus)) {
        previousFocus.focus();
      }
    };
  }, [isAboutOpen]);

  useEffect(() => {
    function handleNativeMenuCommand(command: NativeMenuCommand) {
      if (command === "open-llm-settings") {
        resetPendingRegenerateDraft();
        setIsLlmPanelOpen(true);
      }
      if (command === "open-about") {
        void openAboutPanel();
      }
    }

    const unsubscribe = window.journalDesktop?.onNativeMenuCommand?.(handleNativeMenuCommand);
    const domListener = (event: Event) => {
      const command = (event as CustomEvent<NativeMenuCommand>).detail;
      handleNativeMenuCommand(command);
    };
    window.addEventListener(nativeMenuDomEventName, domListener);

    return () => {
      unsubscribe?.();
      window.removeEventListener(nativeMenuDomEventName, domListener);
    };
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
  const apiHealthState = health?.status === "ok"
    ? "ok"
    : loadState === "loading"
      ? "checking"
      : "error";
  const apiHealthLabel = apiHealthState === "ok"
    ? "API 连接正常"
    : apiHealthState === "checking"
      ? "正在检查 API"
      : "API 连接异常";
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
  const localServiceDetail = localServiceState
    ? [
        isTrustedLocalServiceState(localServiceState)
          ? localServiceState.apiBaseUrl ?? (localServiceState.port ? `127.0.0.1:${localServiceState.port}` : "")
          : "",
        localServiceState.pid ? `PID ${localServiceState.pid}` : ""
      ].filter(Boolean).join(" · ")
    : "";
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
    setIsRegenerateConfirmOpen(false);
  }

  function closeAboutPanel() {
    aboutRequestIdRef.current += 1;
    aboutInFlightRef.current = null;
    setIsAboutOpen(false);
    setAboutError("");
  }

  async function openAboutPanel() {
    setIsAboutOpen(true);
    setAboutError("");
    if (aboutInFlightRef.current) {
      return;
    }

    const requestId = aboutRequestIdRef.current + 1;
    aboutRequestIdRef.current = requestId;
    const request = getAppInfo();
    aboutInFlightRef.current = request;
    try {
      const result = await request;
      if (requestId === aboutRequestIdRef.current) {
        setAppInfo(result);
        setAboutError("");
      }
    } catch (caught) {
      if (requestId === aboutRequestIdRef.current) {
        setAboutError(getErrorMessage(caught));
      }
    } finally {
      if (aboutInFlightRef.current === request) {
        aboutInFlightRef.current = null;
      }
    }
  }

  async function runHarnessAndRefresh(
    startRun: () => Promise<StartHarnessRunResponse>,
    options: { clearInputAfterRefresh: boolean }
  ) {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setValidationError("");
    setIsSubmitting(true);
    try {
      const started = await startRun();
      if (requestId !== requestIdRef.current) {
        return;
      }

      harnessEventsRef.current?.close();
      harnessEventsRef.current = null;
      let stream: EventSource | null = null;
      let didRefresh = false;
      const refreshAfterTerminalEvent = async () => {
        if (didRefresh) {
          return;
        }

        didRefresh = true;
        stream?.close();
        if (harnessEventsRef.current === stream) {
          harnessEventsRef.current = null;
        }

        try {
          const next = await getTodayEditor();
          if (requestId === requestIdRef.current) {
            setEditor(next);
            if (options.clearInputAfterRefresh) {
              setInput("");
            }
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
      };

      stream = openHarnessRunEvents(
        started.run.id,
        event => {
          if (requestId === requestIdRef.current && isTerminalHarnessEvent(event)) {
            void refreshAfterTerminalEvent();
          }
        },
        error => {
          stream?.close();
          if (harnessEventsRef.current === stream) {
            harnessEventsRef.current = null;
          }
          if (requestId === requestIdRef.current) {
            setApiError(error.message);
            setIsSubmitting(false);
          }
        }
      );
      harnessEventsRef.current = stream;

      if (terminalHarnessStatuses.has(started.run.status)) {
        await refreshAfterTerminalEvent();
      }
    } catch (caught) {
      if (requestId === requestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
    } finally {
      if (requestId === requestIdRef.current && !harnessEventsRef.current) {
        setIsSubmitting(false);
      }
    }
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

    await runHarnessAndRefresh(
      () => startAppendHarnessRun(trimmedInput),
      { clearInputAfterRefresh: true }
    );
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

  async function handleRegenerateDraft() {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    resetPendingRegenerateDraft();
    await runHarnessAndRefresh(
      () => startReorganizeHarnessRun(),
      { clearInputAfterRefresh: false }
    );
  }

  async function handleRegenerateCurrentDraft() {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    setValidationError("");
    setIsRegenerateConfirmOpen(true);
  }

  async function openAuditWorkbench() {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    const date = today?.date.isoDate ?? editor?.date.isoDate ?? "";
    if (!date) {
      return;
    }

    const auditRequestId = auditRequestIdRef.current + 1;
    auditRequestIdRef.current = auditRequestId;
    resetPendingRegenerateDraft();
    setValidationError("");
    setAuditDate(date);
    setApiError("");
    try {
      const runs = await getJournalAudit(date);
      if (auditRequestId === auditRequestIdRef.current) {
        setAuditRuns(runs);
        setWorkspaceMode("audit");
      }
    } catch (caught) {
      if (auditRequestId === auditRequestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
    }
  }

  async function handleAuditDateChange(date: string) {
    const auditRequestId = auditRequestIdRef.current + 1;
    auditRequestIdRef.current = auditRequestId;
    setAuditDate(date);
    if (!date) {
      setAuditRuns([]);
      return;
    }

    setApiError("");
    try {
      const runs = await getJournalAudit(date);
      if (auditRequestId === auditRequestIdRef.current) {
        setAuditRuns(runs);
      }
    } catch (caught) {
      if (auditRequestId === auditRequestIdRef.current) {
        setApiError(getErrorMessage(caught));
      }
    }
  }

  async function loadHistoryEntryForRequest(date: string, historyRequestId: number) {
    const [detail, versions] = await Promise.all([
      getJournalHistoryEntry(date),
      getJournalHistoryVersions(date)
    ]);

    if (historyRequestId === historyRequestIdRef.current) {
      setHistoryDetail(detail);
      setHistoryVersions(versions);
      setHistoryVersionDetail(null);
      setHistoryError("");
    }
  }

  async function refreshHistory(query = historyQuery, status = historyStatus) {
    const historyRequestId = historyRequestIdRef.current + 1;
    historyRequestIdRef.current = historyRequestId;
    setHistoryError("");
    setHistoryDetail(null);
    setHistoryVersions([]);
    setHistoryVersionDetail(null);

    try {
      const result = await getJournalHistory({ query, status, limit: 50 });
      if (historyRequestId !== historyRequestIdRef.current) {
        return;
      }

      const selectedStillExists = result.items.some(item => item.date.isoDate === historySelectedDate);
      const selectedDate = selectedStillExists
        ? historySelectedDate
        : result.items[0]?.date.isoDate ?? "";
      setHistoryEntries(result.items);
      setHistorySelectedDate(selectedDate);

      if (!selectedDate) {
        return;
      }

      await loadHistoryEntryForRequest(selectedDate, historyRequestId);
    } catch (caught) {
      if (historyRequestId === historyRequestIdRef.current) {
        setHistoryError(getErrorMessage(caught));
      }
    }
  }

  async function refreshAnniversary(monthDay = anniversaryMonthDay) {
    const historyRequestId = historyRequestIdRef.current + 1;
    historyRequestIdRef.current = historyRequestId;
    setHistoryError("");
    setAnniversaryResult(null);
    setHistorySelectedDate("");
    setHistoryDetail(null);
    setHistoryVersions([]);
    setHistoryVersionDetail(null);

    try {
      const result = await getJournalAnniversaryWheel(monthDay, 50);
      if (historyRequestId !== historyRequestIdRef.current) {
        return;
      }

      const selectedStillExists = result.items.some(item => item.date.isoDate === historySelectedDate);
      const selectedDate = selectedStillExists
        ? historySelectedDate
        : result.items[0]?.date.isoDate ?? "";
      setAnniversaryResult(result);
      setHistorySelectedDate(selectedDate);

      if (!selectedDate) {
        return;
      }

      await loadHistoryEntryForRequest(selectedDate, historyRequestId);
    } catch (caught) {
      if (historyRequestId === historyRequestIdRef.current) {
        setHistoryError(getErrorMessage(caught));
      }
    }
  }

  async function openHistoryWorkbench() {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    resetPendingRegenerateDraft();
    setValidationError("");
    setHistoryViewMode("search");
    setWorkspaceMode("history");
    await refreshHistory(historyQuery, historyStatus);
  }

  async function openAnniversaryWorkbench() {
    if (hasLocalUnsavedChanges) {
      resetPendingRegenerateDraft();
      setValidationError(localUnsavedChangeMessage);
      return;
    }

    const monthDay = editor?.today.date.monthDay ?? today?.date.monthDay ?? "";
    if (!monthDay) {
      return;
    }

    resetPendingRegenerateDraft();
    setValidationError("");
    setHistoryViewMode("anniversary");
    setAnniversaryMonthDay(monthDay);
    setWorkspaceMode("history");
    await refreshAnniversary(monthDay);
  }

  function openHistoryFromJournalCorridor() {
    setJournalCorridorMenu(null);
    void openHistoryWorkbench();
  }

  function openAnniversaryFromJournalCorridor() {
    setJournalCorridorMenu(null);
    void openAnniversaryWorkbench();
  }

  function handleAnniversaryMonthDayChange(monthDay: string) {
    setAnniversaryMonthDay(monthDay);
    if (isValidAnniversaryMonthDay(monthDay)) {
      void refreshAnniversary(monthDay);
      return;
    }

    historyRequestIdRef.current += 1;
    historyVersionRequestIdRef.current += 1;
    setAnniversaryResult(null);
    setHistorySelectedDate("");
    setHistoryDetail(null);
    setHistoryVersions([]);
    setHistoryVersionDetail(null);
    setHistoryError("monthDay is invalid");
  }

  function clearHistoryVersionDetail() {
    historyVersionRequestIdRef.current += 1;
    setHistoryVersionDetail(null);
  }

  async function handleHistorySelectDate(date: string) {
    const historyRequestId = historyRequestIdRef.current + 1;
    historyRequestIdRef.current = historyRequestId;
    setHistorySelectedDate(date);
    setHistoryDetail(null);
    setHistoryVersions([]);
    setHistoryVersionDetail(null);
    setHistoryError("");

    try {
      await loadHistoryEntryForRequest(date, historyRequestId);
    } catch (caught) {
      if (historyRequestId === historyRequestIdRef.current) {
        setHistoryError(getErrorMessage(caught));
      }
    }
  }

  async function handleViewHistoryVersion(version: JournalEntryVersion) {
    const historyRequestId = historyRequestIdRef.current;
    const historyVersionRequestId = historyVersionRequestIdRef.current + 1;
    historyVersionRequestIdRef.current = historyVersionRequestId;
    setHistoryError("");
    try {
      const detail = await getJournalHistoryVersion(version.date.isoDate, version.id);
      if (
        historyRequestId === historyRequestIdRef.current
        && historyVersionRequestId === historyVersionRequestIdRef.current
        && detail.version.date.isoDate === version.date.isoDate
        && detail.version.id === version.id
      ) {
        setHistoryVersionDetail(detail);
      }
    } catch (caught) {
      if (
        historyRequestId === historyRequestIdRef.current
        && historyVersionRequestId === historyVersionRequestIdRef.current
      ) {
        setHistoryError(getErrorMessage(caught));
      }
    }
  }

  async function handleRestoreHistoryVersion(version: JournalEntryVersion) {
    setHistoryError("");
    setIsSubmitting(true);
    try {
      const restored = await restoreJournalHistoryVersionDraft(version.date.isoDate, version.id);
      setEditor(restored);
      setWorkspaceMode("today");
      setWorkbenchView("assistant");
    } catch (caught) {
      setHistoryError(getErrorMessage(caught));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="desktop-shell" aria-label="Journal 今日工作台">
      <header className="top-context command-top-context">
        <div className="brand">
          <strong>Journal</strong>
          <span>本地优先晨间日记</span>
        </div>
        <div className="today-line">
          <span
            className={`status-dot api-health-dot api-health-${apiHealthState}`}
            aria-label={apiHealthLabel}
            title={apiHealthLabel}
          ></span>
          <span><strong>{zhDateLabel}</strong> · {weekdayLabel} · {inputCount > 0 ? "原始表达已保留" : "等待第一句原始表达"}</span>
        </div>
        {localServiceState ? (
          <div className="local-service-line" aria-label="本地服务状态">
            <span>{getLocalServiceStatusLabel(localServiceState.status)}</span>
            {localServiceDetail ? <span>{localServiceDetail}</span> : null}
          </div>
        ) : null}
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

      <section className={`workspace command-workspace ${workspaceMode === "today" && workbenchView === "journal" ? "journal-only" : ""}`}>
        {workspaceMode === "audit" ? (
          <AuditWorkbench
            runs={auditRuns}
            selectedDate={auditDate}
            onDateChange={handleAuditDateChange}
            onReturnToday={() => setWorkspaceMode("today")}
          />
        ) : workspaceMode === "history" ? (
          historyViewMode === "anniversary" ? (
            <AnniversaryWheelWorkbench
              isBusy={isBusy}
              monthDay={anniversaryMonthDay}
              result={anniversaryResult}
              selectedDate={historySelectedDate}
              detail={historyDetail}
              versions={historyVersions}
              selectedVersionDetail={historyVersionDetail}
              error={historyError}
              onBack={() => setWorkspaceMode("today")}
              onRefresh={() => void refreshAnniversary()}
              onMonthDayChange={handleAnniversaryMonthDayChange}
              onSelectDate={date => void handleHistorySelectDate(date)}
              onViewVersion={version => void handleViewHistoryVersion(version)}
              onClearVersion={clearHistoryVersionDetail}
            />
          ) : (
            <HistoryWorkbench
              isBusy={isBusy}
              query={historyQuery}
              status={historyStatus}
              entries={historyEntries}
              detail={historyDetail}
              selectedDate={historySelectedDate}
              versions={historyVersions}
              selectedVersionDetail={historyVersionDetail}
              error={historyError}
              onBack={() => setWorkspaceMode("today")}
              onQueryChange={value => {
                setHistoryQuery(value);
                void refreshHistory(value, historyStatus);
              }}
              onStatusChange={value => {
                setHistoryStatus(value);
                void refreshHistory(historyQuery, value);
              }}
              onSelectDate={date => void handleHistorySelectDate(date)}
              onRefresh={() => void refreshHistory()}
              onViewVersion={version => void handleViewHistoryVersion(version)}
              onClearVersion={clearHistoryVersionDetail}
              onRestoreVersion={version => void handleRestoreHistoryVersion(version)}
            />
          )
        ) : (
        <>
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
              <h2>今日材料</h2>
              <span className="rail-count" aria-label="今日材料数量" title={`${inputCount} 条今日材料`}>{inputCount} 条</span>
            </div>
            <div className="source-stack">
              {rawInputViews.length > 0 ? rawInputViews.map(({ raw, tags, target }, index) => (
                <article className={`source-item ${index === rawInputViews.length - 1 ? "is-active" : ""}`} key={raw.id}>
                  <div className="source-meta">
                    <span>{formatRawInputTime(raw.createdAt)}</span>
                    {tags[0] ? <span>{tags[0]}</span> : null}
                  </div>
                  <p>{getRawInputPreview(raw.text, 56)}</p>
                  <div className="source-map">写入：{target}</div>
                </article>
              )) : (
                <p className="muted">还没有今日材料。先在下方写一句今天的事。</p>
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
            </div>
            <div className="stage-actions">
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
              <button
                type="button"
                className="journal-corridor-trigger"
                aria-label="日记回廊"
                title="日记回廊"
                aria-expanded={journalCorridorMenu !== null}
                onClick={event => {
                  const rect = event.currentTarget.getBoundingClientRect();
                  setJournalCorridorMenu({
                    x: Math.max(12, rect.right - 168),
                    y: rect.bottom + 8
                  });
                }}
              >
                <History size={16} strokeWidth={2.2} aria-hidden="true" />
              </button>
            </div>
          </div>

          {journalCorridorMenu ? (
            <div
              className="journal-corridor-menu"
              role="menu"
              aria-label="日记回廊菜单"
              style={{ left: journalCorridorMenu.x, top: journalCorridorMenu.y }}
            >
              <button type="button" role="menuitem" onClick={openHistoryFromJournalCorridor}>
                <History size={15} aria-hidden="true" />
                查看历史
              </button>
              <button type="button" role="menuitem" onClick={openAnniversaryFromJournalCorridor}>
                <History size={15} aria-hidden="true" />
                同日年轮
              </button>
            </div>
          ) : null}

          <div
            className="document-scroll"
            onContextMenu={event => {
              event.preventDefault();
              setJournalCorridorMenu({
                x: event.clientX,
                y: event.clientY
              });
            }}
          >
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
              <div className="compose-input-card">
                <textarea
                  id="today-input"
                  aria-label="补充今天的自然语言输入"
                  value={input}
                  onChange={event => {
                    resetPendingRegenerateDraft();
                    setInput(event.target.value);
                  }}
                  placeholder={hasEditableJournal ? "继续写一句今天的事..." : "今天发生了什么？"}
                  rows={2}
                  disabled={isBusy}
                />
                <div className="compose-toolbar" aria-label="输入工具栏">
                  <div className="compose-tool-group">
                    {hasEditableJournal ? (
                      <button
                        type="button"
                        className="compose-icon-button compose-tool-action"
                        aria-label="重新整理"
                        title="重新整理草稿"
                        onClick={handleRegenerateCurrentDraft}
                        disabled={isBusy || hasLocalUnsavedChanges}
                      >
                        <RefreshCw size={16} strokeWidth={2.2} aria-hidden="true" />
                      </button>
                    ) : null}
                  </div>
                  <div className="compose-action-group">
                    {canConfirm ? (
                      <button
                        type="button"
                        className="compose-icon-button compose-save-action"
                        aria-label="保存日记"
                        title="保存日记"
                        onClick={handleConfirm}
                        disabled={isBusy}
                      >
                        <Save size={16} strokeWidth={2.2} aria-hidden="true" />
                      </button>
                    ) : null}
                    <button
                      type="submit"
                      className="compose-icon-button compose-send-action"
                      aria-label="生成草稿"
                      title="写入今天的材料"
                      disabled={isBusy || hasLocalUnsavedChanges}
                    >
                      <SendHorizontal size={17} strokeWidth={2.4} aria-hidden="true" />
                    </button>
                  </div>
                </div>
              </div>
            </form>
          </section>
          {isRegenerateConfirmOpen ? (
            <div className="confirm-overlay">
              <section
                className="confirm-dialog"
                role="dialog"
                aria-modal="true"
                aria-labelledby="regenerate-dialog-title"
              >
                <div>
                  <p className="confirm-eyebrow">重新整理</p>
                  <h2 id="regenerate-dialog-title">重新整理草稿</h2>
                  <p>这会覆盖当前草稿内容，但不会影响正式日记。</p>
                </div>
                <div className="confirm-actions">
                  <button type="button" className="secondary-action secondary" onClick={resetPendingRegenerateDraft}>
                    取消
                  </button>
                  <button type="button" className="primary-action primary" onClick={() => handleRegenerateDraft()} disabled={isBusy}>
                    确认重新整理
                  </button>
                </div>
              </section>
            </div>
          ) : null}
        </section>

        {workbenchView === "assistant" ? (
        <aside className="assistant-panel today-assistant" aria-label="今日助手">
          <div className="assistant-head">
            <div>
              <p className="assistant-eyebrow">Today Assistant</p>
              <h2>今天被这样整理</h2>
              <div className="assistant-meta">
                {visibleTodayTags.slice(0, 2).map(tag => (
                  <span className="assistant-meta-chip assistant-meta-tag" key={`assistant-meta-${tag}`}>{tag}</span>
                ))}
                <span className="assistant-meta-chip assistant-meta-provider" aria-label={`LLM：${activeProviderName}`}>
                  <span className="assistant-meta-dot" aria-hidden="true"></span>
                  {activeProviderName}
                </span>
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
            </section>

            <section className="assistant-card">
              <div className="assistant-card-head">
                <h3>统计</h3>
              </div>
              <div className="quiet-metrics" aria-label="统计">
                <div className="metric">
                  <strong>{assistantSummary.rawInputCount}</strong>
                  <span>材料</span>
                </div>
                <div className="metric">
                  <strong>{assistantSummary.sectionCount}</strong>
                  <span>段落</span>
                </div>
                <div className="metric">
                  <strong>{assistantSummary.editedCount}</strong>
                  <span>编辑中</span>
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
                <h3>整理证据</h3>
                <button type="button" className="assistant-inline-action" onClick={openAuditWorkbench}>
                  查看审计
                </button>
              </div>
              {rawInputViews.length > 0 ? (
                <div className="evidence-list">
                  {rawInputViews.map(({ raw, tags, target }) => (
                    <div className="evidence-item" key={raw.id}>
                      <div className="evidence-meta">
                        <span>{formatRawInputTime(raw.createdAt)}</span>
                        {tags[0] ? <span>{tags[0]}</span> : null}
                        <span>写入 {target}</span>
                      </div>
                      {getRawInputPreview(raw.text, 54)}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted">还没有输入。这里之后会显示原始表达如何进入日记段落。</p>
              )}
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
        </>
        )}
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
      {isAboutOpen ? (
        <div className="llm-settings-backdrop" role="presentation">
          <section
            className="about-panel"
            role="dialog"
            aria-modal="true"
            aria-label="关于 Journal"
            ref={aboutDialogRef}
            tabIndex={-1}
          >
            <header>
              <h2>关于 Journal</h2>
              <button type="button" ref={aboutCloseButtonRef} onClick={closeAboutPanel}>关闭</button>
            </header>
            <dl>
              <dt>Release</dt>
              <dd>{appInfo?.releaseVersion ?? frontendBuildInfo.releaseVersion}</dd>
              <dt>前端</dt>
              <dd>Frontend {frontendBuildInfo.frontendVersion}</dd>
              <dt>Backend</dt>
              <dd>{appInfo ? `Backend ${appInfo.version}` : "Backend 未连接"}</dd>
              <dt>Commit</dt>
              <dd>{appInfo?.commit ?? frontendBuildInfo.commit}</dd>
              <dt>Build</dt>
              <dd>{appInfo?.buildTimeUtc ?? frontendBuildInfo.buildTimeUtc}</dd>
              <dt>Data</dt>
              <dd>{appInfo?.dataRoot ?? "本地服务未连接"}</dd>
            </dl>
            {aboutError ? <p className="api-error" role="alert">{aboutError}</p> : null}
            <footer>
              <span>License</span>
              <span>Privacy</span>
              <span>Data Safety</span>
              <span>AI Notice</span>
            </footer>
          </section>
        </div>
      ) : null}
    </main>
  );
}
