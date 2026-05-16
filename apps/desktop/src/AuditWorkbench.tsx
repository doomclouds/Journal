import { useEffect, useState } from "react";
import type { JournalHarnessAuditRun, JournalHarnessAuditToolCall } from "./api";
import { DatePickerField } from "./DatePickerField";

type AuditWorkbenchProps = {
  runs: JournalHarnessAuditRun[];
  selectedDate: string;
  onDateChange: (date: string) => void;
  onReturnToday: () => void;
};

function formatAuditTime(value: string | null) {
  if (!value) {
    return "--:--";
  }

  const match = value.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? value;
}

function getToolCallTone(call: JournalHarnessAuditToolCall) {
  return call.rejectionReason || call.status === "rejected" ? "rejected" : call.status;
}

export function AuditWorkbench({ runs, selectedDate, onDateChange, onReturnToday }: AuditWorkbenchProps) {
  const [selectedRunId, setSelectedRunId] = useState<string | null>(runs[0]?.id ?? null);

  useEffect(() => {
    setSelectedRunId(current => runs.find(run => run.id === current)?.id ?? runs[0]?.id ?? null);
  }, [runs]);

  const selectedRun = runs.find(run => run.id === selectedRunId) ?? runs[0] ?? null;
  const selectedToolCalls = selectedRun?.toolCalls ?? [];

  return (
    <>
      <aside className="context-rail audit-rail" aria-label="审计日期和运行记录">
        <section className="date-card audit-date-card">
          <p className="month">Audit</p>
          <h1>{selectedDate ? selectedDate.slice(5) : "-- --"}<span>当天 harness run</span></h1>
          <DatePickerField
            id="audit-date"
            label="审计日期"
            value={selectedDate}
            onChange={onDateChange}
          />
        </section>

        <section className="rail-section">
          <div className="section-head">
            <h2>运行记录</h2>
            <span>{runs.length} 次</span>
          </div>
          <div className="source-stack audit-run-list" aria-label="审计运行列表">
            {runs.length > 0 ? runs.map(run => (
              <button
                type="button"
                className={`source-item audit-run-item ${run.id === selectedRun?.id ? "is-active" : ""}`}
                key={run.id}
                onClick={() => setSelectedRunId(run.id)}
                aria-pressed={run.id === selectedRun?.id}
              >
                <span className="source-meta">
                  <span>{formatAuditTime(run.createdAt)}</span>
                  <span>{run.status}</span>
                </span>
                <strong>{run.summary || "无摘要"}</strong>
                <span className="source-map">{run.providerId} / {run.promptVersion}</span>
              </button>
            )) : (
              <p className="muted">这个日期还没有 harness run。</p>
            )}
          </div>
        </section>
      </aside>

      <section className="journal-stage audit-stage" aria-label="AI 审计工作台">
        <div className="stage-toolbar">
          <div className="stage-title">
            <p>审计工作台</p>
            <h2>工具调用时间线</h2>
          </div>
          <button type="button" className="secondary audit-return-action" onClick={onReturnToday}>
            返回今日
          </button>
        </div>

        <div className="document-scroll audit-scroll">
          <article className="journal-paper audit-paper">
            {selectedRun ? (
              <>
                <header className="audit-run-summary">
                  <p className="kicker">{selectedRun.status}</p>
                  <h1>{selectedRun.summary || "Harness run"}</h1>
                  <p>
                    {formatAuditTime(selectedRun.startedAt)} - {formatAuditTime(selectedRun.completedAt)}
                    <span>{selectedRun.providerId}</span>
                  </p>
                </header>

                {selectedToolCalls.length > 0 ? selectedToolCalls.map(call => {
                  const tone = getToolCallTone(call);
                  return (
                    <section
                      className={`audit-tool-call audit-tool-call-${tone}`}
                      key={call.id}
                      aria-label={`工具调用 ${call.name}`}
                    >
                      <div className="audit-tool-call-head">
                        <div>
                          <p>{call.operationKind}</p>
                          <h2>{call.name}</h2>
                        </div>
                        <span>{call.status}</span>
                      </div>
                      <dl className="audit-tool-call-details">
                        <div>
                          <dt>目标</dt>
                          <dd>{call.targetSectionId || "未指定"}</dd>
                        </div>
                        <div>
                          <dt>原因</dt>
                          <dd>{call.reason || "无"}</dd>
                        </div>
                        <div>
                          <dt>结果</dt>
                          <dd>{call.resultSummary || "无"}</dd>
                        </div>
                        <div>
                          <dt>拒绝</dt>
                          <dd>{call.rejectionReason ?? "无"}</dd>
                        </div>
                      </dl>
                    </section>
                  );
                }) : (
                  <p className="empty-paper">这次 run 没有工具调用记录。</p>
                )}
              </>
            ) : (
              <section className="empty-paper audit-empty-state">
                <h2>没有审计记录</h2>
                <p>换一个日期，或者先从今日工作流触发 harness run。</p>
              </section>
            )}
          </article>
        </div>
      </section>

      <aside className="assistant-panel today-assistant audit-inspector" aria-label="审计详情">
        <div className="assistant-head">
          <div>
            <p className="assistant-eyebrow">Inspector</p>
            <h2>选中运行详情</h2>
          </div>
          <span className="assistant-time">{selectedRun ? formatAuditTime(selectedRun.createdAt) : "--:--"}</span>
        </div>

        <div className="assistant-body">
          <section className="assistant-card">
            <div className="assistant-card-head">
              <h3>运行摘要</h3>
              <span>{selectedRun?.status ?? "无记录"}</span>
            </div>
            <p>{selectedRun?.summary ?? "当前日期没有可查看的审计记录。"}</p>
          </section>

          <section className="assistant-card">
            <div className="assistant-card-head">
              <h3>工具统计</h3>
              <span>{selectedToolCalls.length} 次</span>
            </div>
            <div className="quiet-metrics" aria-label="审计统计">
              <div className="metric">
                <strong>{selectedToolCalls.length}</strong>
                <span>调用</span>
              </div>
              <div className="metric">
                <strong>{selectedToolCalls.filter(call => !call.rejectionReason && call.status !== "rejected").length}</strong>
                <span>通过</span>
              </div>
              <div className="metric">
                <strong>{selectedToolCalls.filter(call => call.rejectionReason || call.status === "rejected").length}</strong>
                <span>拒绝</span>
              </div>
            </div>
          </section>

          {selectedRun?.errors.length ? (
            <section className="assistant-card attention-panel productized-attention-panel" aria-label="审计错误">
              <div className="assistant-card-head">
                <h3>错误</h3>
                <span>{selectedRun.errors.length} 条</span>
              </div>
              <ul>
                {selectedRun.errors.map(error => <li key={error}>{error}</li>)}
              </ul>
            </section>
          ) : null}
        </div>
      </aside>
    </>
  );
}
