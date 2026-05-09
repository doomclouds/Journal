import type { JmfSection } from "./api";

type JournalBlockCardProps = {
  section: JmfSection;
  value: string;
  onChange: (content: string) => void;
};

export function JournalBlockCard({ section, value, onChange }: JournalBlockCardProps) {
  return (
    <section className="journal-block-card" aria-labelledby={`journal-block-${section.id}`}>
      <div className="section-head">
        <h2 id={`journal-block-${section.id}`}>{section.title}</h2>
        <span>{section.kind}</span>
      </div>
      {section.isEditableInBlockMode ? (
        <textarea
          aria-label={`编辑 ${section.title}`}
          value={value}
          onChange={event => onChange(event.target.value)}
          rows={8}
        />
      ) : (
        <div className="journal-block-readonly">
          {value.split(/\r?\n/).map((line, index) => (
            <p key={`${section.id}-${index}`}>{line || "\u00a0"}</p>
          ))}
        </div>
      )}
    </section>
  );
}
