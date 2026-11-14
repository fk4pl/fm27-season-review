using System;
using System.Collections.Generic;
using System.Linq;
using SeasonReview;
using Xunit;

namespace SeasonReview.Tests
{
    /// <summary>
    /// A table row must fit the panel: its column widths are shares of the row that never add up
    /// to more than the row, padding included, and number columns keep enough room to be read.
    /// </summary>
    public class PanelWidthTests
    {
        /// <summary>The panel at its normal width, and at the allowed share of a narrow window (1000 units wide).</summary>
        private static readonly float[] PanelWidths = { PanelLayout.Width, PanelLayout.WidthOn(1000f) };

        /// <summary>Never more than the row, and no more lost to rounding than one step per column.</summary>
        internal static void AssertFillsTheRow(float[] widths)
        {
            float sum = 0f;
            foreach (var w in widths) sum += w;
            Assert.True(sum <= 100f, $"widths add up to {sum:R}, more than the row");
            Assert.True(sum >= 100f - widths.Length * PanelModelBuilder.WidthStep, $"widths add up to only {sum:R}");
        }

        private static List<string[]> Rows(int columns, int rows, Random random, int longestText)
        {
            var list = new List<string[]>();
            for (int r = 0; r < rows; r++)
            {
                var row = new string[columns];
                for (int c = 0; c < columns; c++) row[c] = new string('x', random.Next(0, longestText + 1));
                list.Add(row);
            }
            return list;
        }

        [Fact]
        public void WidthsNeverAddUpToMoreThanTheRow()
        {
            var random = new Random(27);
            for (int columns = 1; columns <= 16; columns++)
                for (int round = 0; round < 200; round++)
                {
                    var rows = Rows(columns, 1 + round % 6, random, round % 3 == 0 ? 120 : 14);
                    var numeric = Enumerable.Range(0, columns).Select(c => c > 0 && random.Next(3) > 0).ToArray();
                    foreach (var widths in new[] { PanelModelBuilder.ColumnWidths(rows, columns), PanelModelBuilder.ColumnWidths(rows, columns, numeric) })
                    {
                        Assert.Equal(columns, widths.Length);
                        AssertFillsTheRow(widths);
                        Assert.All(widths, w => Assert.True(w > 0f));
                        // Whole steps: the sum above is exact, not rounded.
                        Assert.All(widths, w => Assert.Equal(Math.Floor(w / PanelModelBuilder.WidthStep), w / PanelModelBuilder.WidthStep));
                    }
                }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(12)]
        [InlineData(16)]
        public void ATableWithManyColumnsFitsTheRow_PaddingIncluded(int columns)
        {
            // Like the scorers table, only wider: a name, then numbers.
            var header = new[] { "Player" }.Concat(Enumerable.Range(1, columns - 1).Select(i => "Col " + i)).ToArray();
            var table = new Table { Headers = header, Numeric = header.Select((_, i) => i > 0).ToArray() };
            table.Rows.Add(new[] { "A very long player name that goes on and on" }.Concat(Enumerable.Repeat("1,234", columns - 1)).ToArray());
            table.Rows.Add(new[] { "Short" }.Concat(Enumerable.Repeat("7.25", columns - 1)).ToArray());
            var rows = Tab(table).Rows;

            Assert.Equal(3, rows.Count);
            foreach (var row in rows)
            {
                AssertFillsTheRow(row.Widths);
                foreach (var panel in PanelWidths)
                {
                    float rowWidth = PanelLayout.RowWidth(panel), used = 0f;
                    for (int c = 0; c < columns; c++)
                    {
                        float cell = rowWidth * row.Widths[c] / 100f;
                        // The padding is drawn inside the cell, so it only fits if the cell is wider than its padding.
                        Assert.True(cell > 2f * PanelLayout.CellPadding, $"column {c} is narrower than its padding");
                        used += cell;
                    }
                    Assert.True(used <= rowWidth + 0.01f, $"cells take {used} of {rowWidth}");
                }
            }
        }

        [Fact]
        public void NumbersOfTheRealReportFitTheirColumns()
        {
            int checkedCells = 0;
            foreach (var strings in new[] { TestData.English(), TestData.Turkish() })
            {
                var document = ReportBuilder.Build(SeasonDataBuilder.Build(TestData.Season(), _ => { }), strings, TestData.Money, "test");
                foreach (var tab in PanelModelBuilder.Build(document, strings).Tabs)
                    foreach (var row in tab.Rows.Where(r => r.Numeric != null))
                        checkedCells += AssertNumbersFit(tab.Id, row);
            }
            Assert.True(checkedCells > 100, "the sample report has number columns in several tabs");
        }

        [Fact]
        public void NumbersStillFitNextToVeryLongTexts()
        {
            // Wider than any real table: three long text columns and five number columns.
            var table = new Table
            {
                Headers = new[] { "Player", "Club", "Injury", "Apps", "Minutes", "Goals", "Av. rating", "Fee" },
                Numeric = new[] { false, false, false, true, true, true, true, true },
            };
            for (int i = 0; i < 4; i++)
                table.Rows.Add(new[]
                {
                    new string('n', 60), new string('c', 45), new string('i', 80), "38", "3,420", "27", "7.45", "€125,000,000",
                });
            var rows = Tab(table).Rows;

            foreach (var row in rows) Assert.Equal(8 - 3, AssertNumbersFit("wide", row));

            // The text columns gave way, the number columns did not: without the long texts the
            // numbers would get the same share of the line or less.
            var widths = rows[0].Widths;
            float line = PanelModelBuilder.LineChars;
            Assert.Equal(("€125,000,000".Length + PanelModelBuilder.ColumnPaddingChars) / line * 100f, widths[7], 1);
            Assert.Equal(("Av. rating".Length + PanelModelBuilder.ColumnPaddingChars) / line * 100f, widths[6], 1);
            Assert.All(widths.Take(3), w => Assert.True(w > (PanelModelBuilder.MinColumnChars + PanelModelBuilder.ColumnPaddingChars) / line * 100f - 0.1f));
            Assert.True(widths[0] < widths[7] * (PanelModelBuilder.MaxColumnChars + 2f) / ("€125,000,000".Length + 2f), "the text columns got less than their plain share");
        }

        [Fact]
        public void ATableThatFitsKeepsItsProportions()
        {
            var rows = new List<string[]> { new[] { "Player name here", "38", "3,420" } };
            var plain = PanelModelBuilder.ColumnWidths(rows, 3);
            var numbers = PanelModelBuilder.ColumnWidths(rows, 3, new[] { false, true, true });
            Assert.Equal(plain, numbers); // number columns only matter when the table is too wide for one line
        }

        [Fact]
        public void PanelWidthFollowsTheScreenOnlyWhenItIsNarrow()
        {
            Assert.Equal(PanelLayout.Width, PanelLayout.WidthOn(1920f));
            Assert.Equal(PanelLayout.Width, PanelLayout.WidthOn(1440f)); // a square window
            Assert.Equal(900f, PanelLayout.WidthOn(1000f));
            Assert.True(PanelLayout.RowWidth(PanelLayout.Width) < PanelLayout.Width - 2f * PanelLayout.BoxPadding);
        }

        /// <summary>Every number cell of the row (and its column title) fits on one line at both panel widths. Returns how many were checked.</summary>
        private static int AssertNumbersFit(string where, PanelRow row)
        {
            int count = 0;
            for (int c = 0; c < row.Cells.Length; c++)
            {
                if (!row.Numeric[c]) continue;
                count++;
                foreach (var panel in PanelWidths)
                {
                    float room = PanelLayout.CellTextWidth(row.Widths[c], panel), needed = PanelLayout.TextWidth(row.Cells[c]);
                    Assert.True(needed <= room, $"{where}: \"{row.Cells[c]}\" needs {needed:0} of {room:0} in a {panel:0} wide panel");
                }
            }
            return count;
        }

        private static PanelTab Tab(Table table)
        {
            var document = new ReportDocument { Title = "t" };
            var section = new ReportSection { Id = "x", Title = "x" };
            section.Add(table);
            document.Sections.Add(section);
            return PanelModelBuilder.Build(document, TestData.English()).Tabs[0];
        }
    }
}
