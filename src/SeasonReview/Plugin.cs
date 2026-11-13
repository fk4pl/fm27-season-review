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
        internal static ConfigEntry<bool> ProbeDump;

        public override void Load()
        {
            L = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off.");
            HotkeyText = Config.Bind("General", "Hotkey", "Ctrl+F8",
                "Shortcut that reports on the season so far. Format: [Ctrl+][Shift+][Alt+]<F1-F24, A-Z or 0-9>.");
            ProbeDump = Config.Bind("Developer", "ProbeDump", true,
                "On hotkey, also write discovery dumps (names only, no hidden values) to BepInEx/SeasonReview/.");

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
        // Stage 1: the current date and the manager's club, plus a few values that prove deeper
        // property chains can be read without the matching screen being open.
        private const string Team = "GameCurrentManagerClubTeam";
        private static readonly string[] HelloPaths =
        {
            "CurrentGameDate",
            Team + ".Name",
            Team + ".Club.Name",
            Team + ".Division.Name",
            Team + ".LeaguePositionString",
            Team + ".TeamConfidence.BoardConfidenceOverallString",
            Team + ".TeamConfidence.SupporterConfidenceOverallString",
            Team + ".Club.OverallBalance",
            Team + ".Club.TotalIncomeThisSeason",
        };

        private readonly GameReader _reader = new GameReader();
        private Hotkey _hotkey;
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
                }

                _reader.Tick();

                if (_hotkey.PressedThisFrame())
                {
                    if (_reader.Busy) { Plugin.L.LogInfo("Still reading game data, please wait."); return; }
                    var bindings = Probe.Run();
                    if (bindings != null) _reader.Start(bindings, this, HelloPaths, LogHello);
                }
            }
            catch (Exception e)
            {
                // Never let an exception escape into the game's update loop.
                Plugin.L.LogWarning($"Hotkey handling failed: {e.Message}");
            }
        }

        private static void LogHello(System.Collections.Generic.IReadOnlyDictionary<string, string> values)
        {
            values.TryGetValue("CurrentGameDate", out var rawDate);
            values.TryGetValue(Team + ".Name", out var club);
            Plugin.L.LogInfo(GameText.TryParseGameDate(rawDate, out var date)
                ? $"In-game date: {date:yyyy-MM-dd}"
                : "In-game date: data unavailable");
            Plugin.L.LogInfo($"Club: {(string.IsNullOrEmpty(club) ? "data unavailable" : GameText.StripLink(club))}");
            foreach (var pair in values)
                Plugin.L.LogInfo($"  {pair.Key} = {(pair.Value == null ? "(no answer)" : GameText.StripLink(pair.Value))}");
        }
    }
}
