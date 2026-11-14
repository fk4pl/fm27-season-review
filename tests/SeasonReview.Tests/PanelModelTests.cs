using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeasonReview;
using Xunit;

namespace SeasonReview.Tests
{
    public class PanelModelTests
    {
        private static ReportDocument Document(Strings strings, int remaining = 0) =>
            ReportBuilder.Build(SeasonDataBuilder.Build(TestData.Season(remaining), _ => { }), strings, TestData.Money, "test");

        private static PanelModel Model(Strings strings, int remaining = 0) =>
            PanelModelBuilder.Build(Document(strings, remaining), strings);

        [Fact]
        public void OneTabPerReportSection_InReportOrder()
        {
            var strings = TestData.English();
            var doc = Document(strings);
            var model = PanelModelBuilder.Build(doc, strings);

            Assert.Equal(doc.Sections.Select(s => s.Id), model.Tabs.Select(t => t.Id));
            Assert.Equal(new[] { "summary", "league", "players", "transfers", "finances", "injuries", "board" }, model.Tabs.Select(t => t.Id));
            Assert.Equal(new[] { "Summary", "League", "Players", "Transfers", "Finances", "Injuries", "Board" }, model.Tabs.Select(t => t.Label));
            Assert.Equal(doc.Sections.Select(s => s.Title), model.Tabs.Select(t => t.Title));
            Assert.All(model.Tabs, t => Assert.NotEmpty(t.Rows));
            Assert.Null(model.Message);
        }

        [Fact]
        public void TabLabelsFollowTheLanguage()
        {
            var model = Model(TestData.Turkish());
            Assert.Equal(new[] { "Özet", "Lig", "Oyuncular", "Transferler", "Finans", "Sakatlıklar", "Yönetim" }, model.Tabs.Select(t => t.Label));
            Assert.Equal("Kapat", model.Close);
        }

        [Fact]
        public void TabWithoutAShortNameUsesTheSectionTitle()
        {
            var doc = new ReportDocument { Title = "T", Subtitle = "S" };
            doc.Sections.Add(new ReportSection { Id = "future", Title = "A future section" });
            var tab = Assert.Single(PanelModelBuilder.Build(doc, TestData.English()).Tabs);
            Assert.Equal("A future section", tab.Label);
        }

        [Fact]
        public void HeaderIsTheReportHeader()
        {
            var strings = TestData.English();
            var doc = Document(strings);
            var model = PanelModelBuilder.Build(doc, strings);
            Assert.Equal(doc.Title, model.Title);
            Assert.Equal(doc.Subtitle, model.Subtitle);
            Assert.Equal(doc.Tiles, model.Tiles);
            Assert.Equal(4, model.Tiles.Count);
            Assert.Null(model.Notice);
            Assert.Equal("Close", model.Close);
        }

        [Fact]
        public void InterimReportKeepsItsNotice()
        {
            var strings = TestData.English();
            var doc = Document(strings, remaining: 5);
            Assert.NotNull(doc.Notice);
            Assert.Equal(doc.Notice, PanelModelBuilder.Build(doc, strings).Notice);
        }

        [Fact]
        public void EveryBlockKindBecomesRows()
        {
            var doc = new ReportDocument { Title = "T", Subtitle = "S" };
            var section = new ReportSection { Id = "summary", Title = "Summary title" };
            section.Add(new Subheading { Text = "Heading" });
            section.Add(new Paragraph { Text = "Line one\nline two" });
            section.Add(new Note { Text = "A note" });
            var bullets = new Bullets();
            bullets.Items.AddRange(new[] { "first", "second" });
            section.Add(bullets);
            section.Add(new Highlight { Title = "Player of the season", Text = "A. Player", Detail = "20 goals" });
            var facts = new Facts();
            facts.Items.Add(("Balance", "£1M"));
            facts.Items.Add(("Wages", "£2M"));
            section.Add(facts);
            doc.Sections.Add(section);

            var rows = Assert.Single(PanelModelBuilder.Build(doc, TestData.English()).Tabs).Rows;
            Assert.Equal(new[]
            {
                PanelRowKind.Heading, PanelRowKind.Text, PanelRowKind.Note, PanelRowKind.Bullet, PanelRowKind.Bullet,
                PanelRowKind.Highlight, PanelRowKind.KeyValue, PanelRowKind.KeyValue,
            }, rows.Select(r => r.Kind));
            Assert.Equal("Heading", rows[0].Text);
            Assert.Equal("Line one line two", rows[1].Text); // one line per row
            Assert.Equal("A note", rows[2].Text);
            Assert.Equal("second", rows[4].Text);
            Assert.Equal(new[] { "Player of the season", "A. Player", "20 goals" }, rows[5].Cells);
            Assert.Equal(new[] { "Balance", "£1M" }, rows[6].Cells);
            Assert.Equal(100f, rows[6].Widths.Sum(), 3);
            Assert.Equal(new[] { false, true }, rows[6].Numeric);
            Assert.False(rows[6].Alternate);
            Assert.True(rows[7].Alternate);
        }

        [Fact]
        public void TablesBecomeAHeaderRowAndStripedRows()
        {
            var doc = new ReportDocument { Title = "T", Subtitle = "S" };
            var table = new Table { Headers = new[] { "Name", "Age", "Goals" }, Numeric = new[] { false, true, true } };
            table.Rows.Add(new[] { "A. Player", "24", "12" });
            table.Rows.Add(new[] { "B. Player", "19" });                 // too short: padded
            table.Rows.Add(new[] { "C. Player", "31", "3", "ignored" }); // too long: cut
            doc.Sections.Add(new ReportSection { Id = "players", Title = "Players" }.Add(table));

            var rows = Assert.Single(PanelModelBuilder.Build(doc, TestData.English()).Tabs).Rows;
            Assert.Equal(new[] { PanelRowKind.TableHeader, PanelRowKind.TableRow, PanelRowKind.TableRow, PanelRowKind.TableRow }, rows.Select(r => r.Kind));
            Assert.Equal(new[] { "Name", "Age", "Goals" }, rows[0].Cells);
            Assert.Equal(new[] { "B. Player", "19", "" }, rows[2].Cells);
            Assert.Equal(new[] { "C. Player", "31", "3" }, rows[3].Cells);
            Assert.All(rows, r => Assert.Same(rows[0].Widths, r.Widths));
            Assert.All(rows, r => Assert.Equal(new[] { false, true, true }, r.Numeric));
            Assert.Equal(new[] { false, false, true, false }, rows.Select(r => r.Alternate));
            Assert.Equal(100f, rows[0].Widths.Sum(), 3);
            Assert.True(rows[0].Widths[0] > rows[0].Widths[1], "the name column is the widest");
        }

        [Fact]
        public void ColumnWidthsAreClamped()
        {
            var rows = new List<string[]> { new[] { new string('x', 500), "1", "" } };
            var widths = PanelModelBuilder.ColumnWidths(rows, 3);
            Assert.Equal(100f, widths.Sum(), 3);
            Assert.Equal(widths[1], widths[2], 3);
            float highestRatio = (PanelModelBuilder.MaxColumnChars + 2f) / (PanelModelBuilder.MinColumnChars + 2f);
            Assert.Equal(highestRatio, widths[0] / widths[1], 3);
            Assert.True(widths[1] > 5f, "a one-character column still gets a usable share");
            Assert.Empty(PanelModelBuilder.ColumnWidths(rows, 0));
        }

        [Fact]
        public void RealReportTablesAreWellFormed()
        {
            foreach (var strings in new[] { TestData.English(), TestData.Turkish() })
            {
                var rows = Model(strings).Tabs.SelectMany(t => t.Rows).ToList();
                Assert.Contains(rows, r => r.Kind == PanelRowKind.TableHeader);
                Assert.Contains(rows, r => r.Kind == PanelRowKind.KeyValue);
                foreach (var row in rows)
                {
                    Assert.NotEmpty(row.Cells);
                    Assert.All(row.Cells, c => Assert.NotNull(c));
                    Assert.All(row.Cells, c => Assert.Equal(-1, c.IndexOf('\n')));
                    if (row.Cells.Length > 1 && row.Kind != PanelRowKind.Highlight)
                    {
                        Assert.Equal(row.Cells.Length, row.Widths.Length);
                        Assert.Equal(100f, row.Widths.Sum(), 2);
                        Assert.All(row.Widths, w => Assert.True(w > 3f));
                    }
                    if (row.Numeric != null) Assert.Equal(row.Cells.Length, row.Numeric.Length);
                }
            }
        }

        [Fact]
        public void PanelShowsTheSameCellsAsTheReport()
        {
            var strings = TestData.English();
            var doc = Document(strings);
            var model = PanelModelBuilder.Build(doc, strings);
            for (int i = 0; i < doc.Sections.Count; i++)
            {
                var reportCells = doc.Sections[i].Blocks.OfType<Table>().SelectMany(t => t.Rows.SelectMany(r => r)).ToList();
                var panelCells = model.Tabs[i].Rows.Where(r => r.Kind == PanelRowKind.TableRow).SelectMany(r => r.Cells).ToList();
                Assert.Equal(reportCells, panelCells);
            }
        }

        [Fact]
        public void UnavailableSectionsSaySo()
        {
            var raw = TestData.Season();
            raw.Lists.Remove(SeasonSpec.Players);
            var strings = TestData.English();
            var doc = ReportBuilder.Build(SeasonDataBuilder.Build(raw, _ => { }), strings, TestData.Money, "test");
            var players = PanelModelBuilder.Build(doc, strings).Tabs.Single(t => t.Id == "players");
            var row = Assert.Single(players.Rows);
            Assert.Equal(PanelRowKind.Note, row.Kind);
            Assert.Equal("Data unavailable.", row.Text);
        }

        [Theory]
        [InlineData("panel.loading")]
        [InlineData("panel.nocareer")]
        [InlineData("panel.noclub")]
        [InlineData("panel.failed")]
        public void MessageModelsHaveNoTabs(string key)
        {
            foreach (var strings in new[] { TestData.English(), TestData.Turkish() })
            {
                var model = PanelModelBuilder.Message(strings, key);
                Assert.Equal(strings.Get(key), model.Message);
                Assert.DoesNotContain("[", model.Message); // a missing key would show as [key]
                Assert.Equal(strings.Get("panel.title"), model.Title);
                Assert.Equal(strings.Get("panel.close"), model.Close);
                Assert.Empty(model.Tabs);
                Assert.Empty(model.Tiles);
            }
        }

        [Fact]
        public void TurkishHasItsOwnPanelTexts()
        {
            var english = TestData.English();
            var turkishOnly = TestData.Load("tr"); // no English fallback
            var keys = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "lang", "en.txt"))
                .Where(l => l.StartsWith("panel.", StringComparison.Ordinal))
                .Select(l => l.Substring(0, l.IndexOf('=')).Trim()).ToList();
            Assert.True(keys.Count >= 13);
            foreach (var key in keys)
            {
                Assert.True(turkishOnly.Has(key), "tr.txt lacks " + key);
                Assert.True(english.Has(key));
            }
        }
    }

    public class PanelTextTests
    {
        [Fact]
        public void PlainTextIsLeftAlone()
        {
            Assert.Equal("Beşiktaş 3-1 Fenerbahçe", PanelText.Escape("Beşiktaş 3-1 Fenerbahçe"));
            Assert.Equal("", PanelText.Escape(null));
            Assert.Equal("a > b & c", PanelText.Escape("a > b & c"));
        }

        [Fact]
        public void AngleBracketsCannotFormATag()
        {
            var escaped = PanelText.Escape("<b>bold</b> <color=red>x");
            // Outside the noparse wrappers no "<" is left that could start a tag of its own.
            var withoutWrappers = escaped.Replace("<noparse><</noparse>", "");
            Assert.DoesNotContain("<", withoutWrappers);
            Assert.Equal("b>bold/b> color=red>x", withoutWrappers);
        }

        [Fact]
        public void BoldWrapsEscapedText()
        {
            Assert.Equal("<b>Top scorers</b>", PanelText.Bold("Top scorers"));
            Assert.Equal("<b>a<noparse><</noparse>b</b>", PanelText.Bold("a<b"));
        }

        [Fact]
        public void PositionedRowStartsEachCellAtItsColumn()
        {
            var line = PanelText.PositionedRow(new[] { "Name", "24", "3" }, new[] { 50f, 25f, 25f }, 80);
            Assert.Equal("<pos=0%>Name<pos=50%>24<pos=75%>3", line);
            Assert.Equal("<pos=0%><b>Name</b><pos=62.5%><b>Age</b>",
                PanelText.PositionedRow(new[] { "Name", "Age" }, new[] { 62.5f, 37.5f }, 80, bold: true));
        }

        [Fact]
        public void PositionedRowCutsCellsThatWouldOverlap()
        {
            var line = PanelText.PositionedRow(new[] { "A very long player name indeed", "7" }, new[] { 25f, 75f }, 40);
            Assert.Equal("<pos=0%>A very l…<pos=25%>7", line); // 25% of 40 characters, minus one for the gap
        }
    }

    public class PanelLayoutTests
    {
        [Theory]
        [InlineData("Center", "Center")]
        [InlineData("center", "Center")]
        [InlineData("TopLeft", "TopLeft")]
        [InlineData("top-right", "TopRight")]
        [InlineData("bottom_left", "BottomLeft")]
        [InlineData(" Bottom Right ", "BottomRight")]
        public void PositionsParse(string text, string expected)
        {
            Assert.True(PanelLayout.TryParsePosition(text, out var position));
            Assert.Equal(expected, position.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("middle")]
        [InlineData("3")]
        public void UnknownPositionFallsBackToCenter(string text)
        {
            Assert.False(PanelLayout.TryParsePosition(text, out var position));
            Assert.Equal(PanelPosition.Center, position);
        }

        [Fact]
        public void MarginsPlaceThePanel()
        {
            float gap = PanelLayout.EdgeGap;
            Assert.Equal((null, null, null, null), PanelLayout.Margins(PanelPosition.Center));
            Assert.Equal((gap, gap, null, null), PanelLayout.Margins(PanelPosition.TopLeft));
            Assert.Equal((null, gap, gap, null), PanelLayout.Margins(PanelPosition.TopRight));
            Assert.Equal((gap, null, null, gap), PanelLayout.Margins(PanelPosition.BottomLeft));
            Assert.Equal((null, null, gap, gap), PanelLayout.Margins(PanelPosition.BottomRight));
        }

        [Fact]
        public void PanelNeverFillsTheWholeScreen()
        {
            Assert.InRange(PanelLayout.MaxWidthPercent, 50f, 95f);
            Assert.InRange(PanelLayout.MaxHeightPercent, 50f, 95f);
        }

        [Fact]
        public void DefaultPanelHotkeyParses()
        {
            Assert.True(Hotkey.TryParse("F10", out var key));
            Assert.Equal(0x79, key.VirtualKey);
            Assert.False(key.Ctrl || key.Shift || key.Alt);
        }
    }
}
