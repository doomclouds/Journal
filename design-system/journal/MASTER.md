# Journal UI/UX Design System

> Master source of truth for Journal UI/UX work.
>
> Retrieval rule:
> 1. For a concrete surface, first read `design-system/journal/pages/<surface>.md`.
> 2. Page rules override this file only where explicitly stated.
> 3. If no page file exists, follow this Master file.
> 4. Historical prototypes under `docs/superpowers/specs/` are evidence, not automatic authority.

## Scope

Journal is a local-first morning journal desktop app. The UI must help the user write, review, confirm, search, audit, and preserve personal source material without making the app feel like a marketing site or a generic admin dashboard.

This design system governs:

- React/Electron desktop UI under `apps/desktop/src`.
- Design specs, prototypes, and future UI plans.
- Page-specific UI/UX overrides under `design-system/journal/pages/`.
- Any future visual refresh that touches color, spacing, hierarchy, interaction, or accessibility.

It does not redefine product behavior. Product safety boundaries remain in `docs/agents/PRODUCT_INVARIANTS.md`.

## Skill Evidence

`ui-ux-pro-max` was run for this project on 2026-05-15.

Accepted signals:

- `E-Ink / Paper`: strong fit for reading, journaling, calm writing, high contrast, low distraction.
- `Swiss Modernism 2.0`: strong fit for strict grid, rational layout, and tool-like hierarchy.
- `Micro-interactions`: useful only as restrained feedback for saves, validation, loading, focus, and command state.
- `Data-Dense Dashboard`: useful only for History, Audit, settings, and backup surfaces where scanning density matters.
- `Chinese Simplified` typography: validates a Chinese-first readable sans stack.
- `Productivity Tool` palette: validates teal as focus/action color, but the generated bright orange CTA is too loud for Journal and is demoted.

Rejected signals:

- Marketplace / Directory pattern: wrong product model.
- Portfolio Grid pattern: wrong information architecture.
- Handwritten or casual font pairing: too ornamental for durable personal records.
- Blue analytics palette: too generic and less aligned with the current paper/sage identity.
- Bright teal/orange full palette: usable as evidence, but too saturated for the existing desktop tone.

## Design Thesis

Journal should feel like a quiet, trustworthy writing desk with a precise inspection bench attached.

The primary experience is not "AI chat" and not "dashboard control". The main artifact is the journal paper. Supporting panels explain state, provenance, settings, and history without stealing the center.

## Product Pillars

- **Paper first**: the durable Markdown entry or draft stays visually central.
- **Source material protected**: raw input, snapshots, audit, and settings safety are visible when needed, never decorative.
- **Dense but calm**: desktop workflows can show many controls, but spacing and hierarchy must keep scanning cheap.
- **Confirmation over automation**: AI output must look reviewable, not magically final.
- **Local trust**: data paths, backup/import, API key safety, and provider state need direct, plain presentation.

## Visual Direction

Use a restrained paper-and-ink UI with sage as the main operational accent and old gold only as a quiet highlight.

The current CSS direction is broadly correct:

```css
:root {
  --ink: #24231f;
  --ink-soft: #47433b;
  --muted: #6e675c;
  --line: rgba(52, 45, 36, 0.14);
  --paper: #fffdf7;
  --sage: #2f6f5f;
  --gold: #af7a30;
}
```

Future work should refine this system rather than replace it wholesale.

## Color Tokens

| Role | Token | Target | Usage |
| --- | --- | --- | --- |
| App background | `--surface-canvas` | `#eeece4` | Overall desktop shell |
| Paper | `--surface-paper` | `#fffdf7` | Journal preview, cards, modals |
| Paper soft | `--surface-soft` | `#fbfaf6` | Secondary cards and nested panels |
| Ink | `--ink` | `#24231f` | Primary text |
| Ink soft | `--ink-soft` | `#47433b` | Secondary headings and body |
| Muted | `--muted` | `#6e675c` | Metadata, helper text |
| Line | `--line` | `rgba(52, 45, 36, 0.14)` | Borders and separators |
| Primary accent | `--sage` | `#2f6f5f` | Primary actions, active states, focus |
| Highlight | `--gold` | `#af7a30` | Evidence, memory, subtle emphasis |
| Danger | `--danger` | `#8c332b` | Validation and destructive warnings |
| Warning | `--warning` | `#765622` | Attention states |
| Success | `--success` | `#426327` | Confirmed and healthy states |

Rules:

- Keep light mode as the default identity.
- Do not introduce a purple/blue SaaS theme unless a future explicit brand decision overrides this file.
- Avoid one-note beige. The sage and gold accents must carry status and action hierarchy.
- Use red only for errors or blocked states.
- Every muted text choice must remain readable on paper and canvas backgrounds.

## Typography

Primary stack:

```css
font-family: Inter, "Microsoft YaHei", "PingFang SC", system-ui, sans-serif;
```

Rules:

- Keep Chinese readability first. Do not add web-font imports that make the packaged desktop app depend on Google Fonts.
- Use sans-serif for UI and metadata.
- Use larger line-height for journal prose: `1.65` to `1.75`.
- Use compact but readable UI text: 12px to 15px for chips, labels, and dense workbench controls.
- Do not use handwritten fonts. The journal is personal because of the content, not because the UI pretends to be handwriting.
- Keep letter spacing at `0` except for tiny uppercase labels already established as UI metadata.

## Layout System

Journal uses a desktop workbench layout, not a landing-page layout.

Canonical structure:

```text
Top context / command strip
  -> Workspace grid
       left or mode rail
       central paper/workbench document
       right assistant/inspector
  -> Bottom compose or contextual command surface
```

Rules:

- The central paper/document area owns visual priority.
- Side panels are supportive, scrollable, and information-dense.
- Bottom compose stays close to the writing task.
- Modals are for focused configuration or data operations, not for core writing flow.
- Avoid cards inside cards. Repeated list items may be cards; page sections should be layout regions.
- Use stable grid dimensions for rails, paper, toolbars, icon buttons, and chips so hover and state changes do not shift layout.

## Component Rules

### Buttons

- Use Lucide icons for compact tool commands when an icon exists.
- Text buttons are for clear primary commands, dangerous confirmations, or forms where wording matters.
- Icon buttons must include `aria-label` and `title`.
- Hover should change color, border, or shadow only. Avoid scale transforms.
- Minimum target height: 34px for dense controls, 40px for normal actions.

### Chips And Status

- Chips communicate state, not decoration.
- Status colors must map consistently:
  - Sage/green: ready, confirmed, active, connected.
  - Gold/amber: attention, draft needs review, not yet final.
  - Red: failed, blocked, invalid.
  - Neutral paper/gray: metadata and passive counts.

### Cards And Panels

- Radius should stay at 6px to 8px for tool surfaces.
- Use border first, shadow second. Heavy shadows only for modal overlays or the paper surface.
- Nested panels should reduce contrast instead of adding more borders.

### Forms

- Labels are required.
- Inputs must keep visible focus states.
- API key views must use safe previews only and never expose full keys unless an explicit reveal flow already exists.
- Validation errors should name the blocked action and the safe state of data.

### Markdown Paper

- Prose should use generous line-height and a readable max width.
- Hidden JMF provenance markers stay hidden in normal preview.
- Source material and formal Markdown must never be visually implied as disposable cache.

## Motion And Interaction

Use motion only for feedback and orientation.

- Default transitions: 150ms to 220ms.
- Keep motion subtle: color, border, opacity, and small shadow changes.
- Respect `prefers-reduced-motion`.
- Avoid decorative loops, bounce, parallax, and scroll-jacking.
- Loading states should feel calm and deterministic, not playful.

## Accessibility Gates

Before UI code is considered complete:

- Keyboard can reach every command that mouse can reach.
- Focus ring is visible on all buttons, tabs, inputs, and menu-like controls.
- Tab order follows visual order.
- Dialogs restore focus to their opener where practical.
- Text contrast meets WCAG AA minimum.
- Color is not the only state indicator.
- Responsive checks cover 375px, 768px, 1024px, and 1440px where the browser surface is relevant.
- No horizontal scroll on narrow layouts unless an explicit data table demands it and has a visible scroll affordance.

## Page Override Index

- `pages/today-workbench.md`: daily writing, draft review, assistant, compose bar.
- `pages/history-workbench.md`: search, details, versions, same-day anniversary.
- `pages/audit-workbench.md`: Harness run inspection and provenance evidence.
- `pages/settings-and-data.md`: LLM settings, About, backup/import, release/runtime dialogs.

When adding a new major surface, create a page override before implementing visual changes.

## Anti-Patterns

- Marketing hero layout.
- Generic SaaS blue dashboard palette.
- Bright decorative gradients, orbs, bokeh, or atmospheric backgrounds.
- Emoji as UI icons.
- Oversized typography inside dense tool panels.
- Hidden or hover-only critical controls.
- Layout-shifting hover effects.
- Treating AI output as final without review state.
- Using UI copy that implies cloud sync, autosave, delete, rich text, item-level provenance, rollback UI, or full key export/import unless code proves it.

## Pre-Delivery Checklist

- [ ] Page override checked before editing a surface.
- [ ] Existing CSS tokens reused or deliberately extended.
- [ ] Lucide icons used for icon commands.
- [ ] Clickable controls have pointer cursor and visible hover/focus.
- [ ] Text fits at 375px, 768px, 1024px, and 1440px.
- [ ] Reduced motion is respected.
- [ ] No source material, API key, or formal-entry safety boundary is weakened.
- [ ] Relevant Vitest or backend tests are updated when behavior changes.
