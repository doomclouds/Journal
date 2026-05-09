import type { JmfValidationResult } from "./api";

type ValidationPanelProps = {
  validation: JmfValidationResult;
};

export function ValidationPanel({ validation }: ValidationPanelProps) {
  if (validation.isValid || validation.issues.length === 0) {
    return null;
  }

  return (
    <section className="attention-panel" aria-label="JMF 校验问题">
      <div className="section-head">
        <h2>需要处理</h2>
        <span>attention</span>
      </div>
      <ul>
        {validation.issues.map(issue => (
          <li key={`${issue.code}-${issue.message}`}>
            <strong>{issue.message}</strong>
            <p>{issue.repairHint}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
