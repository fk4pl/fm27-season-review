# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
