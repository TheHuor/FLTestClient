# Compact fleet and settings design QA

## Approved target

The user approved the settings layout from the first generated image (exec-af4818c2),
compact fleet cards and inspector from the third (exec-e5aaf8de), plus the contextual actions
from the second (exec-f0cbe1df). This is an intentional combination in the existing native WPF
application, not a browser prototype. Existing logo, palette and protocol remain authoritative.

## Comparison evidence

Actual compiled WPF windows were rendered with isolated settings/sample bots at 1440 x 1024,
matching the requested desktop mockup viewport, and at the application's 1100 x 680 minimum.
Additional 860 x 520 content renders cover settings tabs, behaviour, chat, admin, diagnostics,
and dialogs. English/German and Dark/Bright were generated; full-window dark/German and
bright/English, the settings connection tab, collapsed inspector, minimum window, and add-bot
dialog were opened and visually inspected. Generated source images were opened directly.
Artifacts remain local in the session visualization folder; no sample screenshots were uploaded.

## Fidelity surfaces

- Typography: retained native Segoe UI, explicit 15px bot names/21px inspector title,
  smaller secondary labels for compact monitoring. Names no longer inherit cyan navigation text.
- Spacing/layout: two columns of compact cards with twelve visible at 1440 x 1024; fixed-width
  collapsible inspector, horizontal direct settings tabs and side-by-side endpoints. At minimum
  size, independent scroll panes keep long actions reachable. Card checkbox/overflow remain visible.
- Colors/tokens: existing Dark/Bright resources retained; semantic state labels use both text
  and color. Menu styles and selection checkboxes follow the same palette.
- Images/assets: original LogoMark and Segoe MDL2 Assets retained; no invented mock logo,
  spaceship avatars or generated decorative assets. This screen needs no new raster imagery.
- Copy/content: all new text is localized, both resource dictionaries have matching keys.
  Actual state labels and base nicknames replace illustrative mock metadata. Connection
  credentials remain masked, and the save note states the actual save-on-exit behaviour.

## Findings and corrections

- P2: card location Run.Text attempted TwoWay binding to readonly properties. Reproduced in
  populated-card layout check, fixed to OneWay; now passes.
- P2: a bot chat draft could carry over to another bot. Reproduced, now cleared on focus change.
- P2: table realization could overwrite independently focused bot. Reproduced in actual
  FleetView layout, now only user table interaction changes focus.
- P2: keyboard menu used old inspector focus instead of current table row. Target resolver
  now derives keyboard scope from CurrentItem; regression check passes.
- P2: actual context-menu layout applied a MenuItem style to separators. The menu-layout regression reproduced the failure; entries now receive their style individually and the full suite passes.
- Visual: square selection boxes replace global switch styling on bot cards. Search hint,
  compact header, neutral bot names and explicit endpoint headings match the intended hierarchy.

## Intentional differences and limits

- Preserve existing WPF fonts/sizes and logo rather than copying mock-specific branding,
  invented dates, unsupported error details or fake server health.
- Existing base nicknames remain the command-safe choice; localized game-name expansion is
  outside this iteration. Global movement is not falsely exposed as per-bot behaviour.
- Native controls are smaller than the generated concept's enlarged presentation, allowing
  both monitoring density and the retained table. Both sizes were visually checked.
- No live game-server mutation, end-to-end network session or full accessibility certification.

## Acceptance

- [x] Direct settings tabs, compact cards, overflow/right-click menu and collapsible inspector.
- [x] Table retained, single-bot and bulk scope separated, filtered/removed focus invalidated.
- [x] 25 executable x86 checks pass; Release solution build has zero warnings/errors.
- [x] Independent code review findings reproduced and fixed with regression checks.
- [x] Dark/Bright and English/German render checks; no remaining P0/P1/P2 visual findings.

final result: passed
