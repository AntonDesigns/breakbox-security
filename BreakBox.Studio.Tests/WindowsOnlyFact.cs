// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using Xunit;

namespace BreakBox.Tests;

// The targets are WinForms desktop apps, so the generator only compiles them on Windows. A test that
// builds a target through the generator is therefore Windows-only: on Linux it reports as skipped
// (honestly, not a silent pass). The security CI already runs on windows-latest, so these run there.
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows-only: the targets are WinForms apps and are generated only on Windows.";
    }
}
