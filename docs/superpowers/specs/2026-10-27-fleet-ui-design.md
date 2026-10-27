# Fleet UI design

Approved direction: retain WPF, existing theme and network implementation; reorganize the UI around fleet operations.

- Fleet: persistent selection summary, search/status filter, explicit add/connect vs launch, combined relocation/distribution dialog with eligible bots and round-robin preview.
- Behaviour: separate page for global live movement, session cycles and chat scripting; auto launch belongs here and applies to the session.
- Chat: explicitly identify sending bots, show selection and make quick commands prepare text rather than send immediately.
- Administration: admin endpoint and last authentication check, player list, rehash, console; relocate distribution to fleet.
- Diagnostics: bot activity and server log; observable monitoring state and optional auto scroll.
- Settings: stable connection, identity, character and file sections; readable labels and browsing for files.
- Keep English/German resource parity, x86/.NET 10 and XML documentation requirements. No live-server mutations during verification.

Implementation approach: incremental changes to existing views and view model, small pure action-plan class for preview and validation. Avoid replacing the network stack or adding runtime dependencies. Per-bot behaviour presets are deferred.
