using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SeasonReview
{
    /// <summary>
    /// Stage 2 draft: the collected season data as plain Markdown, to check the numbers against
    /// the game. The real report (templates, languages, HTML) comes in Stage 3.
    /// </summary>
    internal static class DraftReport
    {
        private const string Unavailable = "data unavailable";

        /// <param name="money">Formats a raw amount (pounds) in the player's currency.</param>
        public static string Build(SeasonData d, Func<double, string> money)
        {
            var t = new StringBuilder();
            string Money(double? value) => value == null ? Unavailable : money(value.Value);
            bool Missing(string section)
            {
                if (!d.Unavailable.Contains(section)) return false;
                t.AppendLine(Unavailable).AppendLine();
                return true;
            }

            t.AppendLine($"# {d.Club ?? "?"} - {d.Season ?? "?"} season review (draft)").AppendLine();
            t.AppendLine($"In-game date: {Day(d.Date)}. " +
                         (d.SeasonComplete ? "League season complete." : $"Season in progress ({d.LeagueFixturesRemaining} league fixtures left).")).AppendLine();

            t.AppendLine("## Summary").AppendLine();
            t.AppendLine($"- Division: {d.Division ?? Unavailable}");
            t.AppendLine($"- League position: {d.LeaguePositionText ?? d.LeaguePosition?.ToString() ?? Unavailable}" +
                         (d.LeagueSize != null ? $" of {d.LeagueSize}" : ""));
            if (d.MediaPrediction != null) t.AppendLine($"- Media prediction: {d.MediaPrediction}");
            foreach (var o in d.Objectives)
                t.AppendLine($"- Board expectation: {o.Name}" + (o.BoardView != null ? $" - {o.BoardView}" : ""));
            foreach (var c in d.Cups)
                t.AppendLine($"- {c.Competition}: P{c.Played} W{c.Won} D{c.Drawn} L{c.Lost}, last match {c.LastStage} ({c.LastResult})");
            t.AppendLine();

            t.AppendLine("## League performance").AppendLine();
            if (!Missing("league") && d.Total != null)
            {
                t.AppendLine("| | P | W | D | L | GF | GA | Pts |").AppendLine("|---|---|---|---|---|---|---|---|");
                Row(t, "Total", d.Total, d.PointsFromTable);
                if (d.Home != null) Row(t, "Home", d.Home, null);
                if (d.Away != null) Row(t, "Away", d.Away, null);
                t.AppendLine();
                if (!d.Unavailable.Contains("matches"))
                    t.AppendLine($"Longest winning run: {d.LongestWinningRun}. Longest unbeaten run: {d.LongestUnbeatenRun}.").AppendLine();
            }

            t.AppendLine("## Players").AppendLine();
            if (!Missing("players"))
            {
                if (d.PlayerOfTheSeason != null)
                    t.AppendLine($"Player of the season: **{d.PlayerOfTheSeason.Name}** ({d.PlayerOfTheSeason.Appearances} apps, average rating {Rating(d.PlayerOfTheSeason)})").AppendLine();
                Players(t, "Top scorers", d.Players.Where(p => p.Goals > 0).OrderByDescending(p => p.Goals).ThenBy(p => p.Appearances).Take(5));
                Players(t, "Most assists", d.Players.Where(p => p.Assists > 0).OrderByDescending(p => p.Assists).ThenBy(p => p.Appearances).Take(5));
                Players(t, "Most appearances", d.Players.Where(p => p.Appearances > 0).OrderByDescending(p => p.Appearances).ThenByDescending(p => p.Minutes).Take(5));
                Players(t, "Best average rating", d.Players.Where(p => p.AverageRating > 0).OrderByDescending(p => p.AverageRating).Take(5));
                Players(t, "Young players (21 and under)", d.Players.Where(p => p.Age > 0 && p.Age <= 21).OrderByDescending(p => p.Minutes));
            }

            t.AppendLine("## Transfers").AppendLine();
            if (!Missing("transfers"))
            {
                Deals(t, "Arrivals", "from", d.Arrivals, money);
                Deals(t, "Departures", "to", d.Departures, money);
                t.AppendLine($"Fees paid: {Money(d.FeesPaid)}. Fees received: {Money(d.FeesReceived)}. Net spend: {Money(d.NetSpend)}.").AppendLine();
            }

            t.AppendLine("## Finances").AppendLine();
            if (!Missing("finances"))
            {
                t.AppendLine($"- Balance at start ({Day(d.BalanceStartDate)}): {Money(d.BalanceStart)}");
                t.AppendLine($"- Balance now: {Money(d.BalanceEnd)}");
                t.AppendLine($"- Income this season: {Money(d.Income)}; expenditure: {Money(d.Expenditure)}; profit/loss: " +
                             Money(d.Income != null && d.Expenditure != null ? d.Income - d.Expenditure : null));
                t.AppendLine($"- Wage bill: {d.WageText ?? Money(d.WeeklyWages * 52) + " p/a"}");
                if (d.FinancialStatus != null) t.AppendLine($"- Financial status: {d.FinancialStatus}");
                t.AppendLine();
            }

            t.AppendLine("## Injuries").AppendLine();
            if (!Missing("injuries"))
            {
                t.AppendLine($"{d.Injuries.Count} injuries this season, {d.Injuries.Sum(i => i.Days)} days lost in total " +
                             $"(major {d.Major ?? 0}, moderate {d.Moderate ?? 0}, minor {d.Minor ?? 0}, slight {d.Slight ?? 0}).").AppendLine();
                foreach (var i in d.Injuries.OrderByDescending(i => i.Days).Take(5))
                    t.AppendLine($"- {i.Player}: {i.Description ?? "?"}, {(i.Active ? "still out" : i.Days + " days")} (from {Day(i.Date)})");
                t.AppendLine();
            }

            t.AppendLine("## Board and supporters").AppendLine();
            if (!Missing("confidence"))
            {
                Confidence(t, "Board", d.Board);
                Confidence(t, "Supporters", d.Supporters);
            }
            return t.ToString();
        }

        private static void Row(StringBuilder t, string label, LeagueRecord r, int? points) =>
            t.AppendLine($"| {label} | {r.Played} | {r.Won} | {r.Drawn} | {r.Lost} | {r.GoalsFor} | {r.GoalsAgainst} | {points ?? r.Points} |");

        private static void Players(StringBuilder t, string title, IEnumerable<PlayerSeason> players)
        {
            var list = players.ToList();
            t.AppendLine($"**{title}**").AppendLine();
            if (list.Count == 0) { t.AppendLine("None.").AppendLine(); return; }
            t.AppendLine("| Player | Age | Apps | Mins | Goals | Assists | Av. rating |").AppendLine("|---|---|---|---|---|---|---|");
            foreach (var p in list)
                t.AppendLine($"| {p.Name} | {p.Age} | {p.Appearances} | {p.Minutes} | {p.Goals} | {p.Assists} | {Rating(p)} |");
            t.AppendLine();
        }

        private static void Deals(StringBuilder t, string title, string direction, List<TransferDeal> deals, Func<double, string> money)
        {
            t.AppendLine($"**{title}** ({deals.Count})").AppendLine();
            foreach (var deal in deals.OrderByDescending(x => x.Fee))
                t.AppendLine($"- {deal.Player} {direction} {deal.OtherClub ?? "?"}: " +
                             (deal.Loan ? "loan" : deal.Fee > 0 ? money(deal.Fee) : "free") + $" ({Day(deal.Date)})");
            t.AppendLine();
        }

        private static void Confidence(StringBuilder t, string title, Confidence c)
        {
            t.AppendLine($"**{title}**").AppendLine();
            foreach (var line in new[] { c.Overall, c.Matches, c.Squad, c.Transfers, c.Vision }.Where(x => x != null))
                t.AppendLine("- " + line);
            foreach (var line in c.Highlights) t.AppendLine("- (+) " + line);
            foreach (var line in c.Criticism) t.AppendLine("- (-) " + line);
            t.AppendLine();
        }

        private static string Rating(PlayerSeason p) =>
            p.AverageRating > 0 ? p.AverageRating.ToString("0.00", CultureInfo.InvariantCulture) : "-";

        private static string Day(DateTime? date) =>
            date == null ? "?" : date.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }
}
