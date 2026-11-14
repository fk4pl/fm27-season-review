using System;
using System.IO;
using System.Text;

namespace SeasonReview
{
    /// <summary>File names and writing for finished reports.</summary>
    internal static class ReportFiles
    {
        /// <summary>
        /// "&lt;Club&gt;_&lt;Season&gt;" for a season-end report, e.g. "Beşiktaş_2026-27";
        /// interim reports get the in-game date appended so they never replace the final one.
        /// </summary>
        public static string BaseName(string club, string season, DateTime? interimDate)
        {
            var name = Clean(club ?? "Club") + "_" + Clean(season ?? "Season");
            if (interimDate != null) name += "_" + interimDate.Value.ToString("yyyy-MM-dd");
            return name;
        }

        private static string Clean(string text)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var s = new StringBuilder(text.Length);
            foreach (char c in text.Trim())
            {
                if (c == '/' || c == '\\' || c == ':') s.Append('-');
                else if (char.IsWhiteSpace(c)) s.Append('_');
                else if (Array.IndexOf(invalid, c) >= 0 || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|') continue;
                else s.Append(c);
            }
            var result = s.ToString().Trim('.', '_', '-');
            return result.Length == 0 ? "_" : result;
        }

        /// <summary>Writes "&lt;name&gt;.html" and "&lt;name&gt;.md"; returns the HTML path.</summary>
        public static string Write(string folder, string baseName, string html, string markdown)
        {
            DiskWrites.Demand("report files");
            Directory.CreateDirectory(folder);
            var utf8 = new UTF8Encoding(false);
            var htmlPath = Path.Combine(folder, baseName + ".html");
            WriteAtomically(htmlPath, html, utf8);
            WriteAtomically(Path.Combine(folder, baseName + ".md"), markdown, utf8);
            return htmlPath;
        }

        // Write to a temporary file first so a crash never leaves a half-written report.
        private static void WriteAtomically(string path, string content, Encoding encoding)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, content, encoding);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
