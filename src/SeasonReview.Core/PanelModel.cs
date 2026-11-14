using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SeasonReview
{
    /// <summary>
    /// What the in-game panel shows, as plain data: built from the same
    /// <see cref="ReportDocument"/> the HTML and Markdown reports are rendered from, so the panel
    /// can never show different numbers or wording. All text is plain (not escaped); the view
    /// decides how to draw it. No game or Unity types here, so it is unit tested.
    /// </summary>
    internal sealed class PanelModel
    {
        public string Title, Subtitle, Notice;
        /// <summary>Label of the Close button.</summary>
        public string Close;
        /// <summary>Set for states without a report ("Loading...", no career loaded); then there are no tabs.</summary>
        public string Message;
        public readonly List<(string Label, string Value)> Tiles = new();
        public readonly List<PanelTab> Tabs = new();
    }

    internal sealed class PanelTab
    {
        /// <summary>The report section id, e.g. "league".</summary>
        public string Id;
        /// <summary>Short text for the tab button.</summary>
        public string Label;
        /// <summary>The section's full title, shown as the heading inside the tab.</summary>
        public string Title;
        public readonly List<PanelRow> Rows = new();
    }

    internal enum PanelRowKind
    {
        /// <summary>One cell: a heading inside a tab.</summary>
        Heading,
        /// <summary>One cell: running text.</summary>
        Text,
        /// <summary>One cell: a remark such as "Data unavailable."</summary>
        Note,
        /// <summary>One cell: a list item.</summary>
        Bullet,
        /// <summary>Three cells: title, text, detail (e.g. the player of the season).</summary>
        Highlight,
        /// <summary>Two cells: label and value.</summary>
        KeyValue,
        /// <summary>Column titles of a table.</summary>
        TableHeader,
        /// <summary>One table row.</summary>
        TableRow,
    }

    internal sealed class PanelRow
    {
        public PanelRowKind Kind;
        public string[] Cells;
        /// <summary>Column widths in percent (sum 100) for rows with more than one cell; shared by all rows of a table.</summary>
        public float[] Widths;
        /// <summary>Columns holding numbers (right-aligned). Null when no column is numeric.</summary>
        public bool[] Numeric;
        /// <summary>Every second table row, for a striped background.</summary>
        public bool Alternate;

        public string Text => Cells.Length > 0 ? Cells[0] : "";
    }

    internal static class PanelModelBuilder
    {
        /// <summary>Widest and narrowest a table column may get, measured in characters, before scaling to percent.</summary>
        internal const int MinColumnChars = 5, MaxColumnChars = 42;
        /// <summary>Characters added to every column for its left and right padding.</summary>
        internal const int ColumnPaddingChars = 2;
        /// <summary>
        /// About how many characters fit across one table row in the panel. A table that needs
        /// more gives the room to its number columns first; its text columns share the rest and wrap.
        /// </summary>
        internal const int LineChars = 94;
        /// <summary>Widths are whole multiples of 1/64 percent: exact as floats, so a row can be added up without rounding error.</summary>
        internal const float WidthStep = 1f / 64f;
        /// <summary>Share of the row given to the label of a label/value pair.</summary>
        internal const float KeyWidth = 45f;

        /// <summary>The report as tabs: one per report section, in report order.</summary>
        public static PanelModel Build(ReportDocument doc, Strings strings)
        {
            var model = new PanelModel
            {
                Title = doc.Title,
                Subtitle = doc.Subtitle,
                Notice = doc.Notice,
                Close = strings.Get("panel.close"),
            };
            model.Tiles.AddRange(doc.Tiles);

            foreach (var section in doc.Sections)
            {
                var key = "panel.tab." + section.Id;
                var tab = new PanelTab
                {
                    Id = section.Id,
                    Label = strings.Has(key) ? strings.Get(key) : section.Title,
                    Title = section.Title,
                };
                foreach (var block in section.Blocks) AddBlock(tab.Rows, block);
                model.Tabs.Add(tab);
            }
            return model;
        }

        /// <summary>A panel without a report: only a title and one message, e.g. "panel.loading".</summary>
        public static PanelModel Message(Strings strings, string messageKey) => new PanelModel
        {
            Title = strings.Get("panel.title"),
            Close = strings.Get("panel.close"),
            Message = strings.Get(messageKey),
        };

        private static void AddBlock(List<PanelRow> rows, Block block)
        {
            switch (block)
            {
                case Subheading s:
                    rows.Add(Single(PanelRowKind.Heading, s.Text));
                    break;
                case Paragraph p:
                    rows.Add(Single(PanelRowKind.Text, p.Text));
                    break;
                case Note n:
                    rows.Add(Single(PanelRowKind.Note, n.Text));
                    break;
                case Bullets b:
                    foreach (var item in b.Items) rows.Add(Single(PanelRowKind.Bullet, item));
                    break;
                case Highlight h:
                    rows.Add(new PanelRow { Kind = PanelRowKind.Highlight, Cells = new[] { Clean(h.Title), Clean(h.Text), Clean(h.Detail) } });
                    break;
                case Facts f:
                    var keyValue = new[] { KeyWidth, 100f - KeyWidth };
                    var valueIsNumber = new[] { false, true };
                    for (int i = 0; i < f.Items.Count; i++)
                        rows.Add(new PanelRow
                        {
                            Kind = PanelRowKind.KeyValue,
                            Cells = new[] { Clean(f.Items[i].Label), Clean(f.Items[i].Value) },
                            Widths = keyValue,
                            Numeric = valueIsNumber,
                            Alternate = i % 2 == 1,
                        });
                    break;
                case Table t:
                    AddTable(rows, t);
                    break;
            }
        }

        private static void AddTable(List<PanelRow> rows, Table table)
        {
            int columns = table.Headers.Length;
            var numeric = new bool[columns];
            bool anyNumeric = false;
            for (int c = 0; c < columns; c++)
            {
                numeric[c] = table.Numeric != null && c < table.Numeric.Length && table.Numeric[c];
                anyNumeric |= numeric[c];
            }

            var cells = new List<string[]> { Fit(table.Headers, columns) };
            foreach (var row in table.Rows) cells.Add(Fit(row, columns));
            var widths = ColumnWidths(cells, columns, numeric);

            for (int r = 0; r < cells.Count; r++)
                rows.Add(new PanelRow
                {
                    Kind = r == 0 ? PanelRowKind.TableHeader : PanelRowKind.TableRow,
                    Cells = cells[r],
                    Widths = widths,
                    Numeric = anyNumeric ? numeric : null,
                    Alternate = r > 0 && r % 2 == 0,
                });
        }

        /// <summary>
        /// Column widths in percent of the row, from the longest text in each column. The widths
        /// of a row never add up to more than 100: each is rounded down to <see cref="WidthStep"/>,
        /// and a cell's padding is drawn inside its width, not added to it.
        ///
        /// Lengths are clamped so one long sentence cannot starve the other columns and a
        /// one-digit column stays readable. When a table needs more than <see cref="LineChars"/>,
        /// only the text columns give way (they wrap); number columns keep the room they need.
        /// </summary>
        internal static float[] ColumnWidths(List<string[]> rows, int columns, bool[] numeric = null)
        {
            var widths = new float[columns];
            if (columns == 0) return widths;

            const float narrowest = MinColumnChars + ColumnPaddingChars;
            var chars = new double[columns];
            double numbers = 0, text = 0;
            int textColumns = 0;
            for (int c = 0; c < columns; c++)
            {
                int longest = 0;
                foreach (var row in rows) longest = Math.Max(longest, row[c].Length);
                chars[c] = Math.Min(Math.Max(longest, MinColumnChars), MaxColumnChars) + ColumnPaddingChars;
                if (numeric != null && c < numeric.Length && numeric[c]) numbers += chars[c];
                else { text += chars[c]; textColumns++; }
            }

            double room = LineChars - numbers, least = textColumns * narrowest;
            if (numbers + text > LineChars && text > least && room >= least)
                for (int c = 0; c < columns; c++)
                    if (numeric == null || c >= numeric.Length || !numeric[c])
                        chars[c] = narrowest + (chars[c] - narrowest) * (room - least) / (text - least);

            double total = 0;
            foreach (var w in chars) total += w;
            for (int c = 0; c < columns; c++)
                widths[c] = (float)(Math.Floor(chars[c] / total * 100.0 / WidthStep) * WidthStep);
            return widths;
        }

        private static PanelRow Single(PanelRowKind kind, string text) => new PanelRow { Kind = kind, Cells = new[] { Clean(text) } };

        /// <summary>Exactly <paramref name="columns"/> cells: missing ones become empty, extra ones are dropped.</summary>
        private static string[] Fit(string[] row, int columns)
        {
            var cells = new string[columns];
            for (int c = 0; c < columns; c++) cells[c] = Clean(row != null && c < row.Length ? row[c] : null);
            return cells;
        }

        /// <summary>Never null, and on one line.</summary>
        private static string Clean(string text) =>
            string.IsNullOrEmpty(text) ? "" : text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    /// <summary>Rich-text helpers for the panel's labels (TextCore tags, as used by UI Toolkit).</summary>
    internal static class PanelText
    {
        /// <summary>
        /// Makes text safe for a rich-text label: every "&lt;" is wrapped in noparse so a name or
        /// sentence from the game can never be read as a tag. Text without "&lt;" is returned as is.
        /// </summary>
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.IndexOf('<') < 0 ? text : text.Replace("<", "<noparse><</noparse>");
        }

        public static string Bold(string text) => "<b>" + Escape(text) + "</b>";

        /// <summary>
        /// A whole row as one rich-text line, with each cell starting at its column's left edge
        /// (&lt;pos=..%&gt;). Used only when side-by-side elements are not available in the game.
        /// Cells are cut to what roughly fits, so columns never run into each other.
        /// </summary>
        /// <param name="charsPerLine">About how many characters fit across the whole row.</param>
        public static string PositionedRow(string[] cells, float[] widths, int charsPerLine, bool bold = false)
        {
            var line = new StringBuilder();
            float left = 0f;
            for (int c = 0; c < cells.Length; c++)
            {
                float width = widths != null && c < widths.Length ? widths[c] : 100f / cells.Length;
                int budget = Math.Max(1, (int)(charsPerLine * width / 100f) - 1);
                var cell = cells[c] ?? "";
                if (cell.Length > budget) cell = budget > 1 ? cell.Substring(0, budget - 1) + "…" : cell.Substring(0, 1);
                line.Append("<pos=").Append(left.ToString("0.#", CultureInfo.InvariantCulture)).Append("%>");
                line.Append(bold ? Bold(cell) : Escape(cell));
                left += width;
            }
            return line.ToString();
        }
    }

    /// <summary>Where the panel sits on the screen (the PanelPosition setting).</summary>
    internal enum PanelPosition { Center, TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>Panel size and placement, in the panel's 1920x1080 reference units. Pure numbers; the view applies them.</summary>
    internal static class PanelLayout
    {
        public const float Width = 1100f, Height = 800f;
        /// <summary>The panel never takes more than this share of the screen, whatever the resolution or window size.</summary>
        public const float MaxWidthPercent = 90f, MaxHeightPercent = 85f;
        /// <summary>Gap to the screen edge for corner positions.</summary>
        public const float EdgeGap = 40f;

        /// <summary>Space between the panel's edge and its content, on each side.</summary>
        public const float BoxPadding = 12f;
        /// <summary>Space kept free on the right of a tab for the vertical scroll bar.</summary>
        public const float ScrollBarRoom = 24f;
        /// <summary>Left and right padding of a table cell, drawn inside the cell's width.</summary>
        public const float CellPadding = 6f;
        /// <summary>Size of the text in rows and tables.</summary>
        public const float TextSize = 15f;
        /// <summary>A cautious guess at how wide one character is, as a share of the text size (digits and bold capitals included).</summary>
        public const float CharWidth = 0.6f;

        /// <summary>
        /// Width of the panel on a screen that is <paramref name="screenWidth"/> reference units
        /// wide: the normal width, or the allowed share of a narrower screen. (The panel scales
        /// with the screen, so a 16:9 screen is always 1920 units wide, whatever its resolution;
        /// only a narrow window gets below the normal width.)
        /// </summary>
        public static float WidthOn(float screenWidth) => Math.Min(Width, screenWidth * MaxWidthPercent / 100f);

        /// <summary>Width of a table row inside a panel of the given width: what is left after the panel's padding and the scroll bar.</summary>
        public static float RowWidth(float panelWidth) => panelWidth - 2f * BoxPadding - ScrollBarRoom;

        /// <summary>Room for text in a cell that gets <paramref name="percent"/> of a row, after its padding.</summary>
        public static float CellTextWidth(float percent, float panelWidth) => RowWidth(panelWidth) * percent / 100f - 2f * CellPadding;

        /// <summary>Estimated width of a text on one line.</summary>
        public static float TextWidth(string text) => (text ?? "").Length * TextSize * CharWidth;

        public static bool TryParsePosition(string text, out PanelPosition position)
        {
            position = PanelPosition.Center;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var wanted = text.Replace(" ", "").Replace("-", "").Replace("_", "");
            foreach (PanelPosition p in Enum.GetValues(typeof(PanelPosition)))
                if (string.Equals(p.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    position = p;
                    return true;
                }
            return false;
        }

        /// <summary>
        /// Margins that place the panel inside a full-screen column container: a number is a fixed
        /// gap, null means "auto" (takes the free space on that side). Two opposite autos centre it.
        /// </summary>
        public static (float? Left, float? Top, float? Right, float? Bottom) Margins(PanelPosition position)
        {
            switch (position)
            {
                case PanelPosition.TopLeft: return (EdgeGap, EdgeGap, null, null);
                case PanelPosition.TopRight: return (null, EdgeGap, EdgeGap, null);
                case PanelPosition.BottomLeft: return (EdgeGap, null, null, EdgeGap);
                case PanelPosition.BottomRight: return (null, null, EdgeGap, EdgeGap);
                default: return (null, null, null, null);
            }
        }
    }
}
