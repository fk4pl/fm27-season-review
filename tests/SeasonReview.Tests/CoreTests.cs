using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeasonReview;
using Xunit;

namespace SeasonReview.Tests
{
    public class GameTextTests
    {
        [Fact]
        public void StripLink_KeepsVisibleText()
        {
            Assert.Equal("Beşiktaş", GameText.StripLink("\u0001AwAAACAx==\u0002Beşiktaş\u0001"));
            Assert.Equal("Plain", GameText.StripLink("Plain"));
            Assert.Equal("A and B", GameText.StripLink("\u0001x\u0002A\u0001 and \u0001y\u0002B\u0001"));
        }

        [Theory]
        [InlineData(265597965u, 2026, 6, 29)] // verified in game
        [InlineData(265598217u, 2026, 6, 30)] // verified in game
        public void TryParseGameDate_IsOneBased(uint packed, int y, int m, int d)
        {
            Assert.True(GameText.TryParseGameDate($"GameDate {{ Data1 = {packed}, Type = FmdateSiDate }}", out var date));
            Assert.Equal(new DateTime(y, m, d), date);
        }

        [Fact]
        public void TryParseGameDate_RejectsGarbage()
        {
            Assert.False(GameText.TryParseGameDate("not a date", out _));
            Assert.False(GameText.TryParseGameDate(null, out _));
        }
    }

    public class HiddenValuesTests
    {
        [Theory]
        [InlineData("CurrentAbility")]
        [InlineData("PotentialAbilityStarRange")]
        [InlineData("InjurySusceptibility")]
        [InlineData("InjuryRiskLevel")]
        [InlineData("DebugAverageRating")]
        [InlineData("Professionalism")]
        [InlineData("CA")]
        public void HiddenNamesAreBlocked(string name) => Assert.True(HiddenValues.IsHidden(name));

        [Theory]
        [InlineData("ClubGoalsThisSeason")]
        [InlineData("ClubAverageRatingThisSeason")]
        [InlineData("InjuryDurationDays")]
        [InlineData("Name")]
        public void VisibleNamesPass(string name) => Assert.False(HiddenValues.IsHidden(name));

        [Fact]
        public void SpecNeverRequestsHiddenValues()
        {
            var paths = new List<string>();
            void Collect(Spec spec, string prefix)
            {
                paths.AddRange(spec.Values.Select(v => prefix + v));
                foreach (var list in spec.Lists) { paths.Add(prefix + list.Path); Collect(list.Item, prefix + list.Path + "."); }
            }
            Collect(SeasonSpec.Build(2026), "");
            Collect(SeasonSpec.BuildHeader(), "");
            Collect(SeasonEnd.BuildCheck(), "");
            Assert.NotEmpty(paths);
            Assert.DoesNotContain(paths, HiddenValues.IsHiddenPath);
        }
    }

    public class HotkeyTests
    {
        [Fact]
        public void ParsesModifiersAndKeys()
        {
            Assert.True(Hotkey.TryParse("Ctrl+F8", out var k));
            Assert.True(k.Ctrl);
            Assert.False(k.Shift);
            Assert.Equal(0x77, k.VirtualKey);
            Assert.Equal("Ctrl+F8", k.ToString());
            Assert.True(Hotkey.TryParse("shift + alt + r", out var r));
            Assert.Equal('R', r.VirtualKey);
            Assert.False(Hotkey.TryParse("Ctrl", out _));
            Assert.False(Hotkey.TryParse("F8+F9", out _));
        }
    }

    public class StringsTests
    {
        [Fact]
        public void EnglishOrdinals()
        {
            var s = TestData.English();
            Assert.Equal(new[] { "1st", "2nd", "3rd", "4th", "11th", "12th", "13th", "21st", "22nd", "101st" },
                new[] { 1, 2, 3, 4, 11, 12, 13, 21, 22, 101 }.Select(s.Ordinal));
        }

        [Fact]
        public void TurkishFormats()
        {
            var s = TestData.Turkish();
            Assert.Equal("4.", s.Ordinal(4));
            Assert.Equal("29 Haziran 2026", s.Date(new DateTime(2026, 6, 29)));
            Assert.Equal("7,86", s.Decimal(7.856));
        }

        [Fact]
        public void EnglishPlurals()
        {
            var s = TestData.English();
            Assert.Equal("1 match", s.Count(1, "match"));
            Assert.Equal("0 matches", s.Count(0, "match"));
            Assert.Equal("3 matches", s.Count(3, "match"));
            Assert.Equal("1 assist", s.Count(1, "assist"));
            Assert.Equal("1 injury", s.Count(1, "injury"));
            Assert.Equal("2 injuries", s.Count(2, "injury"));
        }

        [Fact]
        public void TurkishHasNoPluralAfterNumbers()
        {
            var s = TestData.Turkish();
            Assert.Equal("1 maç", s.Count(1, "match"));
            Assert.Equal("3 maç", s.Count(3, "match"));
        }

        [Fact]
        public void MoneyUsesTheLanguagesDecimalSeparator()
        {
            Assert.Equal("€6,25M", TestData.Turkish().LocalizeDecimal("€6.25M"));
            Assert.Equal("€6.25M", TestData.English().LocalizeDecimal("€6.25M"));
            Assert.Equal("€149K", TestData.Turkish().LocalizeDecimal("€149K"));
            Assert.Equal("$1,234.5", TestData.Turkish().LocalizeDecimal("$1,234.5")); // ambiguous: left alone
        }

        [Fact]
        public void FinancialStatusIsTranslatedWhenKnown()
        {
            Assert.Equal("Normal", TestData.Turkish().GameValue("finances.status.value", "Okay"));
            Assert.Equal("Çok iyi", TestData.Turkish().GameValue("finances.status.value", "Very Good"));
            Assert.Equal("Something New", TestData.Turkish().GameValue("finances.status.value", "Something New"));
            Assert.Equal("Okay", TestData.English().GameValue("finances.status.value", "Okay"));
        }

        [Fact]
        public void PluralFallsBackToOtherWhenNoSingularGiven()
        {
            var s = Strings.Parse("xx", "plural.match.other = {n} Spiele");
            Assert.Equal("1 Spiele", s.Count(1, "match"));
        }

        [Fact]
        public void MissingKeyFallsBackToEnglish()
        {
            var partial = Strings.Parse("xx", "title = Rapor {club}", TestData.English());
            Assert.Equal("Rapor X", partial.Format("title", ("club", "X")));
            Assert.Equal(TestData.English().Get("section.players"), partial.Get("section.players"));
            Assert.Equal("[no.such.key]", TestData.English().Get("no.such.key"));
        }

        [Fact]
        public void TranslationsCoverEveryEnglishKeyAndPlaceholder()
        {
            var keys = Keys("en");
            foreach (var code in new[] { "tr" })
            {
                var translated = Keys(code);
                Assert.Empty(keys.Keys.Except(translated.Keys));
                foreach (var (key, english) in keys)
                    foreach (System.Text.RegularExpressions.Match placeholder in System.Text.RegularExpressions.Regex.Matches(english, @"\{[a-z]+\}"))
                        Assert.True(translated[key].Contains(placeholder.Value), $"{code}:{key} lacks {placeholder.Value}");
            }
        }

        private static Dictionary<string, string> Keys(string code) =>
            File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "lang", code + ".txt"))
                .Select(l => l.Trim()).Where(l => l.Length > 0 && l[0] != '#' && l.Contains('='))
                .ToDictionary(l => l.Substring(0, l.IndexOf('=')).Trim(), l => l.Substring(l.IndexOf('=') + 1).Trim());
    }

    public class SeasonDataTests
    {
        private static SeasonData Build(int remaining = 0)
        {
            var warnings = new List<string>();
            var data = SeasonDataBuilder.Build(TestData.Season(remaining), warnings.Add);
            Assert.Empty(warnings);
            return data;
        }

        [Fact]
        public void LeagueAndSplits()
        {
            var d = Build();
            Assert.Equal(TestData.Club, d.Club);
            Assert.Equal(2, d.LeaguePosition);
            Assert.Equal(3, d.LeagueSize);
            Assert.Equal(11, d.PointsFromTable);
            Assert.Equal(6, d.Total.Played);
            Assert.Equal((3, 2, 1, 0), (d.Home.Played, d.Home.Won, d.Home.Drawn, d.Home.Lost)); // W 2-0, D 1-1, W 2-1
            Assert.Equal((3, 1, 1, 1), (d.Away.Played, d.Away.Won, d.Away.Drawn, d.Away.Lost));
            Assert.True(d.SeasonComplete);
        }

        [Fact]
        public void RunsAndKeyMoments()
        {
            var d = Build();
            Assert.Equal(2, d.LongestWinningRun);   // W W D L W D
            Assert.Equal(3, d.LongestUnbeatenRun);
            Assert.Equal("Third United", d.BiggestWin.Opponent); // 4-0; the 9-0 friendly does not count
            Assert.Equal("Rival FC", d.HeaviestDefeat.Opponent);
        }

        [Fact]
        public void CupsExcludeFriendlies()
        {
            var cup = Assert.Single(Build().Cups);
            Assert.Equal("Example Cup", cup.Competition);
            Assert.Equal((2, 1, 0, 1), (cup.Played, cup.Won, cup.Drawn, cup.Lost));
            Assert.Equal("Big City", cup.LastMatch.Opponent);
        }

        [Fact]
        public void ObjectivesKeepOnlyThisSeasonsCompetitionTargets()
        {
            var o = Assert.Single(Build().Objectives);
            Assert.Equal("Challenge for the league title", o.Name);
            Assert.Equal("The board are delighted.", o.BoardView);
        }

        [Fact]
        public void PlayerOfTheSeasonNeedsEnoughAppearances()
        {
            var d = Build();
            Assert.Equal("Striker One", d.PlayerOfTheSeason.Name); // Sub Three has 8.6 but only 4 apps
        }

        [Fact]
        public void FinancesUseJulyBalanceAndKeepTotalsSeparate()
        {
            var d = Build();
            Assert.Equal(38000000, d.BalanceStart);
            Assert.Equal(new DateTime(2026, 7, 1), d.BalanceStartDate);
            Assert.Equal(30000000, d.BalanceEnd);
            Assert.Equal(90000000, d.Income);
            Assert.Equal(-1500000, d.ProjectedProfit);
        }

        [Fact]
        public void TransfersAndNetSpend()
        {
            var d = Build();
            Assert.Equal(2, d.Arrivals.Count);
            Assert.Equal("Old Club", d.Arrivals[0].OtherClub);
            Assert.Equal("Buyer FC", d.Departures[0].OtherClub);
            Assert.Equal(7000000, d.NetSpend);
        }

        [Fact]
        public void TextIsCleaned()
        {
            var d = Build();
            Assert.Equal("The fans are delighted.", d.Supporters.Overall);
            Assert.Null(SeasonDataBuilder.Text("FM.UI.FixtureNameReference"));
            Assert.Null(SeasonDataBuilder.Text("SI.Bindable.DynamicReference"));
        }

        [Fact]
        public void MissingSectionIsReportedNotThrown()
        {
            var raw = TestData.Season();
            raw.Lists.Remove(SeasonSpec.Transfers);
            var warnings = new List<string>();
            var d = SeasonDataBuilder.Build(raw, warnings.Add);
            Assert.Contains("transfers", d.Unavailable);
            Assert.Single(warnings);
        }

        [Fact]
        public void LongestRun()
        {
            var results = "WWLWWWDW".Select(c => new MatchResult { GoalsFor = c == 'L' ? 0 : c == 'W' ? 1 : 0, GoalsAgainst = c == 'L' ? 1 : 0 }).ToList();
            Assert.Equal(3, SeasonDataBuilder.LongestRun(results, m => m.Outcome == 'W'));
            Assert.Equal(5, SeasonDataBuilder.LongestRun(results, m => m.Outcome != 'L'));
        }

        [Theory]
        [InlineData("2026/27", 2026, 7)]
        [InlineData("2026", 2026, 1)]
        public void SeasonStart(string season, int year, int month) =>
            Assert.Equal(new DateTime(year, month, 1), SeasonDataBuilder.SeasonStart(season));
    }

    public class SeasonEndTests
    {
        private static GameRecord Check(int remaining)
        {
            var season = TestData.Season(remaining);
            var r = new GameRecord();
            foreach (var key in new[] { "GameName", "GameStartDate", SeasonSpec.Team + ".Name", SeasonSpec.Division + ".Name", SeasonSpec.Division + ".SeasonName" })
                r.Values[key] = season.Get(key);
            r.Lists[SeasonEnd.FixtureList] = new List<GameRecord> { new GameRecord() };
            r.Lists[SeasonEnd.FixtureList][0].Lists[SeasonEnd.FixtureList] = season.Items(SeasonSpec.Fixtures)[0].Items(SeasonSpec.Fixtures);
            return r;
        }

        [Fact]
        public void EndsAfterLastLeagueMatch()
        {
            var status = SeasonEnd.Evaluate(Check(0));
            Assert.True(status.Ended, status.Reason);
            Assert.Equal(6, status.LeaguePlayed);
        }

        [Fact]
        public void NotEndedWithFixturesLeft()
        {
            var status = SeasonEnd.Evaluate(Check(2));
            Assert.False(status.Ended);
            Assert.Equal(2, status.LeagueRemaining);
        }

        [Fact]
        public void NotEndedBeforeAnyLeagueMatch()
        {
            var r = Check(0);
            r.Lists[SeasonEnd.FixtureList][0].Lists[SeasonEnd.FixtureList] = new List<GameRecord>
            {
                TestData.Fixture(TestData.League, TestData.Club, "Rival FC", null, null, new DateTime(2026, 8, 1)),
            };
            Assert.False(SeasonEnd.Evaluate(r).Ended);
        }

        [Fact]
        public void KeyIdentifiesCareerClubAndSeason()
        {
            var key = SeasonEnd.Evaluate(Check(0)).Key;
            Assert.Equal(key, SeasonEnd.KeyOf(TestData.Season()));
            Assert.Contains("2026/27", key);
            Assert.Contains("Test career", key);
        }

        [Fact]
        public void ReportedSeasonsPersist()
        {
            var path = Path.Combine(Path.GetTempPath(), "sr-test-" + Guid.NewGuid() + ".txt");
            try
            {
                var first = new ReportedSeasons(path);
                Assert.False(first.Contains("a|b"));
                first.Add("a|b");
                Assert.True(new ReportedSeasons(path).Contains("a|b"));
            }
            finally { File.Delete(path); }
        }
    }

    public class ReportTests
    {
        private static ReportDocument Document(Strings strings, int remaining = 0) =>
            ReportBuilder.Build(SeasonDataBuilder.Build(TestData.Season(remaining), _ => { }), strings, TestData.Money, "test");

        [Fact]
        public void HtmlIsSelfContained()
        {
            var html = HtmlRenderer.Render(Document(TestData.English()));
            Assert.StartsWith("<!DOCTYPE html>", html);
            foreach (var forbidden in new[] { "http://", "https://", "<script", "<img", "<link", "src=", "@import", "url(" })
                Assert.DoesNotContain(forbidden, html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HtmlEscapesGameText()
        {
            var raw = TestData.Season();
            raw.Values[SeasonSpec.Confidence + ".BoardConfidenceOverallString"] = "<script>alert(1)</script> & co";
            var doc = ReportBuilder.Build(SeasonDataBuilder.Build(raw, _ => { }), TestData.English(), TestData.Money, "test");
            var html = HtmlRenderer.Render(doc);
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; co", html);
        }

        [Fact]
        public void EnglishReportTellsTheStory()
        {
            var md = MarkdownRenderer.Render(Document(TestData.English()));
            Assert.Contains("Kartal SK finished 2nd of 3 in the Example League with 11 points.", md);
            Assert.Contains("Striker One: 30 appearances, average rating 7.40, 20 goals and 3 assists.", md);
            Assert.Contains("1 injury this season, 21 days lost in total.", md);
            Assert.Contains("Longest winning run in the league: 2 matches.", md);
            Assert.Contains("better than the media's pre-season prediction of 3rd", md);
            Assert.Contains("Striker One", md);
            Assert.Contains("Profit/loss for the season (change in bank balance): **-£8M**", md);
            Assert.Contains("(the game's projection): **-£1.5M**", md);
            Assert.DoesNotContain("[", md.Replace("\\[", "").Replace("\\]", "")); // no missing keys
            Assert.DoesNotContain("Unused Five", md.Substring(md.IndexOf("Young players", StringComparison.Ordinal)));
        }

        [Fact]
        public void TurkishReportUsesTurkishText()
        {
            var md = MarkdownRenderer.Render(Document(TestData.Turkish()));
            Assert.Contains("Sezon Değerlendirmesi", md);
            Assert.Contains("2. sırada tamamladı ve 11 puan topladı", md);
            Assert.Contains("Bu sezon 1 sakatlık, toplam 21 gün kayıp.", md);
            Assert.Contains("Mali durum: **Normal**", md);
            Assert.Contains("£1,5M", md); // projection, decimal comma
            Assert.Contains("En çok gol atanlar", md);
            Assert.Contains("7,4", md); // decimal comma
            Assert.DoesNotContain("[", md.Replace("\\[", "").Replace("\\]", ""));
        }

        [Fact]
        public void InterimReportSaysSo()
        {
            var doc = Document(TestData.English(), remaining: 4);
            Assert.Contains("(4 league matches left)", doc.Notice);
            Assert.Contains("(1 league match left)", Document(TestData.English(), remaining: 1).Notice);
            var none = Document(TestData.English());
            Assert.Null(none.Notice);
        }

        [Fact]
        public void FeedbackListsHaveSubheadings()
        {
            var board = Document(TestData.English()).Sections.Single(s => s.Id == "board");
            var positives = board.Blocks.FindIndex(b => b is Subheading h && h.Text == "Positives");
            Assert.True(positives > 0);
            Assert.IsType<Bullets>(board.Blocks[positives + 1]);
            Assert.DoesNotContain(board.Blocks, b => b is Paragraph);
        }

        [Fact]
        public void UnavailableSectionsSaySo()
        {
            var raw = TestData.Season();
            raw.Lists.Remove(SeasonSpec.Players);
            var doc = ReportBuilder.Build(SeasonDataBuilder.Build(raw, _ => { }), TestData.English(), TestData.Money, "test");
            var players = doc.Sections.Single(s => s.Id == "players");
            Assert.Equal("Data unavailable.", Assert.IsType<Note>(Assert.Single(players.Blocks)).Text);
        }

        [Fact]
        public void FileNames()
        {
            Assert.Equal("Beşiktaş_2026-27", ReportFiles.BaseName("Beşiktaş", "2026/27", null));
            Assert.Equal("Manchester_United_2026-27_2027-01-31", ReportFiles.BaseName("Manchester United", "2026/27", new DateTime(2027, 1, 31)));
            Assert.Equal("A-B_2026", ReportFiles.BaseName("A:B*?", "2026", null));
        }
    }
}
