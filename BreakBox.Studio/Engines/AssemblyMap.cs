// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BreakBox.Studio.Engines;

// One method, decoded far enough to read: its IL instructions and the strings it carries. LikelyCheck
// marks the method that looks like the licence decision, the one to read first.
public sealed record MapMethod(string Name, bool LikelyCheck, IReadOnlyList<string> Il, IReadOnlyList<string> Strings);

// One type in the assembly, with its methods and the types nested inside it (the WinForms forms live
// nested inside Program, so the tree has to go deeper than the top level).
public sealed record MapType(string Name, IReadOnlyList<MapMethod> Methods, IReadOnlyList<MapType> Nested);

// The whole map: the type tree, plus totals for the header.
public sealed record AssemblyMapResult(string Module, IReadOnlyList<MapType> Types, int MethodCount, int StringCount);

// The engine behind the Studio's X-ray view. It reads deeper than Peek's flat lists: a real type
// tree, each method's IL, and the strings per method, so I can click a method and see inside it. It
// reads the metadata and IL with Mono.Cecil and never runs the target. Same honest, small version of
// what a decompiler does, only structured for a tree.
public static class AssemblyMap
{
    private static readonly string[] CheckWords = { "valid", "check", "licen", "key", "serial", "unlock" };

    public static AssemblyMapResult Read(byte[] assembly)
    {
        using var ms = new MemoryStream(assembly);
        using var module = ModuleDefinition.ReadModule(ms);

        var methodCount = 0;
        var stringCount = 0;
        var types = new List<MapType>();
        foreach (var type in module.Types)
        {
            var mapped = MapOne(type, ref methodCount, ref stringCount);
            if (mapped is not null) types.Add(mapped);
        }

        return new AssemblyMapResult(module.Name, types, methodCount, stringCount);
    }

    private static MapType? MapOne(TypeDefinition type, ref int methodCount, ref int stringCount)
    {
        // Hide the compiler's own generated classes and the module pseudo-type, so the tree reads
        // like the program I wrote.
        if (type.Name.Contains('<') || type.Name == "<Module>") return null;

        var methods = new List<MapMethod>();
        foreach (var method in type.Methods)
        {
            // Property and event accessors are noise in the map; the real logic is the named methods.
            if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ||
                method.Name.StartsWith("add_") || method.Name.StartsWith("remove_"))
                continue;

            var il = new List<string>();
            var strings = new List<string>();
            var bodyLooksLikeCheck = false;

            if (method.HasBody)
            {
                foreach (var ins in method.Body.Instructions)
                {
                    il.Add(ins.ToString());
                    if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s)
                    {
                        strings.Add(s);
                        var low = s.ToLowerInvariant();
                        if (low.Contains("invalid") || low.Contains("wrong") || low.Contains("unlock"))
                            bodyLooksLikeCheck = true;
                    }
                }
            }

            var nameLooksLikeCheck = CheckWords.Any(w => method.Name.ToLowerInvariant().Contains(w));
            methods.Add(new MapMethod(method.Name, nameLooksLikeCheck || bodyLooksLikeCheck, il, strings));
            methodCount++;
            stringCount += strings.Count;
        }

        var nested = new List<MapType>();
        foreach (var child in type.NestedTypes)
        {
            var mapped = MapOne(child, ref methodCount, ref stringCount);
            if (mapped is not null) nested.Add(mapped);
        }

        // A type with nothing worth showing is dropped.
        if (methods.Count == 0 && nested.Count == 0) return null;
        return new MapType(type.Name, methods, nested);
    }
}
