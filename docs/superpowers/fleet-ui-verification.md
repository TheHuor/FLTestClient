# Fleet UI verification — 2026-10-27

## Implemented

- Fleet, Behaviour, Chat, Server administration, Diagnostics and Settings navigation.
- Expandable selection scope on fleet/chat/admin, search/status filters and select-visible.
- Unified relocation/distribution preview using a validated immutable round-robin plan.
- Global live behaviour page, explicit launch automation timing, preparation-only quick chat.
- Admin authentication check with endpoint-change invalidation, explicit log-monitoring state.
- Stable settings sections with file/folder browsing and larger labels.
- Pauseable auto-scroll with navigation subscription cleanup.

## Evidence

- Baseline Release solution build: zero warnings/errors.
- Final Release solution build: zero warnings/errors.
- 13 executable x86 regression checks passed. Includes mixed in-space/docked fleets,
  invalid base/name rejection, selection removal/filtering, stale plans, cross-view pause,
  all six views in EN/DE and Dark/Bright, empty saved targets, active log-path changes,
  and immediate target-edit preview updates.
- Native fleet window opened and inspected. Additional actual WPF renders with sample bots
  inspected at 860 x 520 content size (approximately the minimum application viewport):
  fleet, behaviour, chat, admin, diagnostics, server log, settings sections and distribution.
- Matching English/German resource keys and XML validity checked.
- Independent reviewer found target-preview focus timing, an empty-target command gate,
  and stale file monitoring. Each has a regression test; all now pass.
- Initial tests reproduced missing action planner/quick-chat preparation, missing XAML Field
  resource and missing auto-scroll notifications before those corrections.

## Decisions and limits

- Retained WPF/MVVM, protocol code and global session behaviour; per-bot presets remain deferred.
- Filters clear selection deliberately so actions cannot silently affect hidden rows.
- Server access checks authenticate only; no game-server mutations were used for verification.
- Tests inject settings/persistence. An early file-watcher test wrote a temporary log path into
  local settings; the captured original log path was restored and the test host isolated.
- Main navigation and fleet native window were checked interactively. Remaining visual checks
  used the real compiled WPF views rendered offscreen, not mockups.
- No claim of complete accessibility compliance or live-server integration testing.

## Review ledger

Task 1: complete — action planning and interaction-state checks pass.
Task 2: complete — bilingual/theme views and dialogs build and render.
Final review: independent reviewer; all important findings fixed and regression-tested.
Ruling: Keep session-wide behaviour rather than introducing per-bot options; matches current core.
Ruling: Reject invalid/stale action plans rather than silently changing targets at execution.
Ruling: Clear selection on filter changes to prevent hidden targets; documented in the UI.
Deferred minors: none from the final review; unused distribution API and descriptor lifetime cleaned up.
Task 3: local verification complete; remote synchronization follows.
