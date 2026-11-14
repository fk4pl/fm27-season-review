using System;

namespace SeasonReview
{
    /// <summary>
    /// The panel's own read of the season: header first (which season is it?), then the full
    /// read, then the same data model and report document the HTML report is built from, turned
    /// into a <see cref="PanelModel"/>. Nothing here writes anything.
    ///
    /// The read takes many frames. Each step is a continuation the reader calls later, on the
    /// game's main thread, as one synchronous call. Every continuation of this class (and the
    /// first call) runs inside its own <see cref="DiskWrites.Forbid"/> scope, opened on entry
    /// and closed before the call returns. So the ban on disk writes
    ///  - covers every step of the panel's read, including the later ones, and whatever the
    ///    panel does with the result (the <c>show</c> callback runs inside the scope);
    ///  - is never held between frames, so it cannot be active while the reader hands a result
    ///    to a report: a report written while the panel is open or loading is unaffected.
    /// </summary>
    internal sealed class PanelRead
    {
        internal const string Reason = "in-game panel";

        private readonly Func<Spec, Action<GameRecord>, bool> _startRead;
        private readonly Func<Strings> _strings;
        private readonly Func<double, string> _money;
        private readonly string _version;
        private readonly Action<PanelModel> _show;
        private readonly Action<string> _warn;
        private Strings _loaded;
        private bool _abandoned;

        /// <param name="startRead">Starts one read on the shared reader; false if it cannot start (no career loaded).</param>
        /// <param name="strings">Loads the report text in the configured language.</param>
        /// <param name="show">Receives the finished model, or a message model when there is no report.</param>
        public PanelRead(Func<Spec, Action<GameRecord>, bool> startRead, Func<Strings> strings, Func<double, string> money,
            string version, Action<PanelModel> show, Action<string> warn)
        {
            _startRead = startRead;
            _strings = strings;
            _money = money;
            _version = version;
            _show = show;
            _warn = warn ?? (_ => { });
        }

        /// <summary>True from <see cref="Start"/> until the last continuation has run: the reader is working for the panel.</summary>
        public bool InFlight { get; private set; }

        /// <summary>The panel was closed: finish quietly (no full read after the header, nothing shown).</summary>
        public void Abandon() => _abandoned = true;

        /// <summary>Begins with the header read. Call only when the reader is idle.</summary>
        public void Start() => ReadOnly("start", () =>
        {
            if (_abandoned) return;
            _loaded = _strings();
            if (_startRead(SeasonSpec.BuildHeader(), OnHeader)) InFlight = true;
            else _show(PanelModelBuilder.Message(_loaded, "panel.nocareer"));
        });

        private void OnHeader(GameRecord header) => ReadOnly("header", () =>
        {
            InFlight = false;
            if (_abandoned) return;

            var season = SeasonDataBuilder.Text(header.Get(SeasonSpec.Division + ".SeasonName"));
            if (season == null)
            {
                _show(PanelModelBuilder.Message(_loaded, "panel.noclub"));
                return;
            }
            if (_startRead(SeasonSpec.Build(SeasonYear(season)), OnSeason)) InFlight = true;
            else _show(PanelModelBuilder.Message(_loaded, "panel.failed"));
        });

        private void OnSeason(GameRecord raw) => ReadOnly("season", () =>
        {
            InFlight = false;
            if (_abandoned) return;

            var data = SeasonDataBuilder.Build(raw, _warn);
            var document = ReportBuilder.Build(data, _loaded, _money, _version);
            _show(PanelModelBuilder.Build(document, _loaded));
        });

        /// <summary>
        /// Runs one step with disk writes forbidden. A failing step never throws into the reader:
        /// the panel gets the "could not be read" message instead.
        /// </summary>
        private void ReadOnly(string step, Action body)
        {
            using (DiskWrites.Forbid(Reason))
            {
                try { body(); }
                catch (Exception e)
                {
                    InFlight = false;
                    _warn($"Panel read failed ({step}): {e.Message}");
                    if (_abandoned) return;
                    try { _show(PanelModelBuilder.Message(_loaded ?? Strings.Parse("en", ""), "panel.failed")); }
                    catch (Exception inner) { _warn("Panel could not show the failure: " + inner.Message); }
                }
            }
        }

        /// <summary>Calendar year a season starts in: "2026/27" and "2026" both give 2026 (0 if unrecognised).</summary>
        private static int SeasonYear(string season) =>
            season.Length >= 4 && int.TryParse(season.Substring(0, 4), out var year) ? year : 0;
    }

    /// <summary>
    /// Who may use the single game reader next. Reports (Ctrl+F8 and the automatic season-end
    /// report) always go before the panel:
    ///  - a panel request only waits here; it starts on a frame where the reader is idle and the
    ///    report sources have already had their turn on that same frame;
    ///  - a report key pressed while the reader is busy reading for the panel is remembered and
    ///    served first when the reader is free, before any waiting panel request.
    /// Pure bookkeeping: the controller asks it once per frame.
    /// </summary>
    internal sealed class ReaderTurns
    {
        private bool _panelWaiting, _reportWaiting;

        public bool PanelWaiting => _panelWaiting;
        public bool ReportWaiting => _reportWaiting;

        /// <summary>The panel wants a read. It starts when <see cref="PanelTurn"/> allows it.</summary>
        public void RequestPanel() => _panelWaiting = true;

        /// <summary>The panel no longer wants a read that has not started yet.</summary>
        public void CancelPanel() => _panelWaiting = false;

        /// <summary>
        /// Call on a frame where the reader is busy. While it is busy for the panel, the report
        /// key is still watched so the press is not lost. (While it is busy for a report the key
        /// is not looked at, exactly as before the panel existed.)
        /// </summary>
        public void WhileBusy(bool busyForPanel, Func<bool> reportKeyPressed)
        {
            if (busyForPanel && reportKeyPressed()) _reportWaiting = true;
        }

        /// <summary>Call first on a frame where the reader is idle: true if a report should start now.</summary>
        public bool ReportTurn(Func<bool> reportKeyPressed)
        {
            bool pressed = reportKeyPressed();
            if (!pressed && !_reportWaiting) return false;
            _reportWaiting = false;
            return true;
        }

        /// <summary>
        /// Call last on an idle frame, after every report source had its turn: true if the waiting
        /// panel read should start now. False when a report took the reader on this frame.
        /// </summary>
        public bool PanelTurn(bool readerBusy)
        {
            if (readerBusy || !_panelWaiting) return false;
            _panelWaiting = false;
            return true;
        }
    }
}
