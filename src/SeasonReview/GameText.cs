using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SeasonReview
{
    /// <summary>Pure helpers that turn raw game values into plain text. No game types here.</summary>
    internal static class GameText
    {
        /// <summary>
        /// Name properties arrive as a clickable link: a base64 blob (link target and tooltip)
        /// immediately followed by the visible text, e.g. "BAAAAC…SnM=Beşiktaş". The blob's last
        /// field is a length-prefixed tooltip string, which lets us find where the blob ends.
        /// Returns the visible text; returns the input unchanged when no blob is recognised.
        /// </summary>
        public static string StripLink(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 12) return value;

            // Longest run of base64 characters at the start (padding only at the end).
            int run = 0;
            while (run < value.Length && IsBase64Char(value[run])) run++;
            int padded = run;
            while (padded < value.Length && padded - run < 2 && value[padded] == '=') padded++;

            // Try the longest candidate first: blobs are multiples of 4 characters.
            for (int length = padded - padded % 4; length >= 12; length -= 4)
            {
                if (length > run && length != padded) continue; // padding must close the blob
                byte[] bytes;
                try { bytes = Convert.FromBase64String(value.Substring(0, length)); }
                catch (FormatException) { continue; }
                if (EndsWithLengthPrefixedString(bytes)) return value.Substring(length);
            }
            return value;
        }

        private static bool IsBase64Char(char c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/';

        // True if some int32 in the blob equals the number of bytes that follow it (and is a
        // plausible tooltip length), i.e. the blob ends exactly at the end of its last string.
        private static bool EndsWithLengthPrefixedString(byte[] bytes)
        {
            if (bytes.Length < 12 || BitConverter.ToInt32(bytes, 0) is < 1 or > 64) return false;
            for (int p = bytes.Length - 5; p >= 8; p--)
            {
                int remaining = bytes.Length - p - 4;
                if (remaining > 512) break;
                if (BitConverter.ToInt32(bytes, p) == remaining && remaining >= 1) return true;
            }
            return false;
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
