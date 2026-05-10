/// <reference types="node" />

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, test } from "vitest";

describe("LLM settings responsive styles", () => {
  const css = readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), "styles.css"), "utf8");

  test("uses a single scroll container in narrow layouts", () => {
    expect(css).toMatch(/body:has\(\.llm-settings-overlay\)\s*\{[^}]*overflow:\s*hidden;/s);
    expect(css).toMatch(/@media\s*\(max-width:\s*1180px\)/);
    expect(css).toMatch(/\.llm-settings-grid\s*\{[^}]*overflow:\s*auto;/s);
    expect(css).toMatch(
      /\.llm-provider-list,\s*\.llm-settings-main,\s*\.llm-settings-side\s*\{[^}]*overflow:\s*visible;/s
    );
  });
});

describe("Today workbench productized CSS contract", () => {
  const css = readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), "styles.css"), "utf8");

  function getMediaCss(maxWidth: number) {
    const mediaHeader = new RegExp(`@media\\s*\\(max-width:\\s*${maxWidth}px\\)\\s*\\{`, "g");
    const blocks: string[] = [];
    let match: RegExpExecArray | null;

    while ((match = mediaHeader.exec(css))) {
      let depth = 0;
      let end = match.index;

      for (; end < css.length; end += 1) {
        if (css[end] === "{") {
          depth += 1;
        } else if (css[end] === "}") {
          depth -= 1;
          if (depth === 0) {
            end += 1;
            break;
          }
        }
      }

      blocks.push(css.slice(match.index, end));
    }

    return blocks.join("\n");
  }

  test("defines command surface shell regions", () => {
    expect(css).toMatch(/\.desktop-shell\s*\{/);
    expect(css).not.toMatch(/\.app-window\s*\{/);
    expect(css).not.toMatch(/\.titlebar\s*\{/);
    expect(css).not.toMatch(/\.window-controls\s*\{/);
    expect(css).not.toMatch(/\.menubar\s*\{/);
    expect(css).not.toMatch(/\.menu-panel\s*\{/);
    expect(css).toMatch(/\.command-workspace\s*\{/);
    expect(css).toMatch(/\.context-rail\s*\{/);
    expect(css).toMatch(/\.stage-toolbar\s*\{/);
    expect(css).toMatch(/\.document-scroll\s*\{/);
    expect(css).toMatch(/\.assistant-head\s*\{/);
    expect(css).toMatch(/\.today-assistant\s*\{/);
    expect(css).toMatch(/\.compose-bar\s*\{/);
  });

  test("uses the accepted three-column command surface layout", () => {
    expect(css).toContain("grid-template-columns: 260px minmax(520px, 1fr) minmax(360px, 0.72fr);");
    expect(css).toContain('grid-template-areas: "rail paper assistant";');
    expect(css).toMatch(/@media\s*\(max-width:\s*1040px\)/);
    expect(css).toMatch(/@media\s*\(max-width:\s*820px\)/);
  });

  test("keeps feedback messages in a dedicated row before the workbench", () => {
    expect(css).toMatch(/\.desktop-shell\s*\{[^}]*grid-template-rows:\s*auto\s+auto\s+minmax\(0,\s*1fr\);/s);
    expect(css).toMatch(/\.feedback-row\s*\{/);
    expect(css).toMatch(/\.feedback-row:empty\s*\{[^}]*display:\s*none;/s);
    expect(css).toMatch(/\.command-workspace\s*\{[^}]*grid-row:\s*3;/s);
  });

  test("does not expose advanced source drawer styles in the daily workbench", () => {
    expect(css).not.toMatch(/\.journal-source-drawer\s*\{/);
    expect(css).not.toMatch(/\.journal-editor-source\s*\{/);
  });

  test("styles existing inline block preview and editing states", () => {
    expect(css).toMatch(/\.journal-block-card\s*\{[^}]*display:\s*grid;[^}]*border:\s*0;[^}]*background:\s*transparent;/s);
    expect(css).toMatch(/\.journal-block-readonly\s*\{[^}]*border:\s*0;[^}]*background:\s*transparent;[^}]*line-height:\s*1\.95;/s);
    expect(css).toMatch(/\.edit-chip\s*\{[^}]*position:\s*absolute;[^}]*border-radius:\s*999px;/s);
    expect(css).toMatch(/\.journal-block-inline-editor\s*\{[^}]*border-left:\s*4px\s+solid\s+var\(--sage\);[^}]*background:\s*#eff7f1;/s);
    expect(css).toMatch(/\.journal-block-inline-actions\s*\{[^}]*display:\s*flex;[^}]*justify-content:\s*flex-end;[^}]*gap:\s*10px;/s);
  });

  test("collapses without nested scroll traps on tablet and phone widths", () => {
    const tabletCss = getMediaCss(1040);
    const phoneCss = getMediaCss(820);

    expect(tabletCss).toMatch(/\.desktop-shell\s*\{[^}]*overflow:\s*visible;/s);
    expect(tabletCss).toContain('grid-template-areas:\n      "rail paper"\n      "assistant assistant";');
    expect(tabletCss).toMatch(/\.command-workspace\s+\.assistant-panel\.today-assistant\s*\{[^}]*border-left:\s*0;/s);
    expect(phoneCss).toContain('grid-template-areas:\n      "paper"\n      "rail"\n      "assistant";');
    expect(phoneCss).toMatch(/\.compose-bar,\s*\.compose-bar\s+form\s*\{[^}]*grid-template-columns:\s*1fr;/s);
  });
});
