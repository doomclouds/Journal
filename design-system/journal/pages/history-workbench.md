# History Workbench UI/UX Override

This page overrides `design-system/journal/MASTER.md` for History Search and Same-Day Anniversary.

## Purpose

The History Workbench is a retrieval and memory corridor. It must help the user scan durable entries, raw material snippets, versions, and same-day memories without creating accidental editing or deletion expectations.

## Layout

Preferred regions:

```text
Left rail: search / filters / date or month-day controls
Center: selected entry, anniversary memory, or empty state
Right inspector: versions, restore-to-draft, metadata, source evidence
```

Rules:

- Search and anniversary are modes inside History, not separate product shells.
- Center content should look like readable paper, not a data table.
- Version lists and search results can be denser than Today, but must keep active selection obvious.
- Restore controls must be visibly draft-only and today-limited unless product scope changes.

## Boundaries

- Anniversary mode is read-only.
- Do not expose delete, diff, rollback UI, or non-today restore unless implemented and approved.
- SQLite/FTS is a rebuildable cache; UI copy should not imply it is the source of truth.

## Interaction Rules

- Switching selected date/month-day must clear stale details that no longer match.
- Loading states should preserve mode context.
- Empty search results should suggest refining search, not data loss.
- Restore-to-draft must clearly say it writes a draft and not the formal entry.

## Visual Rules

- Use gold sparingly for memory/time accents.
- Use sage for selected results and available actions.
- Avoid chart-heavy dashboard visuals unless future history analytics are explicitly added.

## Verification

- Test stale selection guards, mode switching, invalid month-day handling, restore availability, and read-only anniversary behavior.
- Manual checks should include no results, raw-only date, entry with versions, and multi-year same-day results.
