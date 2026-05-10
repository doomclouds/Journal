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

  function getNarrowLayoutCss() {
    const mediaHeader = /@media\s*\(max-width:\s*1180px\)\s*\{/g;
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

  function getRuleBody(selectorPattern: string, stylesheet = css) {
    const match = new RegExp(`${selectorPattern}\\s*\\{([^}]*)\\}`, "s").exec(stylesheet);
    expect(match).not.toBeNull();
    return match?.[1] ?? "";
  }

  test("defines the main productized workbench regions", () => {
    expect(css).toMatch(/\.productized-workspace\s*\{/);
    expect(css).toMatch(/\.today-assistant\s*\{/);
    expect(css).toMatch(/\.compose-bar\s*\{/);
    expect(css).toMatch(/\.journal-source-drawer\s*\{/);
  });

  test("uses a dark monospace advanced source drawer", () => {
    const drawer = getRuleBody("\\.journal-source-drawer");
    const textarea = getRuleBody("\\.journal-source-drawer\\s+textarea");

    expect(drawer).toMatch(/background:\s*#151a20;/);
    expect(drawer).toMatch(/color:\s*#e9eef3;/);
    expect(drawer).toMatch(/border:\s*1px\s+solid\s+rgba\(174,\s*190,\s*204,\s*0\.2\);/);
    expect(textarea).toMatch(/background:\s*#0d1117;/);
    expect(textarea).toMatch(/color:\s*#edf4fa;/);
    expect(textarea).toMatch(/font-family:[^;}]*monospace;/);
  });

  test("styles existing inline block preview and editing states", () => {
    const editingCard = getRuleBody("\\.journal-block-card:has\\(\\.journal-block-inline-editor\\)");
    const readonly = getRuleBody("\\.journal-block-readonly");
    const inlineEditor = getRuleBody("\\.journal-block-inline-editor");
    const inlineActions = getRuleBody("\\.journal-block-inline-actions");

    expect(editingCard).toMatch(/border-color:\s*rgba\(47,\s*111,\s*95,\s*0\.34\);/);
    expect(editingCard).toMatch(/background:\s*#f7fbf7;/);
    expect(editingCard).toMatch(/box-shadow:\s*0\s+12px\s+28px\s+rgba\(47,\s*111,\s*95,\s*0\.1\);/);
    expect(readonly).toMatch(/min-height:\s*72px;/);
    expect(readonly).toMatch(/background:\s*#fbfaf5;/);
    expect(readonly).toMatch(/color:\s*#3a352e;/);
    expect(readonly).toMatch(/line-height:\s*1\.65;/);
    expect(readonly).toMatch(/overflow-wrap:\s*anywhere;/);
    expect(inlineEditor).toMatch(/display:\s*grid;/);
    expect(inlineEditor).toMatch(/border-left:\s*4px\s+solid\s+#2f6f5f;/);
    expect(inlineEditor).toMatch(/background:\s*#eff8f2;/);
    expect(inlineEditor).toMatch(/padding:\s*12px;/);
    expect(inlineActions).toMatch(/display:\s*flex;/);
    expect(inlineActions).toMatch(/flex-wrap:\s*wrap;/);
    expect(inlineActions).toMatch(/justify-content:\s*flex-end;/);
    expect(inlineActions).toMatch(/gap:\s*8px;/);
  });

  test("collapses to one primary scroll column below 1180px", () => {
    const narrowLayoutCss = getNarrowLayoutCss();

    expect(narrowLayoutCss).toMatch(/\.productized-workspace\s*\{[^}]*grid-template-columns:\s*1fr;/s);
    expect(narrowLayoutCss).toMatch(/\.today-assistant\s*\{[^}]*overflow:\s*visible;/s);
    expect(narrowLayoutCss).toMatch(/\.compose-bar,\s*\.compose-bar\s+form\s*\{[^}]*grid-template-columns:\s*1fr;/s);
  });
});
