// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.IO.Compression;
using System.Linq;
using BreakBox.Core;
using BreakBox.Generator;
using BreakBox.Studio.Engines;
using Xunit;

namespace BreakBox.Tests;

public sealed class PeekTests
{
    // Peek should find, inside a freshly generated Level 1 target, the prompt strings and the
    // method that does the check. This proves my inspector works on a real compiled assembly.
    [WindowsOnlyFact]
    public void Peek_finds_the_strings_and_the_check_in_a_generated_target()
    {
        var provider = new RoslynChallengeProvider(new LevelCatalog(), new EmbeddedTemplateSource(),
            new RandomKeyMaker(), new RoslynChallengeCompiler());
        var challenge = provider.Build(1);

        byte[] dll;
        using (var zip = new ZipArchive(new MemoryStream(challenge.Bytes)))
        {
            using var entryStream = zip.GetEntry("Level1.dll")!.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            dll = buffer.ToArray();
        }

        var result = new CecilAssemblyInspector().Inspect(dll);

        // The NoteBox target ships its prompt ("Enter your licence key") and its error ("Invalid
        // licence key...") as plain string literals, so Peek reads them straight out of the IL. And
        // IsValid is named like a check, so it lands in LikelyChecks: exactly the method to read.
        Assert.Contains(result.Strings, h => h.Value.Contains("licence key"));
        Assert.Contains(result.Strings, h => h.Value.Contains("Invalid"));
        Assert.Contains(result.LikelyChecks, c => c.Contains("IsValid"));
    }
}
