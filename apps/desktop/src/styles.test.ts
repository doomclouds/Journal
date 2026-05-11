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
    expect(css).toMatch(/\.command-workspace\.journal-only\s*\{[^}]*grid-template-columns:\s*260px\s+minmax\(620px,\s*1fr\);/s);
    expect(css).toMatch(/\.command-workspace\.journal-only\s*\{[^}]*grid-template-areas:\s*"rail paper";/s);
    expect(css).toMatch(/@media\s*\(max-width:\s*1040px\)/);
    expect(css).toMatch(/@media\s*\(max-width:\s*820px\)/);
  });

  test("keeps feedback messages in a dedicated row before the workbench", () => {
    expect(css).toMatch(/\.desktop-shell\s*\{[^}]*grid-template-rows:\s*auto\s+auto\s+minmax\(0,\s*1fr\);/s);
    expect(css).toMatch(/\.feedback-row\s*\{/);
    expect(css).toMatch(/\.feedback-row:empty\s*\{[^}]*display:\s*none;/s);
    expect(css).toMatch(/\.command-workspace\s*\{[^}]*grid-row:\s*3;/s);
  });

  test("keeps only the top API health indicator and removes duplicate LLM status chips", () => {
    expect(css).toMatch(/\.api-health-dot\s*\{[^}]*width:\s*10px;[^}]*height:\s*10px;/s);
    expect(css).toMatch(/\.api-health-ok\s*\{[^}]*background:\s*var\(--sage\);/s);
    expect(css).toMatch(/\.api-health-error\s*\{[^}]*background:\s*#a84e42;/s);
    expect(css).not.toMatch(/\.llm-status-pill/);
    expect(css).not.toMatch(/\.llm-status-dot/);
    expect(css).not.toMatch(/\.assistant-meta-status/);
    expect(css).toMatch(/\.assistant-meta-provider\s*\{[^}]*display:\s*inline-flex;/s);
    expect(css).toMatch(/\.assistant-meta-provider\s+\.assistant-meta-dot\s*\{[^}]*background:\s*var\(--sage\);/s);
  });

  test("keeps the bottom input compact and uses a confirmation dialog for regeneration", () => {
    expect(css).not.toMatch(/\.compose-hint\s*\{/);
    expect(css).not.toMatch(/\.compose-bar\s+label\s*\{/);
    expect(css).toMatch(/\.compose-bar\s*\{[^}]*padding:\s*10px\s+22px;/s);
    expect(css).toMatch(/\.compose-input-card\s*\{[^}]*border:\s*1px\s+solid\s+rgba\(52,\s*45,\s*36,\s*0\.16\);[^}]*border-radius:\s*14px;/s);
    expect(css).toMatch(/\.compose-bar\s+textarea\s*\{[^}]*min-height:\s*52px;[^}]*border:\s*0;/s);
    expect(css).toMatch(/\.compose-toolbar\s*\{[^}]*display:\s*flex;[^}]*min-height:\s*36px;[^}]*border-top:\s*1px\s+solid\s+rgba\(52,\s*45,\s*36,\s*0\.08\);/s);
    expect(css).toMatch(/\.compose-icon-button\s*\{[^}]*width:\s*32px;[^}]*height:\s*32px;[^}]*border-radius:\s*8px;/s);
    expect(css).toMatch(/\.compose-send-action\s*\{[^}]*border-radius:\s*999px;[^}]*background:\s*var\(--sage\);/s);
    expect(css).toMatch(/\.confirm-overlay\s*\{[^}]*position:\s*fixed;[^}]*place-items:\s*center;/s);
    expect(css).toMatch(/\.confirm-dialog\s*\{[^}]*width:\s*min\(420px,\s*100%\);[^}]*border-radius:\s*10px;/s);
    expect(css).toMatch(/\.confirm-actions\s*\{[^}]*justify-content:\s*flex-end;[^}]*gap:\s*10px;/s);
  });

  test("keeps left rail metadata quiet instead of rendering it like a button", () => {
    expect(css).toMatch(/\.context-rail\s+\.section-head\s+span\s*\{[^}]*min-height:\s*0;[^}]*border:\s*0;[^}]*border-radius:\s*0;[^}]*background:\s*transparent;[^}]*padding:\s*0;/s);
    expect(css).toMatch(/\.rail-count\s*\{[^}]*font-variant-numeric:\s*tabular-nums;[^}]*font-size:\s*10px;[^}]*font-weight:\s*500;[^}]*color:\s*rgba\(125,\s*118,\s*107,\s*0\.36\);/s);
  });

  test("keeps next step as a passive status card without action-like controls", () => {
    expect(css).not.toMatch(/\.next-actions\s*\{/);
    expect(css).toMatch(/\.next-step-card\s*\{[^}]*box-shadow:\s*none;/s);
    expect(css).toMatch(/\.next-step-title\s*\{[^}]*align-items:\s*flex-start;/s);
    expect(css).toMatch(/\.next-step-title\s+\.status-dot\s*\{[^}]*margin-top:\s*5px;/s);
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

  test("uses rounded quote accents instead of plain straight borders", () => {
    expect(css).toMatch(/\.material-item::before\s*\{[^}]*position:\s*absolute;[^}]*top:\s*0;[^}]*bottom:\s*0;[^}]*left:\s*0;[^}]*width:\s*4px;[^}]*border-radius:\s*999px;/s);
    expect(css).not.toMatch(/\.raw-fold::before/);
    expect(css).not.toMatch(/\.raw-body::before/);
    expect(css).toMatch(/\.llm-provider-avatar,\s*\.llm-current-avatar\s*\{[^}]*display:\s*inline-grid;[^}]*place-items:\s*center;[^}]*border-radius:\s*999px;/s);
    expect(css).toMatch(/\.llm-provider-avatar\s*\{[^}]*width:\s*34px;[^}]*height:\s*34px;[^}]*font-size:\s*13px;/s);
    expect(css).toMatch(/\.provider-tone-openai\s*\{[^}]*background:\s*#b57f30;/s);
    expect(css).toMatch(/\.provider-tone-custom\s*\{[^}]*background:\s*#a65349;/s);
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
