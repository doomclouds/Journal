import { useState } from "react";
import type { JmfSection } from "./api";
import { MarkdownPreview } from "./MarkdownPreview";
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

function renderPreview(value: string) {
  const previewMarkdown = value.trim().length > 0 ? value : "这一段还没有内容。";

  return (
    <div className="journal-block-readonly">
      <MarkdownPreview markdown={previewMarkdown} />
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
  const isCollapsibleRawInputs = section.id === "raw-inputs";
  const [isRawInputsExpanded, setIsRawInputsExpanded] = useState(!isCollapsibleRawInputs);
  const shouldShowPreview = !isEditing && (!isCollapsibleRawInputs || isRawInputsExpanded);

  return (
    <section className="journal-block-card" aria-labelledby={`journal-block-${section.id}`}>
      <div className="section-head">
        <h2 id={`journal-block-${section.id}`}>{displayTitle}</h2>
        <span>{kindLabel}</span>
      </div>

      {isCollapsibleRawInputs && !isEditing ? (
        <button
          type="button"
          className="raw-inputs-toggle"
          aria-expanded={isRawInputsExpanded}
          onClick={() => setIsRawInputsExpanded(current => !current)}
        >
          {isRawInputsExpanded ? "收起" : "展开"} {displayTitle}
        </button>
      ) : null}

      {shouldShowPreview && renderPreview(value)}

      {section.isEditableInBlockMode && !isEditing ? (
        <button type="button" className="edit-chip" aria-label={`编辑 ${displayTitle}`} disabled={disabled} onClick={onEdit}>
          编辑
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
            <button type="button" disabled={disabled} onClick={onCancel}>
              取消
            </button>
            <button type="button" disabled={disabled} onClick={onSave}>
              保存修改
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}
