# Audit Workbench UI/UX Override

This page overrides `design-system/journal/MASTER.md` for Harness Core audit inspection.

## Purpose

The Audit Workbench explains what an AI/Harness run attempted, which tools were collected, what was accepted or rejected, and why the draft state changed.

It is an inspection bench. It should be dense, precise, and calm.

## Layout

Preferred regions:

```text
Left rail: date and run list
Center: run summary and tool-call evidence
Right/inline inspector: validation, provenance, rejected operations, safety notes
```

Rules:

- Audit details may use a denser dashboard style than Today.
- Run status must be visible before tool-call details.
- Rejected operations need clear reasons.
- Normal journal preview must still hide provenance markers; audit can expose provenance as evidence.

## Status Language

Use precise verbs:

- collected
- rejected
- applied to draft
- no-op
- failed
- interrupted
- validation blocked

Avoid language that suggests direct formal-entry mutation. Harness execution is draft-only.

## Visual Rules

- Use left accent borders on tool-call cards to distinguish accepted/rejected/no-op.
- Use monospace only for IDs, paths, tool names, or raw technical payloads.
- Do not use animated timelines unless they add concrete debugging value.

## Verification

- Test stale run guards, empty audit days, rejected operations, failed runs, and safe display of user/provider data.
- Manual checks should include long prompts, long IDs, many tool calls, and narrow desktop width.
