// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.IO.Compression;
using System.Reflection;
using BreakBox.Core;
using BreakBox.Generator;
using BreakBox.Studio.Engines;
using Xunit;

namespace BreakBox.Tests;

public sealed class Level2Tests
{
    private static RoslynChallengeProvider Provider() =>
        new(new LevelCatalog(), new EmbeddedTemplateSource(), new RandomKeyMaker(), new RoslynChallengeCompiler());

    private static byte[] ExtractDll(byte[] zipBytes, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(zipBytes));
        using var s = zip.GetEntry(name)!.Open();
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    [WindowsOnlyFact]
    public void Build_level2_returns_a_numeric_serial()
    {
        var c = Provider().Build(2);
        Assert.Equal("challenge_2.zip", c.FileName);
        Assert.True(long.TryParse(c.ValidKey, out _));
    }

    // The keygen reads the Seed out of the compiled target and reproduces the serial. If it matches
    // the answer the generator baked in, my keygen has correctly reversed the algorithm.
    [WindowsOnlyFact]
    public void Keygen_reproduces_the_serial_from_the_dll()
    {
        var c = Provider().Build(2);
        var serial = Level2Keygen.SerialFor(ExtractDll(c.Bytes, "Level2.dll"));

        Assert.NotNull(serial);
        Assert.Equal(c.ValidKey, serial!.Value.ToString());
    }

    [WindowsOnlyFact]
    public void Generated_level2_unlocks_with_the_keygen_serial()
    {
        var c = Provider().Build(2);
        var dir = Directory.CreateTempSubdirectory().FullName;
        using (var zip = new ZipArchive(new MemoryStream(c.Bytes)))
            zip.ExtractToDirectory(dir);

        // The target is a GUI app now, so I check the serial logic headlessly instead of driving a
        // window: load the compiled dll and invoke its private IsValid. The keygen's serial passes;
        // a wrong one fails. IsValid touches no WinForms type, so no UI spins up here.
        var asm = Assembly.LoadFile(Path.Combine(dir, "Level2.dll"));
        var program = asm.GetType("Level2.Program")!;
        var isValid = program.GetMethod("IsValid", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.True((bool)isValid.Invoke(null, new object[] { c.ValidKey })!);
        Assert.False((bool)isValid.Invoke(null, new object[] { "0" })!);
    }
}
