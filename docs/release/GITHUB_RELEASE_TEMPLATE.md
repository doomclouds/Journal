# Journal Windows Release

## Assets

- `Journal-Setup-<release-version>.exe`
- `Journal-Setup-<release-version>.sha256`

## Install

Download the setup executable for this release and run it on Windows x64. For example, tag `v0.1.0` publishes `Journal-Setup-0.1.0.exe`.

## Data Safety

The installer preserves `%LocalAppData%/Journal` during upgrade and uninstall. User journal data is not treated as disposable installer output.

## Verification

After downloading both assets, compare the SHA-256 hash of the setup executable with the value in the matching `.sha256` file.
