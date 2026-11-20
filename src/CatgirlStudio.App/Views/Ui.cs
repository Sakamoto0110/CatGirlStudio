using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Views;

/// <summary>A color option as shown in a color combo (ColorItemTemplate binds to it).</summary>
public sealed record ColorItem(string Pt, Brush Fill);

/// <summary>Builds controls with the styles in Theme.xaml, so the views stay short.</summary>
internal static class Ui
{
    public static T Res<T>(string key) => (T)Application.Current.FindResource(key);
    public static Style Style(string key) => Res<Style>(key);

    // Segoe Fluent Icons glyphs
    public const string IconDice = "", IconLock = "", IconUnlock = "", IconCopy = "",
        IconOpen = "", IconFolder = "", IconSparkle = "", IconSearch = "";

    public static TextBlock Text(string text, string style = "Small", Thickness? margin = null) =>
        new() { Text = text, Style = Style(style), Margin = margin ?? new Thickness(0) };

    public static Button Button(string text, bool primary = false, RoutedEventHandler? click = null)
    {
        var b = new Button { Content = text, Style = Style(primary ? "Primary" : "Ghost") };
        if (click != null) b.Click += click;
        return b;
    }

    /// <summary>A button with an icon glyph next to the text.</summary>
    public static Button IconTextButton(string glyph, string text, bool primary = false, RoutedEventHandler? click = null)
    {
        var b = Button("", primary, click);
        b.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, Margin = new Thickness(0, 1, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
            }
        };
        return b;
    }

    public static Button Icon(string glyph, string tip, RoutedEventHandler click)
    {
        var b = new Button { Content = glyph, Style = Style("Icon"), ToolTip = tip };
        b.Click += click;
        return b;
    }

    public static ToggleButton LockToggle(string tip)
    {
        var t = new ToggleButton { Content = IconUnlock, Style = Style("IconToggle"), ToolTip = tip };
        t.Checked += (_, _) => t.Content = IconLock;
        t.Unchecked += (_, _) => t.Content = IconUnlock;
        return t;
    }

    public static ToggleButton Chip(string text) => new() { Content = text, Style = Style("Chip") };

    public static TextBox Input(string hint) => new() { Style = Style("Input"), Tag = hint };

    public static Border Card(UIElement child) => new() { Style = Style("Card"), Child = child };

    /// <summary>A combo for catalog options; the items are the options themselves.</summary>
    public static ComboBox Combo(IEnumerable<object> items, Func<object, string> label)
    {
        var c = new ComboBox();
        foreach (var i in items) c.Items.Add(new ComboBoxItem { Content = label(i), Tag = i });
        return c;
    }

    /// <summary>A color combo: each item shows a swatch of the color's hex. The items are data
    /// with a template (an element can only live in one place, and the closed box shows it too).</summary>
    public static ComboBox ColorCombo(IEnumerable<OptionDef> colors, bool picker = false)
    {
        var c = new ComboBox();
        var template = Res<DataTemplate>("ColorItemTemplate");
        foreach (var o in colors) c.Items.Add(new ComboBoxItem { Content = new ColorItem(o.Pt, Fill(o.Field("hex"))), ContentTemplate = template, Tag = o });
        if (picker) c.Items.Add(new ComboBoxItem { Content = new ColorItem("Outra cor…", Rainbow), ContentTemplate = template, Tag = PickColor });
        return c;
    }

    /// <summary>Tag of the "Outra cor…" item, which opens the color picker.</summary>
    public static readonly object PickColor = new();

    static readonly Brush Rainbow = MakeRainbow();
    static Brush MakeRainbow()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        foreach (var (hex, at) in new[] { ("#FF8A8A", 0.0), ("#FFD27A", 0.25), ("#8FE3A8", 0.5), ("#86B8FF", 0.75), ("#D69BFF", 1.0) })
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(hex), at));
        b.Freeze();
        return b;
    }

    static Brush Fill(string? hex)
    {
        if (hex is not null)
        {
            try
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                b.Freeze();
                return b;
            }
            catch (FormatException) { }
        }
        return Brushes.Gray;
    }

    /// <summary>In a color combo with a picker: shows a picked "#RRGGBB" as its own item (just
    /// before "Outra cor…"), or takes that item away when the value is a listed color.</summary>
    public static void ShowPicked(ComboBox c, string? value)
    {
        var items = c.Items.Cast<ComboBoxItem>().ToList();
        var pick = items.FirstOrDefault(i => i.Tag == PickColor);
        if (pick is null) return;
        var item = items.FirstOrDefault(i => i.Tag is string s && ColorWords.IsHex(s));
        if (!ColorWords.IsHex(value))
        {
            if (item != null) c.Items.Remove(item);
            return;
        }
        if (item is null)
        {
            item = new ComboBoxItem { ContentTemplate = Res<DataTemplate>("ColorItemTemplate") };
            c.Items.Insert(c.Items.IndexOf(pick), item);
        }
        if ((string?)item.Tag == value) return;
        item.Tag = value;
        item.Content = new ColorItem("Cor escolhida  " + value, Fill(value));
    }

    /// <summary>The hex of a color value: a picked "#RRGGBB" or the swatch of a listed color.</summary>
    public static string? HexOf(IEnumerable<OptionDef> colors, string? value) =>
        ColorWords.IsHex(value) ? value : colors.FirstOrDefault(o => o.Id == value)?.Field("hex");

    /// <summary>"Outra cor…" was chosen: opens the picker once the list has closed, then sets the
    /// picked color, or puts the combo back on the current value.</summary>
    public static void PickInto(ComboBox c, string title, string? startHex, Action<string> set, Action restore) =>
        c.Dispatcher.BeginInvoke(() =>
        {
            var hex = ColorPicker.Pick(c, title, startHex);
            if (hex is not null) set(hex);
            else restore();
        });

    public static void Select(ComboBox c, Func<object?, bool> match)
    {
        foreach (ComboBoxItem item in c.Items)
            if (match(item.Tag)) { if (!ReferenceEquals(c.SelectedItem, item)) c.SelectedItem = item; return; }
        c.SelectedIndex = -1;
    }

    /// <summary>A one-column grid with these rows, top to bottom.</summary>
    public static Grid Rows(params (UIElement El, GridLength Height)[] rows)
    {
        var g = new Grid();
        for (var i = 0; i < rows.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = rows[i].Height });
            Grid.SetRow(rows[i].El, i);
            g.Children.Add(rows[i].El);
        }
        return g;
    }

    /// <summary>A label column + control column + trailing buttons row.</summary>
    public static Grid Row(string label, UIElement control, params UIElement[] trailing)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var l = Text(label, "Label");
        l.Margin = new Thickness(0, 8, 10, 0);
        l.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(l, 0);
        g.Children.Add(l);
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        foreach (var t in trailing) buttons.Children.Add(t);
        Grid.SetColumn(buttons, 2);
        g.Children.Add(buttons);
        return g;
    }
}
