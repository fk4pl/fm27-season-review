using System.Text;

namespace SeasonReview
{
    /// <summary>The same report as Markdown, e.g. for forum posts.</summary>
    internal static class MarkdownRenderer
    {
        public static string Render(ReportDocument doc)
        {
            var m = new StringBuilder();
            m.Append("# ").AppendLine(Inline(doc.Title)).AppendLine();
            m.Append('*').Append(Inline(doc.Subtitle)).AppendLine("*").AppendLine();
            if (doc.Notice != null) m.Append("> ").AppendLine(Inline(doc.Notice)).AppendLine();

            if (doc.Tiles.Count > 0)
            {
                var header = new StringBuilder("|");
                var line = new StringBuilder("|");
                var values = new StringBuilder("|");
                foreach (var (label, value) in doc.Tiles)
                {
                    header.Append(' ').Append(Cell(label)).Append(" |");
                    line.Append("---|");
                    values.Append(" **").Append(Cell(value)).Append("** |");
                }
                m.AppendLine(header.ToString()).AppendLine(line.ToString()).AppendLine(values.ToString()).AppendLine();
            }

            foreach (var section in doc.Sections)
            {
                m.Append("## ").AppendLine(Inline(section.Title)).AppendLine();
                foreach (var block in section.Blocks) Block(m, block);
            }

            m.AppendLine("---").AppendLine();
            foreach (var line in doc.Footer) m.Append('*').Append(Inline(line)).AppendLine("*  ");
            return m.ToString();
        }

        private static void Block(StringBuilder m, Block block)
        {
            switch (block)
            {
                case Paragraph p:
                    m.AppendLine(Inline(p.Text)).AppendLine();
                    break;
                case Note n:
                    m.Append('*').Append(Inline(n.Text)).AppendLine("*").AppendLine();
                    break;
                case Subheading s:
                    m.Append("### ").AppendLine(Inline(s.Text)).AppendLine();
                    break;
                case Bullets b:
                    foreach (var item in b.Items) m.Append("- ").AppendLine(Inline(item));
                    m.AppendLine();
                    break;
                case Highlight x:
                    m.Append("**").Append(Inline(x.Title)).Append(": ").Append(Inline(x.Text)).AppendLine("**  ");
                    m.AppendLine(Inline(x.Detail)).AppendLine();
                    break;
                case Facts f:
                    foreach (var (label, value) in f.Items)
                        m.Append("- ").Append(Inline(label)).Append(": **").Append(Inline(value)).AppendLine("**");
                    m.AppendLine();
                    break;
                case Table t:
                    m.Append('|');
                    foreach (var h in t.Headers) m.Append(' ').Append(Cell(h)).Append(" |");
                    m.AppendLine();
                    m.Append('|');
                    for (int i = 0; i < t.Headers.Length; i++)
                        m.Append(t.Numeric != null && i < t.Numeric.Length && t.Numeric[i] ? "---:|" : "---|");
                    m.AppendLine();
                    foreach (var row in t.Rows)
                    {
                        m.Append('|');
                        foreach (var cell in row) m.Append(' ').Append(Cell(cell)).Append(" |");
                        m.AppendLine();
                    }
                    m.AppendLine();
                    break;
            }
        }

        // Escape characters that would change Markdown meaning in running text.
        private static string Inline(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var s = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\\' || c == '*' || c == '_' || c == '`' || c == '[' || c == ']' || c == '<' || c == '>' || c == '#')
                    s.Append('\\');
                s.Append(c == '\n' || c == '\r' ? ' ' : c);
            }
            return s.ToString();
        }

        private static string Cell(string text) => Inline(text).Replace("|", "\\|");
    }
}
