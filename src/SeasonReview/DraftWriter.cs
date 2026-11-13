using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SeasonReview
{
    /// <summary>Writes everything that was read as plain text, for checking the data sources.</summary>
    internal static class DraftWriter
    {
        public static string DefaultFolder()
        {
            // Inside Proton this resolves to C:\users\steamuser\Documents in the game's prefix.
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, "Sports Interactive", "Football Manager 27", "SeasonReviews");
        }

        public static string Write(Record data, string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "draft-raw.txt");
            var text = new StringBuilder();
            text.AppendLine("Season Review: raw data draft (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")");
            text.AppendLine();
            Append(text, data, 0);
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static void Append(StringBuilder text, Record record, int depth)
        {
            var indent = new string(' ', depth * 2);
            foreach (var pair in record.Values)
                text.Append(indent).Append(pair.Key).Append(" = ").AppendLine(Show(pair.Value));

            foreach (var pair in record.Lists)
            {
                record.ListNotes.TryGetValue(pair.Key, out var note);
                text.AppendLine().Append(indent).Append("[").Append(pair.Key).Append("] ").AppendLine(note ?? "");
                int index = 0;
                foreach (var item in pair.Value)
                {
                    // One line per item when it has no nested lists.
                    if (item.Lists.Count == 0)
                        text.Append(indent).Append("  ").Append(index++).Append(": ")
                            .AppendLine(string.Join(" | ", item.Values.Select(v => v.Key + "=" + Show(v.Value))));
                    else
                    {
                        text.Append(indent).Append("  ").Append(index++).AppendLine(":");
                        Append(text, item, depth + 2);
                    }
                }
            }
            foreach (var pair in record.ListNotes.Where(n => !record.Lists.ContainsKey(n.Key)))
                text.Append(indent).Append("[").Append(pair.Key).Append("] ").AppendLine(pair.Value);
        }

        private static string Show(string value) =>
            value == null ? "(no answer)" : GameText.StripLink(value).Replace('\n', ' ').Replace('\r', ' ');
    }
}
