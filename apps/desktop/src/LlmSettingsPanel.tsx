import { Eye, EyeOff, LockKeyhole } from "lucide-react";
import { type FormEvent, useMemo, useRef, useState } from "react";
import {
  type AiProviderApiKeyView,
  type AiProviderHealthResult,
  type AiProviderSaveRequest,
  type AiProviderView,
  type AiSettingsActivationResult,
  type AiSettingsSaveRequest,
  type AiSettingsView
} from "./api";
import { getStaticAiStyleLabel } from "./todayWorkbenchView";

type LlmSettingsPanelProps = {
  settings: AiSettingsView;
  isBusy: boolean;
  onClose: () => void;
  onActivate: (request: AiSettingsSaveRequest) => Promise<AiSettingsActivationResult>;
  onTest: (providerId: string, candidate?: AiSettingsSaveRequest) => Promise<AiProviderHealthResult>;
  onRevealApiKey: (providerId: string) => Promise<AiProviderApiKeyView>;
};

function toSaveRequests(view: AiSettingsView): AiProviderSaveRequest[] {
  return view.providers.map(provider => ({
    id: provider.id,
    type: provider.type,
    displayName: provider.displayName,
    preset: provider.preset,
    baseUrl: provider.baseUrl,
    model: provider.model,
    apiKey: "",
    isEnabled: provider.isEnabled,
    timeoutSeconds: provider.timeoutSeconds,
    temperature: provider.temperature,
    maxTokens: provider.maxTokens,
    stylePreset: provider.stylePreset
  }));
}

function createCandidate(activeProviderId: string, draftProviders: AiProviderSaveRequest[]): AiSettingsSaveRequest {
  return {
    activeProviderId,
    providers: draftProviders.map(provider => ({
      ...provider,
      isEnabled: provider.id === activeProviderId
    }))
  };
}

function providerSourceLabel(source: string) {
  switch (source) {
    case "environment":
      return "环境变量";
    case "file":
      return "本机配置文件";
    case "default":
    case "preset":
      return "默认预设";
    default:
      return source;
  }
}

function providerStatusLabel(provider: AiProviderView, selectedTestResult: AiProviderHealthResult | null) {
  if (selectedTestResult && !selectedTestResult.isSuccess) {
    return "测试失败";
  }

  if (provider.isActive) {
    return "已启用";
  }

  if (provider.id === "mock" || provider.hasApiKey) {
    return "已配置";
  }

  return "需要配置";
}

function providerInitial(displayName: string) {
  return displayName.trim().slice(0, 1).toUpperCase() || "?";
}

function createClientFailure(caught: unknown, technicalDetails: string): AiProviderHealthResult {
  return {
    isSuccess: false,
    status: "request_failed",
    safeResponseSnippet: "",
    httpStatus: null,
    latency: null,
    error: {
      stage: "client",
      code: "request_failed",
      message: caught instanceof Error ? caught.message : "LLM 请求失败。",
      technicalDetails
    }
  };
}

export function LlmSettingsPanel({
  settings,
  isBusy,
  onClose,
  onActivate,
  onTest,
  onRevealApiKey
}: LlmSettingsPanelProps) {
  const [selectedId, setSelectedId] = useState(settings.activeProviderId);
  const selectedIdRef = useRef(settings.activeProviderId);
  const [providers, setProviders] = useState<AiProviderSaveRequest[]>(() => toSaveRequests(settings));
  const [dirtyProviderIds, setDirtyProviderIds] = useState<Set<string>>(() => new Set());
  const [revealedKeyProviderId, setRevealedKeyProviderId] = useState<string | null>(null);
  const [revealedKey, setRevealedKey] = useState("");
  const [isAdvancedOpen, setIsAdvancedOpen] = useState(false);
  const [testResult, setTestResult] = useState<AiProviderHealthResult | null>(null);
  const [testResultIsStale, setTestResultIsStale] = useState(false);
  const [activationResult, setActivationResult] = useState<AiSettingsActivationResult | null>(null);

  const currentViewSettings = activationResult?.saved ? activationResult.settings : settings;
  const selected = useMemo(
    () => providers.find(provider => provider.id === selectedId),
    [providers, selectedId]
  );
  const selectedView = currentViewSettings.providers.find(provider => provider.id === selectedId);
  const selectedUsesEnvironmentKey = Boolean(
    selectedView?.source === "environment"
      && selectedView.hasApiKey
      && !selectedView.canRevealApiKey
  );
  const viewProvidersById = useMemo(
    () => new Map(currentViewSettings.providers.map(provider => [provider.id, provider])),
    [currentViewSettings.providers]
  );

  function resetRevealState() {
    setRevealedKeyProviderId(null);
    setRevealedKey("");
  }

  function handleSelectProvider(providerId: string) {
    selectedIdRef.current = providerId;
    setSelectedId(providerId);
    setTestResult(null);
    setTestResultIsStale(false);
    setActivationResult(null);
    resetRevealState();
  }

  function handleClose() {
    resetRevealState();
    onClose();
  }

  function updateSelected(patch: Partial<AiProviderSaveRequest>) {
    if (!selected) {
      return;
    }

    setProviders(current =>
      current.map(provider => provider.id === selected.id ? { ...provider, ...patch } : provider)
    );
    setDirtyProviderIds(current => {
      const next = new Set(current);
      next.add(selected.id);
      return next;
    });
    setTestResultIsStale(Boolean(testResult));
    setActivationResult(null);
  }

  function updateSelectedNumber(
    field: "timeoutSeconds" | "temperature" | "maxTokens",
    value: string,
    fallback: number
  ) {
    if (value.trim().length === 0) {
      updateSelected({ [field]: fallback });
      return;
    }

    const parsed = Number(value);
    updateSelected({ [field]: Number.isFinite(parsed) ? parsed : fallback });
  }

  async function handleToggleApiKeyReveal() {
    if (!selected) {
      return;
    }

    const providerId = selected.id;

    if (revealedKeyProviderId === selected.id) {
      resetRevealState();
      return;
    }

    try {
      const result = await onRevealApiKey(providerId);
      if (selectedIdRef.current === providerId) {
        setRevealedKeyProviderId(providerId);
        setRevealedKey(result.apiKey);
      }
    } catch (caught) {
      if (selectedIdRef.current === providerId) {
        setTestResult(
          createClientFailure(
            caught,
            "The LLM API key reveal request failed before a result was returned."
          )
        );
        setTestResultIsStale(false);
        setActivationResult(null);
      }
    }
  }

  async function handleTestCurrentForm() {
    if (!selected) {
      return;
    }

    const providerId = selected.id;
    const candidate = createCandidate(providerId, providers);
    setTestResultIsStale(false);
    setActivationResult(null);

    let result: AiProviderHealthResult;
    try {
      result = await onTest(providerId, candidate);
    } catch (caught) {
      result = createClientFailure(caught, "The current LLM form test failed before a result was returned.");
    }

    if (selectedIdRef.current === providerId) {
      setTestResult(result);
    }
  }

  async function handleActivate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) {
      return;
    }

    const providerId = selected.id;
    const candidate = createCandidate(providerId, providers);
    setTestResultIsStale(false);

    let result: AiSettingsActivationResult;
    try {
      result = await onActivate(candidate);
    } catch (caught) {
      result = {
        saved: false,
        settings,
        testResult: createClientFailure(caught, "The LLM activation request failed before a result was returned.")
      };
    }

    if (selectedIdRef.current === providerId) {
      setActivationResult(result);
      setTestResult(result.testResult);
      if (result.saved) {
        const nextSelectedId = result.settings.activeProviderId || providerId;
        selectedIdRef.current = nextSelectedId;
        setSelectedId(nextSelectedId);
        setProviders(toSaveRequests(result.settings));
        setDirtyProviderIds(new Set());
        resetRevealState();
      }
    }
  }

  return (
    <section className="llm-settings-overlay" aria-label="LLM 配置面板">
      <header className="llm-settings-head">
        <div>
          <strong>LLM 配置</strong>
          <span>{settings.runtime}</span>
        </div>
        <button type="button" className="secondary-action" onClick={handleClose}>关闭</button>
      </header>

      <div className="llm-settings-grid">
        <nav className="llm-provider-list" aria-label="LLM 列表">
          <div className="llm-provider-list-head">
            <span className="rail-label">模型来源</span>
            <p>选择今天用于整理日记的模型。</p>
          </div>
          {providers.map(provider => {
            const providerView = viewProvidersById.get(provider.id);
            const isSelected = provider.id === selectedId;
            const selectedProviderTestResult = isSelected ? testResult : null;

            return (
              <button
                key={provider.id}
                type="button"
                className={`llm-provider-card ${isSelected ? "active" : ""}`}
                aria-pressed={isSelected}
                onClick={() => handleSelectProvider(provider.id)}
              >
                <div className="llm-provider-title">
                  <span className="llm-provider-avatar" aria-label={`${provider.displayName} 标识`}>
                    {providerInitial(provider.displayName)}
                  </span>
                  <div>
                    <strong>{provider.displayName}</strong>
                    <small>{provider.id === "mock" ? "本地备用" : providerSourceLabel(providerView?.source ?? "default")}</small>
                  </div>
                </div>
                <span className="llm-provider-status">{providerStatusLabel(providerView ?? {
                  ...provider,
                  isActive: provider.id === currentViewSettings.activeProviderId,
                  hasApiKey: Boolean(provider.apiKey),
                  apiKeyPreview: "",
                  canRevealApiKey: false,
                  source: "default",
                  lastTestStatus: "not-tested"
                }, selectedProviderTestResult)}</span>
              </button>
            );
          })}
        </nav>

        {selected ? (
          <form className="llm-settings-main" onSubmit={handleActivate}>
            <section className="llm-settings-card">
              <span className="rail-label">当前 LLM</span>
              <div className="llm-current-orbit">
                <span className="llm-current-avatar" aria-label={`当前 LLM 标识 ${selected.displayName}`}>
                  {providerInitial(selected.displayName)}
                </span>
                <div>
                  <h2>{selected.displayName}</h2>
                  <p>
                    {selectedView?.source === "environment"
                      ? "当前配置包含环境变量覆盖；环境变量字段不会回写配置文件。"
                      : "切换后会先做一次最小请求测试，通过后才会保存并启用。"}
                  </p>
                </div>
              </div>
            </section>

            <section className="llm-settings-card">
              <span className="rail-label">连接信息</span>
              <label>
                显示名称
                <input
                  value={selected.displayName}
                  onChange={event => updateSelected({ displayName: event.target.value })}
                />
              </label>
              <label>
                模型
                <input
                  aria-label="模型"
                  value={selected.model}
                  onChange={event => updateSelected({ model: event.target.value })}
                />
              </label>
              <label>
                Base URL
                <input
                  value={selected.baseUrl}
                  onChange={event => updateSelected({ baseUrl: event.target.value })}
                />
              </label>
              <label>
                API Key
                {selectedUsesEnvironmentKey ? (
                  <div className="llm-key-display" aria-label="API Key 已从环境变量加载">
                    <LockKeyhole size={16} strokeWidth={1.8} aria-hidden="true" data-testid="environment-key-lock" />
                    已从环境变量加载，不在界面显示
                  </div>
                ) : selectedView?.id === "mock" ? (
                  <div className="llm-key-display" aria-label="Mock 无需 API Key">
                    无需 API Key
                  </div>
                ) : (
                  <div className="llm-key-row">
                    <input
                      aria-label="API Key"
                      value={revealedKeyProviderId === selected.id ? revealedKey : selected.apiKey}
                      placeholder={selectedView?.apiKeyPreview || (selectedView?.hasApiKey ? "API Key 已配置" : "未填写 API Key")}
                      onChange={event => {
                        if (revealedKeyProviderId === selected.id) {
                          setRevealedKey(event.target.value);
                        }
                        updateSelected({ apiKey: event.target.value });
                      }}
                    />
                    {selectedView?.canRevealApiKey && !selected.apiKey ? (
                      <button
                        type="button"
                        className="icon-action"
                        aria-label={revealedKeyProviderId === selected.id ? "隐藏 API Key" : "查看 API Key"}
                        onClick={handleToggleApiKeyReveal}
                      >
                        {revealedKeyProviderId === selected.id ? (
                          <EyeOff size={17} strokeWidth={1.8} aria-hidden="true" />
                        ) : (
                          <Eye size={17} strokeWidth={1.8} aria-hidden="true" />
                        )}
                      </button>
                    ) : null}
                  </div>
                )}
              </label>
              <div className="llm-settings-actions">
                <button type="button" className="secondary-action" onClick={handleTestCurrentForm} disabled={isBusy}>
                  测试当前表单
                </button>
                <button type="submit" className="primary-action" disabled={isBusy}>
                  保存并启用
                </button>
              </div>
              {dirtyProviderIds.has(selected.id) ? (
                <p className="field-hint">当前 provider 有未保存修改。</p>
              ) : null}
            </section>
          </form>
        ) : (
          <section className="llm-settings-main">
            <section className="llm-settings-card attention-panel">
              <span className="rail-label">当前 LLM</span>
              <h2>{selectedId}</h2>
              <p>当前 active LLM 在配置列表中不存在。请从左侧选择一个已配置的 LLM 后保存。</p>
            </section>
          </section>
        )}

        {selected ? (
          <aside className="llm-settings-side">
            <section className="llm-settings-card">
              <span className="rail-label">配置来源</span>
              <h2>来源：{providerSourceLabel(selectedView?.source ?? "default")}</h2>
              <p>
                {selectedView?.source === "environment"
                  ? "当前 provider 有环境变量覆盖，保存时只回写本机配置。"
                  : selectedView?.source === "file"
                    ? "当前 provider 来自本机配置文件，API Key 可按权限显式查看。"
                    : "当前 provider 来自内置预设，保存后会写入本机配置。"}
              </p>
            </section>

            <section className="llm-settings-card">
              <span className="rail-label">整理方式</span>
              <h2>{getStaticAiStyleLabel()}</h2>
              <p>保留原话优先，轻度整理成日记块。</p>
            </section>

            <section className="llm-settings-card">
              <span className="rail-label">高级参数</span>
              <button
                type="button"
                className="llm-advanced-summary"
                onClick={() => setIsAdvancedOpen(current => !current)}
              >
                <span>
                  高级参数：temperature {selected.temperature} · max tokens {selected.maxTokens} · timeout {selected.timeoutSeconds}s · JSON 模式开启
                </span>
                <strong>{isAdvancedOpen ? "收起" : "展开"}</strong>
              </button>
              {isAdvancedOpen ? (
                <div className="llm-advanced-fields">
                  <label>
                    JSON 模式
                    <input value="json_object" readOnly />
                  </label>
                  <label>
                    超时
                    <input
                      type="number"
                      min={1}
                      step={1}
                      value={selected.timeoutSeconds}
                      onChange={event => updateSelectedNumber("timeoutSeconds", event.target.value, selected.timeoutSeconds)}
                    />
                  </label>
                  <label>
                    Temperature
                    <input
                      type="number"
                      min={0}
                      step={0.1}
                      value={selected.temperature}
                      onChange={event => updateSelectedNumber("temperature", event.target.value, selected.temperature)}
                    />
                  </label>
                  <label>
                    Max tokens
                    <input
                      type="number"
                      min={0}
                      step={1}
                      value={selected.maxTokens}
                      onChange={event => updateSelectedNumber("maxTokens", event.target.value, selected.maxTokens)}
                    />
                  </label>
                </div>
              ) : null}
            </section>

            <section className={`llm-settings-card ${testResult?.isSuccess === false ? "attention-panel" : ""}`}>
              <span className="rail-label">最近诊断</span>
              {testResultIsStale ? <p>测试结果已过期</p> : null}
              {testResult?.isSuccess ? <h2>连接测试通过</h2> : null}
              {testResult && !testResult.isSuccess ? <h2>测试失败，配置没有保存</h2> : null}
              {activationResult?.saved ? <p>可以回到今日页重新整理</p> : null}
              {testResult ? (
                <dl className="llm-diagnostics-list">
                  <dt>HTTP</dt>
                  <dd>{testResult.httpStatus ?? "未返回"}</dd>
                  <dt>Status</dt>
                  <dd>{testResult.status}</dd>
                  <dt>Provider</dt>
                  <dd>{selected.displayName}</dd>
                  <dt>Model</dt>
                  <dd>{selected.model || "未填写"}</dd>
                  <dt>Base URL</dt>
                  <dd>{selected.baseUrl}</dd>
                </dl>
              ) : (
                <p>测试会向当前 LLM 发送一次最小请求，可能产生少量 token 消耗。</p>
              )}
              {testResult?.error?.message ? <p>{testResult.error.message}</p> : null}
              {testResult?.error ? (
                <details open>
                  <summary>技术详情</summary>
                  <pre>{testResult.error.technicalDetails}</pre>
                </details>
              ) : null}
            </section>
          </aside>
        ) : (
          <aside className="llm-settings-side">
            <section className="llm-settings-card attention-panel">
              <span className="rail-label">最近诊断</span>
              <p>选择一个已配置的 LLM 后才能测试连接。</p>
            </section>
          </aside>
        )}
      </div>
    </section>
  );
}
