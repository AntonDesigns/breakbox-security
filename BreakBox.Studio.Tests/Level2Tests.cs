// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.Diagnostics;
using System.IO.Compression;
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

    [Fact]
    public void Build_level2_returns_a_numeric_serial()
    {
        var c = Provider().Build(2);
        Assert.Equal("challenge_2.zip", c.FileName);
        Assert.True(long.TryParse(c.ValidKey, out _));
    }

    // The keygen reads the Seed out of the compiled target and reproduces the serial. If it matches
    // the answer the generator baked in, my keygen has correctly reversed the algorithm.
    [Fact]
    public void Keygen_reproduces_the_serial_from_the_dll()
    {
        var c = Provider().Build(2);
        var serial = Level2Keygen.SerialFor(ExtractDll(c.Bytes, "Level2.dll"));

        Assert.NotNull(serial);
        Assert.Equal(c.ValidKey, serial!.Value.ToString());
    }

    [Fact]
    public void Generated_level2_unlocks_with_the_keygen_serial()
    {
        var c = Provider().Build(2);
        var dir = Directory.CreateTempSubdirectory().FullName;
        using (var zip = new ZipArchive(new MemoryStream(c.Bytes)))
            zip.ExtractToDirectory(dir);

        var psi = new ProcessStartInfo("dotnet", $"\"{Path.Combine(dir, "Level2.dll")}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        p.StandardInput.WriteLine(c.ValidKey);
        p.StandardInput.WriteLine();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(20000);

        Assert.Contains("Premium unlocked", output);
    }
}
