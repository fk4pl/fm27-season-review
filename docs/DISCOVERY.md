# Discovery notes

Findings from Stage 0. Everything here was verified on the development machine unless marked
**UNVERIFIED**. Hidden values (CA/PA, hidden attributes) are deliberately not explored or recorded.

Last updated: 2026-11-13.

## 1. Environment

| Item | Value |
|---|---|
| OS | Arch Linux, game runs through Proton (prefix created with Proton 11.0-100, Proton Experimental) |
| Steam app id | 4530370 |
| Steam build id | 26577676 (game files last updated 2026-11-13) |
| Game folder | `~/.local/share/Steam/steamapps/common/Football Manager 27/` |
| Executable | `fm.exe` (data folder `fm_Data`) |
| Proton prefix | `~/.local/share/Steam/steamapps/compatdata/4530370/pfx/` |
| Unity version | `6000.3.18f1-custom (96cdb67fb884)` (from `globalgamemanagers`, `UnityPlayer.dll`, `Player.log`) |
| Scripting backend | IL2CPP: `GameAssembly.dll` + `fm_Data/il2cpp_data/Metadata/global-metadata.dat`, no `Managed/` folder |
| IL2CPP metadata version | 39 (`0x27` in the metadata header) |
| .NET SDK | 10.0.114 (system package); plugins target `net6.0` |
| Decompiler | `ilspycmd` 11.2.0 (`~/.dotnet/tools/ilspycmd`) |

In-game version string (e.g. "27.0.x"): **UNVERIFIED**, not present in any log; read it from the
game's main menu.

### User data folders (inside the Proton prefix)

Base: `<prefix>/drive_c/users/steamuser/`

| Purpose | Path under base |
|---|---|
| Saves | `Documents/Sports Interactive/Football Manager 27/games/*.fm` |
| Other user data (graphics, editor data, matches, languages) | `Documents/Sports Interactive/Football Manager 27/` |
| **Season review output (planned)** | `Documents/Sports Interactive/Football Manager 27/SeasonReviews/` |
| Preferences, caches, SI logs, cloud-synced views/shortlists | `AppData/Local/Sports Interactive/Football Manager 27/` |
| Unity `Player.log`, custom view collections | `AppData/LocalLow/Sports Interactive/Football Manager 27/` |

On Windows the same folders are `%USERPROFILE%\Documents\Sports Interactive\Football Manager 27\`,
`%LOCALAPPDATA%\Sports Interactive\Football Manager 27\` and
`%USERPROFILE%\AppData\LocalLow\Sports Interactive\Football Manager 27\`. The plugin runs inside
Wine/Proton, so it sees the Windows-style paths on both platforms; resolve the Documents folder
through the .NET/Windows API at runtime instead of hard-coding it.

## 2. Game architecture (before interop generation)

- The managed side is split into many assemblies (`fm_Data/ScriptingAssemblies.json`). The relevant
  ones: `FM.GamePlugin`, `FM.GameSimulation`, `FM.UI`, `FMGame`, `SI.Bindable`,
  `SI.Bindable.Reference.Core`, `SI.Interop`, `SI.Core`, `SI.Data`, `SI.Marshalling`, `SI.UI`,
  `SI.Services`, `Assembly-CSharp`.
- The simulation itself is **native C++**: `fm_Data/Plugins/x86_64/game_plugin.dll`. The C# layer is
  mostly UI (Unity UI Toolkit) and talks to the simulation through an interop/binding layer. So
  there are probably no plain C# objects such as `Club.Finances` to read; data is requested from
  the native side by reference + property id. To be confirmed once interop assemblies exist.

## 3. BepInEx

| Plugin | BepInEx build it targets | Target framework | Licence |
|---|---|---|---|
| FM View Exporter (ElectroHugin) | 6.0.0-be.755 ("first build that reads IL2CPP metadata v39"; accepts same or newer) | net6.0 | GPLv3 |
| FMDataDisplay (fredGob) | 6.0.0-be.791 ("791 or newer") | net6.0 | none stated in repo |
| FM27 Exporter (v1adicke) | 6.0.0-be.793 | net6.0 | MIT |
| FM27 Player Export, macOS port (oeknudsen) | not stated | netstandard2.1 | MIT |

**Chosen: BepInEx 6.0.0-be.793+dbc82f0** (Unity IL2CPP, win-x64), the newest bleeding-edge build
as of 2026-11-13 and the newest of the builds the reference plugins were tested with; the others
state "or newer". Downloaded from
`https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip`
(sha256 `5b63d7d0e92a2448ccd13b738ff95ff1cc1026c6d718555d4df8e6c824953ac4`) and extracted
unchanged next to `fm.exe` (`BepInEx/`, `dotnet/`, `winhttp.dll`, `doorstop_config.ini`,
`.doorstop_version`, `changelog.txt`).

Proton needs the Steam launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%` so Wine loads the
`winhttp.dll` proxy from the game folder. Windows needs no launch option.

## 4. How the reference plugins read data

Only the approach was studied; **no code has been copied** from any of them so far, so the licence
choice for this mod is still free.

- **FM View Exporter, FM27 Player Export, FM27 Exporter**: read the cells of the table currently
  visible on screen (Unity UI Toolkit elements bound through `SI.Bindable`), scrolling to collect
  all rows. Screen-scraping; only what the user has opened.
- **FMDataDisplay**: asks the game engine directly, without the data being on screen. It builds an
  `SI.Interop.InteropReference` for a person/club/competition, attaches its own binding nodes
  (`AddNode` / `OpenChannel` on a binding subsystem, keyed by `SI.Bindable.Bindings.Key` and a
  `PropertyID`), and receives the values asynchronously (`SI.Core.TypedValue`) through a Harmony
  prefix on the channel-data callback. Properties are addressed by name (`"Goals"`, `"Minutes"`,
  `"AverageRating"`, `"Appearances"`, `"Club"`, `"Division"`, `"FullContract"` → `"EndDate"` …).
  Its nodes hang off a UI node, so it needs a screen showing a person (or a validated permanent
  node). This is the most promising technique for us; class and member names must still be
  verified against our own interop assemblies.
- **FM Season Hub**: closed-source Windows companion that reads the game's process memory live.
  Not usable from inside a plugin in a maintainable way (offsets change with every patch) and not
  studied further; noted only as a last-resort alternative.

## 5. Data sources for the report

Pending: requires the interop assemblies (`BepInEx/interop/`), generated on the first game launch
with BepInEx.

| Need | Status |
|---|---|
| In-game date / season | pending |
| Human manager's club | pending |
| League table, fixtures/results | pending |
| Player season stats | pending (property names seen in FMDataDisplay, unverified) |
| Transfer history | pending |
| Club finances | pending |
| Board expectations / confidence | pending |
| Supporter confidence | pending |
| Injury records | pending |
| Inbox / notification system | pending |
