import { useEffect, useMemo, useState } from "react";
import "./styles.css";

type ApiStatus = "checking" | "online" | "offline";

type HealthResponse = {
  app: string;
  status: string;
  version: string;
  environment: string;
  serverTime: string;
};

const apiBaseUrl = import.meta.env.VITE_JOURNAL_API_URL ?? "http://localhost:5057";

export default function App() {
  const [status, setStatus] = useState<ApiStatus>("checking");
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string>("");

  const today = useMemo(() => {
    return new Intl.DateTimeFormat("zh-CN", {
      dateStyle: "full"
    }).format(new Date());
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function loadHealth() {
      try {
        const response = await fetch(`${apiBaseUrl}/health`);
        if (!response.ok) {
          throw new Error(`health check failed: ${response.status}`);
        }

        const payload = await response.json() as HealthResponse;
        if (!cancelled) {
          setHealth(payload);
          setStatus("online");
          setError("");
        }
      } catch (caught) {
        if (!cancelled) {
          setStatus("offline");
          setHealth(null);
          setError(caught instanceof Error ? caught.message : "unknown error");
        }
      }
    }

    loadHealth();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="app-shell">
      <section className="hero-panel">
        <p className="eyebrow">Phase 1 Skeleton</p>
        <h1>Journal</h1>
        <p className="lead">每天 3 分钟的人生坐标系统，先从一个稳定的桌面工程骨架开始。</p>
      </section>

      <section className="status-panel" aria-label="应用状态">
        <div className="status-row">
          <span>今天</span>
          <strong>{today}</strong>
        </div>
        <div className="status-row">
          <span>API 状态</span>
          <strong className={`status status-${status}`}>{status}</strong>
        </div>
        {health ? (
          <dl className="health-grid">
            <div>
              <dt>服务</dt>
              <dd>{health.app}</dd>
            </div>
            <div>
              <dt>版本</dt>
              <dd>{health.version}</dd>
            </div>
            <div>
              <dt>环境</dt>
              <dd>{health.environment}</dd>
            </div>
            <div>
              <dt>服务端时间</dt>
              <dd>{health.serverTime}</dd>
            </div>
          </dl>
        ) : (
          <p className="error-text">{status === "checking" ? "正在检查本地 API..." : error}</p>
        )}
      </section>

      <section className="next-panel">
        <span>Next</span>
        <p>阶段 2：自然语言文本 -&gt; Mock AI JSON -&gt; JMF Markdown 预览。</p>
      </section>
    </main>
  );
}
