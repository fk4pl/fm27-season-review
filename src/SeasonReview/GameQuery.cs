using System.Collections.Generic;

namespace SeasonReview
{
    /// <summary>
    /// Describes what to read from one game object: plain values by property path, and lists
    /// whose items are read with a nested spec. Paths are dot-separated property names relative
    /// to the object (e.g. "Club.Name").
    /// </summary>
    internal sealed class Spec
    {
        public readonly List<string> Values = new();
        public readonly List<ListSpec> Lists = new();

        public Spec Value(params string[] paths) { Values.AddRange(paths); return this; }

        public Spec List(string path, Spec item, int maxItems = 200)
        {
            Lists.Add(new ListSpec { Path = path, Item = item, MaxItems = maxItems });
            return this;
        }
    }

    internal sealed class ListSpec
    {
        public string Path;
        public Spec Item;
        public int MaxItems;
    }

    /// <summary>What came back: null for a value the game did not answer.</summary>
    internal sealed class Record
    {
        public readonly Dictionary<string, string> Values = new();
        public readonly Dictionary<string, List<Record>> Lists = new();

        /// <summary>Diagnostic notes per list path (runtime type, item count, problems).</summary>
        public readonly Dictionary<string, string> ListNotes = new();

        public string Get(string path) => Values.TryGetValue(path, out var v) ? v : null;

        public List<Record> Items(string path) =>
            Lists.TryGetValue(path, out var l) ? l : new List<Record>();
    }
}
