# Approved bot cards and settings design

User approved: settings from the first displayed mock, compact cards/inspector from the third,
and right-click actions from the second. Keep the existing WPF app, palette, logo, localization
and protocol. Mock-specific invented dates/endpoints are not requirements.

- Four directly visible settings tabs: Connection, Bots & Characters, Files, Appearance.
  Show game and admin endpoints together; bot identity and character defaults together.
- Compact cards, two columns where room permits, with selection checkbox, name, state,
  location, ping and visible overflow menu. Retain a switchable table for large fleets.
- Right-click/overflow actions explicitly target that card's bot. Bulk actions retain their
  separate selected-bot scope. Keyboard equivalents and visible focus must work.
- Collapsible inspector with Details, Chat and Actions tabs. It follows the focused bot,
  clears on removal/filtering and can relocate that bot using the validated action planner.
- No unsupported per-bot movement, trading or fighting. Global behaviour remains global.
- Simplify the main fleet toolbar; adding bots uses a compact form. Diagnostics and chat remain
  available, and admin connection editing is grouped under Settings.
- Test actual WPF views in English/German, both palettes, minimum and normal window sizes.
- Sync via PR and the required GitHub build check; no live game-server mutations for tests.
