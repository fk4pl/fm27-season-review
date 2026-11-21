using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SeasonReview
{
    /// <summary>
    /// Decides whether the human manager's league season is over, from a small read of the
    /// fixture list. A season is over when at least one league match has been played and no
    /// league or league play-off fixture of the club is left without a result (so a season
    /// with play-offs is reported once the club's play-offs are over).
    /// </summary>
    internal static class SeasonEnd
    {
        public const string FixtureList = "CheckFixtures";

        public static Spec BuildCheck() => new Spec()
            .Value("CurrentGameDate", "GameName", "GameStartDate",
                   SeasonSpec.Team + ".Name", SeasonSpec.Division + ".Name", SeasonSpec.Division + ".SeasonName")
            .Object(SeasonSpec.Team, SeasonSpec.FixtureContexts(new Spec())
                .List("FixtureList", new Spec().Value("Competition.Name", "HasResult", "IsPlayOffs"), 120, FixtureList), FixtureList);

        /// <summary>
        /// Identifies one season of one club in one career, so a report is only ever produced
        /// once, even across game sessions. Needs the values read by <see cref="BuildCheck"/>
        /// (the full season read includes them too).
        /// </summary>
        public static string KeyOf(GameRecord r) => string.Join("|",
            CareerName(SeasonDataBuilder.Text(r.Get("GameName"))) ?? "-",
            SeasonDataBuilder.Text(r.Get("GameStartDate")) ?? "-",
            SeasonDataBuilder.Text(r.Get(SeasonSpec.Team + ".Name")) ?? "-",
            SeasonDataBuilder.Text(r.Get(SeasonSpec.Division + ".Name")) ?? "-",
            SeasonDataBuilder.Text(r.Get(SeasonSpec.Division + ".SeasonName")) ?? "-");

        /// <summary>
        /// The career's save name without the game's save counter. The game names the career
        /// e.g. <c>"Manager - Club" (38 defa kaydedildi)</c> or <c>"Manager - Club" (saved 38
        /// times)</c>: the counter changes with every save and its wording with the game's
        /// language, so it must not be part of the key. A quoted name is kept as it is, with
        /// any brackets of its own (<c>"Manager - Club (v03)"</c>).
        /// </summary>
        public static string CareerName(string gameName)
        {
            if (gameName == null) return null;
            var name = gameName.Trim();
            if (name.Length > 2 && name[0] == '"')
            {
                int close = name.IndexOf('"', 1);
                if (close > 1) return name.Substring(0, close + 1);
            }
            // Unquoted (not seen in game): drop a trailing "(… 12 …)" with a number and a space.
            var counter = Regex.Match(name, @"^(.*\S)\s*\((?=[^()]*\d)(?=[^()]*\s)[^()]*\)$");
            return counter.Success ? counter.Groups[1].Value : name;
        }

        /// <summary>A key with its career name normalised (see <see cref="CareerName"/>), so keys
        /// written by older versions still match.</summary>
        public static string NormaliseKey(string key)
        {
            if (key == null) return null;
            key = key.Trim();
            int bar = key.IndexOf('|');
            return bar < 0 ? key : CareerName(key.Substring(0, bar)) + key.Substring(bar);
        }

        internal sealed class Status
        {
            public string Key, Club, Division, Season;
            public int LeaguePlayed, LeagueRemaining, PlayOffsRemaining;
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
                // The league's play-offs have the league's name: not league matches, but the
                // season is not over while one is still to be played.
                bool playOff = SeasonDataBuilder.Bool(f.Get("IsPlayOffs"));
                bool played = SeasonDataBuilder.Bool(hasResult);
                if (playOff) { if (!played) status.PlayOffsRemaining++; }
                else if (played) status.LeaguePlayed++;
                else status.LeagueRemaining++;
            }

            status.Ended = status.LeaguePlayed > 0 && status.LeagueRemaining == 0 && status.PlayOffsRemaining == 0;
            status.Reason = status.Ended
                ? "all league fixtures played"
                : status.LeagueRemaining == 0 && status.LeaguePlayed > 0
                    ? $"{status.LeaguePlayed} league matches played, {status.PlayOffsRemaining} play-off matches remaining"
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
                        _keys.Add(SeasonEnd.NormaliseKey(line));
        }

        public bool Contains(string key) => key != null && _keys.Contains(SeasonEnd.NormaliseKey(key));

        public void Add(string key)
        {
            DiskWrites.Demand("the list of reported seasons");
            if (key == null || !_keys.Add(SeasonEnd.NormaliseKey(key))) return;
            var lines = new List<string> { "# Season Review: seasons that already have a report. Delete a line to allow a new report." };
            lines.AddRange(_keys.OrderBy(k => k, StringComparer.Ordinal));
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            File.WriteAllLines(_path, lines, new UTF8Encoding(false));
        }
    }
}
