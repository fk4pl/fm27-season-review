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

In-game version: **27.0.1** (reported by the user from the main menu; not present in any log).

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

## 5. How the game exposes data (verified in the interop assemblies)

Interop assemblies: 182 DLLs in `<game>/BepInEx/interop/`, generated by BepInEx be.793 on first
launch (game version **27.0.1**, Steam build 26577676). BepInEx loaded cleanly under Proton
(`.NET 6.0.7`, `Wine 11.0`), no errors in `LogOutput.log`.

The simulation is native (`game_plugin.dll`, ~408 MB). Managed code never holds game objects; it
holds **references** and asks the native side for **properties** of them over **channels**:

| Concept | Verified type / member |
|---|---|
| Reference to a game object | `SI.Interop.InteropReference` (list of `InteropReference.Pair { uint m_id; int m_value; }`) |
| Root of the game | `FM.UI.GameReference : InteropReference`, parameterless ctor |
| Database records | `FM.UI.ClubReference`, `TeamReference`, `PersonReference`, `CompReference` (all `: DatabaseRecordReference`), ctors `(int index)` and `(List<Pair> ids)` |
| Property id | `SI.Bindable.Reference.Core.PropertyID { uint id; }`; name ↔ id through `PropertyIdentifierSet` (`GetID(ReadOnlySpan<char>)`, `GetInfo(id)`) and `IdentifierUtils.GetName` |
| Per-type property list | every `*Reference` class has static `GetPropertiesInternal(List<PropertyID>)`, `AcceptsPropertyInternal(uint)`, `GetPropertyTypeInternal(uint)`, `GetPropertyDescriptionInternal(uint)` |
| Requesting a value | `FM.GamePlugin.GameInteropSubsystem.AddNode(Key, Key parent, PropertyID)`, `OpenChannel(InteropReference, PropertyID, Key)`, `CloseChannel(Key)`, `RemoveNode(Key)` |
| Receiving a value | asynchronous: `GameInteropSubsystem.OnChannelDataChange` event (`ValueChangedWithSizeCallback`) / `GameReceiver.SendDataResponse…`; values are `SI.Core.TypedValue` |
| Dates | `FM.GamePlugin.GameDate` (`m_packedDate`, static `ToDateTime`) |
| Game events | `FM.GamePlugin.InteropEventIDs` (e.g. `EventFmxLoadedSaveGame = 10`, `EventFmxContinueGame = 13`, `EventFmxSaveGame = 14`, `EventFmxExitMatch = 17`) |

Consequences:

- Reading is a **request/response** protocol, not a field read. All reads must be queued, awaited
  and timed out. It is read-only as long as we only open/close channels and never call
  `SendEvent`, `SetData` or `UpdateChannelData`.
- FM.UI contains 974 `*Reference` classes. They are thin descriptors; the **property names are
  runtime data**. Names were found as string literals in `global-metadata.dat`, which proves the
  name exists in this build, but **not which reference type owns it nor its value type**. That
  mapping must be dumped at runtime (Stage 1 probe: call `GetPropertiesInternal` on the reference
  types listed below and log name, kind and description). No property name below may be used in
  code before that dump confirms it.
- Receiving values needs either subscribing to `OnChannelDataChange` or one narrow Harmony patch
  (as FMDataDisplay does). To be decided in Stage 1; prefer the event if it delivers our keys.
- **UNVERIFIED:** whether our own nodes can hang off a root key without a UI screen being open
  (FMDataDisplay needed a person node from the UI). If not, fallback: ask the user to open a
  specific screen, or read on-screen tables like the exporter plugins.

## 6. Data sources for the report

Legend: **type** = reference class verified in `FM.UI.dll`; **name** = property-name literal
verified in metadata (owner/type still to be confirmed at runtime); **missing** = nothing found.

| Need | Reference types (verified) | Property-name literals (verified to exist) | Assessment |
|---|---|---|---|
| In-game date / season | `GameReference`, `CalendarReference` | `CurrentGameDate`, `CurrentGameDateString`, `CurrentGameYear`, `SeasonYear`, `SeasonYearLong` | likely |
| Human manager's club | `GameReference`, `TeamReference`, `ClubReference` | `HumanTeam`, `HumanTeamNewsHeadline`, … | likely |
| League table | `LeagueTableRowReference`, `LeagueCombinedTableRowReference`, `CompetitionStageReference` | `LeagueTable`, `LeagueTableRow`, `LeagueTableAroundTeam`, `LeaguePosition`, `LeaguePositionString`, `LeaguePoints`, `GamesPlayed`, `GamesWon`, `GamesDrawn`, `GamesLost`, `TotalGoalsFor`, `TotalGoalsAgainst`, `TotalGoalDifference` | likely |
| Home/away split | — | no `Home…Won`-style literals found | **uncertain**: derive from the fixture list, or read the table's home/away view |
| Longest winning / unbeaten run | `LastFiveMatchesFormReference`, `MatchFormReference` | none for "longest run" | **compute ourselves** from results |
| Fixtures / results | `TeamFixtureReference`, `FixtureReference`, `FixtureListElementReference`, `CompAndFixturesReference` | `Fixtures`, `FixturesCount`, `TeamFixtureList`, `HomeGoals`, … | likely |
| Cup results | `ClubSeasonSummaryCompRowReference`, `SeasonSummaryCompetitionReference`, `CompTreeReference` | `SeasonSummary`, `SeasonSummaryCompetitions`, `SeasonSummaryOverview`, `…FateString` | likely |
| Player season stats | `PersonReference`, `ClubSeasonSummaryPlayerRowReference`, `SeasonStatsReference`, `SeasonStatsRowReference` | `Appearances`, `Minutes`, `Goals`, `Assists`, `AverageRating`, `ClubAppearancesThisSeason`, `ClubGoalsThisSeason`, `ClubAssistsThisSeason`, `ClubAverageRatingThisSeason`, `Age` | likely (FMDataDisplay reads the first five) |
| Transfers | `PastTransferReference`, `CompletedTransferReference`, `SeasonTransferInfoReference`, `TransferInOutReference`, `TransferWindowNetSpendReference` | `SeasonTransfersInCount/InPlayers/InTotalValue`, `SeasonTransfersOutCount/OutPlayers/OutTotalValue`, `SeasonTransfersNetSpend`, `PastTransfersIncoming/Outgoing/InAmount/OutAmount`, `TransferFee`, `TransferInOutFeeString` | likely |
| Finances | `IClubFinanceOverviewReference`, `FinanceBreakdownRowReference`, `FinanceWagesDataReference`, `FinanceProjectionsDataReference` | `ClubFinanceOverview`, `OverallBalance`, `TotalIncomeThisSeason`, `TotalIncomeLastSeason`, `TotalWageExpenditure`, `BankBalanceItems` | likely for end balance, income, wages; **season-start balance uncertain** (may need a balance-history graph or our own snapshot at season start) |
| Board expectations | `ClubSeasonExpectationsCompetitionReference`, `ClubVisionObjectiveReference`, `AdjustSeasonExpectationsReference` | `ClubSeasonExpectationsCompetitionList`, `BoardExpectationsList`, `BoardExpectedResult`, `ClubVisionObjectivesList` | likely |
| Board confidence | `TeamConfidenceReference`, `TransferConfidenceReference`, `ManagerSupportReference` | `BoardConfidenceOverallGrade`, `BoardConfidenceOverallString`, `BoardMatchGrade`, `BoardFeedbackHighlights`, `BoardFeedbackCriticism` | likely |
| Supporter confidence | `SupportersFeedbackReference`, `SupporterProfileReference` | `SupporterConfidenceOverallGrade`, `SupporterConfidenceOverallString`, `SupporterFeedbackHighlights`, `SupporterFeedbackCriticism` | likely |
| Injuries | `InjuryListItemReference`, `PlayerInjuryReference`, `TeamCurrentInjuryDataReference`, `CompTeamInjuriesReference`, `CompPlayerInjuryReference` | `CurrentInjuryList`, `CompPlayerInjuryDate/Description/Duration/Count`, `TotalInjuriesCount`, `InjuriesPercentageMissed`, `GamesMissedInARow` | **partial**: current injuries and per-competition injury tables exist; a season-long "matches missed" total was not found and may have to be approximated |
| Inbox / notification | `NewsItemReference`, `FM.UI.NewsItem`, `NewsItemList` | `HumanPortalMessage`, `HumanPortalNewMessages`, `NewsItems`, `NewsItemId`, `NewsItemTimeSent` | reading is likely; **creating** an inbox item has no known API and would mean changing game state, so it is out of scope. Stage 4 should use an overlay notice instead |
| Season-end detection | — | `ClubSeasonExpectationsIsCompetitionFinished`, `LeagueMaxGames`, `GamesPlayed` | plan: poll after each `EventFmxContinueGame`/match exit; season is over when league games played equals league max games (or the "competition finished" flag) |

Hidden values: literals such as `CurrentAbility…`, `PotentialAbility…` and many `Debug…`
properties exist. The mod must use an **allow-list** of property names so none of these can ever
be requested or logged; the Stage 1 dump must filter them out before writing to the log.

## 7. Open questions for Stage 1

1. Can a plugin-owned node be attached and a channel opened on `new GameReference()` without a
   specific screen being open?
2. Which reference type owns each property in section 6, and what `TypedValue` kind is returned
   (number, string, list of references)?
3. Does `OnChannelDataChange` deliver values for plugin-owned keys, or is a Harmony patch needed?
4. Is there a season-start bank balance, or must the mod snapshot it itself?
