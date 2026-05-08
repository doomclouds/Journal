import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test } from "vitest";
import { MarkdownPreview } from "./MarkdownPreview";

afterEach(() => {
  cleanup();
});

describe("MarkdownPreview", () => {
  test("hides JMF metadata while preserving journal content", () => {
    const markdown = `---
schema: journal.v3
provider: jmf
---

# 2026-05-08

<!-- journal:section raw-inputs -->
## Raw Inputs

- 今天完成 Today Workbench 预览修复
<!-- /journal:section raw-inputs -->

<!-- journal:section today-focus -->
## Today Focus

- 保持日记纸面为主
<!-- /journal:section today-focus -->
`;

    render(<MarkdownPreview markdown={markdown} />);

    const preview = screen.getByTestId("markdown-preview");
    expect(screen.getByRole("heading", { name: "2026-05-08" })).toBeInTheDocument();
    expect(screen.getByText("今天完成 Today Workbench 预览修复")).toBeInTheDocument();
    expect(screen.getByText("保持日记纸面为主")).toBeInTheDocument();
    expect(preview).not.toHaveTextContent("schema:");
    expect(preview).not.toHaveTextContent("provider:");
    expect(preview).not.toHaveTextContent("journal:section");
  });
});
