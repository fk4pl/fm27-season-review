using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeasonReview;

namespace SeasonReview.Tests
{
    /// <summary>Fictional season data shaped like what the game returns (no real game data).</summary>
    internal static class TestData
    {
        public const string Club = "Kartal SK";
        public const string League = "Example League";

        public static Strings English() => Load("en");

        public static Strings Turkish() => Strings.Parse("tr", File.ReadAllText(Lang("tr")), English());

        public static Strings Load(string code) => Strings.Parse(code, File.ReadAllText(Lang(code)));

        private static string Lang(string code) => Path.Combine(AppContext.BaseDirectory, "lang", code + ".txt");

        /// <summary>Raw date value as the game prints it.</summary>
        public static string GameDate(int year, int month, int day)
        {
            int dayOfYear = new DateTime(year, month, day).DayOfYear;
            uint packed = (uint)(year << 17 | dayOfYear << 8 | 13);
            return $"GameDate {{ Data1 = {packed}, Type = FmdateSiDate }}";
        }

        /// <summary>A link-wrapped name, as names arrive from the game.</summary>
        public static string Link(string text) => "\u0001QUJDREVGR0g=\u0002" + text + "\u0001";

        public static GameRecord Fixture(string competition, string home, string away, int? homeGoals, int? awayGoals,
            DateTime date, bool friendly = false)
        {
            var f = new GameRecord();
            f.Values["FixtureDate"] = GameDate(date.Year, date.Month, date.Day);
            f.Values["Competition.Name"] = Link(competition);
            f.Values["Competition.IsFriendly"] = friendly ? "True" : "False";
            f.Values["HomeTeam.Name"] = Link(home);
            f.Values["AwayTeam.Name"] = Link(away);
            f.Values["HasResult"] = homeGoals != null ? "True" : "False";
            f.Values["HomeScore"] = homeGoals?.ToString() ?? "";
            f.Values["AwayScore"] = awayGoals?.ToString() ?? "";
            return f;
        }

        /// <summary>A whole season's raw read. <paramref name="remaining"/> league fixtures are unplayed.</summary>
        public static GameRecord Season(int remaining = 0)
        {
            var r = new GameRecord();
            void V(string path, string value) => r.Values[path] = value;
            V("CurrentGameDate", GameDate(2027, 5, 20));
            V("GameName", "Test career");
            V("GameStartDate", "Nation - 29/6/2026");
            V(SeasonSpec.Team + ".Name", Link(Club));
            V(SeasonSpec.Team + ".LongName", Link(Club + " FC"));
            V(SeasonSpec.Team + ".LeaguePosition", "2");
            V(SeasonSpec.Team + ".MediaExpectedPosition", "3");
            V(SeasonSpec.Division + ".Name", Link(League));
            V(SeasonSpec.Division + ".SeasonName", "2026/27");
            V(SeasonSpec.Club + ".OverallBalance", "30000000");
            V(SeasonSpec.Club + ".TotalIncomeThisSeason", "90000000");
            V(SeasonSpec.Club + ".TotalExpenditureThisSeason", "60000000");
            V(SeasonSpec.Club + ".CurrentWageSpending", "1000000");
            V(SeasonSpec.Club + ".ProjectionProfitLoss.ProjectionEndOfSeasonOne.PropertyValue", "-1500000");
            V(SeasonSpec.Club + ".FinancialStatus.DisplayValue", "Okay");
            V(SeasonSpec.Confidence + ".BoardConfidenceOverallString", "The board are pleased.");
            V(SeasonSpec.Confidence + ".SupporterConfidenceOverallString", "The fans are\n delighted.");
            V(SeasonSpec.InjurySummary + ".MajorInjuryCount", "1");

            // League table
            var stage = new GameRecord();
            stage.Lists[SeasonSpec.LeagueTable] = new List<GameRecord>
            {
                Row("Rival FC", 1, 6, 4, 1, 1, 10, 4, 13),
                Row(Club, 2, 6, 3, 2, 1, 9, 6, 11),
                Row("Third United", 3, 6, 0, 0, 6, 2, 15, 0),
            };
            r.Lists[SeasonSpec.Stages] = new List<GameRecord> { stage };

            // Fixtures: friendly, 6 league matches (WWDLWD), one cup run, then unplayed ones.
            var d = new DateTime(2026, 7, 10);
            var fixtures = new List<GameRecord>
            {
                Fixture("Friendly", Club, "Visitors", 9, 0, d, friendly: true),
                Fixture(League, Club, "Rival FC", 2, 0, d.AddDays(30)),
                Fixture(League, "Third United", Club, 0, 4, d.AddDays(37)),
                Fixture(League, Club, "Rival FC", 1, 1, d.AddDays(44)),
                Fixture(League, "Rival FC", Club, 3, 0, d.AddDays(51)),
                Fixture(League, Club, "Third United", 2, 1, d.AddDays(58)),
                Fixture(League, "Third United", Club, 2, 2, d.AddDays(65)),
                Fixture("Example Cup", Club, "Small Town", 3, 1, d.AddDays(70)),
                Fixture("Example Cup", "Big City", Club, 2, 1, d.AddDays(90)),
            };
            for (int i = 0; i < remaining; i++)
                fixtures.Add(Fixture(League, Club, "Rival FC", null, null, d.AddDays(100 + i)));
            var team = new GameRecord();
            team.Lists[SeasonSpec.Fixtures] = fixtures;
            r.Lists[SeasonSpec.Fixtures] = new List<GameRecord> { team };

            r.Lists[SeasonSpec.Players] = new List<GameRecord>
            {
                Player("Striker One", 27, 30, 20, 3, 7.4, 2500),
                Player("Keeper Two", 31, 32, 0, 0, 7.1, 2880),
                Player("Sub Three", 24, 4, 1, 0, 8.6, 120),
                Player("Youngster Four", 19, 12, 2, 4, 6.9, 700),
                Player("Unused Five", 18, 0, 0, 0, 0, 0),
            };

            var transfers = new GameRecord();
            transfers.Lists[SeasonSpec.TransfersIn] = new List<GameRecord>
            {
                Deal("Striker One", "Old Club", null, 12000000, false),
                Deal("Loanee", "Parent FC", null, 0, true),
            };
            transfers.Lists[SeasonSpec.TransfersOut] = new List<GameRecord> { Deal("Seller", null, "Buyer FC", 5000000, false) };
            r.Lists[SeasonSpec.Transfers] = new List<GameRecord> { transfers };

            var objectives = new GameRecord();
            objectives.Lists[SeasonSpec.Objectives] = new List<GameRecord>
            {
                Objective("Challenge for the league title", 1, League, "The board are delighted."),
                Objective("Work within wage budget", 2, null, "Fine."),
                Objective("Maintain a title challenge", 1, League, ""), // a future season: no verdict yet
            };
            r.Lists[SeasonSpec.Objectives] = new List<GameRecord> { objectives };

            r.Lists[SeasonSpec.BalanceHistory] = new List<GameRecord>
            {
                Point(2026, 6, 1, 40000000), Point(2026, 7, 1, 38000000), Point(2026, 8, 1, 35000000),
            };
            r.Lists["BoardHighlights"] = new List<GameRecord> { Text("Pleased with the results.") };

            var injury = new GameRecord();
            injury.Values["Player.Name"] = Link("Keeper Two");
            injury.Values["InjuryDescription"] = "A broken toe";
            injury.Values["InjuryDurationDays"] = "21";
            injury.Values["IsActive"] = "False";
            injury.Values["InjuryDate"] = GameDate(2026, 9, 1);
            r.Lists[SeasonSpec.PastInjuries] = new List<GameRecord> { injury };
            return r;
        }

        private static GameRecord Row(string team, int pos, int p, int w, int d, int l, int gf, int ga, int pts)
        {
            var row = new GameRecord();
            row.Values["Team.Name"] = Link(team);
            row.Values["TablePosition"] = pos.ToString();
            row.Values["LeagueGamesPlayed"] = p.ToString();
            row.Values["LeagueGamesWon"] = w.ToString();
            row.Values["LeagueGamesDrawn"] = d.ToString();
            row.Values["LeagueGamesLost"] = l.ToString();
            row.Values["LeagueGoalsFor"] = gf.ToString();
            row.Values["LeagueGoalsAgainst"] = ga.ToString();
            row.Values["LeaguePoints"] = pts.ToString();
            return row;
        }

        private static GameRecord Player(string name, int age, int apps, int goals, int assists, double rating, int minutes)
        {
            var p = new GameRecord();
            p.Values["Name"] = Link(name);
            p.Values["Age"] = age.ToString();
            p.Values["ClubAppearancesThisSeason"] = apps.ToString();
            p.Values["ClubGoalsThisSeason"] = goals.ToString();
            p.Values["ClubAssistsThisSeason"] = assists.ToString();
            p.Values["ClubAverageRatingThisSeason"] = rating.ToString(System.Globalization.CultureInfo.InvariantCulture);
            p.Values["Minutes.PropertyValue"] = minutes.ToString();
            return p;
        }

        private static GameRecord Deal(string player, string seller, string buyer, double fee, bool loan)
        {
            var t = new GameRecord();
            t.Values["Player.Name"] = Link(player);
            t.Values["SellingTeam.Name"] = seller == null ? null : Link(seller);
            t.Values["BuyingTeam.Name"] = buyer == null ? null : Link(buyer);
            t.Values["Fee"] = fee.ToString(System.Globalization.CultureInfo.InvariantCulture);
            t.Values["IsOnLoan"] = loan ? "True" : "False";
            t.Values["Date"] = GameDate(2026, 7, 1);
            return t;
        }

        private static GameRecord Objective(string name, int category, string competition, string verdict)
        {
            var o = new GameRecord();
            o.Values["VisionObjectiveName"] = name;
            o.Values["VisionObjectiveCategory"] = category.ToString();
            o.Values["VisionObjectivePerformance"] = verdict;
            o.Values["Competition.Name"] = competition == null ? null : Link(competition);
            return o;
        }

        private static GameRecord Point(int y, int m, int d, double value)
        {
            var p = new GameRecord();
            p.Values["GraphPointX"] = GameDate(y, m, d);
            p.Values["GraphPointY"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return p;
        }

        private static GameRecord Text(string text)
        {
            var t = new GameRecord();
            t.Values["."] = text;
            return t;
        }

        /// <summary>Stand-in for the game's currency formatter.</summary>
        public static string Money(double pounds) => "£" + (pounds / 1_000_000).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "M";
    }
}
