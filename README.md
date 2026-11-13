# Season Review for Football Manager 27

*Türkçe: [README.tr.md](README.tr.md)*

A BepInEx 6 plugin that brings back the end-of-season review that Football Manager had up to
FM24. When your club plays its last league match of the season, the mod writes the story of
that season into one report: how you finished against expectations, the key moments, your
players, transfers, finances, injuries, and what the board and supporters think.

It runs inside the game, needs no companion app, and only **reads** game data. It never writes
to your save files and never shows hidden values (current/potential ability, hidden attributes,
injury proneness and the like).

> **Status: 0.1.0, early release.** Tested only on **Linux with Proton**. It has **not been
> tested on Windows**, although the plugin is the same file on both.

## What the report contains

Each report is one self-contained HTML file (no internet, no scripts, prints cleanly) plus a
Markdown copy of the same text, for example for forum posts.

- **Header:** league position, points, W-D-L and goals at a glance.
- **The season in brief:** where you finished, your record, how that compares with the media's
  pre-season prediction; the board's expectations with the board's own verdict; your cup runs;
  key moments (biggest win, heaviest defeat, biggest signing, top scorer).
- **League performance:** played, won, drawn, lost, goals and points, split into home and away,
  plus the longest winning and unbeaten league runs.
- **Players:** player of the season, top scorers, most assists, best average rating, young
  players (21 and under) who played, and players out on loan listed separately.
- **Transfers:** arrivals and departures with fees and dates, loans, fees paid and received,
  net spend.
- **Finances:** bank balance at the start of the season and now, profit/loss (the change in the
  balance), the game's own season income and expenditure totals, its projected profit/loss,
  the wage bill and the club's financial status.
- **Injuries:** number of injuries and days lost, and the longest absences.
- **Board and supporters:** the game's own end-of-season verdicts, with positives and concerns.

Player of the season: the highest average rating among players who appeared in at least two
thirds of the club's competitive matches; ties go to goals plus assists. The report states this.

Things FM27 already shows well (end-of-season awards, Best XI, monthly finance reports) are not
duplicated.

## Languages

Reports are available in **English** (default) and **Turkish**. All report text lives in one plain
text file per language in `lang/`. To translate, copy `lang/en.txt` to `lang/<code>.txt`, translate
the text after each `=`, and set `Language = <code>` in the config. Missing lines fall back to
English.

Text that comes from the game itself (board and supporter feedback, competition names) appears in
the game's language. A translation file can also contain a small dictionary for board objectives,
injury names and financial status values; `lang/tr.txt` has one. Anything not in the dictionary is
shown as the game gives it.

## Requirements

- Football Manager 27 on Steam (tested with **27.0.1**, Steam build 26577676).
- **BepInEx 6.0.0-be.793**, Unity IL2CPP, Windows x64. Other recent bleeding-edge builds (be.755 or
  newer can read FM27) may work but were not tested.
- Linux: Steam with Proton (tested with Proton Experimental).

## Installation

Back up your saves first (`Documents/Sports Interactive/Football Manager 27/games`). The mod does
not write to them, but it is early software.

### 1. Install BepInEx (skip if you already use other FM27 BepInEx mods)

1. Download `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793+dbc82f0.zip` from
   <https://builds.bepinex.dev/projects/bepinex_be/793>.
2. In Steam: right-click Football Manager 27 > *Manage* > *Browse local files*.
3. Extract the zip into that folder, next to `fm.exe` (you should get `BepInEx/`, `dotnet/`,
   `winhttp.dll`, `doorstop_config.ini`).
4. **Linux/Proton only:** in Steam, Football Manager 27 > *Properties* > *Launch options*, add
   `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
   If you already have launch options, put the `WINEDLLOVERRIDES=...` part in front of them,
   for example `WINEDLLOVERRIDES="winhttp=n,b" mywrapper %command%`.
5. Start the game once, wait for the main menu (the first start takes a few minutes), then quit.
   `BepInEx/LogOutput.log` should now exist.

### 2. Install Season Review

**Windows:** extract the release zip and copy its `BepInEx` folder into the game folder, merging
with the existing `BepInEx` folder. You should end up with
`Football Manager 27\BepInEx\plugins\SeasonReview\SeasonReview.dll` and its `lang` folder.

**Linux:** extract the release zip and run the installer from that folder:

```sh
./install-linux.sh                 # finds the game in your Steam libraries
./install-linux.sh --game-dir "/path/to/Football Manager 27"
./install-linux.sh --uninstall     # removes the plugin, keeps BepInEx and your reports
```

The installer copies the plugin, can download and install BepInEx be.793 if it is missing (it
asks first and checks the download's checksum), and reminds you of the launch option. Close the
game before installing.

To check it works, look for `Season Review 0.1.0 loaded` in `BepInEx/LogOutput.log`.

## Usage

Play as usual. After your club's last league match of the season, the report is written once.
To get an interim report for the season so far, press **Ctrl+F8** in a loaded career and wait
about 20-40 seconds (the game stays usable). The default hotkey avoids F9, which FM View Exporter
and FM27 Player Export use.

Reports are saved to `Documents/Sports Interactive/Football Manager 27/SeasonReviews/`:

- season-end report: `<Club>_<Season>.html` and `.md`, e.g. `Beşiktaş_2026-27.html`
- interim report: `<Club>_<Season>_<date>.html`, e.g. `Beşiktaş_2026-27_2026-08-31.html`

On Linux that folder is inside the game's Proton prefix:
`~/.local/share/Steam/steamapps/compatdata/4530370/pfx/drive_c/users/steamuser/Documents/Sports Interactive/Football Manager 27/SeasonReviews/`

To test without waiting for a season to end, use a throwaway career: holiday to shortly before
the last league match, then continue.

## Settings

`BepInEx/config/io.github.seasonreview.fm27.cfg` (created on the first start):

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Turn the mod on or off. |
| `AutoReport` | `true` | Write the report automatically after the last league match. |
| `Language` | `en` | `en` or `tr`, or any code with a matching `lang/<code>.txt`. |
| `OutputFolder` | empty | Where reports go; empty means the `SeasonReviews` folder above. |
| `Hotkey` | `Ctrl+F8` | Interim report. Format `[Ctrl+][Shift+][Alt+]<F1-F24, A-Z, 0-9>`. |
| `ProbeHotkey` | `false` | Developer: Shift+hotkey dumps property names of the current screen. |
| `RawDump` | `false` | Developer: also write the unformatted data next to the report. |

Seasons that already have a report are listed in
`BepInEx/config/io.github.seasonreview.fm27.reported.txt`. Delete a line there to allow the
automatic report for that season again.

## Known limitations

- **Tested on Linux/Proton only.** Not tested on Windows.
- **Game updates can break the mod.** FM27 has no modding API; the mod reads the game's internal
  UI data, which can change with any patch. Sections that cannot be read show "Data unavailable"
  instead of crashing, but a patch may need a new release.
- **Holidaying past the end of the season can miss the report.** The mod checks once per in-game
  day while the game is idle. If you holiday from before the last league match into the next
  season without the game ever pausing, the season can roll over before the check runs, and that
  season gets no report. Stop the holiday before the season ends, or press Ctrl+F8 after the last
  league match.
- **Cup round names are not shown.** Cup lines give the record and the last match, not the round
  ("semi-final" and so on).
- **No "matches missed through injury" total.** The game has no such figure; the report gives the
  number of injuries and days lost.
- **Loans:** the game's season statistics count all of a player's club matches, for whichever club.
  Players sent out on loan this season are therefore left out of the club's statistics and listed
  separately with their (mostly loan-club) numbers. Players who joined during the season are
  marked with `*`, because their numbers may include matches for their previous club. Loans
  agreed in an earlier season are not recognised.
- **Game text stays in the game's language.** Board and supporter feedback and competition names
  are quoted from the game. With a Turkish report and an English game you will see both languages.
- **Cup matches after the league ends** (for example a cup final a week later) are not in the
  season-end report.
- **League record from results:** if the league table cannot be read, the league record is
  calculated from the club's results and the report says so; point deductions are then missing.
- Reloading a save from before the last match and playing on can change results; the report
  describes the season as you finally played it.

## Compatibility

The mod uses no Harmony patches and changes nothing in the game. It is designed to run alongside
other BepInEx 6 FM27 mods such as FM View Exporter, FMDataDisplay and FM27 Exporter, but this
combination has not been tested.

## Building from source

Requirements: .NET SDK 8 or newer, and FM27 started once with BepInEx so that
`BepInEx/interop/` exists. The game's assemblies are only local build references; they are not in
this repository and not in the release.

```sh
dotnet test tests/SeasonReview.Tests                         # no game needed
dotnet build src/SeasonReview -c Release                      # copies the plugin into the game
dotnet build src/SeasonReview -c Release -p:GameDir="C:\...\Football Manager 27"
scripts/package.sh                                            # builds dist/SeasonReview-<version>.zip
```

Notes on how the game's data is read are in [docs/DISCOVERY.md](docs/DISCOVERY.md).

## Licence

MIT, see [LICENSE](LICENSE). No code from other FM27 plugins was copied; their approach was studied
(FM View Exporter, FM27 Player Export, FMDataDisplay, FM27 Exporter). Football Manager is a
trademark of Sports Interactive and SEGA; this project is not affiliated with them.
