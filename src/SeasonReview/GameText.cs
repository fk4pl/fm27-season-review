using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SeasonReview
{
    /// <summary>Pure helpers that turn raw game values into plain text. No game types here.</summary>
    internal static class GameText
    {
        // Link markup: SOH, base64 link data, STX, visible text, SOH.
        private static readonly Regex LinkData = new Regex("\u0001[^\u0001\u0002]*\u0002", RegexOptions.Compiled);

        /// <summary>
        /// Names and sentences can contain clickable links, encoded as
        /// U+0001 + base64 link data + U+0002 + visible text + U+0001 (verified in game).
        /// Returns the visible text only.
        /// </summary>
        public static string StripLink(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\u0001') < 0) return value;
            return LinkData.Replace(value, "").Replace("\u0001", "").Replace("\u0002", "");
        }

        private static readonly Regex PackedDate = new Regex(@"Data1\s*=\s*(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Parses the game's date value, printed as "GameDate { Data1 = N, Type = FmdateSiDate }",
        /// where N = year &lt;&lt; 17 | one-based day of year &lt;&lt; 8 | flags
        /// (verified in game: day 180 of 2026 is shown as 29 June 2026).
        /// </summary>
        public static bool TryParseGameDate(string value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrEmpty(value)) return false;
            var match = PackedDate.Match(value);
            if (!match.Success || !uint.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var packed))
                return false;

            int year = (int)(packed >> 17);
            int dayOfYear = (int)((packed & 0x1FFFF) >> 8);
            if (year < 1900 || year > 2200 || dayOfYear < 1 || dayOfYear > 366) return false;
            date = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
            return true;
        }
    }
}
