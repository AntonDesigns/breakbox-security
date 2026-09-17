// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using BreakBox.Core;
using BreakBox.Studio.Engines;

namespace BreakBox.Studio;

public partial class MainWindow : Window
{
    // The engine is the same class the tests exercise. The window is only a face over it.
    private readonly IAssemblyInspector _inspector = new CecilAssemblyInspector();

    // Palette, matching App.axaml, for the controls I build in code.
    private static readonly IBrush PanelBr = new SolidColorBrush(Color.Parse("#13161b"));
    private static readonly IBrush LineBr = new SolidColorBrush(Color.Parse("#262c34"));
    private static readonly IBrush TextBr = new SolidColorBrush(Color.Parse("#cdd3da"));
    private static readonly IBrush DimBr = new SolidColorBrush(Color.Parse("#7c8590"));
    private static readonly IBrush AmberBr = new SolidColorBrush(Color.Parse("#e0a458"));
    private static readonly IBrush GreenBr = new SolidColorBrush(Color.Parse("#6cc07a"));
    private static readonly IBrush RedBr = new SolidColorBrush(Color.Parse("#c76b6b"));

    // Keygen state: the Seed I recovered, the serial it produces, and how far the walkthrough is.
    private int? _seed;
    private long _serial;
    private int _step;

    public MainWindow()
    {
        InitializeComponent();
        Select("peek");
    }

    // ---- navigation ----
    private void OnNav(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tool) Select(tool);
    }

    private void Select(string tool)
    {
        PeekPanel.IsVisible = tool == "peek";
        KeygenPanel.IsVisible = tool == "keygen";
        SetActive(NavPeek, tool == "peek");
        SetActive(NavKeygen, tool == "keygen");
    }

    private static void SetActive(Button b, bool active)
    {
        if (active) { if (!b.Classes.Contains("active")) b.Classes.Add("active"); }
        else b.Classes.Remove("active");
    }

    // ---- Peek: read only inspection, never runs the file ----
    private async void OnOpenDll(object? sender, RoutedEventArgs e)
    {
        var file = await PickAssembly("Choose a .NET assembly");
        if (file is null) return;

        PeekTarget.Text = file.Name;
        try { PeekResults.Text = Format(_inspector.Inspect(await ReadBytes(file))); }
        catch (Exception ex) { PeekResults.Text = $"Could not read this file as a .NET assembly.\n\n{ex.Message}"; }
    }

    private static string Format(InspectionResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Methods ({r.Methods.Count}):");
        foreach (var m in r.Methods) sb.AppendLine($"  {m}");
        sb.AppendLine();
        sb.AppendLine($"Strings in the code ({r.Strings.Count}):");
        foreach (var s in r.Strings) sb.AppendLine($"  {s.Type}.{s.Method}: \"{s.Value}\"");
        sb.AppendLine();
        sb.AppendLine("Likely check methods (start here):");
        foreach (var c in r.LikelyChecks) sb.AppendLine($"  {c}");
        return sb.ToString();
    }

    // ---- Keygen: walk the reverse, let the user compute it, reveal on demand ----
    private async void OnOpenLevel2(object? sender, RoutedEventArgs e)
    {
        var file = await PickAssembly("Choose the Level 2 target (Level2.dll)");
        if (file is null) return;

        KgTarget.Text = file.Name;
        KgSteps.Children.Clear();
        _seed = null;

        try
        {
            var rev = Level2Keygen.Explain(await ReadBytes(file));
            if (rev is null)
            {
                KgSteps.Children.Add(Card("not a level 2 target",
                    "I could not find a Seed constant in this file. Open a Level 2 download (Level2.dll).", RedBr));
                return;
            }

            _seed = rev.Value.Seed;
            _serial = rev.Value.Serial;
            _step = 0;
            ShowKeygen();
        }
        catch (Exception ex)
        {
            KgSteps.Children.Add(Card("could not read the file", ex.Message, RedBr));
        }
    }

    private void ShowKeygen()
    {
        KgSteps.Children.Clear();
        if (_seed is null) return;
        int seed = _seed.Value;

        KgSteps.Children.Add(Card("1 / the problem",
            "The serial is not stored anywhere in Level 2. Peeking the strings will not reveal it, because the " +
            "program computes the serial from a Seed and only compares my input to the result. So I have to reverse the algorithm."));

        if (_step >= 1)
            KgSteps.Children.Add(Card("2 / recover the seed",
                $"I searched the assembly's metadata for a constant named Seed. Found it:  Seed = {seed}.  " +
                "Mono.Cecil read that value straight out of the binary, without running the program."));

        if (_step >= 2)
            KgSteps.Children.Add(Card("3 / the rule",
                $"The check computes the serial with this rule (it is Expected() inside the target):\n\n" +
                $"    serial = Seed * 31 + 1337\n    serial = {seed} * 31 + 1337\n\n" +
                "That is the whole algorithm. A keygen just runs it forward."));

        if (_step == 3)
            KgSteps.Children.Add(ComputeCard(seed));

        if (_step >= 4)
            KgSteps.Children.Add(Card("5 / the serial",
                $"Valid serial:  {_serial}\n\nRun Level2.exe, type this in, and it unlocks. You reproduced the " +
                "program's own rule: that is a keygen. The real lesson is that an algorithm which ships with the " +
                "program is not a secret.", GreenBr));

        if (_step < 3)
            KgSteps.Children.Add(NextButton(_step == 0 ? "Next: recover the seed" : _step == 1 ? "Next: the rule" : "Next: your turn"));
    }

    private Control ComputeCard(int seed)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "4 / your turn", Foreground = AmberBr, FontSize = 11, FontWeight = FontWeight.Bold });
        panel.Children.Add(Body($"Work out {seed} * 31 + 1337 and type the serial. Or reveal it if you would rather just see it."));

        var input = new TextBox { Watermark = "your serial", Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        var result = new TextBlock { Foreground = DimBr, TextWrapping = TextWrapping.Wrap };

        var check = new Button { Content = "Check" };
        check.Classes.Add("primary");
        check.Click += (_, _) =>
        {
            if (long.TryParse((input.Text ?? "").Trim(), out var n))
            {
                if (n == _serial) { result.Text = "Correct. You reversed it yourself."; result.Foreground = GreenBr; }
                else { result.Text = "Not the serial yet. Check the arithmetic, or reveal it."; result.Foreground = RedBr; }
            }
            else { result.Text = "Type a number."; result.Foreground = RedBr; }
        };

        var reveal = new Button { Content = "Reveal serial" };
        reveal.Click += (_, _) => { _step = 4; ShowKeygen(); };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(input);
        row.Children.Add(check);
        row.Children.Add(reveal);

        panel.Children.Add(row);
        panel.Children.Add(result);
        return Shell(panel, null);
    }

    private Button NextButton(string label)
    {
        var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        b.Classes.Add("primary");
        b.Click += (_, _) => { _step++; ShowKeygen(); };
        return b;
    }

    // ---- little UI helpers ----
    private Border Card(string title, string body, IBrush? accent = null)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, Foreground = accent ?? AmberBr, FontSize = 11, FontWeight = FontWeight.Bold });
        panel.Children.Add(Body(body));
        return Shell(panel, accent);
    }

    private Border Shell(Control content, IBrush? accent) => new()
    {
        Background = PanelBr,
        BorderBrush = accent ?? LineBr,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(14, 12, 14, 14),
        Child = content
    };

    private static TextBlock Body(string s) => new() { Text = s, Foreground = TextBr, TextWrapping = TextWrapping.Wrap };

    // ---- shared file access ----
    private async Task<IStorageFile?> PickAssembly(string title)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(".NET assembly") { Patterns = new[] { "*.dll", "*.exe" } } }
        });
        return files.Count > 0 ? files[0] : null;
    }

    private static async Task<byte[]> ReadBytes(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
