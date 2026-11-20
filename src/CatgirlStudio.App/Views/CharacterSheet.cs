using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Views;

internal interface ISyncView
{
    void Sync();
}

/// <summary>The character sheet: one row per variable the prompt reaches, grouped like the
/// catalog, with the outfit editor in "Roupa". Rows are rebuilt only when the set of fields
/// changes (a new pose, a piece that opens other fields); otherwise they update in place.</summary>
internal sealed class CharacterSheet : UserControl
{
    readonly Workbench wb;
    readonly StackPanel stack = new();
    readonly ScrollViewer scroll;
    List<string> shown = [];
    readonly List<ISyncView> rows = [];

    public CharacterSheet(Workbench wb)
    {
        this.wb = wb;
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = false };
        head.Children.Add(Ui.Text("Personagem e cena", "H2"));
        var hint = Ui.Text("sorteia ou trava cada campo pelos botões à direita", "Small", new Thickness(12, 4, 0, 0));
        head.Children.Add(hint);

        scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0) };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(scroll, 1);
        grid.Children.Add(head);
        grid.Children.Add(scroll);
        Content = grid;

        wb.Changed += Sync;
        Sync();
    }

    void Sync()
    {
        var used = wb.Result.Used;
        var desired = new List<string>();
        foreach (var group in wb.Catalog.Groups)
        {
            var names = wb.Catalog.Vars.Values.Where(v => v.Group == group && used.Contains(v.Key)).Select(v => v.Key).ToList();
            if (group == "Roupa" && used.Contains(OutfitEngine.Var)) names.Insert(0, OutfitEngine.Var);
            if (names.Count == 0) continue;
            desired.Add("#" + group);
            desired.AddRange(names);
        }
        if (desired.SequenceEqual(shown))
        {
            foreach (var r in rows) r.Sync();
            return;
        }
        var offset = scroll.VerticalOffset;
        stack.Children.Clear();
        rows.Clear();
        foreach (var key in desired)
        {
            if (key.StartsWith('#'))
            {
                stack.Children.Add(new TextBlock { Text = key[1..].ToUpperInvariant(), Style = Ui.Style("Legend"), Margin = new Thickness(0, stack.Children.Count == 0 ? 4 : 18, 0, 10) });
                continue;
            }
            ISyncView row = key == OutfitEngine.Var ? new OutfitEditor(wb) : wb.Catalog.Vars[key].Multi ? new MultiField(wb, wb.Catalog.Vars[key]) : new SingleField(wb, wb.Catalog.Vars[key]);
            rows.Add(row);
            stack.Children.Add((UIElement)row);
        }
        shown = desired;
        scroll.ScrollToVerticalOffset(offset);
    }

    /// <summary>A closed drop-down passes the mouse wheel on, so scrolling the sheet never changes a field.</summary>
    public static void PassWheel(ComboBox c)
    {
        c.PreviewMouseWheel += (_, e) =>
        {
            if (c.IsDropDownOpen) return;
            e.Handled = true;
            var parent = VisualTreeHelper.GetParent(c) as UIElement;
            parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = c });
        };
    }
}

/// <summary>A single-choice field: option list (or a typed "Personalizado" value), dice and lock.</summary>
internal sealed class SingleField : ContentControl, ISyncView
{
    readonly Workbench wb;
    readonly VarDef def;
    readonly ComboBox combo;
    readonly TextBox custom;
    readonly ToggleButton lockButton;
    readonly TextBlock label;
    bool syncing;

    public SingleField(Workbench wb, VarDef def)
    {
        this.wb = wb;
        this.def = def;
        combo = Ui.Combo(def.Options.Cast<object>().Append(PromptEngine.Custom), o => o is OptionDef d ? d.Pt : "Personalizado (escrever em inglês)…");
        CharacterSheet.PassWheel(combo);
        combo.SelectionChanged += (_, _) =>
        {
            if (syncing || combo.SelectedItem is not ComboBoxItem item) return;
            wb.SetValue(def.Key, item.Tag is OptionDef o ? o.Id : PromptEngine.Custom);
            if (item.Tag is string) Dispatcher.BeginInvoke(() => custom!.Focus());
        };
        custom = Ui.Input("ex.: " + Cut((def.Find(def.Default) ?? def.Options[0]).En, 60));
        custom.FontFamily = Ui.Res<FontFamily>("MonoFont");
        custom.Margin = new Thickness(0, 6, 0, 0);
        custom.TextChanged += (_, _) => { if (!syncing) wb.SetCustom(def.Key, custom.Text); };

        var dice = Ui.Icon(Ui.IconDice, "Sortear " + def.Label.ToLowerInvariant(), (_, _) => wb.Randomize(def.Key));
        lockButton = Ui.LockToggle("Travar: não muda ao trocar de pose nem ao sortear");
        lockButton.Click += (_, _) => wb.ToggleLock(def.Key);
        var grid = Ui.Row(def.Label, new StackPanel { Children = { combo, custom } }, dice, lockButton);
        label = (TextBlock)grid.Children[0];
        Content = grid;
        Sync();
    }

    static string Cut(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    public void Sync()
    {
        syncing = true;
        var value = wb.Sel.Values.GetValueOrDefault(def.Key);
        var isCustom = value == PromptEngine.Custom;
        Ui.Select(combo, t => isCustom ? t is string : t is OptionDef o && o.Id == (def.Find(value)?.Id ?? def.Default));
        custom.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        var text = wb.Sel.Custom.GetValueOrDefault(def.Key) ?? "";
        if (custom.Text != text) custom.Text = text;
        var locked = wb.Locks.Contains(def.Key);
        lockButton.IsChecked = locked;
        label.Foreground = locked ? Ui.Res<Brush>("Accent") : Ui.Res<Brush>("Muted");
        syncing = false;
    }
}

/// <summary>A multi-choice field (accessories): pill chips; options sharing a slot exclude each other.</summary>
internal sealed class MultiField : ContentControl, ISyncView
{
    readonly Workbench wb;
    readonly VarDef def;
    readonly WrapPanel chips = new() { Margin = new Thickness(0, 2, 0, 0) };
    readonly ToggleButton lockButton;

    public MultiField(Workbench wb, VarDef def)
    {
        this.wb = wb;
        this.def = def;
        foreach (var o in def.Options)
        {
            var chip = Ui.Chip(o.Pt);
            chip.Tag = o;
            chip.Click += (_, _) => wb.ToggleOption(def.Key, o);
            chips.Children.Add(chip);
        }
        var dice = Ui.Icon(Ui.IconDice, "Sortear " + def.Label.ToLowerInvariant(), (_, _) => wb.Randomize(def.Key));
        lockButton = Ui.LockToggle("Travar: não muda ao trocar de pose nem ao sortear");
        lockButton.Click += (_, _) => wb.ToggleLock(def.Key);
        Content = Ui.Row(def.Label, chips, dice, lockButton);
        Sync();
    }

    public void Sync()
    {
        var on = wb.Sel.Lists.GetValueOrDefault(def.Key) ?? [];
        foreach (ToggleButton c in chips.Children) c.IsChecked = on.Contains(((OptionDef)c.Tag).Id);
        lockButton.IsChecked = wb.Locks.Contains(def.Key);
    }
}
