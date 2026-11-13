using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SI.Bindable;
using SI.Bindable.Reference.Core;
using SI.Core;
using Il2CppList = Il2CppSystem.Collections.Generic;

namespace SeasonReview
{
    /// <summary>
    /// Stage 1 probe. Strictly read-only: it walks the UI binding tree the game already
    /// maintains in managed memory and lists the property names each reference type declares.
    /// It never opens channels, sends events or sets values.
    /// </summary>
    internal static class Probe
    {
        // Node names whose VALUE may be logged. Everything else is logged by name and type only.
        private static readonly string[] DateNames =
            { "currentgamedate", "currentgamedatestring", "currentgameyear", "seasonyear", "seasonyearlong" };
        private static readonly string[] ClubNames = { "humanteam", "humanclub" };
        private static readonly string[] LabelNames = { "name", "shortname", "fullname", "club", "team" };

        // Context nodes whose values tell us what the game's screens pass to list properties.
        private static readonly HashSet<string> ContextNames = new HashSet<string>
        {
            "year", "yearindex", "leaguetabletype", "stringtype", "transfertype", "viewid", "number", "type",
            "visionobjectivesetter", "visionobjectivetype", "visionobjectiveyear", "visionisusingperformancedata",
            "previousamount", "futureamount", "fixturepadding", "selectedobject", "id",
        };

        private const int MaxNodes = 400_000;
        private const int MaxDepth = 48;

        private static int _dumpIndex;

        /// <summary>Returns the binding tree, or null (with a warning) when no game is loaded.</summary>
        public static Bindings Run(bool dump)
        {
            Plugin.L.LogInfo("---- Season Review: discovery probe ----");


            Bindings bindings = null;
            try
            {
                if (GameSubsystems.TryGet<BindingSubsystem>(out var subsystem)) bindings = subsystem;
            }
            catch (Exception e) { Plugin.L.LogWarning($"Binding subsystem lookup failed: {e.Message}"); }

            if (bindings == null)
            {
                Plugin.L.LogWarning("Game data unavailable: binding subsystem not found (is a save loaded?).");
                return null;
            }

            if (!dump) return bindings;

            var dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "SeasonReview");
            Directory.CreateDirectory(dir);

            try { WalkTree(bindings, Path.Combine(dir, $"probe-tree-{++_dumpIndex}.txt")); }
            catch (Exception e) { Plugin.L.LogWarning($"Binding tree walk failed: {e}"); }

            try { DumpProperties(Path.Combine(dir, "probe-properties.txt")); }
            catch (Exception e) { Plugin.L.LogWarning($"Property dump failed: {e}"); }
            return bindings;
        }

        // ── Binding tree ───────────────────────────────────────────────────

        private static void WalkTree(Bindings bindings, string path)
        {
            var root = bindings.m_root;
            if (root == null) { Plugin.L.LogWarning("Game data unavailable: binding tree has no root."); return; }

            var dates = new List<string>();
            var clubs = new List<string>();
            var typeCounts = new Dictionary<string, int>();
            int nodes = 0, skippedHidden = 0;
            bool truncated = false;

            using var writer = path == null ? null : new StreamWriter(path, false, new UTF8Encoding(false));
            writer?.WriteLine("# Season Review probe: binding tree (names and value types only; hidden values skipped)");

            // Iterative depth-first walk: (node, depth, path)
            var stack = new Stack<(Bindings.Node node, int depth, string path)>();
            stack.Push((root, 0, ""));

            while (stack.Count > 0)
            {
                var (node, depth, parentPath) = stack.Pop();
                if (node == null) continue;
                if (++nodes > MaxNodes) { truncated = true; break; }

                string name = Safe(() => node.Name) ?? "?";
                if (HiddenValues.IsHidden(name)) { skippedHidden++; continue; } // skip the whole subtree

                string full = parentPath.Length == 0 ? name : parentPath + "." + name;
                TypedValue value = Safe(() => node.Value);
                string type = value == null ? "-" : (Safe(() => value.GetIl2CppType().Name) ?? "?");
                typeCounts[type] = typeCounts.TryGetValue(type, out var c) ? c + 1 : 1;

                string shown = null;
                string lower = name.ToLowerInvariant();
                bool isDate = DateNames.Contains(lower);
                bool isClub = ClubNames.Contains(lower);
                bool isLabelUnderClub = LabelNames.Contains(lower) && ClubNames.Any(n => ContainsSegment(parentPath, n));
                // Binding variables and contexts are written in lower case by the game's UI files
                // (property names are PascalCase); their values tell us which contexts lists need.
                bool isVariable = (name.Length > 1 && name == lower && char.IsLetter(name[0]) && type != "ReferenceTypedValue")
                                  || ContextNames.Contains(lower);
                if (value != null && (isDate || isClub || isLabelUnderClub || isVariable))
                {
                    shown = Safe(() => value.AsString());
                    if (!string.IsNullOrEmpty(shown))
                    {
                        if (isDate && dates.Count < 12) dates.Add($"{full} = {shown}");
                        if ((isClub || isLabelUnderClub) && clubs.Count < 12) clubs.Add($"{full} = {shown}");
                    }
                }

                if (writer != null)
                {
                    writer.Write(new string(' ', Math.Min(depth, MaxDepth) * 2));
                    writer.Write(name);
                    writer.Write("  [");
                    writer.Write(type);
                    writer.Write(']');
                    if (shown != null) { writer.Write("  = "); writer.Write(shown.Replace('\n', ' ')); }
                    writer.WriteLine();
                }

                if (depth >= MaxDepth) continue;
                // Push siblings in reverse so the file keeps the tree's own order.
                var children = new List<Bindings.Node>();
                for (var child = Safe(() => node.FirstChild); child != null; child = Safe(() => child.NextSibling))
                {
                    children.Add(child);
                    if (children.Count > 20_000) break;
                }
                for (int i = children.Count - 1; i >= 0; i--) stack.Push((children[i], depth + 1, full));
            }

            writer?.Flush();

            Plugin.L.LogInfo($"Binding tree: {nodes} nodes visited{(truncated ? " (truncated)" : "")}, {skippedHidden} hidden subtrees skipped.");
            Plugin.L.LogInfo("Value types: " + string.Join(", ", typeCounts.OrderByDescending(p => p.Value).Take(12).Select(p => $"{p.Key}×{p.Value}")));

            if (path != null) Plugin.L.LogInfo("Tree dump written to " + path);
        }

        private static bool ContainsSegment(string path, string segment) =>
            ("." + path + ".").IndexOf("." + segment + ".", StringComparison.OrdinalIgnoreCase) >= 0;

        // ── Property names per reference type ──────────────────────────────

        private static void DumpProperties(string path)
        {
            var assembly = typeof(FM.UI.GameReference).Assembly;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            var identifiers = Safe(() => Bindings.Node.s_propertyIdentifierSet);
            int typeCount = 0, propertyCount = 0, failed = 0;

            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("# Season Review probe: property names per reference type (hidden values skipped)");

            foreach (var type in types.Where(t => t.Name.EndsWith("Reference", StringComparison.Ordinal)).OrderBy(t => t.FullName))
            {
                var method = type.GetMethod("GetPropertiesInternal", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (method == null) continue;

                var kindMethod = type.GetMethod("GetPropertyTypeInternal", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                var contextMethod = type.GetMethod("GetContextsInternal", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                var names = new List<string>();
                try
                {
                    var list = new Il2CppList.List<PropertyID>();
                    method.Invoke(null, new object[] { list });
                    for (int i = 0; i < list.Count; i++)
                    {
                        uint id = list[i].id;
                        string name = null;
                        if (identifiers != null) name = Safe(() => identifiers.GetInfo(id)?.Name);
                        name ??= "#" + id;
                        if (HiddenValues.IsHidden(name)) continue;
                        names.Add(name + Describe(kindMethod, contextMethod, id));
                    }
                }
                catch (Exception) { failed++; continue; }

                typeCount++;
                propertyCount += names.Count;
                writer.WriteLine();
                writer.WriteLine($"## {type.FullName} ({names.Count})");
                foreach (var n in names.OrderBy(n => n, StringComparer.Ordinal)) writer.WriteLine(n);
                writer.Flush();
            }

            Plugin.L.LogInfo($"Property dump: {typeCount} reference types, {propertyCount} properties, {failed} failed -> {path}");
        }

        // "  [Kind, list] ctx: a, b" for one property; empty when the game gives no details.
        private static string Describe(MethodInfo kindMethod, MethodInfo contextMethod, uint id)
        {
            var text = new StringBuilder();
            try
            {
                if (kindMethod?.Invoke(null, new object[] { id }) is BindingKind kind)
                {
                    text.Append("  [").Append(kind.Kind.ToString());
                    if (kind.IsList) text.Append(", list");
                    var valueType = Safe(() => kind.Type?.Name);
                    if (!string.IsNullOrEmpty(valueType)) text.Append(", ").Append(valueType);
                    text.Append(']');
                }
            }
            catch (Exception) { }
            try
            {
                var contexts = new Il2CppList.List<ContextID>();
                contextMethod?.Invoke(null, new object[] { id, contexts });
                if (contexts.Count > 0)
                {
                    var names = new List<string>();
                    for (int i = 0; i < contexts.Count; i++)
                    {
                        var context = contexts[i];
                        uint contextId = context.id;
                        names.Add(Safe(() => context.GetIdentifierSet().GetInfo(contextId)?.Name) ?? ("#" + contextId));
                    }
                    text.Append("  ctx: ").Append(string.Join(", ", names));
                }
            }
            catch (Exception) { }
            return text.ToString();
        }

        private static T Safe<T>(Func<T> read)
        {
            try { return read(); }
            catch (Exception) { return default; }
        }
    }
}
