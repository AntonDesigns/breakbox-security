// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using BreakBox.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BreakBox.Studio.Engines;

// Peek's engine. This is the reverse-engineering heart of my app. It opens a compiled .NET assembly
// and reads its metadata and IL with Mono.Cecil, which is the honest, small version of what a
// decompiler like dnSpy does under the hood. The point it hammers home: a .NET assembly is NOT a
// black box. The method names, the type layout and every string literal are still sitting in the
// file, so my first move in any crack is simply to read them.
//
// Security lesson I take to Bosch: shipping compiled code hides almost nothing. Anything I bake into
// a binary (a key, a token, an internal URL) can be read straight back out. That is exactly why the
// real key in BreakBox never ships inside the target, only on the server.
public sealed class CecilAssemblyInspector : IAssemblyInspector
{
    // If a method name contains one of these words, it is probably the check I want to attack first.
    private static readonly string[] CheckWords = { "valid", "check", "licen", "key", "serial", "unlock" };

    // Inspect: walk every type and method and report the three things I look at first when cracking.
    // I read the bytes from memory and never execute them, so pointing Peek at any file is safe.
    public InspectionResult Inspect(byte[] assembly)
    {
        using var ms = new MemoryStream(assembly);
        using var module = ModuleDefinition.ReadModule(ms);

        var methods = new List<string>();
        var strings = new List<StringHit>();
        var likely = new List<string>();

        foreach (var type in module.Types)
        {
            foreach (var method in type.Methods)
            {
                // 1) The method list is the map of the program. A name like IsValid or Expected tells
                //    me where the logic lives before I read a single instruction.
                var full = $"{type.Name}.{method.Name}";
                methods.Add(full);

                var nameLooksLikeCheck = CheckWords.Any(w => method.Name.ToLowerInvariant().Contains(w));
                var bodyHasCheckString = false;

                // 2) Ldstr is the IL instruction that loads a string literal. Pulling these out is how
                //    I read the hardcoded key in Level 1 WITHOUT running the program: the string is
                //    right there in the code. This is the whole "never hardcode a secret" lesson, live.
                if (method.HasBody)
                {
                    foreach (var ins in method.Body.Instructions)
                    {
                        if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s)
                        {
                            strings.Add(new StringHit(type.Name, method.Name, s));
                            var low = s.ToLowerInvariant();
                            if (low.Contains("invalid") || low.Contains("wrong") || low.Contains("unlock"))
                                bodyHasCheckString = true;
                        }
                    }
                }

                // 3) A method named like a check, or one that prints "invalid"/"unlock", is where the
                //    decision lives. That is the line I read for a key, or patch to always pass.
                if (nameLooksLikeCheck || bodyHasCheckString)
                    likely.Add(full);
            }
        }

        return new InspectionResult(methods, strings, likely);
    }
}
