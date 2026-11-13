using System.Net;
using System.Text;

namespace SeasonReview
{
    /// <summary>
    /// One self-contained HTML file: inline CSS, no scripts, no images, no external resources.
    /// Readable on screen and printable. All text is HTML-escaped.
    /// </summary>
    internal static class HtmlRenderer
    {
        private const string Css = @"
:root { --ink:#1d2430; --muted:#5b6575; --line:#d9dee6; --band:#1f3a5f; --soft:#f3f5f8; --accent:#2f6fb3; }
* { box-sizing:border-box; }
body { margin:0; background:#fff; color:var(--ink); font:15px/1.55 -apple-system,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif; }
main { max-width:920px; margin:0 auto; padding:0 24px 40px; }
header.band { background:var(--band); color:#fff; padding:28px 24px 22px; }
header.band .inner { max-width:872px; margin:0 auto; }
h1 { margin:0 0 4px; font-size:28px; line-height:1.2; }
.subtitle { margin:0; opacity:.85; }
.notice { margin:18px 0 0; padding:10px 14px; border-left:4px solid #e0a43a; background:#fff7e8; color:#5a4210; }
.tiles { display:flex; flex-wrap:wrap; gap:12px; margin:22px 0 6px; }
.tile { flex:1 1 150px; border:1px solid var(--line); border-radius:8px; padding:12px 14px; background:var(--soft); }
.tile .label { font-size:12px; text-transform:uppercase; letter-spacing:.05em; color:var(--muted); }
.tile .value { font-size:24px; font-weight:600; margin-top:2px; }
section { margin-top:30px; }
h2 { font-size:20px; margin:0 0 10px; padding-bottom:6px; border-bottom:2px solid var(--band); }
h3 { font-size:15px; margin:18px 0 8px; color:var(--band); }
p { margin:8px 0; }
.note { color:var(--muted); font-size:13px; }
ul { margin:6px 0 10px; padding-left:22px; }
li { margin:3px 0; }
table { border-collapse:collapse; width:100%; margin:6px 0 12px; font-size:14px; }
th, td { text-align:left; padding:6px 8px; border-bottom:1px solid var(--line); vertical-align:top; }
th { background:var(--soft); font-weight:600; }
td.num, th.num { text-align:right; font-variant-numeric:tabular-nums; white-space:nowrap; }
dl.facts { display:grid; grid-template-columns:minmax(220px,1fr) auto; margin:6px 0 10px; border-top:1px solid var(--line); }
dl.facts dt, dl.facts dd { margin:0; padding:6px 8px; border-bottom:1px solid var(--line); }
dl.facts dd { text-align:right; font-weight:600; font-variant-numeric:tabular-nums; }
.highlight { border:1px solid var(--line); border-left:4px solid var(--accent); border-radius:6px; padding:10px 14px; margin:12px 0; background:var(--soft); }
.highlight .label { font-size:12px; text-transform:uppercase; letter-spacing:.05em; color:var(--muted); }
.highlight .name { font-size:20px; font-weight:600; }
footer { margin-top:36px; padding-top:12px; border-top:1px solid var(--line); color:var(--muted); font-size:12.5px; }
footer p { margin:3px 0; }
@media print {
  body { font-size:11pt; }
  header.band { background:none; color:var(--ink); padding:0 0 8px; border-bottom:2px solid var(--ink); }
  main { max-width:none; padding:0; }
  section, table, .highlight, dl.facts { break-inside:avoid; }
  h2, h3 { break-after:avoid; }
}";

        public static string Render(ReportDocument doc)
        {
            var h = new StringBuilder();
            h.AppendLine("<!DOCTYPE html>");
            h.Append("<html lang=\"").Append(E(doc.Language)).AppendLine("\">");
            h.AppendLine("<head>");
            h.AppendLine("<meta charset=\"utf-8\">");
            h.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            h.Append("<title>").Append(E(doc.Title)).AppendLine("</title>");
            h.Append("<style>").Append(Css).AppendLine("</style>");
            h.AppendLine("</head>");
            h.AppendLine("<body>");

            h.AppendLine("<header class=\"band\"><div class=\"inner\">");
            h.Append("<h1>").Append(E(doc.Title)).AppendLine("</h1>");
            h.Append("<p class=\"subtitle\">").Append(E(doc.Subtitle)).AppendLine("</p>");
            h.AppendLine("</div></header>");

            h.AppendLine("<main>");
            if (doc.Notice != null) h.Append("<p class=\"notice\">").Append(E(doc.Notice)).AppendLine("</p>");
            if (doc.Tiles.Count > 0)
            {
                h.AppendLine("<div class=\"tiles\">");
                foreach (var (label, value) in doc.Tiles)
                    h.Append("<div class=\"tile\"><div class=\"label\">").Append(E(label))
                     .Append("</div><div class=\"value\">").Append(E(value)).AppendLine("</div></div>");
                h.AppendLine("</div>");
            }

            foreach (var section in doc.Sections)
            {
                h.Append("<section id=\"").Append(E(section.Id)).AppendLine("\">");
                h.Append("<h2>").Append(E(section.Title)).AppendLine("</h2>");
                foreach (var block in section.Blocks) Block(h, block);
                h.AppendLine("</section>");
            }

            h.AppendLine("<footer>");
            foreach (var line in doc.Footer) h.Append("<p>").Append(E(line)).AppendLine("</p>");
            h.AppendLine("</footer>");
            h.AppendLine("</main>");
            h.AppendLine("</body>");
            h.AppendLine("</html>");
            return h.ToString();
        }

        private static void Block(StringBuilder h, Block block)
        {
            switch (block)
            {
                case Paragraph p:
                    h.Append("<p>").Append(E(p.Text)).AppendLine("</p>");
                    break;
                case Note n:
                    h.Append("<p class=\"note\">").Append(E(n.Text)).AppendLine("</p>");
                    break;
                case Subheading s:
                    h.Append("<h3>").Append(E(s.Text)).AppendLine("</h3>");
                    break;
                case Bullets b:
                    h.AppendLine("<ul>");
                    foreach (var item in b.Items) h.Append("<li>").Append(E(item)).AppendLine("</li>");
                    h.AppendLine("</ul>");
                    break;
                case Highlight x:
                    h.Append("<div class=\"highlight\"><div class=\"label\">").Append(E(x.Title))
                     .Append("</div><div class=\"name\">").Append(E(x.Text)).Append("</div><p>")
                     .Append(E(x.Detail)).AppendLine("</p></div>");
                    break;
                case Facts f:
                    h.AppendLine("<dl class=\"facts\">");
                    foreach (var (label, value) in f.Items)
                        h.Append("<dt>").Append(E(label)).Append("</dt><dd>").Append(E(value)).AppendLine("</dd>");
                    h.AppendLine("</dl>");
                    break;
                case Table t:
                    h.AppendLine("<table>");
                    h.Append("<thead><tr>");
                    for (int i = 0; i < t.Headers.Length; i++)
                        h.Append(IsNumeric(t, i) ? "<th class=\"num\">" : "<th>").Append(E(t.Headers[i])).Append("</th>");
                    h.AppendLine("</tr></thead>");
                    h.AppendLine("<tbody>");
                    foreach (var row in t.Rows)
                    {
                        h.Append("<tr>");
                        for (int i = 0; i < row.Length; i++)
                            h.Append(IsNumeric(t, i) ? "<td class=\"num\">" : "<td>").Append(E(row[i])).Append("</td>");
                        h.AppendLine("</tr>");
                    }
                    h.AppendLine("</tbody></table>");
                    break;
            }
        }

        private static bool IsNumeric(Table t, int column) => t.Numeric != null && column < t.Numeric.Length && t.Numeric[column];

        private static string E(string text) => WebUtility.HtmlEncode(text ?? "");
    }
}
