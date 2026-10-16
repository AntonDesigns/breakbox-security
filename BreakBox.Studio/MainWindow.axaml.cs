// BreakBox. Written by Max-Anton Horvat. Complex Software Systems (S6).
// Signature 0x4D414836 = "MAH6" in ASCII (my initials + semester 6). I wrote this.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using BreakBox.Studio.Engines;

namespace BreakBox.Studio;

public partial class MainWindow : Window
{
    // Palette, matching App.axaml, for the controls I build in code.
    private static readonly IBrush PanelBr = new SolidColorBrush(Color.Parse("#13161b"));
    private static readonly IBrush LineBr = new SolidColorBrush(Color.Parse("#262c34"));
    private static readonly IBrush TextBr = new SolidColorBrush(Color.Parse("#cdd3da"));
    private static readonly IBrush DimBr = new SolidColorBrush(Color.Parse("#7c8590"));
    // A calm, desaturated steel accent, not a neon amber: the de-vibe keeps the dark toolkit look
    // without the warm glow. Matches App.axaml's Accent.
    private static readonly IBrush AccentBr = new SolidColorBrush(Color.Parse("#7e96b3"));
    private static readonly IBrush GreenBr = new SolidColorBrush(Color.Parse("#86ab8d"));
    private static readonly IBrush RedBr = new SolidColorBrush(Color.Parse("#c76b6b"));
    private static readonly IBrush Panel2Br = new SolidColorBrush(Color.Parse("#10141a"));
    private static readonly IBrush CodeBg = new SolidColorBrush(Color.Parse("#0c0f14"));
    private static readonly IBrush ChipBg = new SolidColorBrush(Color.Parse("#141f1b"));
    private static readonly IBrush ChipEdge = new SolidColorBrush(Color.Parse("#26392f"));
    private static readonly IBrush CalloutEdge = new SolidColorBrush(Color.Parse("#2e3a46"));

    // File-picker glob patterns, as static fields so the filters are not rebuilt on every open/save.
    private static readonly string[] DllPattern = { "*.dll" };
    private static readonly string[] DllExePattern = { "*.dll", "*.exe" };

    // Keygen state: the Seed I recovered, the serial it produces, and how far the walkthrough is.
    private int? _seed;
    private long _serial;
    private int _step;

    // Flip state: the last patch result (the found gate and the patched bytes) and the source file's
    // base name, for the suggested save name.
    private FlipResult? _flip;
    private string _flipBaseName = "target";

    // The tree row for the first likely check method, so opening a target lands you on it.
    private TreeViewItem? _checkItem;

    public MainWindow() : this(null) { }

    public MainWindow(string[]? args)
    {
        InitializeComponent();
        Select("peek");

        // Open a target straight away when one is passed on the command line ("BreakBox path\to.dll"),
        // so the tool can be launched onto a file. "--flip path" opens it in Flip instead of Peek. Done
        // after the window loads so the controls exist.
        var path = args?.FirstOrDefault(a =>
            File.Exists(a) && (a.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                            || a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));
        var intoFlip = args?.Contains("--flip") == true;
        if (path is not null)
            Loaded += async (_, _) => { if (intoFlip) { Select("flip"); await FlipPath(path); } else await LoadPath(path); };
    }

    private async Task LoadPath(string path)
    {
        try { LoadAssembly(Path.GetFileName(path), await File.ReadAllBytesAsync(path)); }
        catch (Exception ex) { ShowLoadError(ex.Message); }
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
        FlipPanel.IsVisible = tool == "flip";
        SetActive(NavPeek, tool == "peek");
        SetActive(NavKeygen, tool == "keygen");
        SetActive(NavFlip, tool == "flip");
    }

    private static void SetActive(Button b, bool active)
    {
        if (active) { if (!b.Classes.Contains("active")) b.Classes.Add("active"); }
        else b.Classes.Remove("active");
    }

    // ---- Peek: read only X-ray, never runs the file ----
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, monospace");
    private static readonly IBrush CheckBg = new SolidColorBrush(Color.Parse("#1b222b"));

    private async void OnOpenDll(object? sender, RoutedEventArgs e)
    {
        var file = await PickAssembly("Choose a .NET assembly");
        if (file is null) return;
        LoadAssembly(file.Name, await ReadBytes(file));
    }

    // Read a target's structure and fill the tree, the stats bar and the detail pane. Shared by the
    // file picker and the command-line path.
    private void LoadAssembly(string name, byte[] bytes)
    {
        PeekTarget.Text = name;
        PeekTree.Items.Clear();
        PeekDetail.Children.Clear();
        PeekStats.Children.Clear();
        _checkItem = null;

        try
        {
            var map = AssemblyMap.Read(bytes);

            var typeCount = map.Types.Sum(CountTypes);

            PeekStats.Children.Add(StatPill(typeCount, typeCount == 1 ? "type" : "types", AccentBr));
            PeekStats.Children.Add(StatPill(map.MethodCount, "methods", TextBr));
            PeekStats.Children.Add(StatPill(map.StringCount, "strings", GreenBr));

            foreach (var t in map.Types) PeekTree.Items.Add(MakeTypeItem(t));

            // Land on the likely check method, since that is what a cracker reads first.
            if (_checkItem is not null)
                _checkItem.IsSelected = true;
            else
                PeekDetail.Children.Add(new TextBlock
                {
                    Text = "Pick a method from the tree. The one marked check is where the licence decision lives.",
                    Foreground = DimBr,
                    TextWrapping = TextWrapping.Wrap,
                });
        }
        catch (Exception ex)
        {
            ShowLoadError(ex.Message);
        }
    }

    private void ShowLoadError(string message)
    {
        PeekStats.Children.Clear();
        PeekDetail.Children.Clear();
        PeekDetail.Children.Add(new TextBlock
        {
            Text = $"Could not read this file as a .NET assembly.\n\n{message}",
            Foreground = RedBr,
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private TreeViewItem MakeTypeItem(MapType type)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new TextBlock { Text = type.Name, Foreground = TextBr, FontWeight = FontWeight.SemiBold });
        if (type.Methods.Count > 0)
            head.Children.Add(new TextBlock
            {
                Text = type.Methods.Count.ToString(), Foreground = DimBr, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            });

        var item = new TreeViewItem { Header = head, IsExpanded = true };
        foreach (var m in type.Methods) item.Items.Add(MakeMethodItem(m));
        foreach (var n in type.Nested) item.Items.Add(MakeTypeItem(n));
        return item;
    }

    private TreeViewItem MakeMethodItem(MapMethod m)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = "ƒ", Foreground = DimBr, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = m.Name, Foreground = m.LikelyCheck ? AccentBr : TextBr, VerticalAlignment = VerticalAlignment.Center });
        if (m.LikelyCheck)
            row.Children.Add(new Border
            {
                Background = CheckBg,
                BorderBrush = CalloutEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 0, 6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "check", Foreground = AccentBr, FontSize = 10, FontWeight = FontWeight.Bold },
            });
        var item = new TreeViewItem { Header = row, Tag = m };
        if (m.LikelyCheck) _checkItem ??= item;
        return item;
    }

    private void OnMethodSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (PeekTree.SelectedItem is TreeViewItem { Tag: MapMethod m }) ShowMethod(m);
    }

    private void ShowMethod(MapMethod m)
    {
        PeekDetail.Children.Clear();

        PeekDetail.Children.Add(new TextBlock
        {
            Text = m.Name, Foreground = m.LikelyCheck ? AccentBr : TextBr, FontSize = 18, FontWeight = FontWeight.Bold,
        });
        PeekDetail.Children.Add(new TextBlock
        {
            Text = $"{m.Il.Count} instructions  ·  {m.Strings.Count} strings",
            Foreground = DimBr, FontSize = 12,
        });

        if (m.LikelyCheck)
            PeekDetail.Children.Add(Callout(
                "This looks like the licence check. Read what it compares against: that value is the answer, or the algorithm that computes it."));

        if (m.Strings.Count > 0)
        {
            PeekDetail.Children.Add(SectionLabel($"STRINGS ({m.Strings.Count})"));
            var chips = new StackPanel { Spacing = 6 };
            foreach (var s in m.Strings) chips.Children.Add(Chip(s));
            PeekDetail.Children.Add(chips);
        }

        PeekDetail.Children.Add(SectionLabel($"IL ({m.Il.Count} instructions)"));
        if (m.Il.Count == 0)
            PeekDetail.Children.Add(new TextBlock { Text = "no body (abstract, or declared elsewhere)", Foreground = DimBr, FontSize = 12 });
        else
            PeekDetail.Children.Add(IlBlock(m.Il));
    }

    // Count every type in the map, nested ones included. A real method (not a captured local) so the
    // analyzer can follow the count and the "1 type / N types" label is never seen as dead code.
    private static int CountTypes(MapType t) => 1 + t.Nested.Sum(CountTypes);

    // A rounded count pill for the stats bar: the number in an accent colour, the label dimmed.
    private static Border StatPill(int count, string label, IBrush accent)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(new TextBlock { Text = count.ToString(), Foreground = accent, FontWeight = FontWeight.Bold, FontFamily = Mono });
        row.Children.Add(new TextBlock { Text = label, Foreground = DimBr });
        return new Border
        {
            Background = Panel2Br, BorderBrush = LineBr, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999), Padding = new Thickness(12, 5), Child = row,
        };
    }

    // The amber callout above a check method, so the important method stands out in the detail pane.
    private static Border Callout(string text) => new()
    {
        Background = CheckBg, BorderBrush = CalloutEdge, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8), Padding = new Thickness(13, 11),
        Child = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "LIKELY CHECK", Foreground = AccentBr, FontSize = 10, FontWeight = FontWeight.Bold },
                new TextBlock { Text = text, Foreground = TextBr, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 },
            },
        },
    };

    // Each string literal as a green, monospace chip.
    private static Border Chip(string s) => new()
    {
        Background = ChipBg, BorderBrush = ChipEdge, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6),
        Child = new TextBlock
        {
            Text = "“" + s + "”", Foreground = GreenBr, FontFamily = Mono, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
        },
    };

    // The decoded IL in an editor-style card, each instruction numbered in a gutter.
    private static Border IlBlock(IReadOnlyList<string> il)
    {
        int w = il.Count.ToString().Length;
        var text = string.Join("\n", il.Select((ins, i) => (i + 1).ToString().PadLeft(w) + "   " + ins));
        var box = new TextBox
        {
            Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = TextBr, FontFamily = Mono, FontSize = 12.5, Padding = new Thickness(0),
        };
        return new Border
        {
            Background = CodeBg, BorderBrush = LineBr, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12), Child = box,
        };
    }

    private static TextBlock SectionLabel(string s) => new()
    {
        Text = s, Foreground = DimBr, FontSize = 11, FontWeight = FontWeight.Bold,
        LetterSpacing = 1.2, Margin = new Thickness(0, 6, 0, 0),
    };

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
        {
            string next = "Next: your turn";
            if (_step == 0) next = "Next: recover the seed";
            else if (_step == 1) next = "Next: the rule";
            KgSteps.Children.Add(NextButton(next));
        }
    }

    private Border ComputeCard(int seed)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "4 / your turn", Foreground = AccentBr, FontSize = 11, FontWeight = FontWeight.Bold });
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

    // ---- Flip: patch the client-side gate so it always returns true ----
    private async Task FlipPath(string path)
    {
        try
        {
            FlipTarget.Text = Path.GetFileName(path);
            _flipBaseName = Path.GetFileNameWithoutExtension(path);
            _flip = FlipPatcher.Flip(await File.ReadAllBytesAsync(path));
            ShowFlip();
        }
        catch (Exception ex)
        {
            _flip = null;
            FlipDetail.Children.Clear();
            FlipDetail.Children.Add(Card("could not read the file", ex.Message, RedBr));
        }
    }

    private async void OnOpenFlipDll(object? sender, RoutedEventArgs e)
    {
        var file = await PickAssembly("Choose a .NET assembly to patch");
        if (file is null) return;

        FlipTarget.Text = file.Name;
        _flipBaseName = Path.GetFileNameWithoutExtension(file.Name);
        FlipDetail.Children.Clear();

        try
        {
            _flip = FlipPatcher.Flip(await ReadBytes(file));
            ShowFlip();
        }
        catch (Exception ex)
        {
            _flip = null;
            FlipDetail.Children.Add(Card("could not read the file", ex.Message, RedBr));
        }
    }

    private void ShowFlip()
    {
        FlipDetail.Children.Clear();
        if (_flip is null)
        {
            FlipDetail.Children.Add(Card("no gate to flip",
                "I could not find a client-side gate here. Flip targets a bool method named like a licence " +
                "check (for example IsPremium) whose body just returns false, which is the Level 3 gate. Open " +
                "a Pixler target (Level3.dll).", RedBr));
            return;
        }

        var f = _flip;

        var head = new StackPanel { Spacing = 6 };
        head.Children.Add(new TextBlock { Text = "THE GATE", Foreground = AccentBr, FontSize = 11, FontWeight = FontWeight.Bold });
        head.Children.Add(new TextBlock { Text = f.Method, Foreground = TextBr, FontFamily = Mono, FontSize = 16, FontWeight = FontWeight.Bold });
        head.Children.Add(Body("This method decides whether the premium features are unlocked. In the shipped build it " +
            "returns false. Flip rewrites its body to return true and saves a valid assembly."));
        FlipDetail.Children.Add(Shell(head, AccentBr));

        FlipDetail.Children.Add(SectionLabel("BEFORE  ·  returns false"));
        FlipDetail.Children.Add(IlBlock(f.Before));
        FlipDetail.Children.Add(SectionLabel("AFTER  ·  returns true"));
        FlipDetail.Children.Add(IlBlock(f.After));

        var save = new Button { Content = "Patch and save…", HorizontalAlignment = HorizontalAlignment.Left };
        save.Classes.Add("primary");
        save.Click += async (_, _) => await SavePatched();
        FlipDetail.Children.Add(save);
    }

    private async Task SavePatched()
    {
        if (_flip is null) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the patched assembly",
            SuggestedFileName = _flipBaseName + ".patched.dll",
            DefaultExtension = "dll",
            FileTypeChoices = new[] { new FilePickerFileType(".NET assembly") { Patterns = DllPattern } },
        });
        if (file is null) return;

        await using (var stream = await file.OpenWriteAsync())
            await stream.WriteAsync(_flip.Patched);

        FlipDetail.Children.Insert(0, Card("patched and saved",
            $"Saved {file.Name}. The gate now returns true, so the premium features unlock when you run it. " +
            "Open it in Peek to confirm the method changed. The real fix is to never ship that check: keep it " +
            "on the server, where the attacker cannot rewrite it.", GreenBr));
    }

    // ---- little UI helpers ----
    private Border Card(string title, string body, IBrush? accent = null)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, Foreground = accent ?? AccentBr, FontSize = 11, FontWeight = FontWeight.Bold });
        panel.Children.Add(Body(body));
        return Shell(panel, accent);
    }

    private static Border Shell(Control content, IBrush? accent) => new()
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
            FileTypeFilter = new[] { new FilePickerFileType(".NET assembly") { Patterns = DllExePattern } }
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
