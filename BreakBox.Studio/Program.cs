// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.
//
// The BreakBox toolkit as a real desktop application (Avalonia). It reuses the same engines
// the console tools use, so this is a new face over the SOLID core, not a rewrite.

using Avalonia;
using System;

namespace BreakBox.Studio;

internal static class Program
{
    // Avalonia needs an STA thread and its own start-up; nothing Avalonia-specific runs before this.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
