// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BreakBox.Studio.Engines;

// What Flip found and did: the gate it rewrote, the method body before and after, and the patched
// assembly bytes ready to save. Before/after are the decoded IL, so the write-up shows exactly the
// one change a patch makes.
public sealed record FlipResult(string Method, IReadOnlyList<string> Before, IReadOnlyList<string> After, byte[] Patched);

// Flip, the offensive counterpart to Pixler. It is the Level 3 crack written as my own tool instead of
// clicking dnSpy: load an assembly with Mono.Cecil, find the client-side premium gate (a bool method
// like IsPremium() that returns false), and rewrite its body to the two instructions ldc.i4.1 ; ret so
// it always returns true. It writes a new, valid assembly and never runs the target. Same Mono.Cecil
// that Peek already uses, so there is no new dependency.
//
// The whole point is the lesson: a check that ships inside the program I was handed is a single bit to
// flip, which is why the real check has to live on the server.
public static class FlipPatcher
{
    // A method whose name contains one of these, and which returns bool, is probably the gate.
    private static readonly string[] GateWords = { "premium", "licen", "valid", "unlock", "registered", "activated", "pro" };

    // Find the gate and rewrite it. Returns null if there is no client-side bool gate to flip.
    public static FlipResult? Flip(byte[] assembly)
    {
        using var ms = new MemoryStream(assembly);
        var module = ModuleDefinition.ReadModule(ms);

        var method = AllTypes(module.Types)
            .SelectMany(t => t.Methods)
            .FirstOrDefault(IsGate);
        if (method is null) return null;

        var before = method.Body.Instructions.Select(i => i.ToString()).ToList();

        // Replace the whole body with "push 1, return": the method now always returns true.
        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();
        il.Append(il.Create(OpCodes.Ldc_I4_1));
        il.Append(il.Create(OpCodes.Ret));
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 8;

        var after = method.Body.Instructions.Select(i => i.ToString()).ToList();

        using var outMs = new MemoryStream();
        module.Write(outMs);

        var name = $"{method.DeclaringType.Name}.{method.Name}";
        return new FlipResult(name, before, after, outMs.ToArray());
    }

    // A gate is a bool-returning method, named like a premium/licence check, that currently returns
    // false. I keep the bar deliberately narrow so Flip rewrites the real check, not some helper.
    private static bool IsGate(MethodDefinition m)
    {
        if (!m.HasBody || m.ReturnType.MetadataType != MetadataType.Boolean) return false;
        if (!GateWords.Any(w => m.Name.ToLowerInvariant().Contains(w))) return false;
        return ReturnsFalse(m);
    }

    // True when the body is just "load 0, return" (return false), ignoring a leading nop the compiler
    // sometimes emits. That is exactly the shipped Pixler gate.
    private static bool ReturnsFalse(MethodDefinition m)
    {
        var body = m.Body.Instructions.Where(i => i.OpCode != OpCodes.Nop).ToList();
        return body.Count == 2
            && (body[0].OpCode == OpCodes.Ldc_I4_0
                || (body[0].OpCode == OpCodes.Ldc_I4 && body[0].Operand is int n && n == 0))
            && body[1].OpCode == OpCodes.Ret;
    }

    // Walk top-level and nested types, because the WinForms targets keep their code in nested classes.
    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var n in AllTypes(t.NestedTypes)) yield return n;
        }
    }
}
