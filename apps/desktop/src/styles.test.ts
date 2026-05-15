/// <reference types="node" />

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, test } from "vitest";

describe("LLM settings responsive styles", () => {
  const css = readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), "styles.css"), "utf8");

  test("renders settings as a compact centered modal instead of a full-screen sheet", () => {
    expect(css).toMatch(/\.llm-settings-backdrop\s*\{[^}]*position:\s*fixed;[^}]*inset:\s*0;[^}]*z-index:\s*9;[^}]*background:\s*rgba\(36,\s*33,\s*29,\s*0\.24\);/s);
    expect(css).toMatch(/\.llm-settings-overlay\s*\{[^}]*top:\s*50%;[^}]*left:\s*50%;[^}]*width:\s*min\(1040px,\s*calc\(100vw\s*-\s*96px\)\);[^}]*height:\s*min\(720px,\s*calc\(100vh\s*-\s*72px\)\);[^}]*transform:\s*translate\(-50%,\s*-50%\);/s);
    expect(css).not.toMatch(/\.llm-settings-overlay\s*\{[^}]*inset:\s*10px;/s);
    expect(css).toMatch(/\.llm-settings-head\s*\{[^}]*min-height:\s*48px;[^}]*padding:\s*0\s+12px\s+0\s+16px;/s);
  });

  test("uses compact icon controls for modal actions and advanced disclosure", () => {
    expect(css).toMatch(/\.llm-settings-actions\s*\{[^}]*justify-content:\s*flex-end;[^}]*gap:\s*7px;/s);
    expect(css).toMatch(/\.icon-action\s*\{[^}]*width:\s*36px;[^}]*height:\s*36px;[^}]*border-radius:\s*999px;/s);
    expect(css).toMatch(/\.llm-primary-icon\s*\{[^}]*background:\s*#2b6860;[^}]*color:\s*#fffdf8;/s);
    expect(css).toMatch(/\.llm-advanced-chevron\s*\{[^}]*display:\s*inline-grid;[^}]*place-items:\s*center;/s);
  });

  test("presents advanced LLM parameters as productized runtime chips", () => {
    expect(css).toMatch(/\.llm-runtime-card\s*\{[^}]*display:\s*grid;[^}]*gap:\s*10px;/s);
    expect(css).toMatch(/\.llm-runtime-summary\s*\{[^}]*display:\s*flex;[^}]*align-items:\s*center;[^}]*justify-content:\s*space-between;/s);
    expect(css).toMatch(/\.llm-runtime-chips\s*\{[^}]*display:\s*flex;[^}]*flex-wrap:\s*wrap;/s);
    expect(css).toMatch(/\.llm-runtime-chip\s*\{[^}]*border-radius:\s*999px;[^}]*background:\s*rgba\(230,\s*243,\s*235,\s*0\.72\);/s);
    expect(css).toMatch(/\.llm-json-mode-status\s*\{[^}]*min-height:\s*34px;[^}]*display:\s*flex;[^}]*align-items:\s*center;/s);
  });

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

    return blocks.join("\n").replace(/\r\n/g, "\n");
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
    expect(css).toContain("grid-template-columns: 250px minmax(640px, 1fr) minmax(300px, 0.46fr);");
    expect(css).toContain('grid-template-areas: "rail paper assistant";');
    expect(css).toMatch(/\.command-workspace\.journal-only\s*\{[^}]*grid-template-columns:\s*250px\s+minmax\(760px,\s*1fr\);/s);
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
    expect(css).toMatch(/\.compose-bar\s*\{[^}]*border-top:\s*1px\s+solid\s+rgba\(52,\s*45,\s*36,\s*0\.1\);[^}]*background:\s*rgba\(238,\s*236,\s*228,\s*0\.58\);[^}]*padding:\s*10px\s+22px;/s);
    expect(css).toMatch(/\.productized-journal-stage\s+\.compose-bar\s*\{[^}]*background:\s*rgba\(238,\s*236,\s*228,\s*0\.58\);[^}]*box-shadow:\s*none;/s);
    expect(css).toMatch(/\.compose-input-card\s*\{[^}]*border:\s*1px\s+solid\s+rgba\(52,\s*45,\s*36,\s*0\.11\);[^}]*border-radius:\s*10px;[^}]*background:\s*rgba\(238,\s*236,\s*228,\s*0\.48\);[^}]*box-shadow:\s*none;/s);
    expect(css).toMatch(/\.compose-input-card:focus-within\s*\{[^}]*border-color:\s*rgba\(47,\s*111,\s*95,\s*0\.26\);[^}]*background:\s*rgba\(242,\s*239,\s*231,\s*0\.86\);/s);
    expect(css).toMatch(/\.compose-bar\s+textarea\s*\{[^}]*min-height:\s*42px;[^}]*max-height:\s*120px;[^}]*border:\s*0;[^}]*padding:\s*9px\s+12px\s+5px;/s);
    expect(css).toMatch(/\.compose-toolbar\s*\{[^}]*display:\s*flex;[^}]*min-height:\s*32px;[^}]*border-top:\s*1px\s+solid\s+rgba\(52,\s*45,\s*36,\s*0\.07\);/s);
    expect(css).toMatch(/\.compose-icon-button\s*\{[^}]*width:\s*32px;[^}]*height:\s*32px;[^}]*border-radius:\s*8px;/s);
    expect(css).toMatch(/\.compose-send-action\s*\{[^}]*border-radius:\s*999px;[^}]*background:\s*var\(--sage\);/s);
    expect(css).toMatch(/\.confirm-overlay\s*\{[^}]*position:\s*fixed;[^}]*place-items:\s*center;/s);
    expect(css).toMatch(/\.confirm-dialog\s*\{[^}]*width:\s*min\(420px,\s*100%\);[^}]*border-radius:\s*10px;/s);
    expect(css).toMatch(/\.confirm-actions\s*\{[^}]*justify-content:\s*flex-end;[^}]*gap:\s*10px;/s);
  });

  test("keeps left rail metadata quiet instead of rendering it like a button", () => {
    expect(css).toMatch(/\.context-rail\s+\.section-head\s+span\s*\{[^}]*min-height:\s*0;[^}]*border:\s*0;[^}]*border-radius:\s*0;[^}]*background:\s*transparent;[^}]*padding:\s*0;/s);
    expect(css).toMatch(/\.rail-count\s*\{[^}]*font-variant-numeric:\s*tabular-nums;[^}]*font-size:\s*10px;[^}]*font-weight:\s*500;[^}]*color:\s*rgba\(125,\s*118,\s*107,\s*0\.36\);/s);
    expect(css).toMatch(/\.command-workspace\s+\.context-rail\s*\{[^}]*background:[^}]*#f2efe7;/s);
    expect(css).toMatch(/\.source-stack\s*\{[^}]*display:\s*grid;[^}]*gap:\s*9px;/s);
    expect(css).toMatch(/\.source-item\.is-active\s*\{[^}]*border-color:\s*rgba\(181,\s*127,\s*48,\s*0\.26\);/s);
    expect(css).toMatch(/\.source-meta\s+span:first-child\s*\{[^}]*color:\s*var\(--gold\);/s);
  });

  test("keeps today workbench scrollbars quiet until users interact", () => {
    expect(css).toMatch(
      /\.command-workspace\s+\.context-rail,\s*\.document-scroll,\s*\.assistant-body\s*\{[^}]*scrollbar-width:\s*thin;[^}]*scrollbar-color:\s*transparent\s+transparent;[^}]*scrollbar-gutter:\s*stable;/s
    );
    expect(css).toMatch(
      /\.command-workspace\s+\.context-rail:hover,[^}]*\.assistant-body:focus-within\s*\{[^}]*scrollbar-color:\s*rgba\(52,\s*45,\s*36,\s*0\.26\)\s+transparent;/s
    );
    expect(css).toMatch(
      /\.command-workspace\s+\.context-rail::-webkit-scrollbar-thumb,[^}]*\.assistant-body::-webkit-scrollbar-thumb\s*\{[^}]*background-color:\s*transparent;[^}]*background-clip:\s*content-box;/s
    );
    expect(css).toMatch(
      /\.command-workspace\s+\.context-rail:hover::-webkit-scrollbar-thumb,[^}]*\.assistant-body:focus-within::-webkit-scrollbar-thumb\s*\{[^}]*background-color:\s*rgba\(52,\s*45,\s*36,\s*0\.26\);/s
    );
  });

  test("keeps next step as a passive status card without action-like controls", () => {
    expect(css).not.toMatch(/\.next-actions\s*\{/);
    expect(css).toMatch(/\.next-panel\s*\{[^}]*border:\s*1px\s+solid\s+rgba\(181,\s*127,\s*48,\s*0\.13\);[^}]*border-radius:\s*10px;[^}]*background:\s*rgba\(255,\s*250,\s*240,\s*0\.62\);/s);
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
    expect(css).toMatch(/\.evidence-item::before\s*\{[^}]*position:\s*absolute;[^}]*top:\s*0;[^}]*bottom:\s*0;[^}]*left:\s*0;[^}]*width:\s*4px;[^}]*border-radius:\s*999px;/s);
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
