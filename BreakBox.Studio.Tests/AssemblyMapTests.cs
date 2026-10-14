// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System.IO.Compression;
using System.Linq;
using BreakBox.Core;
using BreakBox.Generator;
using BreakBox.Studio.Engines;
using Xunit;

namespace BreakBox.Tests;

// The X-ray engine behind the Studio's tree view. These check it builds the real shape: nested form
// types, the check method with decoded IL, and Level 2's algorithm methods. Windows-only, because
// building a target is Windows-only.
public sealed class AssemblyMapTests
{
    private static byte[] BuildDll(int level)
    {
        var provider = new RoslynChallengeProvider(
            new LevelCatalog(), new EmbeddedTemplateSource(), new RandomKeyMaker(), new RoslynChallengeCompiler());
        var challenge = provider.Build(level);

        using var zip = new ZipArchive(new MemoryStream(challenge.Bytes));
        using var entry = zip.GetEntry($"Level{level}.dll")!.Open();
        using var ms = new MemoryStream();
        entry.CopyTo(ms);
        return ms.ToArray();
    }

    private static MapMethod? Find(AssemblyMapResult map, string method)
    {
        MapMethod? found = null;
        void Walk(MapType t)
        {
            foreach (var m in t.Methods) if (m.Name == method) found = m;
            foreach (var n in t.Nested) Walk(n);
        }
        foreach (var t in map.Types) Walk(t);
        return found;
    }

    [WindowsOnlyFact]
    public void Level1_map_has_nested_forms_and_isvalid_with_il()
    {
        var map = AssemblyMap.Read(BuildDll(1));

        Assert.DoesNotContain(map.Types, t => t.Name.Contains('<'));   // no compiler-generated types
        Assert.Contains(map.Types, t => t.Nested.Count > 0);           // the forms are nested inside Program

        var isValid = Find(map, "IsValid");
        Assert.NotNull(isValid);
        Assert.True(isValid!.LikelyCheck);
        Assert.NotEmpty(isValid.Il);                                    // the IL was decoded
    }

    [WindowsOnlyFact]
    public void Level2_map_exposes_the_algorithm_methods()
    {
        var map = AssemblyMap.Read(BuildDll(2));

        Assert.NotNull(Find(map, "IsValid"));
        Assert.NotNull(Find(map, "Expected"));   // the serial algorithm is a method you can read
        Assert.True(map.MethodCount > 0);
        Assert.True(map.StringCount > 0);
    }
}
