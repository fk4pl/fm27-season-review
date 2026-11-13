using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace SeasonReview
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "io.github.seasonreview.fm27";
        public const string Name = "Season Review";
        public const string Version = "0.1.0";

        internal static ManualLogSource L;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> AutoReport;
        internal static ConfigEntry<string> Language;
        internal static ConfigEntry<string> OutputFolder;
        internal static ConfigEntry<string> HotkeyText;
        internal static ConfigEntry<bool> ProbeHotkey;
        internal static ConfigEntry<bool> RawDump;

        public override void Load()
        {
            L = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off.");
            AutoReport = Config.Bind("General", "AutoReport", true,
                "Write the season review automatically after your club's last league match of the season (once per season).");
            Language = Config.Bind("General", "Language", "en",
                "Report language: en (English) or tr (Türkçe). Other codes work if a matching lang/<code>.txt file exists in the plugin folder.");
            OutputFolder = Config.Bind("General", "OutputFolder", "",
                "Where reports are saved. Empty = Documents/Sports Interactive/Football Manager 27/SeasonReviews.");
            HotkeyText = Config.Bind("General", "Hotkey", "Ctrl+F8",
                "Writes a report for the season so far. Format: [Ctrl+][Shift+][Alt+]<F1-F24, A-Z or 0-9>.");
            ProbeHotkey = Config.Bind("Developer", "ProbeHotkey", false,
                "Shift+<Hotkey> writes discovery dumps of the current screen to BepInEx/SeasonReview/ (names only, hidden values skipped). For adapting the mod after game updates.");
            RawDump = Config.Bind("Developer", "RawDump", false,
                "Also write everything that was read, unformatted, to raw-data.txt next to the reports.");

            if (!Enabled.Value)
            {
                L.LogInfo("Season Review is disabled in the config.");
                return;
            }

            AddComponent<SeasonReviewBehaviour>();
            L.LogInfo($"Season Review {Version} loaded (reports: {ReportFolder()})");
        }

        internal static string ReportFolder()
        {
            if (!string.IsNullOrWhiteSpace(OutputFolder.Value)) return OutputFolder.Value.Trim();
            // Under Proton this resolves to C:\users\steamuser\Documents inside the game's prefix.
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, "Sports Interactive", "Football Manager 27", "SeasonReviews");
        }

        internal static string StatePath() => Path.Combine(Paths.ConfigPath, Guid + ".reported.txt");

        /// <summary>
        /// Report text in the configured language. A lang/&lt;code&gt;.txt file next to the plugin
        /// wins over the built-in copy, so translations can be added without rebuilding.
        /// </summary>
        internal static Strings LoadStrings()
        {
            var english = Strings.Parse("en", ReadLanguage("en") ?? "");
            var code = (Language.Value ?? "en").Trim().ToLowerInvariant();
            if (code == "en" || code.Length == 0) return english;
            var text = ReadLanguage(code);
            if (text != null) return Strings.Parse(code, text, english);
            L.LogWarning($"Language '{code}' not found; using English.");
            return english;
        }

        private static string ReadLanguage(string code)
        {
            try
            {
                var folder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                if (!string.IsNullOrEmpty(folder))
                {
                    var file = Path.Combine(folder, "lang", code + ".txt");
                    if (File.Exists(file)) return File.ReadAllText(file);
                }
            }
            catch (Exception e) { L.LogWarning($"Could not read language file '{code}': {e.Message}"); }

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SeasonReview.lang.{code}.txt");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
