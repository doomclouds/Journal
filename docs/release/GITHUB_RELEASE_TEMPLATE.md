# Journal Windows Release

## Assets

- `Journal-Setup-<release-version>.exe`
- `Journal-Setup-<release-version>.sha256`

## Install

Download the setup executable for this release and run it on Windows x64. For example, tag `v0.1.0` publishes `Journal-Setup-0.1.0.exe`.

## Highlights

- Local-first daily journal workflow with JMF draft validation and confirmation.
- OpenAI-compatible LLM provider settings plus Mock provider fallback.
- Harness Core audit trail, local history search, version snapshots, and same-day anniversary wheel.
- Data export/import with current data summary and import-time backup.

## Data Safety

The installer preserves `%LocalAppData%/Journal` during upgrade and uninstall. User journal data is not treated as disposable installer output. Export packages do not include full API keys by default.

## Verification

After downloading both assets, compare the SHA-256 hash of the setup executable with the value in the matching `.sha256` file.
