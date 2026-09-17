// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.IO;
using Mono.Cecil;

namespace BreakBox.Studio.Engines;

// My keygen for Level 2. This is the step up from Level 1. In Level 1 the key is a stored string, so
// Peek finds it. In Level 2 the serial is NOT stored anywhere: the program computes it from a Seed
// with an algorithm and compares my input to the result. Reading strings gets me nothing. So I do
// what a keygen author does: I recover the Seed from the binary and run the SAME algorithm forward,
// which lets me generate a valid serial on demand.
//
// Security lesson I take to Bosch: hiding a value behind an algorithm that ships with the program is
// weak, because the algorithm is right there to be read and reproduced. The only real fix is to not
// depend on a client-side secret at all: do the check on a server the attacker does not control.
public static class Level2Keygen
{
    // The reverse, broken into the pieces my Keygen tool walks the user through: the Seed I recovered
    // from the binary, and the serial that Seed produces.
    public readonly record struct Reverse(int Seed, long Serial);

    // The algorithm in ONE place. It must match Expected() inside level2.template exactly, because a
    // keygen is only "correct" when it reproduces the target's own rule, not an approximation of it.
    public static long SerialFromSeed(int seed) => (long)seed * 31 + 1337;

    // Recover the Seed constant from the assembly with Mono.Cecil and reproduce the serial. Cecil
    // reads the field's baked-in constant value straight from the metadata: no need to run anything.
    // Returns null when there is no Seed field, i.e. this is not a Level 2 target.
    public static Reverse? Explain(byte[] assembly)
    {
        using var ms = new MemoryStream(assembly);
        using var module = ModuleDefinition.ReadModule(ms);

        foreach (var type in module.Types)
        foreach (var field in type.Fields)
            if (field.Name == "Seed" && field.HasConstant && field.Constant is int seed)
                return new Reverse(seed, SerialFromSeed(seed));

        return null;
    }

    // Just the serial, for tests and scripts that do not need the breakdown.
    public static long? SerialFor(byte[] assembly) => Explain(assembly)?.Serial;
}
