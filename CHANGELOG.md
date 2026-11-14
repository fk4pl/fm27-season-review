# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Planned

- Season-end check during holidays: run the once-a-day season check while the game is processing a
  holiday (not during matches), so that holidaying past the last league match into the next season
  no longer misses the report. Needs testing for performance and stability first.

## [0.2.0] - 2026-11-14

Tested with Football Manager 27.0.1 and BepInEx 6.0.0-be.793 on Linux with Proton.
Not tested on Windows.

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
