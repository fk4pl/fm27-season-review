using System;
using UnityEngine;

namespace SeasonReview
{
    /// <summary>
    /// Runs on Unity's main thread. Watches for the end of the league season, handles the
    /// hotkey, and turns game data into reports. Only reads from the game.
    /// </summary>
    public class SeasonReviewBehaviour : MonoBehaviour
    {
        private const double PeekSeconds = 3;            // how often to look at the in-game date
        private const double FallbackCheckSeconds = 120; // if the date cannot be peeked

        private enum Job { None, HotkeyHeader, HotkeyReport, SeasonCheck, SeasonReport }

        private readonly GameReader _reader = new GameReader();
        private ReportedSeasons _reported;
        private Hotkey _hotkey, _probeHotkey;
        private string _parsedFrom;
        private Job _job;
        private SI.Bindable.Bindings _bindings;
        private string _seasonKey;
        private string _lastCheckedDate;
        private DateTime _nextPeek, _nextFallbackCheck;

        public SeasonReviewBehaviour(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            try
            {
                ParseHotkeys();
                _reader.Tick();
                if (_reader.Busy) return;

                if (_probeHotkey != null && _probeHotkey.PressedThisFrame())
                {
                    Probe.Run(true);
                    return;
                }
                if (_hotkey.PressedThisFrame())
                {
                    StartHotkeyReport();
                    return;
                }
                if (Plugin.AutoReport.Value) WatchSeason();
            }
            catch (Exception e)
            {
                // Never let an exception escape into the game's update loop.
                Plugin.L.LogWarning($"Season Review update failed: {e.Message}");
                _job = Job.None;
            }
        }

        private void ParseHotkeys()
        {
            var text = Plugin.HotkeyText.Value;
            if (text == _parsedFrom) return;
            _parsedFrom = text;
            if (!Hotkey.TryParse(text, out _hotkey))
            {
                Plugin.L.LogWarning($"Invalid hotkey '{text}', falling back to Ctrl+F8.");
                Hotkey.TryParse("Ctrl+F8", out _hotkey);
            }
            _probeHotkey = null;
            if (Plugin.ProbeHotkey.Value && !_hotkey.Shift && Hotkey.TryParse("Shift+" + _hotkey, out var probe))
                _probeHotkey = probe;
            Plugin.L.LogInfo($"Hotkey: {_hotkey}" + (_probeHotkey != null ? $", discovery probe: {_probeHotkey}" : ""));
        }

        // ── Season-end watch ───────────────────────────────────────────────

        /// <summary>
        /// Once per in-game day, while the game is idle, checks the fixture list. When the
        /// club's last league match has been played and this season has no report yet, the
        /// full report is written.
        /// </summary>
        private void WatchSeason()
        {
            var now = DateTime.UtcNow;
            if (now < _nextPeek) return;
            _nextPeek = now.AddSeconds(PeekSeconds);

            var bindings = GameReader.TryGetBindings();
            if (bindings == null) { _lastCheckedDate = null; return; }

            if (GameReader.Peek(bindings, "GameProcessing") == "True"
                || GameReader.Peek(bindings, "IsDuringMatch") == "True"
                || GameReader.Peek(bindings, "IsViewingMatch") == "True")
                return;

            var date = GameReader.Peek(bindings, "CurrentGameDate");
            if (date == null)
            {
                if (now < _nextFallbackCheck) return;
                _nextFallbackCheck = now.AddSeconds(FallbackCheckSeconds);
            }
            else if (date == _lastCheckedDate) return;

            if (_reader.Start(bindings, this, SeasonEnd.BuildCheck(), OnSeasonCheck))
            {
                _lastCheckedDate = date;
                _bindings = bindings;
                _job = Job.SeasonCheck;
            }
        }

        private void OnSeasonCheck(GameRecord record)
        {
            _job = Job.None;
            var status = SeasonEnd.Evaluate(record);
            // Quiet during the season; visible as the end approaches.
            if (status.Ended || (status.LeagueRemaining > 0 && status.LeagueRemaining <= 3) || status.LeaguePlayed == 0)
                Plugin.L.LogInfo($"Season check: {status.Club ?? "-"} {status.Season ?? "-"}: {status.Reason}");
            if (!status.Ended) return;

            _reported ??= new ReportedSeasons(Plugin.StatePath());
            if (_reported.Contains(status.Key)) return;

            Plugin.L.LogInfo($"{status.Club}: the {status.Division} {status.Season} season is over. Writing the season review...");
            _seasonKey = status.Key;
            StartFullRead(status.Season, Job.SeasonReport);
        }

        // ── Hotkey ─────────────────────────────────────────────────────────

        private void StartHotkeyReport()
        {
            var bindings = GameReader.TryGetBindings();
            if (bindings == null)
            {
                Plugin.L.LogWarning("Game data unavailable: load a career first.");
                return;
            }
            if (_reader.Start(bindings, this, SeasonSpec.BuildHeader(), OnHotkeyHeader))
            {
                _bindings = bindings;
                _job = Job.HotkeyHeader;
                Plugin.L.LogInfo("Reading season data from the game...");
            }
        }

        // First read: which season is it? Then the full read with that season's year.
        private void OnHotkeyHeader(GameRecord header)
        {
            _job = Job.None;
            var season = SeasonDataBuilder.Text(header.Get(SeasonSpec.Division + ".SeasonName"));
            if (season == null)
            {
                Plugin.L.LogWarning("Game data unavailable: no club or season (are you employed by a club?).");
                return;
            }
            _seasonKey = null;
            StartFullRead(season, Job.HotkeyReport);
        }

        // ── Report ─────────────────────────────────────────────────────────

        private void StartFullRead(string season, Job job)
        {
            int year = SeasonYear(season);
            if (year == 0) Plugin.L.LogWarning($"Unrecognised season name '{season}'; transfers will be unavailable.");
            var bindings = _bindings ?? GameReader.TryGetBindings();
            if (bindings != null && _reader.Start(bindings, this, SeasonSpec.Build(year), raw => OnFullRead(raw, job)))
                _job = job;
            else
                Plugin.L.LogWarning("Game data unavailable: could not start reading the season.");
        }

        private void OnFullRead(GameRecord raw, Job job)
        {
            _job = Job.None;
            _bindings = null;

            var data = SeasonDataBuilder.Build(raw, message => Plugin.L.LogWarning(message));
            if (job == Job.SeasonReport && !data.SeasonComplete)
            {
                Plugin.L.LogWarning("The season check and the full read disagree on whether the season is over; trying again tomorrow.");
                return;
            }

            try
            {
                var strings = Plugin.LoadStrings();
                var document = ReportBuilder.Build(data, strings, Money, Plugin.Version);
                var name = ReportFiles.BaseName(data.Club, data.Season, data.SeasonComplete ? null : data.Date);
                var folder = Plugin.ReportFolder();
                var path = ReportFiles.Write(folder, name, HtmlRenderer.Render(document), MarkdownRenderer.Render(document));
                Plugin.L.LogInfo("Season review written: " + path);
                if (data.Unavailable.Count > 0)
                    Plugin.L.LogWarning("Sections without data: " + string.Join(", ", data.Unavailable));
                if (Plugin.RawDump.Value) Plugin.L.LogInfo("Raw data written: " + RawDump.Write(raw, folder));

                if (data.SeasonComplete)
                {
                    _reported ??= new ReportedSeasons(Plugin.StatePath());
                    _reported.Add(_seasonKey ?? SeasonEnd.KeyOf(raw));
                }
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning($"Could not write the season review: {e.Message}");
            }
        }

        /// <summary>Calendar year a season starts in: "2026/27" and "2026" both give 2026.</summary>
        private static int SeasonYear(string season) =>
            season != null && season.Length >= 4 && int.TryParse(season.Substring(0, 4), out var year) ? year : 0;

        /// <summary>Formats a raw amount (pounds) with the game's own formatter, which converts
        /// to the currency chosen in the game's preferences.</summary>
        private static string Money(double pounds)
        {
            try { return FM.UI.ChartCurrencyVisualFunctionLibrary.TranslateCurrencyValue(pounds); }
            catch (Exception) { return "£" + pounds.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
        }
    }
}
