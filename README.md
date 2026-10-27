# FLTestClient

[![build](https://github.com/TheHuor/FLTestClient/actions/workflows/build.yml/badge.svg)](https://github.com/TheHuor/FLTestClient/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/TheHuor/FLTestClient?include_prereleases)](https://github.com/TheHuor/FLTestClient/releases)

A headless Freelancer client for testing FLServer and FLHook: it simulates any number of players
without graphics. The bots speak the real network protocol (DirectPlay 8 via `dpnet.dll` and the
FL RPC packets), so the server treats them like ordinary players.

Use it on your **own test server** for load tests, FLHook plugin tests and reproducing bugs that
need several players online.

## Download

Ready-to-run builds are on the [releases page](https://github.com/TheHuor/FLTestClient/releases):

- `FLTestClientGui-<version>-win-x86.zip`: the GUI
- `FLTestClient-cli-<version>-win-x86.zip`: the command line version

Both are self-contained (no .NET installation needed). Windows must have the **DirectPlay**
feature enabled (Control Panel → Windows Features → Legacy Components → DirectPlay); Freelancer
needs it too.

## Projects

| Project | Content |
|---|---|
| `FLTestClient.Core` | protocol, `Bot`, `BotManager` (network thread), `FLHookAdminClient`, `UniverseNames`, `ServerLogWatcher` |
| `FLTestClient.Cli` | command line `FLTestClient.exe` (scripts, background runs) |
| `FLTestClient.Gui` | GUI `FLTestClientGui.exe` (WPF, dark/bright theme, English/German) |

All projects are x86 (like Freelancer itself), because the DirectPlay structures use the 32-bit layout.

## Building

Requirements: Windows, [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build FLTestClient.slnx -c Release
```

Code comments and XML documentation are in English; missing XML comments on public members fail
the build (CS1591).

### Versioning and releases

The version comes from git tags via [MinVer](https://github.com/adamralph/minver):

- tag `v1.2.0` → version `1.2.0`
- commits after the tag → pre-release `1.2.1-alpha.0.N`
- tag `v1.3.0-beta.1` → published as a pre-release

Every push and pull request to `main` is built by the `build` workflow. Pushing a tag `vX.Y.Z`
runs the `release` workflow, which publishes both programs and creates the GitHub release with
generated release notes:

```
git tag v1.0.0
git push origin v1.0.0
```

## GUI

- **Fleet:** compact bot cards by default, with a switchable table for large fleets. Search by
  name/number/system and filter by state. "Add bots" opens the range form. Click a card to
  inspect/select it; use its checkbox or Ctrl-click for multiple selection. Filtering clears
  selection so actions never silently target hidden rows.
- **Single-bot actions:** right-click a card or table row, or use its visible `…` menu. These
  actions target the named bot, independently from the bulk selection. The collapsible
  inspector contains Details, Chat and Actions; changing bots clears any unsent chat draft.
- **Bulk selection:** "Select visible" selects filtered rows. "Selection actions" explicitly
  operates on the count shown in the footer; switching cards/table preserves this scope.
- **Relocate / distribute:** choose target bases and inspect each assignment directly from
  Fleet. FLHook `beam` teleports in-space bots; docked bots are skipped. The preview identifies
  the admin endpoint and auto-launch setting. Changed/removed bots require a new preview.
  Command results appear in both fleet activity and the admin console.
- **Behaviour:** global movement, session cycles, automated chat and launch automation.
  Direct tabs separate Movement, Sessions and Chat automation. Changes apply to all running
  bots. Circle radius takes effect at the next launch;
  launch automation takes effect at the next docking.
- **Chat:** the scope selects the bots that **send** the message, not in-game recipients.
  Quick commands prepare the input; only Send executes it.
- **Server admin:** admin authentication check, player list, rehash, kick and free console.
  The check reports its last result and time; each operation uses a separate connection.
- **Diagnostics:** bot activity, undecoded packets and server log with explicit monitoring
  state and counters. Auto-scroll can be paused. Changing an actively monitored log path
  switches the watcher; an invalid path disables monitoring visibly.
- **Settings:** four directly visible tabs: Connection, Bots & Characters, Local files and
  Appearance & language. Game-server and FLHook-admin credentials are shown together;
  bot identity and new-character defaults share one tab. No category dropdown is required.

Last inputs (never passwords) are stored in `%APPDATA%\FLTestClient\settings.json`.

### Regression checks

Build and run the dependency-free WPF regression suite without a game server:

```powershell
dotnet build FLTestClient.slnx -c Release
& "${env:ProgramFiles(x86)}/dotnet/dotnet.exe" FLTestClient.Gui.Tests/bin/Release/net10.0-windows/FLTestClient.Gui.Tests.dll
```

The suite checks action plans, invalid arguments, selection/filter changes, log-path changes,
single-bot vs bulk scope, retained table focus, keyboard context menus, a 200-bot table,
and runtime loading of all views in English/German and dark/bright themes. It injects isolated
settings and does not persist user settings or contact a game server. CI installs the x86
Windows Desktop runtime needed to execute it.

Optional `--render ABSOLUTE_OUTPUT_FOLDER` renders the actual views with sample bots in every
language/theme combination. The test host temporarily creates offscreen windows to complete
WPF layout before rendering; generated images do not enter the repository.

### Theme and language

- **Dark / Bright:** toggle at the top right or in Settings; palettes in `Themes/Dark.xaml` and
  `Themes/Bright.xaml`.
- **English (default) / German:** `EN`/`DE` button at the top right or in Settings; applies
  immediately, including the bot log lines. UI texts are in `Languages/en.xaml` and
  `Languages/de.xaml` (same keys; a missing key falls back to English). Texts of the core (bot log,
  command line) are in `FLTestClient.Core/Texts.cs`.
- To add a language: copy `en.xaml`, translate it and register it in `App.ApplyLanguage` and `Texts`.

The logo is drawn in `Assets/Logo.xaml`; `Assets/make-icon.ps1` renders the `.ico` from it.

## Command line

```
FLTestClient --server 10.0.0.5 --password <PW> --count 5 --launch --data C:\Freelancer\EXE_SERVER\DATA
FLTestClient --server 10.0.0.5 --password <PW> --say "/help" --duration 30
FLTestClient --server 10.0.0.5 --password <PW> --count 10 --launch --circle 600:80 --cycle 120-300:30 --chat-interval 60 --chat-line /ping --chat-line /time
```

FLServer does not bind to `127.0.0.1`, so use the LAN address (default: the first local IPv4
address). `--help` lists all options. Commands are read interactively or from `--commands FILE`
(new lines are executed): `list`, `say TARGET TEXT`, `launch TARGET`, `reconnect TARGET`,
`stop TARGET`, `quit` (TARGET = bot number or `all`).

## What the bots can do

- create an account (`fltest-NNN`) and a character (`TestBotNN`, values from `newcharacter.ini`)
- select the character, enter the base, launch (also characters saved in space)
- stay in space (position update every 0.5 s), send and receive chat
- handle landing and the server-side character selection after `beam`, then launch again
- log in again after 10 s on "account already logged in"
- **dock at a chosen base** via FLHook `beam`, also across systems
- **circle flight:** first 1000 m straight ahead in launch direction, then a horizontal circle
  (radius and speed adjustable); other players see the movement
- **respawn:** 5 s after being destroyed, select the character again and dock at the last base
- **session cycle:** log out cleanly after a random MIN..MAX s online, log in again after OFF s
- **chat script:** every n s (±50 %) send a random line or FLHook command

## Known limits and pitfalls

- **Anticheat (dsacesrv):** without a `DSAce.dll` answer, dsacesrv reports "no dsace response" and
  kicks or even **auto-bans** the account (file `banned` in the account folder). On the test server
  set `KickOnNoClientDll=no` in `dsacesrv.cfg`.
- **FLHook DuplicateIPs:** all bots come from the same IP. Add your IP as an exception under
  `[DuplicateIPs]` in `flhook.ini` or set `Enable=no`.
- **Player limit:** `MaxPlayers` in `FLServer.cfg` limits the number of bots.
- Unknown packets have no known length; the rest of the DirectPlay message is dropped. In practice
  the server almost always sends one packet per message.
- **No collision avoidance in circle flight:** the bot does not know the objects in the system.
  Launching from a dock is safe; a character saved in space may fly into a station or planet and is
  destroyed (then it respawns).
- No real approach to docking rings, no combat, trading or jump gates.

## License

[MIT](LICENSE). Freelancer is a trademark of Microsoft Corporation. This project is not affiliated
with or endorsed by Microsoft and contains no game files.
