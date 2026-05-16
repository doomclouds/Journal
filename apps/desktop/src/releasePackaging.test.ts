import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, test } from "vitest";

const thisFile = fileURLToPath(import.meta.url);
const repoRoot = resolve(dirname(thisFile), "../../..");

describe("release packaging", () => {
  test("uses relative Vite asset paths for packaged file loading", () => {
    const viteConfig = readFileSync(join(repoRoot, "apps", "desktop", "vite.config.ts"), "utf8");

    expect(viteConfig).toContain('base: "./"');
  });

  test("writes frontend version into build metadata", () => {
    const tempRoot = mkdtempSync(join(tmpdir(), "journal-build-metadata-"));
    const outputPath = join(tempRoot, "build-metadata.env");

    try {
      execFileSync(
        "powershell",
        [
          "-NoProfile",
          "-ExecutionPolicy",
          "Bypass",
          "-File",
          join(repoRoot, "scripts", "release", "write-build-metadata.ps1"),
          "-ReleaseVersion",
          "9.8.7",
          "-OutputPath",
          outputPath
        ],
        { cwd: repoRoot, stdio: "pipe" }
      );

      const metadata = readFileSync(outputPath, "utf8");
      expect(metadata).toContain("JOURNAL_FRONTEND_VERSION=9.8.7");
      expect(metadata).toContain("VITE_JOURNAL_FRONTEND_VERSION=9.8.7");
    } finally {
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });

  test("keeps the modular Electron preload outside the renderer sandbox", () => {
    const mainProcess = readFileSync(join(repoRoot, "apps", "desktop", "electron", "main.cjs"), "utf8");

    expect(mainProcess).toContain('preload: path.join(__dirname, "preload.cjs")');
    expect(mainProcess).toContain("contextIsolation: true");
    expect(mainProcess).toContain("nodeIntegration: false");
    expect(mainProcess).toContain("sandbox: false");
  });

  test("opts GitHub release actions into the Node 24 runtime", () => {
    const releaseWorkflow = readFileSync(join(repoRoot, ".github", "workflows", "release-windows.yml"), "utf8");

    expect(releaseWorkflow).toContain("FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: true");
  });

  test("generates tag-range GitHub release notes before publishing", () => {
    const releaseWorkflow = readFileSync(join(repoRoot, ".github", "workflows", "release-windows.yml"), "utf8");

    expect(releaseWorkflow).toContain("fetch-depth: 0");
    expect(releaseWorkflow).toContain("write-github-release-notes.ps1");
    expect(releaseWorkflow).toContain("artifacts/installer/release-assets/GITHUB_RELEASE_NOTES.md");
    expect(releaseWorkflow).not.toContain("body_path: docs/release/GITHUB_RELEASE_TEMPLATE.md");
  });

  test("writes release notes from the previous version tag to HEAD", () => {
    const tempRoot = mkdtempSync(join(tmpdir(), "journal-release-notes-"));
    const outputPath = join(tempRoot, "release-notes.md");

    try {
      execFileSync(
        "powershell",
        [
          "-NoProfile",
          "-ExecutionPolicy",
          "Bypass",
          "-File",
          join(repoRoot, "scripts", "release", "write-github-release-notes.ps1"),
          "-ReleaseVersion",
          "0.1.1",
          "-OutputPath",
          outputPath
        ],
        { cwd: repoRoot, stdio: "pipe" }
      );

      const notes = readFileSync(outputPath, "utf8");
      expect(notes).toContain("# Journal v0.1.1");
      expect(notes).toContain("## Highlights");
      expect(notes).toContain("Added an in-app legal document reader in About");
      expect(notes).toContain("Changes Since v0.1.0");
      expect(notes).toContain("add in-app legal document reader");
      expect(notes).toMatch(/add in-app legal document reader \([0-9a-f]{7,}\)/);
      expect(notes).not.toContain("$hash");
      expect(notes).toContain("Journal-Setup-0.1.1.exe");
    } finally {
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });

  test("about panel styles use concrete theme values", () => {
    const styles = readFileSync(join(repoRoot, "apps", "desktop", "src", "styles.css"), "utf8");
    const aboutPanelMatch = styles.match(/\.about-panel\s*\{(?<body>[^}]+)\}/);

    expect(aboutPanelMatch?.groups?.body).toContain("background: #fffdf8");
    expect(styles).toContain(".about-product-mark");
    expect(styles).toContain(".about-release-card");
    expect(styles).toContain(".about-runtime-list");
    expect(styles).toContain(".about-legal-detail");
    expect(styles).toContain(".modal-close-action");
    expect(aboutPanelMatch?.groups?.body).not.toContain("var(--surface)");
    expect(aboutPanelMatch?.groups?.body).not.toContain("var(--border)");
    expect(aboutPanelMatch?.groups?.body).not.toContain("var(--shadow-lg)");
    expect(aboutPanelMatch?.groups?.body).not.toContain("var(--text-primary)");
  });
});
