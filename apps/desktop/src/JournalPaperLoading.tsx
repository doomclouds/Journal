type JournalPaperLoadingProps = {
  label: string;
  title?: string;
  detail?: string;
};

export function JournalPaperLoading({
  label,
  title = "正在铺开这一天的日记",
  detail = "完整内容马上就好。"
}: JournalPaperLoadingProps) {
  return (
    <section className="paper-loading-state" aria-label={label} aria-live="polite">
      <div className="paper-loading-copy">
        <span>读取中</span>
        <strong>{title}</strong>
        <p>{detail}</p>
      </div>
      <div className="paper-loading-lines" aria-hidden="true">
        <span className="wide"></span>
        <span></span>
        <span className="short"></span>
      </div>
    </section>
  );
}
