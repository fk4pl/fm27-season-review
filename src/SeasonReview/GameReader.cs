using System;
using System.Collections.Generic;
using SI.Bindable;
using SI.Core;
using Il2CppCollections = Il2CppSystem.Collections;
using Il2CppMemory = Il2CppSystem.MemoryExtensions;

namespace SeasonReview
{
    /// <summary>
    /// Reads game values through the game's own UI binding tree. Read-only towards the game.
    ///
    /// The tree has a permanent "game" node (a GameReference). Binding a property path under a
    /// node makes the game resolve it exactly as it does for its own screens; the value arrives
    /// some frames later. List values are enumerated and each item is parked on a private node
    /// (under one top-level node owned by this mod) so its properties can be bound the same way.
    /// The private nodes are removed when the read finishes. No values are ever written to
    /// game-owned nodes and no events are sent to the simulation.
    /// </summary>
    internal sealed class GameReader
    {
        private const string PrivateRoot = "SeasonReviewMod";
        private const int MaxOpenRequests = 2500;  // throttle: bindings waiting for an answer
        private const int IdleFrames = 240;        // finish when nothing has arrived for this long
        private const int MaxFrames = 60 * 90;     // hard stop
        private const int MaxScopes = 4000;        // safety cap on objects visited

        private sealed class Scope
        {
            public Bindings.Key Root;
            public Spec Spec;
            public GameRecord GameRecord;
        }

        private sealed class Request
        {
            public Bindings.Key Key;
            public string Path;
            public GameRecord GameRecord;
            public ListSpec List; // null for plain values
        }

        private readonly Queue<Scope> _scopes = new();
        private readonly List<Request> _open = new();
        private readonly List<Request> _bound = new();
        private Bindings _bindings;
        private Il2CppSystem.Object _owner;
        private Bindings.ValueChangedCallback _callback; // kept alive while bound
        private Action<GameRecord> _done;
        private GameRecord _result;
        private int _idle, _frames, _scopeCount, _privateCount, _answered, _requested;

        public bool Busy => _bindings != null;
        public string Progress => $"{_answered}/{_requested} values, {_scopeCount} objects";

        public bool Start(Bindings bindings, Il2CppSystem.Object owner, Spec spec, Action<GameRecord> done)
        {
            if (Busy) return false;

            var game = FindGameNode(bindings);
            if (game == null)
            {
                Plugin.L.LogWarning("Game data unavailable: no 'game' node in the binding tree (is a save loaded?).");
                return false;
            }

            _bindings = bindings;
            _owner = owner;
            _done = done;
            _result = new GameRecord();
            _idle = _frames = _scopeCount = _privateCount = _answered = _requested = 0;
            _callback = (Action<Bindings.Key, TypedValue>)OnValue;

            _scopes.Enqueue(new Scope { Root = game.Key, Spec = spec, GameRecord = _result });
            return true;
        }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (!Busy) return;
            _frames++;
            _idle++;

            // Open more bindings while under the throttle.
            while (_scopes.Count > 0 && _open.Count < MaxOpenRequests)
                OpenScope(_scopes.Dequeue());

            // Poll: values that were already cached do not always raise the callback.
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var r = _open[i];
                try
                {
                    var key = r.Key;
                    Answer(r, _bindings.Get(ref key));
                }
                catch (Exception) { /* not ready yet */ }
            }

            bool finished = _open.Count == 0 && _scopes.Count == 0;
            if (finished || _idle > IdleFrames) Finish(false);
            else if (_frames > MaxFrames) Finish(true);
        }

        private void OpenScope(Scope scope)
        {
            if (++_scopeCount > MaxScopes) return;
            foreach (var path in scope.Spec.Values) Bind(scope, path, null);
            foreach (var list in scope.Spec.Lists) Bind(scope, list.Path, list);
        }

        private void Bind(Scope scope, string path, ListSpec list)
        {
            if (list == null) scope.GameRecord.Values[path] = null;
            if (HiddenValues.IsHiddenPath(path)) return; // defence in depth

            try
            {
                var root = scope.Root;
                var span = Il2CppMemory.AsSpan(path);
                var key = _bindings.Bind(ref root, ref span, _owner, _callback);
                var request = new Request { Key = key, Path = path, GameRecord = scope.GameRecord, List = list };
                _open.Add(request);
                _bound.Add(request);
                _requested++;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning($"Could not request '{path}': {e.Message}");
            }
        }

        private void OnValue(Bindings.Key key, TypedValue value)
        {
            try
            {
                for (int i = _open.Count - 1; i >= 0; i--)
                    if (_open[i].Key.m_key == key.m_key)
                        Answer(_open[i], value);
            }
            catch (Exception) { /* never throw into the game's binding update */ }
        }

        private void Answer(Request r, TypedValue value)
        {
            if (value == null || !_open.Contains(r)) return;

            if (r.List != null)
            {
                Il2CppSystem.Object obj = null;
                try { obj = value.Get(); } catch (Exception) { }
                if (obj == null) return; // list not delivered yet
                _open.Remove(r);
                _answered++;
                _idle = 0;
                ExpandList(r, obj);
                return;
            }

            var text = value.AsString();
            if (text == null) return;
            r.GameRecord.Values[r.Path] = text;
            if (text.Length == 0) return; // may be a placeholder; keep waiting for a real value
            Flatten(r.GameRecord, r.Path, value);
            _open.Remove(r);
            _answered++;
            _idle = 0;
        }

        private void ExpandList(Request r, Il2CppSystem.Object obj)
        {
            var records = new List<GameRecord>();
            r.GameRecord.Lists[r.List.Key] = records;
            string typeName = "?";
            try { typeName = obj.GetIl2CppType().FullName; } catch (Exception) { }

            List<Il2CppSystem.Object> items;
            try { items = Enumerate(obj, r.List.MaxItems); }
            catch (Exception e)
            {
                r.GameRecord.ListNotes[r.List.Key] = $"type {typeName}; cannot enumerate: {e.Message}";
                return;
            }
            if (items == null)
            {
                r.GameRecord.ListNotes[r.List.Key] = $"type {typeName}; not a collection";
                return;
            }

            string itemType = "-";
            foreach (var item in items)
            {
                if (item == null) continue;
                try
                {
                    // Lists hold either references directly or TypedValues wrapping them.
                    var payload = item;
                    var wrapped = item.TryCast<TypedValue>();
                    if (wrapped != null) payload = wrapped.Get();
                    if (payload == null) continue;
                    if (itemType == "-") itemType = payload.GetIl2CppType().FullName;

                    // A fresh holder, so removing our node can never release a game-owned value.
                    var holder = TypedValue.GetReferenceTypedValue();
                    holder.SetValue(payload);

                    // Private container: context values plus the object itself as "src".
                    string container = PrivateRoot + "." + (_privateCount++).ToString("X");
                    foreach (var context in r.List.Item.Contexts)
                    {
                        TypedValue contextValue;
                        if (ReferenceEquals(context.Value, Spec.Self))
                        {
                            var self = TypedValue.GetReferenceTypedValue();
                            self.SetValue(payload);
                            contextValue = self;
                        }
                        else if (context.Value is bool flag) contextValue = TypedValue.Create(flag);
                        else contextValue = TypedValue.Create(Convert.ToInt32(context.Value));
                        var contextSpan = Il2CppMemory.AsSpan(container + "." + context.Key);
                        _bindings.Set(ref contextSpan, contextValue, Bindings.NodeFlags.None, (Bindings.SetFlags)0);
                    }
                    var span = Il2CppMemory.AsSpan(container + ".src");
                    var key = _bindings.Set(ref span, holder, Bindings.NodeFlags.None, (Bindings.SetFlags)0);

                    var record = new GameRecord();
                    records.Add(record);
                    if (wrapped != null) record.Values["."] = wrapped.AsString();
                    Flatten(record, "", wrapped);
                    _scopes.Enqueue(new Scope { Root = key, Spec = r.List.Item, GameRecord = record });
                }
                catch (Exception e)
                {
                    r.GameRecord.ListNotes[r.List.Key] = $"type {typeName}; item failed: {e.Message}";
                    return;
                }
            }
            r.GameRecord.ListNotes[r.List.Key] = $"type {typeName}; {records.Count} items of {itemType}";
        }

        /// <summary>
        /// Some values are small records (property id -> value) delivered in one piece. Their
        /// fields are copied into the record as "path.FieldName"; hidden values are skipped.
        /// </summary>
        private static void Flatten(GameRecord record, string path, TypedValue value, int depth = 0)
        {
            try
            {
                var dynamic = value?.Get()?.TryCast<DynamicReference>();
                if (dynamic == null) return;
                var dictionary = dynamic.Cast<Il2CppSystem.Collections.Generic.Dictionary<uint, TypedValue>>();
                int count = dictionary.Count;
                if (count == 0 || count > 200) return;
                var keys = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<uint>(count);
                dictionary.Keys.CopyTo(keys, 0);
                var identifiers = Bindings.Node.s_propertyIdentifierSet;
                foreach (uint id in keys)
                {
                    string name = identifiers?.GetInfo(id)?.Name ?? ("#" + id);
                    if (HiddenValues.IsHidden(name)) continue;
                    if (!dictionary.TryGetValue(id, out var field) || field == null) continue;
                    string fieldPath = path.Length == 0 ? name : path + "." + name;
                    record.Values[fieldPath] = field.AsString();
                    if (depth < 2) Flatten(record, fieldPath, field, depth + 1); // records inside records
                }
            }
            catch (Exception) { /* not a record, or not readable: keep the plain value */ }
        }

        private static List<Il2CppSystem.Object> Enumerate(Il2CppSystem.Object obj, int max)
        {
            var result = new List<Il2CppSystem.Object>();

            var list = obj.TryCast<Il2CppCollections.IList>();
            var collection = obj.TryCast<Il2CppCollections.ICollection>();
            if (list != null && collection != null)
            {
                int count = Math.Min(collection.Count, max);
                for (int i = 0; i < count; i++) result.Add(list[i]);
                return result;
            }

            var enumerable = obj.TryCast<Il2CppCollections.IEnumerable>();
            if (enumerable == null || obj.TryCast<Il2CppSystem.String>() != null)
            {
                result.Add(obj); // a single object: a list of one
                return result;
            }
            var enumerator = enumerable.GetEnumerator();
            while (result.Count < max && enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        private void Finish(bool timedOut)
        {
            // Some values legitimately never answer (e.g. the competition of an objective that
            // has none), so unanswered values are only worth a warning on a hard timeout.
            if (timedOut)
                Plugin.L.LogWarning($"Game data read timed out ({Progress}); {_open.Count} values unanswered.");
            else
                Plugin.L.LogDebug($"Game data read finished: {Progress}, {_open.Count} without an answer.");

            foreach (var r in _bound)
            {
                try
                {
                    var key = r.Key;
                    if (key.IsValid()) _bindings.Unbind(ref key, _callback);
                }
                catch (Exception) { /* node may already be gone */ }
            }

            if (_privateCount > 0)
            {
                try
                {
                    var span = Il2CppMemory.AsSpan(PrivateRoot);
                    var key = Bindings.MakeKey(ref span);
                    _bindings.Remove(ref key);
                }
                catch (Exception e) { Plugin.L.LogWarning($"Could not remove private nodes: {e.Message}"); }
            }

            var done = _done;
            var result = _result;
            _bindings = null;
            _owner = null;
            _done = null;
            _callback = null;
            _result = null;
            _open.Clear();
            _bound.Clear();
            _scopes.Clear();

            try { done?.Invoke(result); }
            catch (Exception e) { Plugin.L.LogWarning($"Result handler failed: {e}"); }
        }

        /// <summary>The game's UI binding tree, or null when no game is running.</summary>
        public static Bindings TryGetBindings()
        {
            try
            {
                if (GameSubsystems.TryGet<BindingSubsystem>(out var subsystem)) return subsystem;
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// Reads a value the game's own UI already keeps under the "game" node (e.g.
        /// "CurrentGameDate"), without requesting anything. Null if it is not there.
        /// </summary>
        public static string Peek(Bindings bindings, string name)
        {
            try
            {
                var game = FindGameNode(bindings);
                for (var child = game?.FirstChild; child != null; child = child.NextSibling)
                    if (string.Equals(child.Name, name, StringComparison.Ordinal))
                        return child.Value?.AsString();
            }
            catch (Exception) { }
            return null;
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
