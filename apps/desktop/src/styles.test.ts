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

  test("defines the main productized workbench regions", () => {
    expect(css).toMatch(/\.productized-workspace\s*\{/);
    expect(css).toMatch(/\.today-assistant\s*\{/);
    expect(css).toMatch(/\.compose-bar\s*\{/);
    expect(css).toMatch(/\.journal-source-drawer\s*\{/);
  });

  test("uses a dark monospace advanced source drawer", () => {
    expect(css).toMatch(/\.journal-source-drawer\s*\{[^}]*background:\s*#[0-9a-fA-F]{3,6}/s);
    expect(css).toMatch(/\.journal-source-drawer\s+textarea\s*\{[^}]*background:\s*#[0-9a-fA-F]{3,6}/s);
    expect(css).toMatch(/\.journal-source-drawer\s+textarea\s*\{[^}]*color:\s*#[0-9a-fA-F]{3,6}/s);
    expect(css).toMatch(/\.journal-source-drawer\s+textarea\s*\{[^}]*font-family:[^;}]*monospace/s);
  });

  test("styles existing inline block preview and editing states", () => {
    expect(css).toMatch(/\.journal-block-readonly\s*\{/);
    expect(css).toMatch(/\.journal-block-inline-editor\s*\{/);
    expect(css).toMatch(/\.journal-block-inline-actions\s*\{/);
  });

  test("collapses to one primary scroll column below 1180px", () => {
    const narrowLayoutCss = getNarrowLayoutCss();

    expect(narrowLayoutCss).toMatch(/\.productized-workspace\s*\{[^}]*grid-template-columns:\s*1fr;/s);
    expect(narrowLayoutCss).toMatch(/\.today-assistant\s*\{[^}]*overflow:\s*visible;/s);
    expect(narrowLayoutCss).toMatch(/\.compose-bar,\s*\.compose-bar\s+form\s*\{[^}]*grid-template-columns:\s*1fr;/s);
  });
});
