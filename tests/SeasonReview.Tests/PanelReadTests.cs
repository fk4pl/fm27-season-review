using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeasonReview;
using Xunit;

namespace SeasonReview.Tests
{
    /// <summary>
    /// Stands in for the game reader: one read at a time, answered some frames later with one
    /// synchronous call, after the reader is free again (as GameReader.Finish does).
    /// </summary>
    internal sealed class FakeReader
    {
        private Action<GameRecord> _done;
        private int _framesLeft;

        public int FramesPerRead = 5;
        public bool CanStart = true;
        public Func<GameRecord> Answer = () => TestData.Season();
        public readonly List<string> Started = new();
        /// <summary>Runs inside Start, i.e. inside whichever step asked for the read.</summary>
        public Action<int> OnStart;

        public bool Busy => _done != null;

        public bool Start(string who, Action<GameRecord> done)
        {
            if (Busy || !CanStart) return false;
            Started.Add(who);
            OnStart?.Invoke(Started.Count);
            _done = done;
            _framesLeft = FramesPerRead;
            return true;
        }

        public void Tick()
        {
            if (!Busy || --_framesLeft > 0) return;
            var done = _done;
            _done = null;
            done(Answer());
        }
    }

    public class PanelReadTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "SeasonReviewTests-" + Guid.NewGuid().ToString("N"));
        private readonly FakeReader _reader = new FakeReader();
        private readonly List<PanelModel> _shown = new();
        private readonly List<string> _warnings = new();

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private PanelRead Panel(Func<double, string> money = null, Action<PanelModel> show = null, Func<Strings> strings = null) =>
            new PanelRead((spec, done) => _reader.Start("panel", done), strings ?? TestData.English, money ?? TestData.Money,
                "test", show ?? _shown.Add, _warnings.Add);

        private void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _reader.Tick();
                Assert.False(DiskWrites.Forbidden, "the ban on disk writes must never last beyond one panel step");
            }
        }

        /// <summary>What the report path does when its read is finished: files, then the reported-seasons list.</summary>
        private string WriteReport(string name, string seasonKey)
        {
            var strings = TestData.English();
            var document = ReportBuilder.Build(SeasonDataBuilder.Build(TestData.Season(), _ => { }), strings, TestData.Money, "test");
            var path = ReportFiles.Write(Path.Combine(_folder, "reports"), name, HtmlRenderer.Render(document), MarkdownRenderer.Render(document));
            new ReportedSeasons(Path.Combine(_folder, "reported.txt")).Add(seasonKey);
            return path;
        }

        private string[] ReportedKeys() =>
            File.ReadAllLines(Path.Combine(_folder, "reported.txt")).Where(l => !l.StartsWith("#")).ToArray();

        private void AssertCannotWrite()
        {
            Assert.True(DiskWrites.Forbidden);
            Assert.Throws<InvalidOperationException>(() => ReportFiles.Write(Path.Combine(_folder, "panel"), "x", "<html>", "# md"));
            Assert.Throws<InvalidOperationException>(() => new ReportedSeasons(Path.Combine(_folder, "panel-reported.txt")).Add("Club|2026/27"));
            Assert.Throws<InvalidOperationException>(() => RawDump.Write(TestData.Season(), Path.Combine(_folder, "panel")));
        }

        [Fact]
        public void ShowsTheReportAfterTheHeaderAndTheSeasonRead()
        {
            var panel = Panel();
            panel.Start();
            Assert.True(panel.InFlight);
            Frames(_reader.FramesPerRead - 1);
            Assert.Empty(_shown);

            Frames(1); // header answered: the full read starts in the same step
            Assert.True(panel.InFlight);
            Assert.Equal(2, _reader.Started.Count);
            Assert.Empty(_shown);

            Frames(_reader.FramesPerRead);
            Assert.False(panel.InFlight);
            Assert.False(_reader.Busy);
            var model = Assert.Single(_shown);
            Assert.Null(model.Message);
            Assert.Equal(7, model.Tabs.Count);
            Assert.Contains(TestData.Club, model.Title);
            Assert.Empty(_warnings);
        }

        // Edge case (b): the panel path cannot write, in its first call or in any later step.
        [Fact]
        public void NoStepOfThePanelReadCanWrite()
        {
            var reached = new List<string>();
            _reader.OnStart = n =>
            {
                reached.Add(n == 1 ? "start step (first frame)" : "header step (later frame)");
                AssertCannotWrite();
            };
            var panel = Panel(
                strings: () => { reached.Add("loading texts"); AssertCannotWrite(); return TestData.English(); },
                money: pounds => { if (!reached.Contains("building the report")) reached.Add("building the report"); AssertCannotWrite(); return TestData.Money(pounds); },
                show: model => { reached.Add("showing the result (last frame)"); AssertCannotWrite(); _shown.Add(model); });

            panel.Start();
            Frames(_reader.FramesPerRead * 2);

            Assert.Equal(new[]
            {
                "loading texts", "start step (first frame)", "header step (later frame)",
                "building the report", "showing the result (last frame)",
            }, reached);
            Assert.Null(Assert.Single(_shown).Message); // the read itself was not disturbed
            Assert.False(Directory.Exists(_folder));    // and nothing reached the disk
            Assert.False(DiskWrites.Forbidden);
        }

        [Fact]
        public void AWriteAttemptInsideThePanelPathBecomesAFailureMessageNotAFile()
        {
            var panel = Panel(money: pounds =>
            {
                new ReportedSeasons(Path.Combine(_folder, "reported.txt")).Add("Club|2026/27");
                return "never";
            });
            panel.Start();
            Frames(_reader.FramesPerRead * 2);

            Assert.Equal(TestData.English().Get("panel.failed"), Assert.Single(_shown).Message);
            Assert.Contains(_warnings, w => w.Contains("disk writes are forbidden"));
            Assert.False(Directory.Exists(_folder));
            Assert.False(panel.InFlight);
        }

        // Edge case (a): a report written while the panel read is in flight is written normally.
        [Fact]
        public void ReportsAreWrittenNormallyWhileThePanelReadIsInFlight()
        {
            var panel = Panel();
            panel.Start();

            Frames(2); // header read in flight
            Assert.True(panel.InFlight);
            var first = WriteReport("Club_2026-27_during-header", "Club|2026/27|header");
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(Path.ChangeExtension(first, ".md")));

            Frames(_reader.FramesPerRead); // header step done, season read in flight
            Assert.True(panel.InFlight);
            Assert.Equal(2, _reader.Started.Count);
            var second = WriteReport("Club_2026-27_during-season", "Club|2026/27|season");
            Assert.True(File.Exists(second));

            Frames(_reader.FramesPerRead); // panel finished
            Assert.False(panel.InFlight);
            var third = WriteReport("Club_2026-27_after", "Club|2026/27|after");
            Assert.True(File.Exists(third));

            Assert.Equal(new[] { "Club|2026/27|after", "Club|2026/27|header", "Club|2026/27|season" }, ReportedKeys());
            Assert.Null(Assert.Single(_shown).Message); // and the panel still got its report
        }

        // Edge case (a), end to end through the single reader, in the controller's frame order.
        [Fact]
        public void CtrlF8DuringAPanelReadIsServedNextAndWritesItsFiles()
        {
            var turns = new ReaderTurns();
            PanelRead panel = null;
            string reportPath = null;
            bool keyDown = false;
            var order = new List<string>();

            void Frame()
            {
                _reader.Tick();
                Assert.False(DiskWrites.Forbidden);
                if (_reader.Busy)
                {
                    turns.WhileBusy(panel != null && panel.InFlight, () => keyDown);
                    return;
                }
                if (turns.ReportTurn(() => keyDown))
                {
                    order.Add("report");
                    Assert.True(_reader.Start("report", raw => reportPath = WriteReport("Club_2026-27", SeasonEnd.KeyOf(raw))));
                    return;
                }
                if (turns.PanelTurn(_reader.Busy))
                {
                    order.Add("panel");
                    panel = Panel();
                    panel.Start();
                }
            }

            turns.RequestPanel();
            Frame();                       // panel read starts
            Frame();
            keyDown = true; Frame();       // Ctrl+F8 while the reader works for the panel
            keyDown = false;
            turns.RequestPanel();          // and the panel is asked for again (e.g. reopened)
            for (int i = 0; i < 40 && (reportPath == null || _shown.Count < 2); i++) Frame();

            Assert.Equal(new[] { "panel", "report", "panel" }, order); // the report went before the second panel read
            Assert.True(File.Exists(reportPath));
            Assert.Single(ReportedKeys());
            Assert.Equal(2, _shown.Count);
            Assert.All(_shown, m => Assert.Null(m.Message));
        }

        [Fact]
        public void AbandonedReadStopsAfterTheHeaderAndShowsNothing()
        {
            var panel = Panel();
            panel.Start();
            Frames(2);
            panel.Abandon();
            Frames(_reader.FramesPerRead * 3);

            Assert.Single(_reader.Started); // no full read was started for a closed panel
            Assert.False(panel.InFlight);
            Assert.False(_reader.Busy);
            Assert.Empty(_shown);
        }

        [Fact]
        public void NoCareerLoaded()
        {
            _reader.CanStart = false;
            var panel = Panel();
            panel.Start();
            Assert.False(panel.InFlight);
            Assert.Equal(TestData.English().Get("panel.nocareer"), Assert.Single(_shown).Message);
        }

        [Fact]
        public void NoClubOrSeason()
        {
            _reader.Answer = () => new GameRecord();
            var panel = Panel();
            panel.Start();
            Frames(_reader.FramesPerRead);
            Assert.False(panel.InFlight);
            Assert.Single(_reader.Started);
            Assert.Equal(TestData.English().Get("panel.noclub"), Assert.Single(_shown).Message);
        }

        [Fact]
        public void AFailingStepShowsAMessageAndNeverThrowsIntoTheReader()
        {
            var panel = Panel(money: _ => throw new FormatException("boom"));
            panel.Start();
            Frames(_reader.FramesPerRead * 2); // would throw here if the step let the exception out

            Assert.Equal(TestData.English().Get("panel.failed"), Assert.Single(_shown).Message);
            Assert.Contains(_warnings, w => w.Contains("boom"));
            Assert.False(panel.InFlight);
            Assert.False(DiskWrites.Forbidden);
        }

        [Fact]
        public void PanelTextFollowsTheLanguage()
        {
            var panel = Panel(strings: TestData.Turkish);
            panel.Start();
            Frames(_reader.FramesPerRead * 2);
            Assert.Equal("Özet", Assert.Single(_shown).Tabs[0].Label);
        }
    }

    public class ReaderTurnsTests
    {
        [Fact]
        public void PanelRequestWaitsWhileTheReaderIsBusy()
        {
            var turns = new ReaderTurns();
            turns.RequestPanel();
            Assert.False(turns.PanelTurn(readerBusy: true));
            Assert.True(turns.PanelWaiting);
            Assert.True(turns.PanelTurn(readerBusy: false));
            Assert.False(turns.PanelWaiting);
            Assert.False(turns.PanelTurn(readerBusy: false)); // one request, one read
        }

        [Fact]
        public void CancelledPanelRequestNeverStarts()
        {
            var turns = new ReaderTurns();
            turns.RequestPanel();
            turns.CancelPanel();
            Assert.False(turns.PanelTurn(readerBusy: false));
        }

        [Fact]
        public void ReportKeyPressedDuringAPanelReadIsRemembered()
        {
            var turns = new ReaderTurns();
            turns.WhileBusy(busyForPanel: true, () => true);
            Assert.True(turns.ReportWaiting);
            Assert.True(turns.ReportTurn(() => false));  // served without a second press
            Assert.False(turns.ReportTurn(() => false)); // once
        }

        [Fact]
        public void ReportKeyIsNotLookedAtWhileAReportIsBeingRead()
        {
            var turns = new ReaderTurns();
            int polled = 0;
            turns.WhileBusy(busyForPanel: false, () => { polled++; return true; });
            Assert.Equal(0, polled); // unchanged behaviour of the report path
            Assert.False(turns.ReportWaiting);
            Assert.False(turns.ReportTurn(() => false));
        }

        [Fact]
        public void ReportGoesBeforeAWaitingPanelOnTheSameFrame()
        {
            var turns = new ReaderTurns();
            turns.RequestPanel();
            Assert.True(turns.ReportTurn(() => true)); // the report takes the reader...
            Assert.False(turns.PanelTurn(readerBusy: true));
            Assert.True(turns.PanelWaiting);           // ...and the panel keeps waiting
        }

        /// <summary>Worst case: the panel is asked for again the moment each of its reads ends.</summary>
        [Fact]
        public void ConstantPanelRequestsCannotStarveAReport()
        {
            var reader = new FakeReader();
            var turns = new ReaderTurns();
            PanelRead panel = null;
            var started = new List<(int Frame, string Who)>();
            int pressedAt = 12, reportStartedAt = -1, firstPanelEndAfterPress = -1;

            for (int frame = 0; frame < 200; frame++)
            {
                turns.RequestPanel(); // asked for on every single frame
                bool keyDown = frame == pressedAt;

                reader.Tick();
                if (reader.Busy)
                {
                    turns.WhileBusy(panel != null && panel.InFlight, () => keyDown);
                    continue;
                }
                if (frame > pressedAt && firstPanelEndAfterPress < 0) firstPanelEndAfterPress = frame;
                if (turns.ReportTurn(() => keyDown))
                {
                    started.Add((frame, "report"));
                    reportStartedAt = frame;
                    reader.Start("report", _ => { });
                    continue;
                }
                if (turns.PanelTurn(reader.Busy))
                {
                    started.Add((frame, "panel"));
                    panel = new PanelRead((spec, done) => reader.Start("panel", done), TestData.English, TestData.Money, "test", _ => { }, _ => { });
                    panel.Start();
                }
            }

            Assert.Equal("panel", started[0].Who);
            Assert.True(reportStartedAt > pressedAt, "the report was started");
            Assert.Equal(firstPanelEndAfterPress, reportStartedAt); // on the very first frame the reader was free
            Assert.Single(started, s => s.Who == "report");
            Assert.True(started.Count(s => s.Who == "panel") > 5);  // and the panel carried on afterwards
        }
    }
}
