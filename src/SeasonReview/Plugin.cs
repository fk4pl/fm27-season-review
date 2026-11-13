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
                    if (bindings != null && _reader.Start(bindings, this, SeasonSpec.Build(), OnData))
                        Plugin.L.LogInfo("Reading season data from the game...");
                }
            }
            catch (Exception e)
            {
                // Never let an exception escape into the game's update loop.
                Plugin.L.LogWarning($"Hotkey handling failed: {e.Message}");
            }
        }

        private static void OnData(Record data)
        {
            var rawDate = data.Get("CurrentGameDate");
            var club = data.Get(SeasonSpec.Team + ".Name");
            Plugin.L.LogInfo(GameText.TryParseGameDate(rawDate, out var date)
                ? $"In-game date: {date:yyyy-MM-dd}"
                : "In-game date: data unavailable");
            Plugin.L.LogInfo($"Club: {(string.IsNullOrEmpty(club) ? "data unavailable" : GameText.StripLink(club))}");

            int answered = 0;
            foreach (var value in data.Values.Values) if (value != null) answered++;
            Plugin.L.LogInfo($"Top-level values answered: {answered}/{data.Values.Count}");
            foreach (var note in data.ListNotes) Plugin.L.LogInfo($"  list {note.Key}: {note.Value}");

            var folder = string.IsNullOrWhiteSpace(Plugin.OutputFolder.Value) ? DraftWriter.DefaultFolder() : Plugin.OutputFolder.Value;
            try { Plugin.L.LogInfo("Draft written to " + DraftWriter.Write(data, folder)); }
            catch (Exception e) { Plugin.L.LogWarning($"Could not write the draft: {e.Message}"); }
        }
    }
}
