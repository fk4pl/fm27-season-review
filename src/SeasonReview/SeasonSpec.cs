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
        public const string Manager = "GameCurrentManager";

        public const string Players = Team + ".Players";
        public const string Fixtures = Team + ".FixtureList";
        public const string LeagueTable = Division + ".CurrentStage.LeagueTable";
        public const string Stages = Division + ".CurrentStages";
        public const string TransfersIn = Club + ".PastTransfersIncoming";
        public const string TransfersOut = Club + ".PastTransfersOutgoing";
        public const string ManagerTransfers = Manager + ".HumanTransferInOutList";
        public const string Objectives = Club + ".VisionObjectiveList";
        public const string Expectations = Club + ".ClubVisionMeetingSeasonExpectations.ClubSeasonExpectationsCompetitionList";
        public const string Injuries = Team + ".CurrentInjuryList";
        public const string PastInjuries = Team + ".SeasonInjurySummaryData.PastInjuries";

        public static Spec Build()
        {
            var spec = new Spec().Value(
                "CurrentGameDate", "CurrentGameYear", "GameStartDate",

                Team + ".Name", Team + ".LongName", Team + ".LeaguePosition", Team + ".LeaguePositionString",
                Team + ".MediaExpectedPosition", Team + ".LeagueTableName",
                Division + ".Name", Division + ".ShortName", Division + ".SeasonName",

                Club + ".Name", Club + ".ShortName",
                Club + ".OverallBalance", Club + ".FinancialStatus",
                Club + ".TotalIncomeThisSeason", Club + ".TotalExpenditureThisSeason",
                Club + ".TotalIncomeLastSeason", Club + ".TotalExpenditureLastSeason",
                Club + ".CurrentWageSpending", Club + ".CommittedWageSpending", Club + ".WageBudget",
                Club + ".RemainingWageBudget", Club + ".TransferBudget", Club + ".OriginalTransferBudget",
                Club + ".TransferExpenditureThisSeason", Club + ".ProjectionProfitLoss", Club + ".ProjectionBalance",
                Club + ".PastTransfersInAmount", Club + ".PastTransfersOutAmount",
                Club + ".FinanceBalanceHistory",
                Club + ".VisionObjectiveThisYearObjective", Club + ".VisionObjectiveYearString",

                Confidence + ".BoardConfidenceOverallGrade", Confidence + ".BoardConfidenceOverallString",
                Confidence + ".BoardMatchGrade", Confidence + ".BoardMatchString",
                Confidence + ".BoardSquadConfidenceGrade", Confidence + ".BoardSquadConfidenceString",
                Confidence + ".BoardTransferConfidenceOverallGrade", Confidence + ".BoardTransferConfidenceOverallSummaryText",
                Confidence + ".BoardVisionGrade", Confidence + ".BoardVisionString",
                Confidence + ".BoardFeedbackHighlights", Confidence + ".BoardFeedbackCriticism",
                Confidence + ".SupporterConfidenceOverallGrade", Confidence + ".SupporterConfidenceOverallString",
                Confidence + ".SupporterMatchGrade", Confidence + ".SupporterMatchString",
                Confidence + ".SupporterSquadConfidenceGrade", Confidence + ".SupporterSquadConfidenceString",
                Confidence + ".SupporterTransferConfidenceOverallGrade", Confidence + ".SupporterTransferConfidenceOverallSummaryText",
                Confidence + ".SupporterVisionGrade", Confidence + ".SupporterVisionString",
                Confidence + ".SupporterFeedbackHighlights", Confidence + ".SupporterFeedbackCriticism",

                Team + ".SeasonInjurySummaryData.InjuryRecap",
                Team + ".SeasonInjurySummaryData.MajorInjuryCount", Team + ".SeasonInjurySummaryData.ModerateInjuryCount",
                Team + ".SeasonInjurySummaryData.MinorInjuryCount", Team + ".SeasonInjurySummaryData.SlightInjuryCount");

            spec.List(Players, new Spec().Value(
                "Name", "Age", "DateOfBirth", "Position", "IsLoanIn", "IsInjured",
                "ClubAppearancesThisSeason", "ClubGoalsThisSeason", "ClubAssistsThisSeason", "ClubAverageRatingThisSeason",
                "Appearances", "Minutes", "Goals", "Assists", "AverageRating",
                "TimeMissedThisSeason", "InjuriesPercentageMissedString"), 80);

            var fixture = new Spec().Value(
                "Fixture.FixtureDate", "Fixture.Competition.Name", "Fixture.StageName",
                "Fixture.HomeTeam.Name", "Fixture.AwayTeam.Name", "Fixture.HomeScore", "Fixture.AwayScore",
                "Fixture.HasResult", "Fixture.IsFutureMatch", "Fixture.Result", "Fixture.ResultType",
                "Fixture.HomeTeamWon", "Fixture.AwayTeamWon");
            spec.List(Fixtures, fixture, 90);

            spec.List(LeagueTable, new Spec().Value(
                "Team.Name", "TablePosition", "LeagueGamesPlayed", "LeagueGamesWon", "LeagueGamesDrawn", "LeagueGamesLost",
                "LeagueGoalsFor", "LeagueGoalsAgainst", "LeagueGoalDifference", "LeaguePoints", "LeagueFate.Name"), 40);
            spec.List(Stages, new Spec().Value("Name", "StageName", "IsLeagueStage", "IsCupStage"), 12);

            var transfer = new Spec().Value("Person.Name", "BuyingTeamString", "SellingTeamString", "TransferValue");
            spec.List(TransfersIn, transfer, 60);
            spec.List(TransfersOut, transfer, 60);
            spec.List(ManagerTransfers, new Spec().Value(
                "Date", "Person.Name", "TransferInOutFeeString", "TransferInOutBuyingClub.Name",
                "TransferInOutSellingClubString", "TransferInOutTeamString"), 80);

            spec.List(Objectives, new Spec().Value(
                "VisionObjectiveName", "VisionObjectiveCategory", "VisionObjectiveImportance", "VisionObjectiveStatus",
                "VisionObjectiveProgress", "VisionObjectiveYearString", "Competition.Name"), 40);
            spec.List(Expectations, new Spec().Value(
                "ClubSeasonExpectationsCompetition.Name", "ClubSeasonExpectationsCompetitionImportance",
                "ClubSeasonExpectationsIsCompetitionFinished"), 12);

            var injury = new Spec().Value(
                "Player.Name", "InjuryDescription", "InjuryDate", "InjuryDurationDays", "InjuryDurationLong", "IsActive",
                "DisplayString", "Date");
            spec.List(Injuries, injury, 40);
            spec.List(PastInjuries, injury, 80);

            return spec;
        }
    }
}
