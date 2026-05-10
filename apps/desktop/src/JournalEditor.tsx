import { useEffect, useMemo, useRef, useState } from "react";
import type {
  JmfSection,
  JmfSectionDefinition,
  JournalBlockEditSection,
  TodayEditorState
} from "./api";
import { InsertBlockMenu } from "./InsertBlockMenu";
import { JournalBlockCard } from "./JournalBlockCard";
import { ValidationPanel } from "./ValidationPanel";

type JournalEditorProps = {
  editor: TodayEditorState;
  isBusy: boolean;
  onSaveBlocks: (sections: JournalBlockEditSection[]) => void;
  onSaveSource: (markdown: string) => void;
  onLocalInteraction?: () => void;
  onDirtyChange?: (isDirty: boolean) => void;
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
  onLocalInteraction,
  onDirtyChange
}: JournalEditorProps) {
  const [isSourceOpen, setIsSourceOpen] = useState(false);
  const [sections, setSections] = useState<JmfSection[]>(editor.sections);
  const [sourceMarkdown, setSourceMarkdown] = useState(editor.markdown);
  const [editingSectionId, setEditingSectionId] = useState<string | null>(null);
  const previousEditorRef = useRef<TodayEditorState | null>(null);

  useEffect(() => {
    setSections(editor.sections);
    setSourceMarkdown(editor.markdown);
    setEditingSectionId(null);
    if (previousEditorRef.current) {
      setIsSourceOpen(false);
    }
    previousEditorRef.current = editor;
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

  const hasDirtyEditingSection = useMemo(() => {
    if (!editingSectionId) {
      return false;
    }

    const currentSection = sections.find(section => section.id === editingSectionId);
    if (!currentSection?.isEditableInBlockMode) {
      return false;
    }

    const baselineSection = editor.sections.find(section => section.id === editingSectionId);
    if (!baselineSection) {
      return currentSection.content.length > 0;
    }

    return currentSection.content !== baselineSection.content;
  }, [editingSectionId, editor.sections, sections]);
  const hasDirtySource = sourceMarkdown !== editor.markdown;
  const hasLocalDirty = hasDirtyEditingSection || hasDirtySource;

  useEffect(() => {
    onDirtyChange?.(hasLocalDirty);
  }, [hasLocalDirty, onDirtyChange]);

  const isSourceSaveDisabled = isBusy || hasDirtyEditingSection;
  const areBlockSwitchActionsDisabled = isBusy || hasLocalDirty;

  function resetSectionToBaseline(current: JmfSection[], sectionId: string) {
    const baselineSection = editor.sections.find(section => section.id === sectionId);

    if (!baselineSection) {
      return current.filter(section => section.id !== sectionId);
    }

    return current.map(section =>
      section.id === sectionId
        ? { ...section, content: baselineSection.content }
        : section
    );
  }

  function resetCurrentEditingSection(current: JmfSection[]) {
    return editingSectionId ? resetSectionToBaseline(current, editingSectionId) : current;
  }

  function editSection(sectionId: string) {
    if (hasLocalDirty && editingSectionId !== sectionId) {
      return;
    }

    setSections(current =>
      editingSectionId && editingSectionId !== sectionId
        ? resetSectionToBaseline(current, editingSectionId)
        : current
    );
    setEditingSectionId(sectionId);
  }

  function updateSectionContent(id: string, content: string) {
    onLocalInteraction?.();
    setSections(current =>
      current.map(section => (section.id === id ? { ...section, content } : section))
    );
  }

  function insertSection(definition: JmfSectionDefinition) {
    if (hasLocalDirty) {
      return;
    }

    onLocalInteraction?.();
    setSections(current =>
      [...resetCurrentEditingSection(current), createSectionFromDefinition(definition)]
        .sort((left, right) => compareSections(left, right, orderById))
    );
    setEditingSectionId(definition.id);
  }

  function cancelSection(sectionId: string) {
    setSections(current => resetSectionToBaseline(current, sectionId));
    setEditingSectionId(null);
  }

  function saveSection(sectionId: string) {
    const section = sections.find(currentSection => currentSection.id === sectionId);
    if (!section?.isEditableInBlockMode) {
      return;
    }

    onSaveBlocks([{ id: section.id, content: section.content }]);
  }

  function saveSourceMarkdown() {
    if (hasDirtyEditingSection) {
      return;
    }

    onSaveSource(sourceMarkdown);
  }

  return (
    <section className="journal-editor" aria-label="JMF 编辑器">
      <div className="journal-editor-toolbar productized-editor-toolbar">
        <div>
          <span className="eyebrow">日记纸面</span>
          <p>默认阅读，点击段落即可编辑。</p>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={() => {
            onLocalInteraction?.();
            setIsSourceOpen(current => !current);
          }}
        >
          {isSourceOpen ? "收起高级源码" : "展开高级源码"}
        </button>
      </div>

      <ValidationPanel validation={editor.validation} />

      <div className="journal-editor-blocks">
        <InsertBlockMenu
          sections={insertableSections}
          disabled={areBlockSwitchActionsDisabled}
          onInsert={insertSection}
        />
        {sections.map(section => (
          <JournalBlockCard
            key={section.id}
            section={section}
            value={section.content}
            disabled={isBusy || (hasLocalDirty && editingSectionId !== section.id)}
            isEditing={editingSectionId === section.id}
            onEdit={() => editSection(section.id)}
            onCancel={() => cancelSection(section.id)}
            onChange={content => updateSectionContent(section.id, content)}
            onSave={() => saveSection(section.id)}
          />
        ))}
      </div>

      {isSourceOpen ? (
        <section className="journal-source-drawer" aria-label="高级 JMF 源码">
          <div className="source-drawer-head">
            <div>
              <h2>高级：JMF 源码</h2>
              <p>这里显示 front matter、JMF marker 和技术细节。</p>
            </div>
          </div>
          <textarea
            aria-label="编辑完整 JMF Markdown"
            value={sourceMarkdown}
            disabled={isBusy || hasDirtyEditingSection}
            onChange={event => {
              onLocalInteraction?.();
              setSourceMarkdown(event.target.value);
            }}
            rows={14}
          />
          <div className="source-actions">
            {hasDirtyEditingSection ? (
              <p className="source-save-hint">先保存或取消正在编辑的段落，再保存源码。</p>
            ) : null}
            <button
              type="button"
              className="editor-save-action"
              onClick={saveSourceMarkdown}
              disabled={isSourceSaveDisabled}
            >
              保存源码草稿
            </button>
          </div>
        </section>
      ) : null}
    </section>
  );
}
