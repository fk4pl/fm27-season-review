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

                if (_hotkey.PressedThisFrame())
                    Probe.Run();
            }
            catch (Exception e)
            {
                // Never let an exception escape into the game's update loop.
                Plugin.L.LogWarning($"Hotkey handling failed: {e.Message}");
            }
        }
    }
}
