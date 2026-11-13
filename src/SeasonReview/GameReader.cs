using System;
using System.Collections.Generic;
using SI.Bindable;
using SI.Core;
using Il2CppMemory = Il2CppSystem.MemoryExtensions;

namespace SeasonReview
{
    /// <summary>
    /// Reads game values through the game's own UI binding tree.
    ///
    /// The tree has a permanent "game" node (a GameReference). Binding a relative path such as
    /// "GameCurrentManagerClubTeam.Name" under it makes the game resolve that property exactly
    /// as it does for its own screens; the value arrives a few frames later. This only adds
    /// read subscriptions to the UI tree. It never sets values or sends events to the game.
    /// </summary>
    internal sealed class GameReader
    {
        private sealed class Request
        {
            public string Path;
            public Bindings.Key Key;
            public string Value;
            public bool Answered;
        }

        private const int TimeoutFrames = 600; // ~10 s at 60 fps; the game may be slower

        private readonly List<Request> _requests = new();
        private Bindings _bindings;
        private Bindings.ValueChangedCallback _callback; // kept alive while bound
        private Action<IReadOnlyDictionary<string, string>> _done;
        private int _framesLeft;

        public bool Busy => _bindings != null;

        /// <summary>Starts reading the given paths (relative to the game node). Result values are
        /// null where the game did not answer in time.</summary>
        public bool Start(Bindings bindings, Il2CppSystem.Object owner, IEnumerable<string> paths,
            Action<IReadOnlyDictionary<string, string>> done)
        {
            if (Busy) return false;

            var game = FindGameNode(bindings);
            if (game == null)
            {
                Plugin.L.LogWarning("Game data unavailable: no 'game' node in the binding tree (is a save loaded?).");
                return false;
            }

            _bindings = bindings;
            _done = done;
            _framesLeft = TimeoutFrames;
            _requests.Clear();
            _callback = (Action<Bindings.Key, TypedValue>)OnValue;

            var root = game.Key;
            foreach (var path in paths)
            {
                if (HiddenValues.IsHiddenPath(path)) continue; // defence in depth
                try
                {
                    var span = Il2CppMemory.AsSpan(path);
                    var key = bindings.Bind(ref root, ref span, owner, _callback);
                    _requests.Add(new Request { Path = path, Key = key });
                }
                catch (Exception e)
                {
                    Plugin.L.LogWarning($"Could not request '{path}': {e.Message}");
                    _requests.Add(new Request { Path = path, Answered = true });
                }
            }
            return true;
        }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (!Busy) return;

            bool allAnswered = true;
            foreach (var r in _requests)
            {
                if (r.Answered) continue;
                try
                {
                    var key = r.Key;
                    Capture(r, _bindings.Get(ref key));
                }
                catch (Exception) { /* not ready yet */ }
                if (!r.Answered) allAnswered = false;
            }

            if (allAnswered || --_framesLeft <= 0) Finish();
        }

        private void OnValue(Bindings.Key key, TypedValue value)
        {
            try
            {
                foreach (var r in _requests)
                    if (!r.Answered && r.Key.m_key == key.m_key)
                        Capture(r, value);
            }
            catch (Exception) { /* never throw into the game's binding update */ }
        }

        private static void Capture(Request r, TypedValue value)
        {
            if (value == null) return;
            var text = value.AsString();
            if (text == null) return;
            r.Value = text;
            if (text.Length > 0) r.Answered = true; // an empty string may be a placeholder; keep waiting
        }

        private void Finish()
        {
            var result = new Dictionary<string, string>();
            foreach (var r in _requests)
            {
                result[r.Path] = r.Value;
                if (!r.Key.IsValid()) continue;
                try
                {
                    var key = r.Key;
                    _bindings.Unbind(ref key, _callback); // drop our subscription; nodes stay with the game
                }
                catch (Exception e) { Plugin.L.LogWarning($"Unbind '{r.Path}' failed: {e.Message}"); }
            }

            var done = _done;
            _bindings = null;
            _done = null;
            _callback = null;
            _requests.Clear();

            try { done?.Invoke(result); }
            catch (Exception e) { Plugin.L.LogWarning($"Result handler failed: {e.Message}"); }
        }

        /// <summary>Finds the permanent node named "game" near the top of the tree.</summary>
        private static Bindings.Node FindGameNode(Bindings bindings)
        {
            var root = bindings.m_root;
            if (root == null) return null;
            for (var top = root.FirstChild; top != null; top = top.NextSibling)
            {
                if (IsGame(top)) return top;
                for (var child = top.FirstChild; child != null; child = child.NextSibling)
                    if (IsGame(child)) return child;
            }
            return null;
        }

        private static bool IsGame(Bindings.Node node) =>
            string.Equals(node.Name, "game", StringComparison.Ordinal) && node.Value != null;
    }
}
