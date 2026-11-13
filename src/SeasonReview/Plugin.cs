using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

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
        internal static ConfigEntry<string> HotkeyText;
        internal static ConfigEntry<string> OutputFolder;
        internal static ConfigEntry<bool> ProbeDump;
        internal static ConfigEntry<bool> ProbeHotkey;
        internal static ConfigEntry<bool> RawDump;

        public override void Load()
        {
            L = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off.");
            HotkeyText = Config.Bind("General", "Hotkey", "Ctrl+F8",
                "Shortcut that reports on the season so far. Format: [Ctrl+][Shift+][Alt+]<F1-F24, A-Z or 0-9>.");
            OutputFolder = Config.Bind("General", "OutputFolder", "",
                "Where reports are saved. Empty = Documents/Sports Interactive/Football Manager 27/SeasonReviews.");
            ProbeDump = Config.Bind("Developer", "ProbeDump", false,
                "On hotkey, also write discovery dumps (names only, no hidden values) to BepInEx/SeasonReview/.");

            ProbeHotkey = Config.Bind("Developer", "ProbeHotkey", true,
                "Shift+<Hotkey> writes discovery dumps of the current screen to BepInEx/SeasonReview/.");

            RawDump = Config.Bind("Developer", "RawDump", true,
                "Also write everything that was read, unformatted, to draft-raw.txt next to the report.");

            if (!Enabled.Value)
            {
                L.LogInfo("Season Review is disabled in the config.");
                return;
            }

            AddComponent<SeasonReviewBehaviour>();
            L.LogInfo("Season Review loaded");
        }
    }

    /// <summary>Polls the hotkey once per frame on Unity's main thread.</summary>
    public class SeasonReviewBehaviour : MonoBehaviour
    {
        private readonly GameReader _reader = new GameReader();
        private Hotkey _hotkey, _probeHotkey;
        private SI.Bindable.Bindings _bindings;
        private string _parsedFrom;

        public SeasonReviewBehaviour(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            try
            {
                var text = Plugin.HotkeyText.Value;
                if (text != _parsedFrom)
                {
                    _parsedFrom = text;
                    if (!Hotkey.TryParse(text, out _hotkey))
                    {
                        Plugin.L.LogWarning($"Invalid hotkey '{text}', falling back to Ctrl+F8.");
                        Hotkey.TryParse("Ctrl+F8", out _hotkey);
                    }
                    Plugin.L.LogInfo($"Hotkey: {_hotkey}");
                    // Developer probe: the same shortcut with Shift held.
                    _probeHotkey = null;
                    if (Plugin.ProbeHotkey.Value && !_hotkey.Shift && Hotkey.TryParse("Shift+" + _hotkey, out var probe))
                        _probeHotkey = probe;
                }

                _reader.Tick();

                if (_probeHotkey != null && _probeHotkey.PressedThisFrame())
                {
                    Probe.Run(true);
                    return;
                }

                if (_hotkey.PressedThisFrame())
                {
                    if (_reader.Busy) { Plugin.L.LogInfo("Still reading game data, please wait."); return; }
                    var bindings = Probe.Run();
                    if (bindings != null && _reader.Start(bindings, this, SeasonSpec.BuildHeader(), OnHeader))
                    {
                        _bindings = bindings;
                        Plugin.L.LogInfo("Reading season data from the game...");
                    }
                }
            }
            catch (Exception e)
            {
                // Never let an exception escape into the game's update loop.
                Plugin.L.LogWarning($"Hotkey handling failed: {e.Message}");
            }
        }

        // First read: which season is it? Then the full read with that season's year.
        private void OnHeader(GameRecord header)
        {
            int year = 0;
            var season = SeasonDataBuilder.Text(header.Get(SeasonSpec.Division + ".SeasonName"));
            if (season != null && season.Length >= 4) int.TryParse(season.Substring(0, 4), out year);
            if (year == 0 && GameText.TryParseGameDate(header.Get("CurrentGameDate"), out var date))
                year = date.Month >= 7 ? date.Year : date.Year - 1;
            if (year == 0)
            {
                Plugin.L.LogWarning("Season could not be determined; transfers will be unavailable.");
            }

            if (_bindings == null || !_reader.Start(_bindings, this, SeasonSpec.Build(year), OnData))
                Plugin.L.LogWarning("Game data unavailable: could not start the season read.");
        }

        private void OnData(GameRecord raw)
        {
            _bindings = null;
            var data = SeasonDataBuilder.Build(raw, message => Plugin.L.LogWarning(message));

            string report = DraftReport.Build(data, Money);
            foreach (var line in report.Split('\n'))
                if (line.Trim().Length > 0) Plugin.L.LogInfo(line.TrimEnd());

            var folder = string.IsNullOrWhiteSpace(Plugin.OutputFolder.Value) ? DraftWriter.DefaultFolder() : Plugin.OutputFolder.Value;
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                var path = System.IO.Path.Combine(folder, "draft.md");
                System.IO.File.WriteAllText(path, report, new System.Text.UTF8Encoding(false));
                Plugin.L.LogInfo("Draft report written to " + path);
                if (Plugin.RawDump.Value) Plugin.L.LogInfo("Raw data written to " + DraftWriter.Write(raw, folder));
            }
            catch (Exception e) { Plugin.L.LogWarning($"Could not write the draft: {e.Message}"); }
        }

        /// <summary>Formats a raw amount (pounds) with the game's own formatter, which converts
        /// to the currency chosen in the game's preferences.</summary>
        private static string Money(double pounds)
        {
            try { return FM.UI.ChartCurrencyVisualFunctionLibrary.TranslateCurrencyValue(pounds); }
            catch (Exception) { return "£" + pounds.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
        }
    }
}
