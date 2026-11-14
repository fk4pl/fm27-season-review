using System;
using System.IO;
using System.Linq;
using SeasonReview;
using Xunit;

namespace SeasonReview.Tests
{
    /// <summary>The in-game panel runs inside DiskWrites.Forbid: nothing may reach the disk from there.</summary>
    public class DiskWritesTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "SeasonReviewTests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Fact]
        public void ReportFilesAreNotWrittenInsideAForbiddenScope()
        {
            using (DiskWrites.Forbid("test"))
            {
                var error = Assert.Throws<InvalidOperationException>(() => ReportFiles.Write(_folder, "Club_2026-27", "<html>", "# md"));
                Assert.Contains("report files", error.Message);
            }
            Assert.False(Directory.Exists(_folder));

            var path = ReportFiles.Write(_folder, "Club_2026-27", "<html>", "# md"); // allowed again
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void SeasonsAreNotMarkedAsReportedInsideAForbiddenScope()
        {
            var file = Path.Combine(_folder, "reported.txt");
            var reported = new ReportedSeasons(file);
            using (DiskWrites.Forbid("test"))
                Assert.Throws<InvalidOperationException>(() => reported.Add("Club|2026/27"));

            Assert.False(reported.Contains("Club|2026/27")); // not remembered in memory either
            Assert.False(File.Exists(file));

            reported.Add("Club|2026/27");
            Assert.True(reported.Contains("Club|2026/27"));
            Assert.True(File.Exists(file));
        }

        [Fact]
        public void RawDumpIsNotWrittenInsideAForbiddenScope()
        {
            using (DiskWrites.Forbid("test"))
                Assert.Throws<InvalidOperationException>(() => RawDump.Write(TestData.Season(), _folder));
            Assert.False(Directory.Exists(_folder));
        }

        [Fact]
        public void ScopesNestAndEnd()
        {
            Assert.False(DiskWrites.Forbidden);
            var outer = DiskWrites.Forbid("outer");
            var inner = DiskWrites.Forbid("inner");
            inner.Dispose();
            inner.Dispose(); // disposing twice must not end the outer scope
            Assert.True(DiskWrites.Forbidden);
            Assert.Throws<InvalidOperationException>(() => DiskWrites.Demand("anything"));
            outer.Dispose();
            Assert.False(DiskWrites.Forbidden);
            DiskWrites.Demand("anything");
        }

        [Fact]
        public void BuildingThePanelModelWritesNothing()
        {
            Directory.CreateDirectory(_folder);
            var before = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(_folder);
            try
            {
                using (DiskWrites.Forbid("panel"))
                {
                    var strings = TestData.English();
                    var data = SeasonDataBuilder.Build(TestData.Season(), _ => { });
                    var model = PanelModelBuilder.Build(ReportBuilder.Build(data, strings, TestData.Money, "test"), strings);
                    Assert.Equal(7, model.Tabs.Count);
                }
            }
            finally { Directory.SetCurrentDirectory(before); }
            Assert.Empty(Directory.GetFileSystemEntries(_folder));
        }

        /// <summary>The panel's own source files must not even mention the writers or the file system.</summary>
        [Theory]
        [InlineData("SeasonReview", "ReportPanel.cs", true)]
        [InlineData("SeasonReview.Core", "PanelRead.cs", true)]
        [InlineData("SeasonReview.Core", "PanelModel.cs", false)] // pure data: has no entry points to guard
        public void PanelSourcesDoNotReferenceTheWriters(string project, string file, bool hasEntryPoints)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "src", "SeasonReview.Core"))) root = root.Parent;
            Assert.True(root != null, "repository root not found above " + AppContext.BaseDirectory);

            var text = File.ReadAllText(Path.Combine(root.FullName, "src", project, file));
            foreach (var forbidden in new[] { "ReportFiles", "ReportedSeasons", "RawDump", "Probe.", "StatePath", "ReportFolder",
                                             "System.IO", "File.", "Directory.", "StreamWriter", "FileStream", "HtmlRenderer", "MarkdownRenderer" })
                Assert.False(text.Contains(forbidden), $"{file} mentions {forbidden}");
            if (hasEntryPoints) Assert.Contains("DiskWrites.Forbid(", text); // and its entry points run read-only
        }

        /// <summary>Every source file of the mod that writes to disk must ask DiskWrites first.</summary>
        [Fact]
        public void EveryDiskWriterAsksTheGuard()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "src", "SeasonReview.Core"))) root = root.Parent;
            Assert.True(root != null, "repository root not found above " + AppContext.BaseDirectory);

            var writes = new[] { "File.Write", "File.Append", "File.Move", "File.Copy", "File.Delete", "File.Create", "File.Open",
                                 "Directory.CreateDirectory", "Directory.Delete", "new StreamWriter", "new FileStream" };
            var sources = Directory.GetFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                         && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)).ToList();
            Assert.True(sources.Count > 10);

            foreach (var source in sources)
            {
                var text = File.ReadAllText(source);
                if (Path.GetFileName(source) == "DiskWrites.cs") continue;
                if (writes.Any(text.Contains))
                    Assert.True(text.Contains("DiskWrites.Demand("), Path.GetFileName(source) + " writes to disk without asking DiskWrites.Demand");
            }
        }
    }
}
