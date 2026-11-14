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

## 7. Stage 1 results (verified in the running game, 27.0.1)

Tested with the Stage 1 plugin on 2026-11-13 in a Beşiktaş career, from the portal screen, with
none of the relevant screens open. No crash, no errors in `LogOutput.log`.

### The binding tree is the way in

- `SI.Core.GameSubsystems.TryGet<SI.Bindable.BindingSubsystem>(out b)` returns the UI binding
  tree (`BindingSubsystem : Bindings`). `b.m_root` is a `Bindings.Node`; nodes expose `Name`,
  `Key`, `Value` (`TypedValue`), `FirstChild`, `NextSibling`. About 5,000 nodes on a normal screen.
- A permanent node named `game` (value: `GameReference`) sits one level below the root (parent
  name looked like `GameWorldBackground43`; the suffix is not stable, so search by name).
- `b.Bind(ref gameKey, ref span, owner, callback)` with a dot-separated **property path** makes
  the game resolve it like it does for its own screens; `b.Get(ref key)` returns the value a few
  frames later. The path span comes from `Il2CppSystem.MemoryExtensions.AsSpan(string)`; the
  callback is a `Bindings.ValueChangedCallback` converted from `Action<Bindings.Key, TypedValue>`.
  Afterwards `b.Unbind(ref key, callback)`. **No Harmony patch is needed** and nothing is sent
  to the simulation except the read subscription.
- Chains of at least three references resolve without the screen being open, e.g.
  `GameCurrentManagerClubTeam.TeamConfidence.BoardConfidenceOverallString`.

This supersedes the channel-level plan in section 5 (`AddNode`/`OpenChannel`), which stays
documented only as background.

### Values read successfully

| Path under `game` | Result |
|---|---|
| `CurrentGameDate` | `GameDate { Data1 = N, Type = FmdateSiDate }`, N = year << 17 \| one-based day of year << 8 \| low byte (meaning unknown, changes daily) |
| `CurrentGameYear` | number |
| `GameCurrentManagerClubTeam.Name`, `.Club.Name`, `.Division.Name` | link markup + visible text (see below) |
| `GameCurrentManagerClubTeam.LeaguePositionString` | `4th` |
| `…TeamConfidence.BoardConfidenceOverallString` / `SupporterConfidenceOverallString` | the game's own sentence |
| `…Club.OverallBalance`, `…Club.TotalIncomeThisSeason` | plain numbers in **pounds sterling**, whatever currency the user displays (see section 9) |

`CurrentGameDateString` never answered (10 s timeout); decode `CurrentGameDate` instead.
Day of year is **one-based** (confirmed by the user: day 180 of 2026 was shown in game as
29 June 2026, day 181 as 30 June).

Name properties are returned as a clickable link: a base64 blob (link target and a
length-prefixed tooltip such as "Click to view Beşiktaş JK") immediately followed by the visible
text. `GameText.StripLink` removes the blob. Strings come in the game's UI language.

### Property ownership (from the runtime dump: 972 reference types, 14,505 properties)

Dump files are written to `<game>/BepInEx/SeasonReview/probe-properties.txt` and
`probe-tree.txt` (names and value types only, hidden values skipped; not part of the repository).

| Need | Owner type | Properties |
|---|---|---|
| Date, human club | `GameReference` | `CurrentGameDate`, `CurrentGameYear`, `GameCurrentManager`, `GameCurrentManagerClubTeam`, `GameStartDate` |
| Team basics | `TeamReference` | `Name`, `Club`, `Division`, `LeaguePosition`, `LeaguePositionString`, `Players`, `FixtureList`, `RemainingLeagueFixtures`, `TeamConfidence`, `CurrentInjuryList`, `SeasonInjurySummaryData`, `MediaExpectedPosition` |
| League table row | `LeagueTableRowReference` | `Team`, `TablePosition`, `LeagueGamesPlayed/Won/Drawn/Lost`, `LeagueGoalsFor/Against`, `LeagueGoalDifference`, `LeaguePoints`, `LeagueFate` |
| Season summary | `ClubSeasonSummaryCompRowReference` | `Competition`, `CompetitionLongName`, `DisplayString`, `Results`, `VisionObjectiveStatus` |
| Player stats | `PersonReference` | `Name`, `Age`, `DateOfBirth`, `ClubAppearancesThisSeason`, `ClubGoalsThisSeason`, `ClubAssistsThisSeason`, `ClubAverageRatingThisSeason`, `Appearances`, `Minutes`, `Goals`, `Assists`, `AverageRating` |
| Transfers | `SeasonTransferInfoReference`, `PastTransferReference`, `ClubReference` | `SeasonTransfersIn/OutCount`, `…In/OutPlayers`, `…In/OutTotalValue`, `SeasonTransfersNetSpend`; `Person`, `BuyingTeam`, `SellingTeam`, `TransferValue`; `PastTransfersIncoming/Outgoing`, `PastTransfersIn/OutAmount` |
| Finances | `ClubReference`, `FinanceWagesDataReference` | `OverallBalance`, `FinanceBalanceHistory`, `TotalIncomeThisSeason`, `TotalExpenditureThisSeason`, `CurrentWageSpending`, `WageBudget`, `TransferBudget`, `ProjectionProfitLoss`; `CurrentWageTotalValue`, `CurrentWageBudgetValue` |
| Board, supporters | `TeamConfidenceReference`, `ClubReference` | `Board…`/`Supporter…ConfidenceOverallGrade/String`, `…MatchString`, `…SquadConfidenceString`, `…FeedbackHighlights/Criticism`; `ClubVisionMeetingSeasonExpectations`, `VisionObjectiveList` |
| Injuries | `PersonReference`, `PlayerInjuryReference` | `InjuriesThisSeason`, `TimeMissedThisSeason`, `InjuriesHistory`; `InjuryDate`, `InjuryDescription`, `InjuryDurationDays`, `Player` |

Still not found as ready-made properties: home/away league split and longest winning/unbeaten
runs (compute from `FixtureList`).

## 8. Open questions for Stage 2

1. How list values (`Players`, `FixtureList`, `PastTransfersIncoming`, …) are enumerated through
   binding paths (the tree shows `Items.<index>` and `Count` children under list nodes).
2. Currency unit and formatting of money values; whether `…String` variants give the game's own
   formatted text.
4. Which season a "ThisSeason" property refers to right after the league ends.
5. `InjurySusceptibility`, `InjuryRisk…` exist on `PersonReference`; they are on the hidden-value
   deny-list and must stay off the report.

## 9. Stage 2 findings (running game, 27.0.1)

### Lists and records

- List properties return `List<SI.Core.TypedValue>`; each item wraps a reference
  (`PersonReference`, `CompetitionStageReference`, …), a `SI.Bindable.DynamicReference`, or a
  plain value such as `System.String`.
- To read an item's properties the mod parks it on a private node
  (`SeasonReviewMod.<n>.src`, created with `Bindings.Set(path, value, NodeFlags.None, (SetFlags)0)`)
  and binds property paths under it. The whole `SeasonReviewMod` subtree is removed afterwards.
  Verified: 28 players with stats, past injuries, expectations, stages.
- `SI.Bindable.DynamicReference : SI.Core.Record : Dictionary<uint, TypedValue>` is a small record
  delivered in one piece; its fields can be read directly (property id → value). Examples:
  `PersonReference.Minutes` → `{ PropertyValue, Count }`, `ClubReference.FinancialStatus` →
  `{ DisplayValue, SortValue }`, `FinanceBalanceHistory` items → `{ GraphPointX (date), GraphPointY }`.

### Contexts

Many properties only answer when **context values** are present. The runtime dump lists them per
property (`GetContextsInternal`), e.g.:

| Property | Contexts |
|---|---|
| `TeamReference.FixtureList` | `PreviousAmount`, `FutureAmount`, `FixturePadding`, `YearIndex` (screen uses -1, -1, true/false, 0) |
| `CompetitionStageReference.LeagueTable` | `LeagueTableType` |
| `CompReference.SeasonTransferInfo` | `team`, `Year`, `TransferType` (screen: 0 and 8) |
| `CompReference.TransferInOutList` | `Year`, `ViewID`, `Number` |
| `ClubReference.VisionObjectiveList` | `VisionObjectiveSetter`, `VisionObjectiveType`, `VisionObjectiveYear`, `ViewID`, `VisionIsUsingPerformanceData` |
| `ClubReference.PastTransfersIncoming/Outgoing` | `StringType` |
| `PersonReference.IsLoanIn` | `club` |
| `FixtureReference.ResultType` | `team` |

In the game's screens a context is a node with that name next to (or above) the node holding the
object. The mod does the same inside its private container:
`SeasonReviewMod.<n>.<ContextName>` beside `SeasonReviewMod.<n>.src`. **Verified working** for all rows
below marked ✔.

### Money

- Raw money values are **pounds sterling** regardless of the display currency
  (`FM.GamePlugin.CashTranslatable(long poundsValue)`). Confirmed against the user's screen:
  raw balance 35,844,883 ↔ €41.72M shown; raw wage spending 1,451,677 (**per week**) ↔
  €87.87M p/a shown.
- `FM.UI.ChartCurrencyVisualFunctionLibrary.TranslateCurrencyValue(double)` is the game's own
  formatter; to be confirmed that it converts to the user's currency (**UNVERIFIED**).
- Never print raw money values in the report; always format through the game.

### Other

- Link markup in strings is `U+0001 <base64 link data> U+0002 <visible text> U+0001`.
- `Division.SeasonName` = `2026/27`; `Team.MediaExpectedPosition` = number;
  `TeamConfidence.*Grade` are numbers, `*String` the game's sentences;
  `Board/SupporterFeedbackHighlights/Criticism` are lists of strings.
- `ClubSeasonExpectationsCompetitionList` gives the competitions and an importance number, but
  not the expectation wording; the vision objective list is the candidate for that.
- `ClubAverageRatingThisSeason` is a float (round to 2 decimals).

### Verified data sources (compared with the game's screens by the user, 31 Aug 2026 save)

| Report item | Source | Context values | Status |
|---|---|---|---|
| Fixtures and results | `Team.FixtureList` → `FixtureReference`: `FixtureDate`, `Competition.Name`, `Competition.IsFriendly`, `HomeTeam.Name`, `AwayTeam.Name`, `HomeScore`, `AwayScore`, `HasResult`, `Result` | `PreviousAmount=-1`, `FutureAmount=-1`, `FixturePadding=false`, `YearIndex=0` | ✔ 54 fixtures |
| League table | `Division.CurrentStages[]` → `LeagueTable` rows (records): `Team.Name`, `TablePosition`, `LeagueGames*`, `LeagueGoals*`, `LeaguePoints` | `LeagueTableType=0` | ✔ matches the game |
| Transfers | `Division.SeasonTransferInfo.SeasonTransfersInPlayers/OutPlayers` (records): `Player.Name`, `BuyingTeam.Name`, `SellingTeam.Name`, `Fee`, `IsOnLoan`, `Date` | `team`=the team, `Year`=season start year, `TransferType=0` | ✔ matches the game |
| Board expectations | `Club.VisionObjectiveList` (records): `VisionObjectiveName`, `VisionObjectiveCategory` (1 = competition target), `VisionObjectivePerformance` (the board's wording), `VisionObjectiveYearString`, `Competition.Name` | `VisionObjectiveSetter=1`, `VisionObjectiveType=0`, `VisionObjectiveYear=0`, `ViewID=0`, `VisionIsUsingPerformanceData=true` | ✔ |
| Balance at season start | `Club.FinanceBalanceHistory` (records `GraphPointX` date, `GraphPointY` value), one point per month; use the point nearest the season start (1 July for "2026/27", 1 January for "2026") | none | ✔ |
| Income / expenditure | `Club.TotalIncomeThisSeason`, `Club.TotalExpenditureThisSeason` | none | ✔ equal to the finance screen's "This Season" totals. These are accounting totals, not cash (sponsorship is booked up front; June transfers fall before the season), so income − expenditure ≠ change in balance. The report shows both, separately labelled. Breakdown lists: `IncomeBreakdownThisSeason`, `ExpenditureBreakdownThisSeason` (`Name`, `DisplayValue`, `PropertyValue`) |
| Projected profit/loss | `Club.ProjectionProfitLoss.ProjectionEndOfSeasonOne.PropertyValue` (nested records) | none | ✔ equals Finances > Projections |
| Player minutes | `Person.Minutes.PropertyValue` | none | ✔ |
| Injuries | `Team.SeasonInjurySummaryData.PastInjuries` (records): `Player.Name`, `InjuryDescription`, `InjuryDate`, `InjuryDurationDays`, `IsActive`; counts `Major/Moderate/Minor/SlightInjuryCount` | none | ✔ |

Not available:

- **Cup round names**: `FixtureReference.StageName` is a `FixtureNameReference` whose `Name` needs
  a `StringType` context of unknown value. Cup lines show the last match without the round.
- **Matches missed through injury**: no season total; the report gives injuries and days lost.
- `Team.LeaguePosition` can lag behind the table; the table row's `TablePosition` is used.

## 10. Stage 3 findings (running game, 27.0.1, 23 May 2027 save)

- **Season-end trigger** verified: the daily check saw "31 played, 3 remaining" … "all league
  fixtures played" and wrote the report once, on 23 May 2027.
- **League table after the season ends**: `Division.CurrentStages` returns **no stages** once the
  league is over. The finished season's table comes from `Division.AllStages` read with the
  contexts `Year=<season start year>` and `NextSeason=false` beside the division. Its items are
  tree rows (`SI.Bindable.DynamicReference`): the stage is `RowData.Stage`
  (`CompetitionStageReference`, its table needs `LeagueTableType=0`), sub-stages are rows in
  `ChildList`. Verified: the club's row is found there. If no table row is usable, the league
  record is calculated from the fixtures and the report says so.
- **Media prediction**: `Team.MediaExpectedPosition` is **not** the pre-season prediction (it
  showed the final position, 6, at season end). The League Table > Media Prediction screen's data
  is `Division.OddsLeagueTableList`: rows with `Team`, `MediaExpectedPosition`, `TablePosition`.
  Verified: 3 for Beşiktaş, as shown in game.
- **Loans**: `Team.Players` includes players out on loan, and `Club…ThisSeason` statistics count
  all club matches regardless of club (a player loaned out on 29 June showed 37 matches at the
  loan club). Loaned-out players are recognised from this season's departures (`IsOnLoan`) and
  kept out of the club's statistics; players who joined after the first competitive match are
  flagged.
- **Re-simulation**: reloading a save made before the last match and playing on can change
  results (e.g. a player's goals 22 → 21); the report reflects the save that was played.
- Injury names seen: "A sports hernia", "A twisted ankle", "A tight hamstring", "A bruised
  ankle", "A cut leg", "A tight groin", "A tight thigh", "A double hernia", "A hip injury",
  "A moderate calf strain", "A twisted knee".

## 11. In-game panel spike (running game, 27.0.1, Linux/Proton)

Question: can the plugin draw its own panel on top of the game, read-only and without Harmony?

### Unity modules

BepInEx generates 182 interop assemblies. Both UI systems are present:
`UnityEngine.IMGUIModule` (IMGUI) and `UnityEngine.UIElementsModule` (UI Toolkit). The game's
own screens use UI Toolkit (`UIDocument` + `PanelSettings`, in `SI.UI`, `FM.UI`, `SI.Input`).

### IMGUI: too much is stripped

FM27 is an IL2CPP build, so engine code the game never calls is stripped. Calling a stripped
method throws `NotSupportedException: Method unstripping failed`. `OnGUI` is called and mouse and
scroll events arrive, but drawing window contents failed. Per-call probe in game:

| Works | Stripped (throws) |
|---|---|
| `Event.current`, `GUI.skin` (`.label`, `.font`), `GUI.color`, `GUI.contentColor` | `GUI.DrawTexture` |
| `new GUIStyle()`, `new GUIContent(string)`, `GUIStyle.CalcSize`, `GUIStyle.Draw` | `GUI.TextArea` |
| `GUI.Label`, `GUI.Box`, `GUI.Button` | `GUI.BeginScrollView` |
| `GUI.Window` (with its callback), `GUI.DragWindow` | all of `GUILayout` (`Label`, `Button`, `BeginScrollView`) |
| | `new GUIStyle(GUIStyle)` (copy constructor) |

A panel can be built from the working calls, but without scroll views or layout, and IMGUI cannot
stop clicks from reaching the game (the game uses the Input System). IMGUI was dropped.

### UI Toolkit: works with an own PanelSettings

- Sharing one of the game's `PanelSettings` does not work: `ShortcutLayerSettings`, the obvious
  top layer, has no theme, so labels and buttons have no default style and the content is
  invisible. Of the game's panel settings only `HighlightingLayerSettings` uses Unity's default
  runtime theme (`UnityDefaultRuntimeTheme`).
- Working recipe:
  1. Create an own `PanelSettings` with `ScriptableObject.CreateInstance(Il2CppType.Of<PanelSettings>())`.
  2. Copy `themeStyleSheet` and `textSettings` from the game's `HighlightingLayerSettings`
     (read-only; the game's object is not changed).
  3. Set `scaleMode = ScaleWithScreenSize`, reference resolution 1920x1080, `MatchWidthOrHeight`
     with `match = 0.5`, and `sortingOrder` above the highest game panel.
  4. Create an inactive `GameObject`, add a `UIDocument` with `AddComponent(Il2CppType.Of<UIDocument>())`,
     assign the panel settings, then activate it (`DontDestroyOnLoad`).
  5. Root `pickingMode = Ignore` (clicks outside the panel go to the game); the panel box
     `pickingMode = Position` (clicks on it do not go through).
  6. Take the font from a visible game label: `resolvedStyle.unityFontDefinition`, applied as
     `new StyleFontDefinition(fd)`.
  7. Use inline styles only. `StyleLength`, `StyleColor` and `StyleFloat` are value types;
     `StyleEnum<T>`, `FontDefinition` and `StyleFontDefinition` are generated as classes.
- Generic style values (`StyleEnum<FlexDirection>`, `<WhiteSpace>`, `<TextAnchor>`, `<Overflow>`)
  exist only per enum in the build and cannot be checked statically. All four were confirmed in
  game by the panel's log line ("rows side by side, wrapping on, clipping on, right-aligned numbers
  on"). The panel tries each once and falls back to a simpler layout if one fails.
- `ScrollView(ScrollViewMode.Vertical)` with `horizontalScrollerVisibility = Hidden` and a content
  container fixed to 100% width keeps tables inside the panel; cells get percentage widths with
  `minWidth 0` so long text wraps instead of widening the row.
- Result: opaque, text visible with the game's font (Turkish characters fine), scrolling, tabs and
  Close work, no click-through. Removing the own `GameObject` and `PanelSettings` leaves the game
  as it was.

### Static scan method (`tools/InteropScan`)

The interop assemblies show, without starting the game, whether a method can be called. For each
method body (read with Mono.Cecil):

- contains the string "…unstripping failed": **FAILED** (stripped, throws);
- references a `NativeMethodInfoPtr_*` field: **NATIVE** (compiled into the game, safe);
- calls through a delegate field: **ICALL** (engine-internal function);
- otherwise **MANAGED** (re-created from Unity's reference assemblies), and **BROKEN** if it
  reaches a FAILED method through its calls.

The scan predicted all 12 IMGUI probe results. It cannot prove generic instantiations
(`StyleEnum<T>`, `RegisterCallback<T>`) or run-time behaviour; those need a guarded try in game.
Every UI Toolkit member the panel uses scans as NATIVE or MANAGED.

### Opening the report in a browser does not work under Proton

`Application.OpenURL` (with a `file:///` URL and with a plain path) and starting
`cmd.exe /c start "" <file>` return without an error but open nothing under Proton. The idea was
dropped; the in-game panel replaces it.
