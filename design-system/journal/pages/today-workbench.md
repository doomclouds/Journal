# Today Workbench UI/UX Override

This page overrides `design-system/journal/MASTER.md` for the daily writing surface.

## Purpose

The Today Workbench is the primary writing desk. It must make it easy to:

- write one natural-language input quickly;
- preserve raw input as source material;
- review AI-organized JMF draft content;
- edit draft sections safely;
- confirm only when the draft is valid and the user is ready;
- understand what Journal did without reading logs.

## Layout

Keep the central journal paper as the dominant object.

Preferred regions:

```text
Top context: date, mode, local service/provider state, global commands
Center: paper/document editor
Right: Today Assistant / next step / evidence
Bottom: compose bar and primary writing commands
```

Rules:

- The compose bar belongs at the bottom and should stay visually connected to the paper.
- The assistant is an inspector, not a chat sidebar.
- Raw input evidence should be visible enough to build trust, but not louder than the draft.
- Compact command controls should use icons with accessible labels.

## States

Required state hierarchy:

- Empty: invite one natural-language sentence; do not imply onboarding or marketing.
- Draft/reviewing: show paper, editable sections, next step, and confirm affordance.
- Attention: explain that formal entry was not overwritten and source material is safe.
- Confirmed: show saved state and formal file path where useful.
- Busy: keep previous safe content visible; never replace paper with a full-screen spinner.

## Interaction Rules

- Submit input and confirm entry are distinct actions.
- Reorganize/regenerate must warn when it overwrites the current draft.
- Dirty draft state must block submit/reorganize until saved or canceled.
- Validation errors should appear near the assistant/attention area and remain actionable.
- Keyboard focus must remain predictable after submit, save, cancel, and modal close.

## Visual Rules

- Use paper warmth and sage active states.
- Avoid strong CTA orange. The main action can be sage; warning/attention can use gold.
- Journal prose may breathe; surrounding tool UI should stay compact.
- Do not add decorative illustrations, landing sections, or welcome tours.

## Verification

- Frontend tests should cover disabled states, dirty guards, attention copy, and command availability when behavior changes.
- Visual/manual checks should include empty day, draft day, attention draft, confirmed day, and loading/error states.
