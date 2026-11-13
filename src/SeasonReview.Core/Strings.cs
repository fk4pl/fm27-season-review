using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SeasonReview
{
    /// <summary>
    /// Report text for one language, loaded from a plain "key = value" file (see lang/en.txt).
    /// Placeholders are named, e.g. "{club}". A key missing from a translation falls back to
    /// English, so a partial translation still produces a complete report.
    /// </summary>
    internal sealed class Strings
    {
        private readonly Dictionary<string, string> _values;
        private readonly Strings _fallback;
        private readonly string[] _months;

        public string Code { get; }

        private Strings(string code, Dictionary<string, string> values, Strings fallback)
        {
            Code = code;
            _values = values;
            _fallback = fallback;
            _months = Get("format.months").Split(',');
        }

        public static Strings Parse(string code, string text, Strings fallback = null)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }
            return new Strings(code, values, fallback);
        }

        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>Text for a key; "[key]" when no language has it, so a gap is visible but harmless.</summary>
        public string Get(string key)
        {
            if (_values.TryGetValue(key, out var value)) return value;
            return _fallback != null ? _fallback.Get(key) : "[" + key + "]";
        }

        /// <summary>Text for a key with named placeholders filled in.</summary>
        public string Format(string key, params (string Name, object Value)[] args)
        {
            var text = new StringBuilder(Get(key));
            foreach (var (name, value) in args)
                text.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.InvariantCulture));
            return text.ToString();
        }

        /// <summary>
        /// A number with its unit in the right singular/plural form, from the
        /// "plural.&lt;unit&gt;.one" and "plural.&lt;unit&gt;.other" keys (e.g. "1 match", "3 matches").
        /// Languages without a separate singular can give both keys the same text.
        /// </summary>
        public string Count(int n, string unit)
        {
            string key = "plural." + unit + (n == 1 ? ".one" : ".other");
            if (n == 1 && !Defines(key)) key = "plural." + unit + ".other";
            return Get(key).Replace("{n}", n.ToString(CultureInfo.InvariantCulture));
        }

        private bool Defines(string key) => _values.ContainsKey(key) || (_fallback != null && _fallback.Defines(key));

        /// <summary>1st/2nd/3rd in English; "{n}." style patterns (e.g. "4.") elsewhere.</summary>
        public string Ordinal(int n)
        {
            var pattern = Get("format.ordinal");
            if (pattern != "english") return pattern.Replace("{n}", n.ToString(CultureInfo.InvariantCulture));
            int tens = n % 100, ones = n % 10;
            string suffix = tens >= 11 && tens <= 13 ? "th" : ones == 1 ? "st" : ones == 2 ? "nd" : ones == 3 ? "rd" : "th";
            return n + suffix;
        }

        /// <summary>Formats a date without relying on the system's culture data (which may be
        /// missing under Wine/Proton).</summary>
        public string Date(DateTime? date)
        {
            if (date == null) return Get("common.unknown");
            var d = date.Value;
            string month = d.Month <= _months.Length ? _months[d.Month - 1].Trim() : d.Month.ToString(CultureInfo.InvariantCulture);
            return Get("format.date")
                .Replace("{d}", d.Day.ToString(CultureInfo.InvariantCulture))
                .Replace("{month}", month)
                .Replace("{yyyy}", d.Year.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Translation of a value the game gives in its own language (e.g. a financial status
        /// such as "Okay"), looked up as "&lt;prefix&gt;.&lt;value in lower case, no spaces&gt;". A value
        /// with no translation is returned unchanged.
        /// </summary>
        public string GameValue(string prefix, string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var key = prefix + "." + value.Replace(" ", "").ToLowerInvariant();
            return Defines(key) ? Get(key) : value;
        }

        private readonly Dictionary<string, (Dictionary<string, string> Exact, List<(System.Text.RegularExpressions.Regex Pattern, string Translation)> Patterns)> _dictionaries = new();

        /// <summary>
        /// Translates text the game gives in its own language, using "&lt;category&gt;: &lt;game text&gt; =
        /// &lt;translation&gt;" lines of this language's file (e.g. "injury: sprained ankle = ...").
        /// Matching ignores case and a leading "a", "an" or "the". A "{name}" in the game text
        /// matches any words and can be reused in the translation, e.g.
        /// "objective: Challenge for the {competition} title = {competition} şampiyonluğu için mücadele et".
        /// Text with no match is returned unchanged. Only the language's own file is used, so
        /// the English report is never affected.
        /// </summary>
        /// <param name="known">Placeholder values known from the game (e.g. the objective's
        /// competition): a pattern only matches if its placeholder equals the known value, so
        /// "Win the {competition}" cannot match "Win the board's respect". A known value of
        /// null means the placeholder cannot match at all.</param>
        public string GameText(string category, string text, params (string Name, string Value)[] known)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var (exact, patterns) = Dictionary(category);
            if (exact.TryGetValue(Normalise(text), out var translation)) return translation;
            foreach (var (pattern, template) in patterns)
            {
                var match = pattern.Match(text.Trim());
                if (!match.Success) continue;
                bool consistent = true;
                foreach (var (name, value) in known)
                {
                    var group = match.Groups[name];
                    if (group.Success && (value == null || !string.Equals(group.Value.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase)))
                        consistent = false;
                }
                if (!consistent) continue;
                var result = template;
                foreach (var name in pattern.GetGroupNames())
                    if (!int.TryParse(name, out _)) result = result.Replace("{" + name + "}", match.Groups[name].Value);
                return result;
            }
            return text;
        }

        private (Dictionary<string, string>, List<(System.Text.RegularExpressions.Regex, string)>) Dictionary(string category)
        {
            if (_dictionaries.TryGetValue(category, out var cached)) return cached;
            var exact = new Dictionary<string, string>(StringComparer.Ordinal);
            var patterns = new List<(System.Text.RegularExpressions.Regex, string, int)>();
            string prefix = category + ":";
            foreach (var pair in _values)
            {
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var source = pair.Key.Substring(prefix.Length).Trim();
                if (source.IndexOf('{') < 0) { exact[Normalise(source)] = pair.Value; continue; }

                // "{name}" becomes a named group; everything else must match literally.
                var regex = new StringBuilder("^(?:(?:a|an|the)\\s+)?");
                int literal = 0;
                foreach (System.Text.RegularExpressions.Match part in
                         System.Text.RegularExpressions.Regex.Matches(StripArticle(source), @"\{([a-z]+)\}|[^{]+|\{"))
                {
                    if (part.Groups[1].Success) regex.Append("(?<").Append(part.Groups[1].Value).Append(">.+?)");
                    else { regex.Append(System.Text.RegularExpressions.Regex.Escape(part.Value)); literal += part.Value.Length; }
                }
                regex.Append('$');
                patterns.Add((new System.Text.RegularExpressions.Regex(regex.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant), pair.Value, literal));
            }
            // Most specific first, so "Challenge for the {c} title" wins over "Challenge for the {c}".
            patterns.Sort((a, b) => b.Item3.CompareTo(a.Item3));
            var result = (exact, patterns.ConvertAll(p => (p.Item1, p.Item2)));
            _dictionaries[category] = result;
            return result;
        }

        private static string StripArticle(string text)
        {
            var t = text.Trim();
            foreach (var article in new[] { "a ", "an ", "the " })
                if (t.StartsWith(article, StringComparison.OrdinalIgnoreCase)) return t.Substring(article.Length).TrimStart();
            return t;
        }

        private static string Normalise(string text) =>
            string.Join(" ", StripArticle(text).Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        private static readonly System.Text.RegularExpressions.Regex DecimalPoint =
            new System.Text.RegularExpressions.Regex(@"(?<=\d)\.(?=\d)");

        /// <summary>
        /// Uses this language's decimal separator in an amount formatted by the game (e.g.
        /// "€6.25M" becomes "€6,25M" in Turkish). Text that already contains a comma is left
        /// alone, because the comma could be a thousands separator.
        /// </summary>
        public string LocalizeDecimal(string text)
        {
            var separator = Get("format.decimal");
            if (string.IsNullOrEmpty(text) || separator == "." || text.IndexOf(',') >= 0) return text;
            return DecimalPoint.Replace(text, separator);
        }

        /// <summary>A number with two decimals and the language's decimal separator.</summary>
        public string Decimal(double value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", Get("format.decimal"));
    }
}
