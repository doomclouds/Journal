import { useEffect, useMemo, useState } from "react";
import type {
  JmfSection,
  JmfSectionDefinition,
  JournalBlockEditSection,
  TodayEditorState
} from "./api";
import { InsertBlockMenu } from "./InsertBlockMenu";
import { JournalBlockCard } from "./JournalBlockCard";
import { ValidationPanel } from "./ValidationPanel";

type EditorMode = "blocks" | "source";

type JournalEditorProps = {
  editor: TodayEditorState;
  isBusy: boolean;
  onSaveBlocks: (sections: JournalBlockEditSection[]) => void;
  onSaveSource: (markdown: string) => void;
  onLocalInteraction?: () => void;
};

const jmfSectionCatalogOrder = new Map<string, number>([
  ["raw-inputs", 1],
  ["mood", 2],
  ["yesterday-review", 3],
  ["today-focus", 4],
  ["work", 5],
  ["learning", 6],
  ["health", 7],
  ["relationship", 8],
  ["money", 9],
  ["inspiration", 10],
  ["future-notes", 11],
  ["gratitude", 12],
  ["keywords", 13],
  ["metadata-note", 14]
]);

function createSectionFromDefinition(definition: JmfSectionDefinition): JmfSection {
  return {
    id: definition.id,
    title: definition.title,
    content: "",
    kind: definition.kind,
    isEditableInBlockMode: definition.isEditableInBlockMode
  };
}

function compareSections(
  left: JmfSection,
  right: JmfSection,
  orderById: Map<string, number>
) {
  const leftOrder = orderById.get(left.id) ?? Number.MAX_SAFE_INTEGER;
  const rightOrder = orderById.get(right.id) ?? Number.MAX_SAFE_INTEGER;

  if (leftOrder !== rightOrder) {
    return leftOrder - rightOrder;
  }

  return left.title.localeCompare(right.title, "zh-Hans-CN");
}

export function JournalEditor({
  editor,
  isBusy,
  onSaveBlocks,
  onSaveSource,
  onLocalInteraction
}: JournalEditorProps) {
  const [mode, setMode] = useState<EditorMode>("blocks");
  const [sections, setSections] = useState<JmfSection[]>(editor.sections);
  const [sourceMarkdown, setSourceMarkdown] = useState(editor.markdown);
  const [editingSectionId, setEditingSectionId] = useState<string | null>(null);

  useEffect(() => {
    setSections(editor.sections);
    setSourceMarkdown(editor.markdown);
    setEditingSectionId(null);
  }, [editor]);

  const orderById = useMemo(() => {
    const orders = new Map(jmfSectionCatalogOrder);
    editor.sections.forEach((section, index) => {
      if (!orders.has(section.id)) {
        orders.set(section.id, Number.MAX_SAFE_INTEGER - editor.sections.length + index);
      }
    });
    editor.availableOptionalSections.forEach(section => orders.set(section.id, section.order));
    return orders;
  }, [editor.availableOptionalSections, editor.sections]);

  const insertableSections = useMemo(() => {
    const visibleIds = new Set(sections.map(section => section.id));
    return [...editor.availableOptionalSections]
      .filter(section => !visibleIds.has(section.id))
      .sort((left, right) => left.order - right.order);
  }, [editor.availableOptionalSections, sections]);

  function updateSectionContent(id: string, content: string) {
    onLocalInteraction?.();
    setSections(current =>
      current.map(section => (section.id === id ? { ...section, content } : section))
    );
  }

  function insertSection(definition: JmfSectionDefinition) {
    onLocalInteraction?.();
    setSections(current =>
      [...current, createSectionFromDefinition(definition)]
        .sort((left, right) => compareSections(left, right, orderById))
    );
    setEditingSectionId(definition.id);
  }

  function cancelSection(sectionId: string) {
    const baselineSection = editor.sections.find(section => section.id === sectionId);
    setSections(current =>
      current.map(section =>
        section.id === sectionId
          ? { ...section, content: baselineSection?.content ?? "" }
          : section
      )
    );
    setEditingSectionId(null);
  }

  function saveSection(sectionId: string) {
    const section = sections.find(currentSection => currentSection.id === sectionId);
    if (!section?.isEditableInBlockMode) {
      return;
    }

    onSaveBlocks([{ id: section.id, content: section.content }]);
    setEditingSectionId(null);
  }

  return (
    <section className="journal-editor" aria-label="JMF 编辑器">
      <div className="journal-editor-toolbar">
        <div role="tablist" aria-label="编辑模式">
          <button
            type="button"
            role="tab"
            aria-selected={mode === "blocks"}
            onClick={() => {
              onLocalInteraction?.();
              setMode("blocks");
            }}
          >
            区块模式
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={mode === "source"}
            onClick={() => {
              onLocalInteraction?.();
              setMode("source");
            }}
          >
            源码模式
          </button>
        </div>
        {mode === "source" ? (
          <button
            type="button"
            className="editor-save-action"
            onClick={() => onSaveSource(sourceMarkdown)}
            disabled={isBusy}
          >
            保存源码草稿
          </button>
        ) : null}
      </div>

      <ValidationPanel validation={editor.validation} />

      {mode === "blocks" ? (
        <div className="journal-editor-blocks">
          <InsertBlockMenu
            sections={insertableSections}
            disabled={isBusy}
            onInsert={insertSection}
          />
          {sections.map(section => (
            <JournalBlockCard
              key={section.id}
              section={section}
              value={section.content}
              disabled={isBusy}
              isEditing={editingSectionId === section.id}
              onEdit={() => setEditingSectionId(section.id)}
              onCancel={() => cancelSection(section.id)}
              onChange={content => updateSectionContent(section.id, content)}
              onSave={() => saveSection(section.id)}
            />
          ))}
        </div>
      ) : (
        <div className="journal-editor-source">
          <textarea
            aria-label="编辑完整 JMF Markdown"
            value={sourceMarkdown}
            disabled={isBusy}
            onChange={event => {
              onLocalInteraction?.();
              setSourceMarkdown(event.target.value);
            }}
            rows={14}
          />
        </div>
      )}
    </section>
  );
}
