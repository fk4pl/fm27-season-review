namespace SeasonReview
{
    /// <summary>
    /// Everything the report reads from the game, in one place. Every property name here was
    /// verified against the runtime property dump (see docs/DISCOVERY.md); none is a hidden value.
    /// </summary>
    internal static class SeasonSpec
    {
        public const string Team = "GameCurrentManagerClubTeam";
        public const string Club = Team + ".Club";
        public const string Division = Team + ".Division";
        public const string Confidence = Team + ".TeamConfidence";
        public const string InjurySummary = Team + ".SeasonInjurySummaryData";

        // Result list names
        public const string Players = "Players";
        public const string Fixtures = "Fixtures";
        public const string Stages = "Stages";
        public const string LeagueTable = "LeagueTable";
        public const string Transfers = "Transfers";
        public const string TransfersIn = "Division.SeasonTransferInfo.SeasonTransfersInPlayers";
        public const string TransfersOut = "Division.SeasonTransferInfo.SeasonTransfersOutPlayers";
        public const string Objectives = "Objectives";
        public const string BalanceHistory = "BalanceHistory";
        public const string PastInjuries = "PastInjuries";

        /// <summary>Context values the game's fixtures screen passes for the whole current season
        /// (verified: returns played and future fixtures of all competitions).</summary>
        public static Spec FixtureContexts(Spec spec) => spec
            .Context("PreviousAmount", -1).Context("FutureAmount", -1)
            .Context("FixturePadding", false).Context("YearIndex", 0);

        /// <summary>Small first read: what season is it?</summary>
        public static Spec BuildHeader() => new Spec().Value(
            "CurrentGameDate", "CurrentGameYear", Team + ".Name", Division + ".Name", Division + ".SeasonName");

        /// <param name="seasonYear">Calendar year the season started in (the game's "Year" context).</param>
        public static Spec Build(int seasonYear)
        {
            var spec = new Spec().Value(
                "CurrentGameDate", "GameName", "GameStartDate",
                Team + ".Name", Team + ".LongName", Team + ".LeaguePosition", Team + ".LeaguePositionString",
                Team + ".MediaExpectedPosition",
                Division + ".Name", Division + ".SeasonName",

                Club + ".OverallBalance", Club + ".FinancialStatus",
                Club + ".TotalIncomeThisSeason", Club + ".TotalExpenditureThisSeason",
                Club + ".CurrentWageSpending", Club + ".WageBudget", Club + ".CurrentWageSpendingWithText",
                Club + ".TotalIncomeThisSeasonTurnover", Club + ".ProjectionProfitLoss",

                Confidence + ".BoardConfidenceOverallString", Confidence + ".BoardMatchString",
                Confidence + ".BoardSquadConfidenceString", Confidence + ".BoardTransferConfidenceOverallSummaryText",
                Confidence + ".BoardVisionString",
                Confidence + ".SupporterConfidenceOverallString", Confidence + ".SupporterMatchString",
                Confidence + ".SupporterSquadConfidenceString", Confidence + ".SupporterTransferConfidenceOverallSummaryText",
                Confidence + ".SupporterVisionString",

                InjurySummary + ".MajorInjuryCount", InjurySummary + ".ModerateInjuryCount",
                InjurySummary + ".MinorInjuryCount", InjurySummary + ".SlightInjuryCount");

            spec.List(Team + ".Players", new Spec().Value(
                "Name", "Age", "ClubAppearancesThisSeason", "ClubGoalsThisSeason", "ClubAssistsThisSeason",
                "ClubAverageRatingThisSeason", "Minutes"), 80, Players);

            // Fixture list of the whole season: the game's screen passes these context values.
            var fixture = new Spec().Value(
                "FixtureDate", "Competition.Name", "Competition.IsFriendly",
                "HomeTeam.Name", "AwayTeam.Name", "HomeScore", "AwayScore", "HasResult", "Result",
                "HomeTeamWon", "AwayTeamWon");
            spec.Object(Team, FixtureContexts(new Spec()).List("FixtureList", fixture, 90, Fixtures), Fixtures);

            var tableRow = new Spec().Value(
                "Team.Name", "TablePosition", "LeagueGamesPlayed", "LeagueGamesWon", "LeagueGamesDrawn", "LeagueGamesLost",
                "LeagueGoalsFor", "LeagueGoalsAgainst", "LeagueGoalDifference", "LeaguePoints");
            spec.List(Division + ".CurrentStages", new Spec()
                .Context("LeagueTableType", 0)
                .Value("IsLeagueStage")
                .List("LeagueTable", tableRow, 40, LeagueTable), 12, Stages);

            // The club's transfers this season, as listed for its division.
            var transfer = new Spec().Value("Player.Name", "BuyingTeam.Name", "SellingTeam.Name", "Fee", "IsOnLoan", "Date");
            spec.Object(Team, new Spec()
                .Context("team", Spec.Self).Context("Year", seasonYear).Context("TransferType", 0)
                .Value("Division.SeasonTransferInfo.SeasonTransfersNetSpend")
                .List(TransfersIn, transfer, 80)
                .List(TransfersOut, transfer, 80), Transfers);

            // Board objectives (setter 1 = board, type 0 = all), as on the club vision screen.
            spec.Object(Club, new Spec()
                .Context("VisionObjectiveSetter", 1).Context("VisionObjectiveType", 0)
                .Context("VisionObjectiveYear", 0).Context("ViewID", 0)
                .Context("VisionIsUsingPerformanceData", true)
                .List("VisionObjectiveList", new Spec().Value("Competition.Name"), 40, Objectives), Objectives);

            var asDelivered = new Spec(); // small records or strings, read as they come
            spec.List(Club + ".FinanceBalanceHistory", asDelivered, 80, BalanceHistory);
            foreach (var who in new[] { "Board", "Supporter" })
            {
                spec.List(Confidence + "." + who + "FeedbackHighlights", asDelivered, 12, who + "Highlights");
                spec.List(Confidence + "." + who + "FeedbackCriticism", asDelivered, 12, who + "Criticism");
            }
            spec.List(InjurySummary + ".PastInjuries", new Spec().Value("Player.Name"), 120, PastInjuries);

            return spec;
        }
    }
}
