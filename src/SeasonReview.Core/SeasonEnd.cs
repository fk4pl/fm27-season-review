using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SeasonReview
{
    /// <summary>
    /// Decides whether the human manager's league season is over, from a small read of the
    /// fixture list. A season is over when at least one league match has been played and no
    /// league fixture is left without a result.
    /// </summary>
    internal static class SeasonEnd
    {
        public const string FixtureList = "CheckFixtures";

        public static Spec BuildCheck() => new Spec()
            .Value("CurrentGameDate", "GameName", "GameStartDate",
                   SeasonSpec.Team + ".Name", SeasonSpec.Division + ".Name", SeasonSpec.Division + ".SeasonName")
            .Object(SeasonSpec.Team, SeasonSpec.FixtureContexts(new Spec())
                .List("FixtureList", new Spec().Value("Competition.Name", "HasResult"), 120, FixtureList), FixtureList);

        /// <summary>
        /// Identifies one season of one club in one career, so a report is only ever produced
        /// once, even across game sessions. Needs the values read by <see cref="BuildCheck"/>
        /// (the full season read includes them too).
        /// </summary>
        public static string KeyOf(GameRecord r) => string.Join("|",
            SeasonDataBuilder.Text(r.Get("GameName")) ?? "-",
            SeasonDataBuilder.Text(r.Get("GameStartDate")) ?? "-",
            SeasonDataBuilder.Text(r.Get(SeasonSpec.Team + ".Name")) ?? "-",
            SeasonDataBuilder.Text(r.Get(SeasonSpec.Division + ".Name")) ?? "-",
            SeasonDataBuilder.Text(r.Get(SeasonSpec.Division + ".SeasonName")) ?? "-");

        internal sealed class Status
        {
            public string Key, Club, Division, Season;
            public int LeaguePlayed, LeagueRemaining;
            public bool Ended;
            public string Reason;
        }

        public static Status Evaluate(GameRecord r)
        {
            var status = new Status
            {
                Club = SeasonDataBuilder.Text(r.Get(SeasonSpec.Team + ".Name")),
                Division = SeasonDataBuilder.Text(r.Get(SeasonSpec.Division + ".Name")),
                Season = SeasonDataBuilder.Text(r.Get(SeasonSpec.Division + ".SeasonName")),
            };
            if (status.Club == null || status.Division == null || status.Season == null)
            {
                status.Reason = "no club, division or season (unemployed or no game loaded)";
                return status;
            }

            status.Key = KeyOf(r);

            var fixtures = new List<GameRecord>();
            foreach (var team in r.Items(FixtureList)) fixtures.AddRange(team.Items(FixtureList));
            if (fixtures.Count == 0)
            {
                status.Reason = "fixture list unavailable";
                return status;
            }

            foreach (var f in fixtures)
            {
                if (SeasonDataBuilder.Text(f.Get("Competition.Name")) != status.Division) continue;
                var hasResult = f.Get("HasResult");
                if (hasResult == null) { status.Reason = "fixture list incomplete"; return status; }
                if (SeasonDataBuilder.Bool(hasResult)) status.LeaguePlayed++;
                else status.LeagueRemaining++;
            }

            status.Ended = status.LeaguePlayed > 0 && status.LeagueRemaining == 0;
            status.Reason = status.Ended
                ? "all league fixtures played"
                : $"{status.LeaguePlayed} league matches played, {status.LeagueRemaining} remaining";
            return status;
        }
    }

    /// <summary>
    /// Remembers which seasons already have a report, in a small text file (one key per line)
    /// in BepInEx's config folder. Never touches the game's save files.
    /// </summary>
    internal sealed class ReportedSeasons
    {
        private readonly string _path;
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

        public ReportedSeasons(string path)
        {
            _path = path;
            if (File.Exists(path))
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                    if (line.Trim().Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                        _keys.Add(line.Trim());
        }

        public bool Contains(string key) => key != null && _keys.Contains(key);

        public void Add(string key)
        {
            if (key == null || !_keys.Add(key)) return;
            var lines = new List<string> { "# Season Review: seasons that already have a report. Delete a line to allow a new report." };
            lines.AddRange(_keys.OrderBy(k => k, StringComparer.Ordinal));
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            File.WriteAllLines(_path, lines, new UTF8Encoding(false));
        }
    }
}
