using System.Collections.Generic;

namespace SeasonReview
{
    /// <summary>
    /// A finished report as plain structure: all text is already in the chosen language, so
    /// the HTML and Markdown renderers only decide layout.
    /// </summary>
    internal sealed class ReportDocument
    {
        public string Language, Title, Subtitle, Notice;
        public readonly List<(string Label, string Value)> Tiles = new();
        public readonly List<ReportSection> Sections = new();
        public readonly List<string> Footer = new();
    }

    internal sealed class ReportSection
    {
        public string Id, Title;
        public readonly List<Block> Blocks = new();

        public ReportSection Add(Block block) { Blocks.Add(block); return this; }
    }

    internal abstract class Block { }

    internal sealed class Paragraph : Block { public string Text; }

    internal sealed class Note : Block { public string Text; }

    internal sealed class Subheading : Block { public string Text; }

    internal sealed class Bullets : Block { public readonly List<string> Items = new(); }

    /// <summary>A stand-out item, e.g. the player of the season.</summary>
    internal sealed class Highlight : Block { public string Title, Text, Detail; }

    /// <summary>Label/value pairs, e.g. finance figures.</summary>
    internal sealed class Facts : Block { public readonly List<(string Label, string Value)> Items = new(); }

    internal sealed class Table : Block
    {
        public string[] Headers;
        /// <summary>Columns holding numbers (right-aligned in HTML).</summary>
        public bool[] Numeric;
        public readonly List<string[]> Rows = new();
    }
}
