# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- Leagues with play-offs (e.g. TFF 1. Lig): the game gives play-off matches the league's name, so
  they were counted as league matches. The league table was then rejected as incomplete and the
  record was calculated from results with the play-offs included (41 matches instead of 38).
  Play-off matches are now recognised by the fixture's play-off flag and kept out of the league
  record, the home/away split and the league runs. They are listed as a line of their own next to
  the cups, and the report is written once the club's play-offs are over.
- Matches decided by a penalty shootout show "won/lost on penalties" in the last-match column.
  They still count as draws, as in the game's own records.
- A season could be reported again after every save: the career's name includes the game's save
  counter ("(38 defa kaydedildi)", "(saved 38 times)"), which was part of the "already reported"
  key. The counter is now left out, and entries written by older versions are read the same way,
  so seasons already in the list are not reported again.

### Changed

- The log now says when a finished season is skipped because it already has a report, once per
  season and game session, with the file to edit to allow a new report.

- The plugin version is generated from the project's `<Version>` at build time, so the log line,
  the report footer and BepInEx's plugin list can no longer disagree with the release. The load
  line also names the commit the DLL was built from (`Season Review 0.2.0 (abc1234) loaded`), which
  makes an outdated copy in the plugins folder visible. `package.sh` checks the built DLL's version.

- The default `PanelHotkey` is now `Ctrl+F7` instead of `F10`, which ManagerLife also uses (and
  always listens to). Existing config files keep their value; when it is `F10` and ManagerLife is
  loaded, the log suggests changing it.

### Documentation

- README: a "Hotkey conflicts" section on changing `Hotkey` (Ctrl+F8) and `PanelHotkey` (F10), and
  notes where the keys are mentioned that both can be changed in the config.

### Planned

- Season-end check during holidays: run the once-a-day season check while the game is processing a
  holiday (not during matches), so that holidaying past the last league match into the next season
  no longer misses the report. Needs testing for performance and stability first.

## [0.2.0] - 2026-11-14

Tested with Football Manager 27.0.1 and BepInEx 6.0.0-be.793 on Linux with Proton.
Windows: developed and tested on Linux (Proton). It uses the same BepInEx IL2CPP setup as other FM27
plugins, so it should work on Windows too, but I have not been able to test it there yet. If you try
it, please report how it went in Issues, and confirmed setups will be listed here.

### Added

- In-game panel: press F10 in a loaded career to see the season review inside the game. It reads
  the live data of the loaded career and shows the same report as the HTML file in seven tabs
  (Summary, League, Players, Transfers, Finances, Injuries, Board), in the configured language.
  Closes with its Close button, Esc or F10. It is an extra: the HTML and Markdown report are
  written exactly as before, also while the panel is open or loading. It draws its own UI Toolkit
  overlay, changes no game screen and switches itself off for the session after its first error.
  Tested on Linux/Proton only.
- Settings in a new `[Panel]` section: `PanelEnabled` (default `true`), `PanelHotkey` (default
  `F10`) and `PanelPosition` (`Center`, `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`;
  default `Center`).
- Disk-write guard: every file the mod writes (reports, the list of reported seasons, developer
  dumps) checks a guard first, and the panel runs with writes forbidden, so it cannot write a
  report or mark a season as reported. Report writes are never blocked by the panel.
- `tools/InteropScan`, a developer tool that lists which Unity methods are stripped from the game
  build. Not part of the release package.

### Dropped

- Opening the report in the browser from the game. `Application.OpenURL` and `cmd /c start` do
  nothing under Proton, so the idea was dropped in favour of the in-game panel.

## [0.1.0] - 2026-11-14

First release. Tested with Football Manager 27.0.1 and BepInEx 6.0.0-be.793 on Linux with Proton.
Not tested on Windows.

### Added

- Automatic season review after the club's last league match, written once per season
  (remembered across game sessions; save files are never touched).
- Interim report for the season so far with Ctrl+F8.
- Report as one self-contained HTML file plus a Markdown copy, in
  `Documents/Sports Interactive/Football Manager 27/SeasonReviews/`.
- Sections: season summary (finish, record, media prediction, board expectations with the board's
  verdict, cups, key moments), league performance with home/away split and longest runs, players
  (player of the season, scorers, assists, ratings, young players, players out on loan), transfers,
  finances, injuries, board and supporter confidence.
- English and Turkish report text, one file per language, with plural forms and an optional
  dictionary for game text (board objectives, injury names, financial status).
- Settings: enable/disable, automatic report, language, output folder, hotkey.
- Sections that cannot be read show "Data unavailable" instead of failing.
- `install-linux.sh` for Linux/Proton.

### Known limitations

See "Known limitations" in the README.
