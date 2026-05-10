import type { JmfSection } from "./api";
import { getSectionDisplayTitle, getSectionKindLabel } from "./todayWorkbenchView";

type JournalBlockCardProps = {
  section: JmfSection;
  value: string;
  disabled: boolean;
  isEditing: boolean;
  onEdit: () => void;
  onCancel: () => void;
  onChange: (content: string) => void;
  onSave: () => void;
};

function renderPreview(sectionId: string, value: string) {
  const lines = value.length > 0 ? value.split(/\r?\n/) : ["这一段还没有内容。"];

  return (
    <div className="journal-block-readonly">
      {lines.map((line, index) => (
        <p key={`${sectionId}-${index}`}>{line || "\u00a0"}</p>
      ))}
    </div>
  );
}

export function JournalBlockCard({
  section,
  value,
  disabled,
  isEditing,
  onEdit,
  onCancel,
  onChange,
  onSave
}: JournalBlockCardProps) {
  const displayTitle = getSectionDisplayTitle(section.id, section.title);
  const kindLabel = getSectionKindLabel(section.id, section.isEditableInBlockMode);

  return (
    <section className="journal-block-card" aria-labelledby={`journal-block-${section.id}`}>
      <div className="section-head">
        <h2 id={`journal-block-${section.id}`}>{displayTitle}</h2>
        <span>{kindLabel}</span>
      </div>

      {!isEditing && renderPreview(section.id, value)}

      {section.isEditableInBlockMode && !isEditing ? (
        <button type="button" disabled={disabled} onClick={onEdit}>
          编辑 {displayTitle}
        </button>
      ) : null}

      {section.isEditableInBlockMode && isEditing ? (
        <div className="journal-block-inline-editor">
          <textarea
            aria-label={`编辑 ${displayTitle}`}
            value={value}
            disabled={disabled}
            onChange={event => onChange(event.target.value)}
            rows={5}
          />
          <div className="journal-block-inline-actions">
            <button type="button" disabled={disabled} onClick={onSave}>
              保存这一段
            </button>
            <button type="button" disabled={disabled} onClick={onCancel}>
              取消
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}
