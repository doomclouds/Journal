# Settings And Data UI/UX Override

This page overrides `design-system/journal/MASTER.md` for LLM settings, About, backup/import, release/runtime, and legal/safety dialogs.

## Purpose

Settings and data surfaces are trust surfaces. They must make local state, provider activation, API key safety, and data backup/import behavior explicit without overwhelming the daily writing flow.

## Dialog Rules

- Use modals only for focused tasks.
- Dialog headers must state the task, not a marketing slogan.
- Close controls must be icon buttons with accessible names.
- Dialogs should trap practical interaction focus and restore focus to the opener where possible.
- Long content should scroll inside the dialog, not the whole app shell.

## LLM Settings

Required hierarchy:

- provider list;
- active/effective provider state;
- candidate edit form;
- test result;
- protected activation;
- safe API key display/reveal rules.

Rules:

- Failed health checks must not look like successful activation.
- Environment-provided keys must not be revealable through UI.
- Full key values must not appear in docs, logs, screenshots, exports, or generated metadata.

## Data Backup And Import

Required hierarchy:

- current data summary;
- export action and export path;
- import package selection;
- pre-import backup path;
- failure recovery state.

Rules:

- Export copy must say full API keys are excluded.
- Import copy must say current source material is backed up before replacement.
- Data counts should name records, not files, when that is the backend contract.
- Paths must wrap safely and not overflow modals.

## About And Legal

- Version/runtime data can use dense cards.
- Legal notices should be readable and plain.
- Do not imply cloud sync, automatic updates, signing, or capabilities not in the current release.

## Visual Rules

- Use stronger borders than Today because these surfaces carry trust and configuration.
- Use plain, compact forms.
- Avoid decorative illustrations or onboarding tone.

## Verification

- Test focus return, escape close, safe key reveal paths, disabled busy states, export/import errors, and path wrapping.
