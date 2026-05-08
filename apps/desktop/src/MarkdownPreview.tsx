import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

type MarkdownPreviewProps = {
  markdown: string;
};

function stripJmfMetadata(markdown: string): string {
  return markdown
    .replace(/^---[ \t]*\r?\n[\s\S]*?\r?\n---[ \t]*(?:\r?\n|$)/, "")
    .replace(/<!--\s*\/?journal:section\b[^>]*-->\s*/g, "");
}

export function MarkdownPreview({ markdown }: MarkdownPreviewProps) {
  const previewMarkdown = stripJmfMetadata(markdown);

  return (
    <div className="markdown-preview" data-testid="markdown-preview">
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{previewMarkdown}</ReactMarkdown>
    </div>
  );
}
