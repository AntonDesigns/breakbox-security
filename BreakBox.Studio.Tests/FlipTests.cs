// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BreakBox.Studio.Engines;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace BreakBox.Tests;

// Flip is the Level 3 crack as my own tool. These prove the engine on a tiny stand-in for Pixler's
// gate, so the test needs no generated target: I build an assembly with a bool IsPremium() that
// returns false, exactly like the shipped Pixler, then flip it and load the result.
public sealed class FlipTests
{
    // Build a minimal assembly with one static bool method whose body is "return <value>".
    private static byte[] GateAssembly(string method, bool returns)
    {
        var asm = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Pixlerish", new Version(1, 0, 0, 0)), "Pixlerish", ModuleKind.Dll);
        var module = asm.MainModule;
        var type = new TypeDefinition("Pixlerish", "Program",
            Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed,
            module.TypeSystem.Object);
        module.Types.Add(type);

        var m = new MethodDefinition(method,
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Boolean);
        var il = m.Body.GetILProcessor();
        il.Append(il.Create(returns ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
        il.Append(il.Create(OpCodes.Ret));
        type.Methods.Add(m);

        using var ms = new MemoryStream();
        asm.Write(ms);
        return ms.ToArray();
    }

    private static bool Invoke(byte[] assembly, string method)
    {
        var loaded = Assembly.Load(assembly);
        var m = loaded.GetType("Pixlerish.Program")!.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!;
        return (bool)m.Invoke(null, null)!;
    }

    [Fact]
    public void Flip_rewrites_the_gate_so_it_returns_true()
    {
        var shipped = GateAssembly("IsPremium", returns: false);
        Assert.False(Invoke(shipped, "IsPremium"));   // as shipped, the gate is closed

        var result = FlipPatcher.Flip(shipped);

        Assert.NotNull(result);
        Assert.Contains("IsPremium", result!.Method);
        Assert.Contains(result.Before, i => i.Contains("ldc.i4.0"));  // was: push 0 (false)
        Assert.Contains(result.After, i => i.Contains("ldc.i4.1"));   // now: push 1 (true)
        Assert.True(Invoke(result.Patched, "IsPremium"));             // the patched assembly is valid and unlocked
    }

    [Fact]
    public void Flip_finds_nothing_when_the_gate_is_not_a_returns_false_check()
    {
        // A gate that already returns true is not the shipped Pixler gate, so there is nothing to flip.
        Assert.Null(FlipPatcher.Flip(GateAssembly("IsPremium", returns: true)));
        // A bool method not named like a licence check is left alone.
        Assert.Null(FlipPatcher.Flip(GateAssembly("Compute", returns: false)));
    }
}
