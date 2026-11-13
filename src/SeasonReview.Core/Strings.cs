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

        /// <summary>A number with two decimals and the language's decimal separator.</summary>
        public string Decimal(double value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", Get("format.decimal"));
    }
}
