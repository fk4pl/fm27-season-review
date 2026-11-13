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

        /// <summary>
        /// Context values some properties need (e.g. "YearIndex" for a fixture list). They are
        /// supplied next to the object, the way the game's own screens do it. A value is an int,
        /// a bool, or <see cref="Self"/> for the object itself (e.g. the "team" context).
        /// </summary>
        public readonly Dictionary<string, object> Contexts = new();

        public static readonly object Self = new object();

        public Spec Context(string name, object value) { Contexts[name] = value; return this; }

        public Spec Value(params string[] paths) { Values.AddRange(paths); return this; }

        /// <summary>Reads a list; each item is read with <paramref name="item"/>. A single
        /// object (not a collection) is treated as a list of one.</summary>
        public Spec List(string path, Spec item, int maxItems = 200, string key = null)
        {
            Lists.Add(new ListSpec { Path = path, Key = key ?? path, Item = item, MaxItems = maxItems });
            return this;
        }

        /// <summary>Reads one referenced object with its own spec (and contexts).</summary>
        public Spec Object(string path, Spec item, string key = null) => List(path, item, 1, key);
    }

    internal sealed class ListSpec
    {
        public string Path;
        public string Key; // name of the result list; lets one path be read with several contexts
        public Spec Item;
        public int MaxItems;
    }

    /// <summary>What came back: null for a value the game did not answer.</summary>
    internal sealed class GameRecord
    {
        public readonly Dictionary<string, string> Values = new();
        public readonly Dictionary<string, List<GameRecord>> Lists = new();

        /// <summary>Diagnostic notes per list path (runtime type, item count, problems).</summary>
        public readonly Dictionary<string, string> ListNotes = new();

        public string Get(string path) => Values.TryGetValue(path, out var v) ? v : null;

        public List<GameRecord> Items(string path) =>
            Lists.TryGetValue(path, out var l) ? l : new List<GameRecord>();
    }
}
