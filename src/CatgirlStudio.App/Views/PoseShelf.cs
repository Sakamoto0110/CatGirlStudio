using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Views;

/// <summary>The pose shelf: search, category chips and the prefab cards.</summary>
internal sealed class PoseShelf : UserControl
{
    const string All = "Todas";
    readonly Workbench wb;
    readonly TextBox search = Ui.Input("Buscar pose…");
    readonly WrapPanel cats = new() { Margin = new Thickness(0, 12, 0, 6) };
    readonly ListBox list = new();
    readonly TextBlock count = Ui.Text("", "Small", new Thickness(10, 6, 0, 0));
    string category = All;
    bool syncing;

    public PoseShelf(Workbench wb)
    {
        this.wb = wb;
        list.Style = Ui.Style("PlainList");
        list.ItemContainerStyle = Ui.Style("PoseItem");
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        head.Children.Add(Ui.Text("Poses", "H2"));
        head.Children.Add(count);

        foreach (var c in new[] { All }.Concat(wb.Catalog.Categories))
        {
            var chip = Ui.Chip(c);
            chip.IsChecked = c == All;
            chip.Click += (_, _) =>
            {
                category = c;
                foreach (ToggleButton x in cats.Children) x.IsChecked = (string)x.Content == c;
                Fill();
            };
            cats.Children.Add(chip);
        }

        search.TextChanged += (_, _) => Fill();
        list.SelectionChanged += (_, _) =>
        {
            if (!syncing && list.SelectedItem is ListBoxItem { Tag: Prefab p } && p.Id != wb.Sel.PrefabId) wb.SelectPrefab(p.Id);
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        foreach (var (el, row) in new (UIElement, int)[] { (head, 0), (search, 1), (cats, 2), (list, 3) })
        {
            Grid.SetRow(el, row);
            grid.Children.Add(el);
        }
        Content = grid;

        wb.Changed += SyncSelection;
        Fill();
    }

    void Fill()
    {
        var q = search.Text.Trim().ToLowerInvariant();
        var items = wb.Catalog.Prefabs
            .Where(p => category == All || p.Category == category)
            .Where(p => q.Length == 0 || (p.Name + " " + p.Desc + " " + p.Category).ToLowerInvariant().Contains(q))
            .ToList();
        syncing = true;
        list.Items.Clear();
        foreach (var p in items) list.Items.Add(Card(p));
        syncing = false;
        count.Text = $"{items.Count} de {wb.Catalog.Prefabs.Count}";
        SyncSelection();
    }

    static ListBoxItem Card(Prefab p) => new()
    {
        Tag = p,
        Content = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = p.Category.ToUpperInvariant(), Style = Ui.Style("Legend"), Margin = new Thickness(0, 0, 0, 2), FontSize = 10 },
                new TextBlock { Text = p.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Ui.Res<System.Windows.Media.Brush>("Ink") },
                new TextBlock { Text = p.Desc, Style = Ui.Style("Small"), Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap }
            }
        }
    };

    string? shownPrefab;

    void SyncSelection()
    {
        syncing = true;
        var current = list.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((Prefab)i.Tag).Id == wb.Sel.PrefabId);
        list.SelectedItem = current;
        // follow the pose only when it changed (dice, favorite), not on every field edit
        if (current != null && shownPrefab != wb.Sel.PrefabId) list.ScrollIntoView(current);
        shownPrefab = wb.Sel.PrefabId;
        syncing = false;
    }
}
