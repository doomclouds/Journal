import type { JmfSectionDefinition } from "./api";

type InsertBlockMenuProps = {
  sections: JmfSectionDefinition[];
  disabled?: boolean;
  onInsert: (section: JmfSectionDefinition) => void;
};

export function InsertBlockMenu({ sections, disabled = false, onInsert }: InsertBlockMenuProps) {
  if (sections.length === 0) {
    return null;
  }

  return (
    <section className="insert-block-menu" aria-label="可插入区块">
      {sections.map(section => (
        <button
          key={section.id}
          type="button"
          disabled={disabled}
          onClick={() => onInsert(section)}
        >
          插入 {section.title}
        </button>
      ))}
    </section>
  );
}
