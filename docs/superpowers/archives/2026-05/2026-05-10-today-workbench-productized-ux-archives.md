# Today Workbench Productized UX

- Date: 2026-05-10
- Status: delivered
- Spec: [2026-05-10-today-workbench-productized-ux-design.md](../../specs/2026-05-10-today-workbench-productized-ux-design.md)
- Prototype: [2026-05-10-today-workbench-productized-ux-prototype.html](../../specs/2026-05-10-today-workbench-productized-ux-prototype.html)
- Plan: [2026-05-10-today-workbench-productized-ux-implementation-plan.md](../../plans/2026-05-10-today-workbench-productized-ux-implementation-plan.md)

## Summary

The today journal screen was productized from a debug-panel-like workspace into a daily writing interface. The main screen now prioritizes the journal paper, bottom writing actions, productized status labels, a right-side today assistant, inline section editing, and a dark advanced JMF source drawer.

## Delivered

- Productized today status mapping: `待开始`, `整理中`, `可保存`, `有未保存修改`, `需要处理`, `已保存`.
- Paper-first today layout with bottom compose actions and right-side today assistant.
- Friendly empty, saved, needs-attention, and local-dirty states.
- Reading-first journal sections with per-section inline edit, save, and cancel.
- Dirty-state protection so unsaved block edits cannot be silently lost by confirm, section switching, block save failure, or source save.
- Advanced JMF source drawer hidden by default, with dark code-layer styling.
- Productized validation explanation that preserves raw material and repair hints.
- Narrow-window CSS contract that stacks paper, compose, assistant, and source drawer into one primary page scroll.

## Verification

- `npm test --prefix apps/desktop`
- `npm run build --prefix apps/desktop`
- `dotnet test Journal.slnx --artifacts-path $env:TEMP\journal-test-artifacts`
- Playwright CLI with Microsoft Edge at `1280x720` and `960x640` against `http://127.0.0.1:5173`.

## Notes

- Backend API contracts and JMF storage paths were unchanged.
- LLM settings behavior was preserved; only the today-page entry point text/status integration changed.
- Temporary Playwright screenshots were used for visual verification and then removed from the worktree.
