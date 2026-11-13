using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SeasonReview
{
    // Plain data for the report. No game types here, so this file can be unit tested.

    internal sealed class LeagueRecord
    {
        public int Played, Won, Drawn, Lost, GoalsFor, GoalsAgainst;
        public int Points => Won * 3 + Drawn;
    }

    internal sealed class MatchResult
    {
        public DateTime? Date;
        public string Competition, Stage, Opponent, Score;
        public bool Home, Friendly, League;
        public int GoalsFor, GoalsAgainst;
        public char Outcome => GoalsFor > GoalsAgainst ? 'W' : GoalsFor < GoalsAgainst ? 'L' : 'D';
    }

    internal sealed class CupRun
    {
        public string Competition, LastStage, LastResult, BoardView;
        public int Played, Won, Drawn, Lost;
    }

    internal sealed class PlayerSeason
    {
        public string Name;
        public int Age, Appearances, Goals, Assists, Minutes;
        public double AverageRating;
    }

    internal sealed class TransferDeal
    {
        public string Player, OtherClub;
        public double Fee;
        public bool Loan;
        public DateTime? Date;
    }

    internal sealed class Objective
    {
        public string Name, Competition, Season, BoardView;
    }

    internal sealed class InjuryRecord
    {
        public string Player, Description;
        public int Days;
        public bool Active;
        public DateTime? Date;
    }

    internal sealed class Confidence
    {
        public string Overall, Matches, Squad, Transfers, Vision;
        public List<string> Highlights = new(), Criticism = new();
    }

    internal sealed class SeasonData
    {
        public DateTime? Date;
        public string Club, ClubLongName, Division, Season;
        public bool SeasonComplete;

        // Summary / league
        public int? LeaguePosition, MediaPrediction, LeagueSize, PointsFromTable;
        public string LeaguePositionText;
        public LeagueRecord Total, Home, Away;
        public int LongestWinningRun, LongestUnbeatenRun;
        public int LeagueFixturesRemaining;
        public List<MatchResult> Matches = new();
        public List<CupRun> Cups = new();
        public List<Objective> Objectives = new();

        // Players
        public List<PlayerSeason> Players = new();
        public PlayerSeason PlayerOfTheSeason;

        // Transfers (raw money is pounds; format through the game)
        public List<TransferDeal> Arrivals = new(), Departures = new();
        public double? FeesPaid, FeesReceived, NetSpend;

        // Finances
        public double? BalanceStart, BalanceEnd, WeeklyWages, WeeklyWageBudget;
        /// <summary>The game's own accounting totals for the season (not cash: e.g. sponsorship
        /// is booked up front), and its projected profit/loss for the season.</summary>
        public double? Income, Expenditure, ProjectedProfit;
        public DateTime? BalanceStartDate;
        public string FinancialStatus;

        // Injuries
        public List<InjuryRecord> Injuries = new();
        public int? Major, Moderate, Minor, Slight;

        public Confidence Board = new(), Supporters = new();

        /// <summary>Sections that could not be read; the report says "data unavailable" for them.</summary>
        public List<string> Unavailable = new();
    }

    /// <summary>Turns what was read from the game into report data. Never throws: a section
    /// that cannot be built is recorded as unavailable.</summary>
    internal static class SeasonDataBuilder
    {
        public static SeasonData Build(GameRecord r, Action<string> warn)
        {
            var data = new SeasonData();
            void Section(string name, Action build)
            {
                try { build(); }
                catch (Exception e)
                {
                    data.Unavailable.Add(name);
                    warn($"Section '{name}' unavailable: {e.Message}");
                }
            }

            Section("summary", () =>
            {
                if (GameText.TryParseGameDate(r.Get("CurrentGameDate"), out var date)) data.Date = date;
                data.Club = Text(r.Get(SeasonSpec.Team + ".Name"));
                data.ClubLongName = Text(r.Get(SeasonSpec.Team + ".LongName"));
                data.Division = Text(r.Get(SeasonSpec.Division + ".Name"));
                data.Season = Text(r.Get(SeasonSpec.Division + ".SeasonName"));
                data.LeaguePosition = Int(r.Get(SeasonSpec.Team + ".LeaguePosition"));
                data.LeaguePositionText = Text(r.Get(SeasonSpec.Team + ".LeaguePositionString"));
                data.MediaPrediction = Int(r.Get(SeasonSpec.Team + ".MediaExpectedPosition"));
                if (string.IsNullOrEmpty(data.Club)) throw new InvalidOperationException("club name missing");
            });

            Section("league", () => BuildLeague(r, data));
            Section("matches", () => BuildMatches(r, data));
            Section("objectives", () => BuildObjectives(r, data));
            Section("players", () => BuildPlayers(r, data));
            Section("transfers", () => BuildTransfers(r, data));
            Section("finances", () => BuildFinances(r, data));
            Section("injuries", () => BuildInjuries(r, data));
            Section("confidence", () =>
            {
                data.Board = BuildConfidence(r, "Board");
                data.Supporters = BuildConfidence(r, "Supporter");
                if (data.Board.Overall == null && data.Supporters.Overall == null)
                    throw new InvalidOperationException("no confidence data");
            });
            return data;
        }

        private static void BuildLeague(GameRecord r, SeasonData data)
        {
            foreach (var stage in r.Items(SeasonSpec.Stages))
            {
                var rows = stage.Items(SeasonSpec.LeagueTable);
                var own = rows.FirstOrDefault(row => Text(row.Get("Team.Name")) == data.Club);
                if (own == null) continue;
                data.LeagueSize = rows.Count;
                data.LeaguePosition = Int(own.Get("TablePosition")) ?? data.LeaguePosition;
                data.PointsFromTable = Int(own.Get("LeaguePoints"));
                data.Total = new LeagueRecord
                {
                    Played = Int(own.Get("LeagueGamesPlayed")) ?? 0,
                    Won = Int(own.Get("LeagueGamesWon")) ?? 0,
                    Drawn = Int(own.Get("LeagueGamesDrawn")) ?? 0,
                    Lost = Int(own.Get("LeagueGamesLost")) ?? 0,
                    GoalsFor = Int(own.Get("LeagueGoalsFor")) ?? 0,
                    GoalsAgainst = Int(own.Get("LeagueGoalsAgainst")) ?? 0,
                };
                return;
            }
            throw new InvalidOperationException("club not found in the league table");
        }

        private static void BuildMatches(GameRecord r, SeasonData data)
        {
            var fixtures = Nested(r, SeasonSpec.Fixtures, SeasonSpec.Fixtures);
            if (fixtures.Count == 0) throw new InvalidOperationException("fixture list empty");

            foreach (var f in fixtures)
            {
                string competition = Text(f.Get("Competition.Name"));
                bool league = competition != null && competition == data.Division;
                bool played = Bool(f.Get("HasResult"));
                if (!played)
                {
                    if (league) data.LeagueFixturesRemaining++;
                    continue;
                }

                string home = Text(f.Get("HomeTeam.Name")), away = Text(f.Get("AwayTeam.Name"));
                int? homeGoals = Int(f.Get("HomeScore")), awayGoals = Int(f.Get("AwayScore"));
                if (home == null || away == null || homeGoals == null || awayGoals == null) continue;
                bool isHome = home == data.Club;
                if (!isHome && away != data.Club) continue;

                data.Matches.Add(new MatchResult
                {
                    Date = Date(f.Get("FixtureDate")),
                    Competition = competition,
                    Stage = Text(f.Get("StageName.Name")),
                    Opponent = isHome ? away : home,
                    Home = isHome,
                    League = league,
                    Friendly = Bool(f.Get("Competition.IsFriendly")),
                    GoalsFor = isHome ? homeGoals.Value : awayGoals.Value,
                    GoalsAgainst = isHome ? awayGoals.Value : homeGoals.Value,
                    Score = Text(f.Get("Result")) ?? $"{homeGoals}-{awayGoals}",
                });
            }

            var leagueMatches = data.Matches.Where(m => m.League).ToList();
            data.Home = Tally(leagueMatches.Where(m => m.Home));
            data.Away = Tally(leagueMatches.Where(m => !m.Home));
            data.LongestWinningRun = LongestRun(leagueMatches, m => m.Outcome == 'W');
            data.LongestUnbeatenRun = LongestRun(leagueMatches, m => m.Outcome != 'L');
            data.SeasonComplete = leagueMatches.Count > 0 && data.LeagueFixturesRemaining == 0;

            foreach (var group in data.Matches.Where(m => !m.League && !m.Friendly && m.Competition != null)
                         .GroupBy(m => m.Competition))
            {
                var last = group.Last();
                data.Cups.Add(new CupRun
                {
                    Competition = group.Key,
                    Played = group.Count(),
                    Won = group.Count(m => m.Outcome == 'W'),
                    Drawn = group.Count(m => m.Outcome == 'D'),
                    Lost = group.Count(m => m.Outcome == 'L'),
                    LastStage = last.Stage,
                    LastResult = $"{last.Opponent} ({(last.Home ? "H" : "A")}) {last.GoalsFor}-{last.GoalsAgainst}",
                });
            }
        }

        private static LeagueRecord Tally(IEnumerable<MatchResult> matches)
        {
            var record = new LeagueRecord();
            foreach (var m in matches)
            {
                record.Played++;
                record.GoalsFor += m.GoalsFor;
                record.GoalsAgainst += m.GoalsAgainst;
                if (m.Outcome == 'W') record.Won++;
                else if (m.Outcome == 'D') record.Drawn++;
                else record.Lost++;
            }
            return record;
        }

        public static int LongestRun(IEnumerable<MatchResult> matches, Func<MatchResult, bool> counts)
        {
            int best = 0, current = 0;
            foreach (var m in matches)
            {
                current = counts(m) ? current + 1 : 0;
                if (current > best) best = current;
            }
            return best;
        }

        private static void BuildObjectives(GameRecord r, SeasonData data)
        {
            foreach (var o in Nested(r, SeasonSpec.Objectives, SeasonSpec.Objectives))
            {
                var objective = new Objective
                {
                    Name = Text(o.Get("VisionObjectiveName")),
                    Competition = Text(o.Get("Competition.Name")),
                    Season = Text(o.Get("VisionObjectiveYearString")),
                    BoardView = Text(o.Get("VisionObjectivePerformance")),
                };
                // Category 1 = competition targets: the board's expectations for the season.
                if (objective.Name == null || Int(o.Get("VisionObjectiveCategory")) != 1) continue;
                if (string.IsNullOrEmpty(objective.BoardView)) continue; // future seasons have no verdict yet
                data.Objectives.Add(objective);
            }
            foreach (var cup in data.Cups)
                cup.BoardView = data.Objectives.FirstOrDefault(o => o.Competition == cup.Competition)?.BoardView;
            if (data.Objectives.Count == 0) throw new InvalidOperationException("no competition objectives");
        }

        private static void BuildPlayers(GameRecord r, SeasonData data)
        {
            foreach (var p in r.Items(SeasonSpec.Players))
            {
                var name = Text(p.Get("Name"));
                if (name == null) continue;
                data.Players.Add(new PlayerSeason
                {
                    Name = name,
                    Age = Int(p.Get("Age")) ?? 0,
                    Appearances = Int(p.Get("ClubAppearancesThisSeason")) ?? 0,
                    Goals = Int(p.Get("ClubGoalsThisSeason")) ?? 0,
                    Assists = Int(p.Get("ClubAssistsThisSeason")) ?? 0,
                    AverageRating = Math.Round(Number(p.Get("ClubAverageRatingThisSeason")) ?? 0, 2),
                    Minutes = Int(p.Get("Minutes.PropertyValue")) ?? 0,
                });
            }
            if (data.Players.Count == 0) throw new InvalidOperationException("no players");
            data.PlayerOfTheSeason = PickPlayerOfTheSeason(data.Players);
        }

        /// <summary>
        /// Simple score from visible stats only: average rating, weighted by how much of the
        /// season the player took part in. Needs at least a third of the most-used player's
        /// appearances.
        /// </summary>
        public static PlayerSeason PickPlayerOfTheSeason(List<PlayerSeason> players)
        {
            int most = players.Count == 0 ? 0 : players.Max(p => p.Appearances);
            if (most == 0) return null;
            return players
                .Where(p => p.AverageRating > 0 && p.Appearances * 3 >= most)
                .OrderByDescending(p => p.AverageRating * (0.75 + 0.25 * p.Appearances / most))
                .ThenByDescending(p => p.Appearances)
                .FirstOrDefault();
        }

        private static void BuildTransfers(GameRecord r, SeasonData data)
        {
            var root = r.Items(SeasonSpec.Transfers).FirstOrDefault()
                       ?? throw new InvalidOperationException("transfer info missing");
            if (!root.Lists.ContainsKey(SeasonSpec.TransfersIn) && !root.Lists.ContainsKey(SeasonSpec.TransfersOut))
                throw new InvalidOperationException("transfer lists missing");

            TransferDeal Deal(GameRecord t, bool arrival) => new TransferDeal
            {
                Player = Text(t.Get("Player.Name")) ?? "?",
                OtherClub = Text(t.Get(arrival ? "SellingTeam.Name" : "BuyingTeam.Name")),
                Fee = Number(t.Get("Fee")) ?? 0,
                Loan = Bool(t.Get("IsOnLoan")),
                Date = Date(t.Get("Date")),
            };
            data.Arrivals = root.Items(SeasonSpec.TransfersIn).Select(t => Deal(t, true)).ToList();
            data.Departures = root.Items(SeasonSpec.TransfersOut).Select(t => Deal(t, false)).ToList();
            data.FeesPaid = data.Arrivals.Sum(t => t.Fee);
            data.FeesReceived = data.Departures.Sum(t => t.Fee);
            data.NetSpend = data.FeesPaid - data.FeesReceived;
        }

        private static void BuildFinances(GameRecord r, SeasonData data)
        {
            data.BalanceEnd = Number(r.Get(SeasonSpec.Club + ".OverallBalance"));
            data.Income = Number(r.Get(SeasonSpec.Club + ".TotalIncomeThisSeason"));
            data.Expenditure = Number(r.Get(SeasonSpec.Club + ".TotalExpenditureThisSeason"));
            data.ProjectedProfit = ProjectionValue(r, SeasonSpec.Club + ".ProjectionProfitLoss.ProjectionEndOfSeasonOne");
            data.WeeklyWages = Number(r.Get(SeasonSpec.Club + ".CurrentWageSpending"));
            data.WeeklyWageBudget = Number(r.Get(SeasonSpec.Club + ".WageBudget"));
            data.FinancialStatus = Text(r.Get(SeasonSpec.Club + ".FinancialStatus.DisplayValue"));

            // Balance history: one point per month. The season starts on 1 July for seasons
            // named "2026/27" and on 1 January for calendar-year seasons named "2026".
            var start = SeasonStart(data.Season);
            if (start != null)
            {
                double bestDistance = 20; // days; no usable point = "data unavailable"
                foreach (var point in r.Items(SeasonSpec.BalanceHistory))
                {
                    var date = Date(point.Get("GraphPointX"));
                    var value = Number(point.Get("GraphPointY"));
                    if (date == null || value == null) continue;
                    double distance = Math.Abs((date.Value - start.Value).TotalDays);
                    if (distance > bestDistance) continue;
                    bestDistance = distance;
                    data.BalanceStart = value;
                    data.BalanceStartDate = date;
                }
            }
            if (data.BalanceEnd == null) throw new InvalidOperationException("balance missing");
        }

        private static void BuildInjuries(GameRecord r, SeasonData data)
        {
            data.Major = Int(r.Get(SeasonSpec.InjurySummary + ".MajorInjuryCount"));
            data.Moderate = Int(r.Get(SeasonSpec.InjurySummary + ".ModerateInjuryCount"));
            data.Minor = Int(r.Get(SeasonSpec.InjurySummary + ".MinorInjuryCount"));
            data.Slight = Int(r.Get(SeasonSpec.InjurySummary + ".SlightInjuryCount"));
            if (!r.Lists.ContainsKey(SeasonSpec.PastInjuries)) throw new InvalidOperationException("injury list missing");
            foreach (var i in r.Items(SeasonSpec.PastInjuries))
            {
                data.Injuries.Add(new InjuryRecord
                {
                    Player = Text(i.Get("Player.Name")) ?? "?",
                    Description = Text(i.Get("InjuryDescription")),
                    Days = Int(i.Get("InjuryDurationDays")) ?? 0,
                    Active = Bool(i.Get("IsActive")),
                    Date = Date(i.Get("InjuryDate")),
                });
            }
        }

        private static Confidence BuildConfidence(GameRecord r, string who)
        {
            string c = SeasonSpec.Confidence + "." + who;
            return new Confidence
            {
                Overall = Text(r.Get(c + "ConfidenceOverallString")),
                Matches = Text(r.Get(c + "MatchString")),
                Squad = Text(r.Get(c + "SquadConfidenceString")),
                Transfers = Text(r.Get(c + "TransferConfidenceOverallSummaryText")),
                Vision = Text(r.Get(c + "VisionString")),
                Highlights = r.Items(who + "Highlights").Select(i => Text(i.Get("."))).Where(s => s != null).ToList(),
                Criticism = r.Items(who + "Criticism").Select(i => Text(i.Get("."))).Where(s => s != null).ToList(),
            };
        }

        // Items of an inner list, across all items of an outer list.
        private static List<GameRecord> Nested(GameRecord r, string outer, string inner)
        {
            var result = new List<GameRecord>();
            foreach (var parent in r.Items(outer)) result.AddRange(parent.Items(inner));
            return result;
        }

        // A projection point is a small record; take its numeric value field.
        private static double? ProjectionValue(GameRecord r, string path)
        {
            foreach (var suffix in new[] { ".PropertyValue", ".Value", ".GraphPointY" })
            {
                var value = Number(r.Get(path + suffix));
                if (value != null) return value;
            }
            return null;
        }

        /// <summary>First day of the season: 1 July of the first year for "2026/27", 1 January for "2026".</summary>
        public static DateTime? SeasonStart(string seasonName)
        {
            if (seasonName == null || seasonName.Length < 4 || !int.TryParse(seasonName.Substring(0, 4), out var year)) return null;
            return seasonName.Length > 4 ? new DateTime(year, 7, 1) : new DateTime(year, 1, 1);
        }

        // ── Value parsing ──────────────────────────────────────────────────

        public static string Text(string raw)
        {
            if (raw == null) return null;
            var text = string.Join(" ", GameText.StripLink(raw).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length == 0) return null;
            // An unresolved object prints as its class name; never let that into the report.
            if (text.StartsWith("FM.", StringComparison.Ordinal) || text.StartsWith("SI.", StringComparison.Ordinal)
                || text.StartsWith("System.", StringComparison.Ordinal)) return null;
            return text;
        }

        public static double? Number(string raw) =>
            raw != null && double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

        public static int? Int(string raw)
        {
            var n = Number(raw);
            return n == null ? null : (int)Math.Round(n.Value);
        }

        public static bool Bool(string raw) => string.Equals(raw?.Trim(), "True", StringComparison.OrdinalIgnoreCase);

        public static DateTime? Date(string raw)
        {
            if (raw == null) return null;
            if (GameText.TryParseGameDate(raw, out var gameDate)) return gameDate;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
        }
    }
}
