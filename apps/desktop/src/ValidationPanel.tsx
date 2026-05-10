import type { JmfValidationResult } from "./api";

type ValidationPanelProps = {
  validation: JmfValidationResult;
};

export function ValidationPanel({ validation }: ValidationPanelProps) {
  if (validation.isValid || validation.issues.length === 0) {
    return null;
  }

  return (
    <section className="attention-panel productized-attention-panel" aria-label="需要处理">
      <div className="section-head">
        <h2>这篇草稿需要处理</h2>
        <span>需要处理</span>
      </div>
      <p>正式日记没有被覆盖，原始表达仍然保留。</p>
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
