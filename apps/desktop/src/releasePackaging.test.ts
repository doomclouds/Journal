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
