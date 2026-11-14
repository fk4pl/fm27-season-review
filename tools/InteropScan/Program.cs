// InteropScan: reports which methods of a BepInEx interop assembly can be called in the game.
//
// IL2CPP builds strip engine code the game does not use. Il2CppInterop then generates one of
// three kinds of method body, which this tool tells apart without starting the game:
//   NATIVE   calls the method compiled into the game: safe to call.
//   ICALL    calls an engine-internal function: normally safe.
//   MANAGED  re-created from Unity's reference assemblies; fine unless it calls a FAILED method.
//   FAILED   stripped and could not be re-created: throws "Method unstripping failed".
//   BROKEN   a MANAGED method that reaches a FAILED one (the call chain is shown).
//
// What it cannot tell: whether a generic method or type works for a particular type argument,
// and anything about behaviour at run time (drawing, input, themes). Check those in the game.

using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 4 || !new[] { "list", "bad", "sum", "type" }.Contains(args[1]))
{
    Console.Error.WriteLine(
        "Usage: InteropScan <interop-folder> <list|bad|sum|type> <assembly.dll> <type-regex> [method-regex]\n" +
        "  list  every matching method with its status\n" +
        "  bad   only FAILED and BROKEN methods\n" +
        "  sum   one line per type with counts\n" +
        "  type  shape of each type as generated (value type or class, fields)\n" +
        "Example:\n" +
        "  InteropScan \"<game>/BepInEx/interop\" list UnityEngine.IMGUIModule.dll '^UnityEngine\\.GUI$' '^(Label|Box)$'");
    return 2;
}

var folder = args[0];
var mode = args[1];
var typeFilter = new Regex(args[3]);
var methodFilter = new Regex(args.Length > 4 ? args[4] : ".*");

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(folder);
resolver.AddSearchDirectory(Path.Combine(folder, "..", "core"));
var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(folder, args[2]), new ReaderParameters { AssemblyResolver = resolver });

var known = new Dictionary<MethodDefinition, string>();

// What the body itself does, without following calls.
string Direct(MethodDefinition method)
{
    if (!method.HasBody) return "NOBODY";
    bool native = false, icall = false;
    foreach (var instruction in method.Body.Instructions)
    {
        if (instruction.OpCode == OpCodes.Ldstr && ((string)instruction.Operand).Contains("unstripping failed")) return "FAILED";
        if (instruction.Operand is FieldReference field)
        {
            if (field.Name.StartsWith("NativeMethodInfoPtr_")) native = true;
            else if (field.FieldType.Name.Contains("Delegate") || field.Name.Contains("Delegate")) icall = true;
        }
    }
    return native ? "NATIVE" : icall ? "ICALL" : "MANAGED";
}

// Follows calls out of MANAGED bodies (within Unity assemblies) to find stripped methods.
string Status(MethodDefinition method, int depth)
{
    if (known.TryGetValue(method, out var status)) return status;
    var direct = Direct(method);
    if (direct != "MANAGED" || depth > 12) return known[method] = direct;

    known[method] = "MANAGED"; // guards against cycles
    foreach (var instruction in method.Body.Instructions)
    {
        if (instruction.Operand is not MethodReference reference) continue;
        if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Newobj) continue;

        MethodDefinition callee = null;
        try { callee = reference.Resolve(); } catch (Exception) { }
        if (callee == null || callee == method) continue;
        if (!callee.Module.Assembly.Name.Name.StartsWith("Unity")) continue;

        var calleeStatus = Status(callee, depth + 1);
        if (calleeStatus == "FAILED" || calleeStatus.StartsWith("BROKEN"))
            return known[method] = "BROKEN via " + callee.DeclaringType.Name + "::" + callee.Name +
                                   (calleeStatus.StartsWith("BROKEN") ? " <- " + calleeStatus.Substring("BROKEN via ".Length) : "");
    }
    return known[method] = "MANAGED";
}

IEnumerable<TypeDefinition> WithNested(IEnumerable<TypeDefinition> types) =>
    types.SelectMany(t => new[] { t }.Concat(WithNested(t.NestedTypes)));

foreach (var type in WithNested(assembly.MainModule.Types).Where(t => typeFilter.IsMatch(t.FullName)))
{
    if (mode == "type")
    {
        var fields = type.Fields.Where(f => !f.IsStatic).Select(f => f.FieldType.Name + " " + f.Name);
        Console.WriteLine($"{type.FullName}: valueType={type.IsValueType} base={type.BaseType?.Name} fields=[{string.Join(", ", fields)}]");
        continue;
    }

    var methods = type.Methods
        .Where(m => !(m.IsConstructor && m.IsStatic))
        .Where(m => !(m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "IntPtr")) // interop's own wrapper constructor
        .Where(m => methodFilter.IsMatch(m.Name))
        .ToList();

    if (mode == "sum")
    {
        var counts = methods.GroupBy(m => Status(m, 0).Split(' ')[0]).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}");
        Console.WriteLine($"{type.FullName,-58} {string.Join(" ", counts)}");
        continue;
    }

    foreach (var method in methods)
    {
        var status = Status(method, 0);
        if (mode == "bad" && status != "FAILED" && !status.StartsWith("BROKEN")) continue;
        Console.WriteLine($"{status,-8} {type.Name}::{method.Name}({string.Join(",", method.Parameters.Select(p => p.ParameterType.Name))})");
    }
}
return 0;
