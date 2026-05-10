import { type FormEvent, useMemo, useRef, useState } from "react";
import {
  type AiProviderHealthResult,
  type AiProviderSaveRequest,
  type AiSettingsSaveRequest,
  type AiSettingsView
} from "./api";

type LlmSettingsPanelProps = {
  settings: AiSettingsView;
  isBusy: boolean;
  onClose: () => void;
  onSave: (request: AiSettingsSaveRequest) => Promise<void>;
  onTest: (providerId: string) => Promise<AiProviderHealthResult>;
  onRegenerate: (providerId?: string) => Promise<void>;
};

export function LlmSettingsPanel({
  settings,
  isBusy,
  onClose,
  onSave,
  onTest,
  onRegenerate
}: LlmSettingsPanelProps) {
  const [selectedId, setSelectedId] = useState(settings.activeProviderId);
  const selectedIdRef = useRef(settings.activeProviderId);
  const [providers, setProviders] = useState<AiProviderSaveRequest[]>(
    settings.providers.map(provider => ({
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
    }))
  );
  const [testResult, setTestResult] = useState<AiProviderHealthResult | null>(null);
  const [pendingRegenerate, setPendingRegenerate] = useState<{ providerId?: string } | null>(null);

  const selected = useMemo(
    () => providers.find(provider => provider.id === selectedId) ?? providers[0],
    [providers, selectedId]
  );
  const selectedView = settings.providers.find(provider => provider.id === selectedId);

  if (!selected) {
    return null;
  }

  function updateSelected(patch: Partial<AiProviderSaveRequest>) {
    setTestResult(null);
    setPendingRegenerate(null);
    setProviders(current =>
      current.map(provider => provider.id === selected.id ? { ...provider, ...patch } : provider)
    );
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

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSave({ activeProviderId: selected.id, providers });
  }

  async function handleTest() {
    const providerId = selected.id;
    const result = await onTest(providerId);
    if (selectedIdRef.current === providerId) {
      setTestResult(result);
    }
  }

  async function handleRegenerate(providerId?: string) {
    if (!pendingRegenerate || pendingRegenerate.providerId !== providerId) {
      setPendingRegenerate({ providerId });
      return;
    }

    await onRegenerate(providerId);
    setPendingRegenerate(null);
  }

  return (
    <section className="llm-settings-overlay" aria-label="LLM 配置面板">
      <header className="llm-settings-head">
        <div>
          <strong>LLM 配置</strong>
          <span>{settings.runtime}</span>
        </div>
        <button type="button" className="secondary-action" onClick={onClose}>关闭</button>
      </header>

      <div className="llm-settings-grid">
        <nav className="llm-provider-list" aria-label="Provider 列表">
          {settings.providers.map(provider => (
            <button
              key={provider.id}
              type="button"
              className={`llm-provider-card ${provider.id === selectedId ? "active" : ""}`}
              aria-pressed={provider.id === selectedId}
              onClick={() => {
                selectedIdRef.current = provider.id;
                setSelectedId(provider.id);
                setTestResult(null);
                setPendingRegenerate(null);
              }}
            >
              <strong>{provider.displayName}</strong>
              <span>{provider.isActive ? "active" : provider.source}</span>
              <small>{provider.hasApiKey ? "key ready" : "no key"}</small>
            </button>
          ))}
        </nav>

        <form className="llm-settings-main" onSubmit={handleSave}>
          <section className="llm-settings-card">
            <span className="rail-label">Selected provider</span>
            <h2>{selected.displayName}</h2>
            <p>
              {selectedView?.source === "environment"
                ? "当前配置来自环境变量，API Key 不会显示，也不会回写配置文件。"
                : "保存到本机 ai-providers.json。"}
            </p>
          </section>

          <section className="llm-settings-card">
            <span className="rail-label">Basic config</span>
            <label>
              显示名称
              <input
                value={selected.displayName}
                onChange={event => updateSelected({ displayName: event.target.value })}
              />
            </label>
            <label>
              模型
              <input value={selected.model} onChange={event => updateSelected({ model: event.target.value })} />
            </label>
            <label>
              API Key
              <input
                value={selected.apiKey}
                onChange={event => updateSelected({ apiKey: event.target.value })}
                aria-label={selectedView?.hasApiKey ? "API Key 已加载，值不显示" : "API Key 未配置"}
              />
            </label>
            <label>
              Base URL
              <input value={selected.baseUrl} onChange={event => updateSelected({ baseUrl: event.target.value })} />
            </label>
            <div className="llm-settings-actions">
              <button type="button" className="secondary-action" onClick={handleTest} disabled={isBusy}>
                测试已保存配置
              </button>
              <button type="submit" className="primary-action" disabled={isBusy}>
                启用 Provider
              </button>
            </div>
          </section>

          <section className="llm-settings-card">
            <span className="rail-label">Regenerate</span>
            <h2>重新整理今日草稿</h2>
            <p>{pendingRegenerate ? "这会覆盖当前草稿内容，但不会影响正式日记。" : "使用当前 Provider 重新生成 reviewing draft。"}</p>
            <div className="llm-settings-actions">
              <button
                type="button"
                className="secondary-action danger-action"
                onClick={() => handleRegenerate("mock")}
                disabled={isBusy}
              >
                用 Mock 生成一次
              </button>
              <button type="button" className="primary-action" onClick={() => handleRegenerate()} disabled={isBusy}>
                重新整理草稿
              </button>
            </div>
          </section>
        </form>

        <aside className="llm-settings-side">
          <section className="llm-settings-card">
            <span className="rail-label">Advanced</span>
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
          </section>

          <section className={`llm-settings-card ${testResult?.isSuccess === false ? "attention-panel" : ""}`}>
            <span className="rail-label">Connection test</span>
            <h2>{testResult ? testResult.status : "最小 JSON 请求"}</h2>
            <p>会使用已保存的 Provider 配置，不会测试当前未保存草稿。</p>
            {testResult?.error ? (
              <details>
                <summary>安全技术详情</summary>
                <pre>{testResult.error.technicalDetails}</pre>
              </details>
            ) : null}
          </section>
        </aside>
      </div>
    </section>
  );
}
