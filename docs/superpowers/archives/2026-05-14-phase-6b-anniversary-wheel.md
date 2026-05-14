# Phase 6B Same-Day Anniversary Wheel

**Date:** 2026-05-14
**Status:** Completed

## Delivered

- Added `GET /journal/history/anniversary/{monthDay}` with strict `MM-DD` validation.
- Reused the rebuildable SQLite history index through `entries.month_day`.
- Included processed, attention, missing, and raw-only indexed days in anniversary results.
- Added a Today Assistant entry to open the anniversary workbench for today's month/day.
- Added a dedicated anniversary workbench with year cards, selected Markdown preview, raw material snippets, version preview, and existing draft-only restore action.

## Verification

- `dotnet test Journal.slnx`
- `npm test --prefix apps/desktop`
- `npm run build --prefix apps/desktop`

## Notes

- SQLite remains a rebuildable cache. Markdown entries, raw-input jsonl files, and version files remain the source material.
- Version restore remains limited to today's date through the existing history restore guard.
