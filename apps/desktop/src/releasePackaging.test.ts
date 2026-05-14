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
});
