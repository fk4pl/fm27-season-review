using System;

namespace SeasonReview
{
    /// <summary>
    /// Guards against hidden game values (CA/PA, hidden attributes, debug data) ever reaching
    /// a log or a file. Anything whose name matches is skipped entirely, name included.
    /// </summary>
    internal static class HiddenValues
    {
        private static readonly string[] Fragments =
        {
            "ability", "potential", "hidden", "debug", "proneness", "personality",
            "adaptability", "ambition", "controversy", "loyalty", "pressure", "professional",
            "sportsmanship", "temperament", "consistency", "dirtiness", "importantmatches",
            "versatility", "susceptib", "injuryrisk", "riskfactor",
        };

        public static bool IsHidden(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var fragment in Fragments)
                if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return name.Equals("ca", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("pa", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True if any dot-separated segment of a binding path is a hidden value.</summary>
        public static bool IsHiddenPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var segment in path.Split('.'))
                if (IsHidden(segment)) return true;
            return false;
        }
    }
}
