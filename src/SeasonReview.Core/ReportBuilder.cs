using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SeasonReview
{
    /// <summary>Writes the season's story from <see cref="SeasonData"/> in one language.</summary>
    internal sealed class ReportBuilder
    {
        private readonly SeasonData _d;
        private readonly Strings _s;
        private readonly Func<double, string> _money;

        private ReportBuilder(SeasonData data, Strings strings, Func<double, string> money)
        {
            _d = data;
            _s = strings;
            _money = money;
        }

        /// <param name="money">Formats a raw amount (pounds) in the player's display currency.</param>
        public static ReportDocument Build(SeasonData data, Strings strings, Func<double, string> money, string version) =>
            new ReportBuilder(data, strings, money).Build(version);

        private ReportDocument Build(string version)
        {
            var doc = new ReportDocument
            {
                Language = _s.Code,
                Title = _s.Format("title", ("club", _d.Club ?? "?"), ("season", _d.Season ?? "?")),
                Subtitle = _s.Format("subtitle", ("division", _d.Division ?? "?"), ("date", _s.Date(_d.Date))),
            };
            if (!_d.SeasonComplete)
                doc.Notice = _s.Format("interim", ("remaining", _s.Count(_d.LeagueFixturesRemaining, "leaguematch")));

            if (_d.Total != null)
            {
                var position = _d.LeaguePosition;
                doc.Tiles.Add((_s.Get("tile.position"), position != null ? _s.Ordinal(position.Value) : "-"));
                doc.Tiles.Add((_s.Get("tile.points"), N(_d.PointsFromTable ?? _d.Total.Points)));
                doc.Tiles.Add((_s.Get("tile.record"), $"{_d.Total.Won}-{_d.Total.Drawn}-{_d.Total.Lost}"));
                doc.Tiles.Add((_s.Get("tile.goals"), $"{_d.Total.GoalsFor}-{_d.Total.GoalsAgainst}"));
            }

            doc.Sections.Add(Summary());
            doc.Sections.Add(League());
            doc.Sections.Add(Players());
            doc.Sections.Add(Transfers());
            doc.Sections.Add(Finances());
            doc.Sections.Add(Injuries());
            doc.Sections.Add(BoardAndSupporters());

            doc.Footer.Add(_s.Format("footer.generated", ("version", version), ("date", _s.Date(_d.Date))));
            doc.Footer.Add(_s.Get("footer.language"));
            doc.Footer.Add(_s.Get("footer.money"));
            doc.Footer.Add(_s.Get("footer.seealso"));
            return doc;
        }

        // ── Sections ───────────────────────────────────────────────────────

        private ReportSection Summary()
        {
            var section = Section("summary");
            if (Unavailable(section, "summary")) return section;

            if (_d.Total != null)
            {
                var story = new List<string>();
                if (_d.LeaguePosition != null)
                    story.Add(_s.Format(_d.SeasonComplete ? "summary.finished" : "summary.current",
                        ("club", _d.Club), ("division", _d.Division), ("position", _s.Ordinal(_d.LeaguePosition.Value)),
                        ("teams", N(_d.LeagueSize ?? 0)), ("points", _s.Count(_d.PointsFromTable ?? _d.Total.Points, "point")),
                        ("played", _s.Count(_d.Total.Played, "match"))));
                story.Add(_s.Format("summary.record", ("won", N(_d.Total.Won)), ("drawn", N(_d.Total.Drawn)),
                        ("lost", N(_d.Total.Lost)), ("for", N(_d.Total.GoalsFor)), ("against", N(_d.Total.GoalsAgainst))));
                if (_d.LeaguePosition != null && _d.MediaPrediction is int predicted && predicted > 0)
                {
                    int actual = _d.LeaguePosition.Value;
                    string key = actual < predicted ? "summary.media.better" : actual > predicted ? "summary.media.worse" : "summary.media.same";
                    story.Add(_s.Format(key, ("prediction", _s.Ordinal(predicted))));
                }
                section.Add(new Paragraph { Text = string.Join(" ", story) });
            }

            section.Add(new Subheading { Text = _s.Get("summary.expectations") });
            if (_d.Unavailable.Contains("objectives") || _d.Objectives.Count == 0)
                section.Add(new Note { Text = _s.Get("common.unavailable") });
            else
            {
                var table = new Table { Headers = new[] { _s.Get("summary.objective"), _s.Get("summary.verdict") } };
                foreach (var o in _d.Objectives) table.Rows.Add(new[] { o.Name, o.BoardView ?? "-" });
                section.Add(table);
            }

            section.Add(new Subheading { Text = _s.Get("summary.cups") });
            if (_d.Unavailable.Contains("matches"))
                section.Add(new Note { Text = _s.Get("common.unavailable") });
            else if (_d.Cups.Count == 0)
                section.Add(new Paragraph { Text = _s.Get("summary.cups.none") });
            else
            {
                var table = new Table
                {
                    Headers = new[] { _s.Get("cups.competition"), _s.Get("cups.record"), _s.Get("cups.last") },
                };
                foreach (var cup in _d.Cups)
                    table.Rows.Add(new[]
                    {
                        cup.Competition,
                        $"{cup.Played} / {cup.Won} / {cup.Drawn} / {cup.Lost}",
                        cup.LastMatch == null ? "-" : MatchLine(cup.LastMatch),
                    });
                section.Add(table);
            }

            var moments = new Bullets();
            if (_d.BiggestWin != null) moments.Items.Add(Moment("summary.moment.win", _d.BiggestWin));
            if (_d.HeaviestDefeat != null) moments.Items.Add(Moment("summary.moment.defeat", _d.HeaviestDefeat));
            var signing = _d.Arrivals.Where(t => !t.Loan && t.Fee > 0).OrderByDescending(t => t.Fee).FirstOrDefault();
            if (signing != null)
                moments.Items.Add(_s.Format("summary.moment.signing", ("player", signing.Player),
                    ("club", signing.OtherClub ?? "?"), ("fee", Cash(signing.Fee))));
            var scorer = _d.Players.Where(p => p.Goals > 0).OrderByDescending(p => p.Goals).FirstOrDefault();
            if (scorer != null)
                moments.Items.Add(_s.Format("summary.moment.scorer", ("player", scorer.Name), ("goals", _s.Count(scorer.Goals, "goal"))));
            if (moments.Items.Count > 0)
            {
                section.Add(new Subheading { Text = _s.Get("summary.moments") });
                section.Add(moments);
            }
            return section;
        }

        private ReportSection League()
        {
            var section = Section("league");
            if (Unavailable(section, "league") || _d.Total == null) return section;

            var table = new Table
            {
                Headers = new[]
                {
                    _s.Get("league.split"), _s.Get("league.played"), _s.Get("league.won"), _s.Get("league.drawn"),
                    _s.Get("league.lost"), _s.Get("league.for"), _s.Get("league.against"), _s.Get("league.points"),
                },
                Numeric = new[] { false, true, true, true, true, true, true, true },
            };
            table.Rows.Add(LeagueRow(_s.Get("league.total"), _d.Total, _d.PointsFromTable));
            if (_d.LeagueFromFixtures) section.Add(new Note { Text = _s.Get("league.fromresults") });
            if (_d.Home != null && _d.Home.Played > 0) table.Rows.Add(LeagueRow(_s.Get("league.home"), _d.Home, null));
            if (_d.Away != null && _d.Away.Played > 0) table.Rows.Add(LeagueRow(_s.Get("league.away"), _d.Away, null));
            section.Add(table);

            if (!_d.Unavailable.Contains("matches"))
            {
                var runs = new Bullets();
                runs.Items.Add(_s.Format("summary.moment.winrun", ("count", _s.Count(_d.LongestWinningRun, "match"))));
                runs.Items.Add(_s.Format("summary.moment.unbeaten", ("count", _s.Count(_d.LongestUnbeatenRun, "match"))));
                section.Add(runs);
            }
            return section;
        }

        private ReportSection Players()
        {
            var section = Section("players");
            if (Unavailable(section, "players")) return section;
            section.Add(new Note { Text = _s.Get("players.scope") });

            var pots = _d.PlayerOfTheSeason;
            if (pots != null)
                section.Add(new Highlight
                {
                    Title = _s.Get("players.pots"),
                    Text = pots.Name,
                    Detail = _s.Format("players.pots.text", ("player", pots.Name), ("apps", _s.Count(pots.Appearances, "appearance")),
                        ("rating", Rating(pots)), ("goals", _s.Count(pots.Goals, "goal")), ("assists", _s.Count(pots.Assists, "assist")))
                        + " " + _s.Format("players.pots.how",
                            ("min", _s.Count(_d.PlayerOfTheSeasonMinimum, "appearance")),
                            ("matches", _s.Count(_d.CompetitiveMatches, "match"))),
                });

            int most = _d.Players.Count == 0 ? 0 : _d.Players.Max(p => p.Appearances);
            PlayerTable(section, "players.scorers",
                _d.Players.Where(p => p.Goals > 0).OrderByDescending(p => p.Goals).ThenBy(p => p.Appearances).Take(5));
            PlayerTable(section, "players.assists",
                _d.Players.Where(p => p.Assists > 0).OrderByDescending(p => p.Assists).ThenBy(p => p.Appearances).Take(5));
            PlayerTable(section, "players.ratings",
                _d.Players.Where(p => p.AverageRating > 0 && p.Appearances * 3 >= most)
                    .OrderByDescending(p => p.AverageRating).Take(5));
            PlayerTable(section, "players.young",
                _d.Players.Where(p => p.Age > 0 && p.Age <= 21 && p.Appearances > 0).OrderByDescending(p => p.Minutes));
            return section;
        }

        private ReportSection Transfers()
        {
            var section = Section("transfers");
            if (Unavailable(section, "transfers")) return section;

            DealTable(section, "transfers.arrivals", "transfers.from", _d.Arrivals);
            DealTable(section, "transfers.departures", "transfers.to", _d.Departures);

            var facts = new Facts();
            facts.Items.Add((_s.Get("transfers.paid"), Money(_d.FeesPaid)));
            facts.Items.Add((_s.Get("transfers.received"), Money(_d.FeesReceived)));
            facts.Items.Add((_s.Get("transfers.net"), Money(_d.NetSpend)));
            facts.Items.Add((_s.Get("transfers.loans"), _s.Format("transfers.loans.text",
                ("in", N(_d.Arrivals.Count(t => t.Loan))), ("out", N(_d.Departures.Count(t => t.Loan))))));
            section.Add(facts);
            return section;
        }

        private ReportSection Finances()
        {
            var section = Section("finances");
            if (Unavailable(section, "finances")) return section;

            var facts = new Facts();
            facts.Items.Add((_s.Format("finances.balance.start", ("date", _s.Date(_d.BalanceStartDate))), Money(_d.BalanceStart)));
            facts.Items.Add((_s.Get("finances.balance.end"), Money(_d.BalanceEnd)));
            facts.Items.Add((_s.Get("finances.profit"),
                PlusMinus(_d.BalanceStart != null && _d.BalanceEnd != null ? _d.BalanceEnd - _d.BalanceStart : null)));
            facts.Items.Add((_s.Get("finances.income"), Money(_d.Income)));
            facts.Items.Add((_s.Get("finances.expenditure"), Money(_d.Expenditure)));
            if (_d.ProjectedProfit != null) facts.Items.Add((_s.Get("finances.projection"), PlusMinus(_d.ProjectedProfit)));
            facts.Items.Add((_s.Get("finances.wages"), _d.WeeklyWages == null
                ? _s.Get("common.unavailable")
                : _s.Format("finances.wages.value", ("amount", Cash(_d.WeeklyWages.Value * 52)))));
            if (_d.FinancialStatus != null)
                facts.Items.Add((_s.Get("finances.status"), _s.GameValue("finances.status.value", _d.FinancialStatus)));
            section.Add(facts);
            section.Add(new Note { Text = _s.Get("finances.note") });
            return section;
        }

        private ReportSection Injuries()
        {
            var section = Section("injuries");
            if (Unavailable(section, "injuries")) return section;
            if (_d.Injuries.Count == 0)
            {
                section.Add(new Paragraph { Text = _s.Get("injuries.none") });
                return section;
            }

            section.Add(new Paragraph
            {
                Text = _s.Format("injuries.summary", ("injuries", _s.Count(_d.Injuries.Count, "injury")),
                           ("days", _s.Count(_d.Injuries.Sum(i => i.Days), "day")))
                       + " " + _s.Format("injuries.severity", ("major", N(_d.Major ?? 0)), ("moderate", N(_d.Moderate ?? 0)),
                           ("minor", N(_d.Minor ?? 0)), ("slight", N(_d.Slight ?? 0))),
            });
            section.Add(new Subheading { Text = _s.Get("injuries.longest") });
            var table = new Table
            {
                Headers = new[] { _s.Get("injuries.player"), _s.Get("injuries.injury"), _s.Get("injuries.from"), _s.Get("injuries.duration") },
            };
            foreach (var i in _d.Injuries.OrderByDescending(i => i.Active).ThenByDescending(i => i.Days).Take(5))
                table.Rows.Add(new[]
                {
                    i.Player, i.Description ?? "-", _s.Date(i.Date),
                    i.Active ? _s.Get("injuries.ongoing") : _s.Count(i.Days, "day"),
                });
            section.Add(table);
            return section;
        }

        private ReportSection BoardAndSupporters()
        {
            var section = Section("board");
            if (Unavailable(section, "confidence")) return section;
            Confidence(section, "board.board", _d.Board);
            Confidence(section, "board.supporters", _d.Supporters);
            return section;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private ReportSection Section(string id) => new ReportSection { Id = id, Title = _s.Get("section." + id) };

        private bool Unavailable(ReportSection section, string dataSection)
        {
            if (!_d.Unavailable.Contains(dataSection)) return false;
            section.Add(new Note { Text = _s.Get("common.unavailable") });
            return true;
        }

        private void Confidence(ReportSection section, string titleKey, Confidence c)
        {
            section.Add(new Subheading { Text = _s.Get(titleKey) });
            var lines = new Bullets();
            foreach (var line in new[] { c.Overall, c.Matches, c.Squad, c.Transfers, c.Vision })
                if (line != null) lines.Items.Add(line);
            if (lines.Items.Count == 0) section.Add(new Note { Text = _s.Get("common.unavailable") });
            else section.Add(lines);

            if (c.Highlights.Count > 0)
            {
                var positives = new Bullets();
                positives.Items.AddRange(c.Highlights);
                section.Add(new Subheading { Text = _s.Get("board.positive") });
                section.Add(positives);
            }
            if (c.Criticism.Count > 0)
            {
                var concerns = new Bullets();
                concerns.Items.AddRange(c.Criticism);
                section.Add(new Subheading { Text = _s.Get("board.negative") });
                section.Add(concerns);
            }
        }

        private void PlayerTable(ReportSection section, string titleKey, IEnumerable<PlayerSeason> players)
        {
            section.Add(new Subheading { Text = _s.Get(titleKey) });
            var list = players.ToList();
            if (list.Count == 0) { section.Add(new Paragraph { Text = _s.Get("common.none") }); return; }
            var table = new Table
            {
                Headers = new[]
                {
                    _s.Get("players.name"), _s.Get("players.age"), _s.Get("players.apps"), _s.Get("players.minutes"),
                    _s.Get("players.goals"), _s.Get("players.assistsshort"), _s.Get("players.rating"),
                },
                Numeric = new[] { false, true, true, true, true, true, true },
            };
            foreach (var p in list)
                table.Rows.Add(new[] { p.Name, N(p.Age), N(p.Appearances), N(p.Minutes), N(p.Goals), N(p.Assists), Rating(p) });
            section.Add(table);
        }

        private void DealTable(ReportSection section, string titleKey, string clubKey, List<TransferDeal> deals)
        {
            section.Add(new Subheading { Text = $"{_s.Get(titleKey)} ({deals.Count})" });
            if (deals.Count == 0) { section.Add(new Paragraph { Text = _s.Get("common.none") }); return; }
            var table = new Table
            {
                Headers = new[] { _s.Get("transfers.player"), _s.Get(clubKey), _s.Get("transfers.fee"), _s.Get("transfers.date") },
                Numeric = new[] { false, false, true, false },
            };
            foreach (var t in deals.OrderBy(t => t.Loan).ThenByDescending(t => t.Fee).ThenBy(t => t.Player, StringComparer.Ordinal))
                table.Rows.Add(new[]
                {
                    t.Player, t.OtherClub ?? "-",
                    t.Loan ? _s.Get("common.loan") : t.Fee > 0 ? Cash(t.Fee) : _s.Get("common.free"),
                    _s.Date(t.Date),
                });
            section.Add(table);
        }

        private string Moment(string key, MatchResult m) => _s.Format(key,
            ("score", $"{m.GoalsFor}-{m.GoalsAgainst}"), ("opponent", m.Opponent),
            ("venue", _s.Get(m.Home ? "common.home" : "common.away")),
            ("competition", m.Competition ?? "?"), ("date", _s.Date(m.Date)));

        private string MatchLine(MatchResult m) =>
            $"{m.Opponent} ({_s.Get(m.Home ? "common.home" : "common.away")}) {m.GoalsFor}-{m.GoalsAgainst}, {_s.Date(m.Date)}";

        private static string[] LeagueRow(string label, LeagueRecord r, int? points) => new[]
        {
            label, N(r.Played), N(r.Won), N(r.Drawn), N(r.Lost), N(r.GoalsFor), N(r.GoalsAgainst), N(points ?? r.Points),
        };

        private string Rating(PlayerSeason p) => p.AverageRating > 0 ? _s.Decimal(p.AverageRating) : "-";

        private string Money(double? value) => value == null ? _s.Get("common.unavailable") : Signed(value);

        private string Signed(double? value) =>
            value == null ? _s.Get("common.unavailable")
            : value < 0 ? "-" + Cash(-value.Value)
            : Cash(value.Value);

        /// <summary>Profit/loss style: always signed ("+€1.2M", "-€3.4M").</summary>
        private string PlusMinus(double? value) =>
            value == null ? _s.Get("common.unavailable") : value < 0 ? "-" + Cash(-value.Value) : "+" + Cash(value.Value);

        /// <summary>An amount from the game's formatter, with this language's decimal separator.</summary>
        private string Cash(double pounds) => _s.LocalizeDecimal(_money(pounds));

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
