using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Views;

/// <summary>
/// The outfit, as the OutfitEngine allows it: the mode (ready, pieces, dress), then one card per
/// body slot in use with its piece and only the properties that piece declares. Rebuilt when
/// the structure changes (mode, pieces, Estilo); otherwise the values update in place.
/// </summary>
internal sealed class OutfitEditor : StackPanel, ISyncView
{
    readonly Workbench wb;
    readonly string group = "mode-" + Guid.NewGuid().ToString("N");
    readonly List<(ComboBox Combo, Func<string?> Current)> combos = [];
    readonly List<RadioButton> modes = [];
    ToggleButton? lockButton;
    string signature = "";
    bool syncing;

    OutfitEngine Outfit => wb.Outfit;

    public OutfitEditor(Workbench wb)
    {
        this.wb = wb;
        Margin = new Thickness(0, 0, 0, 6);
        Sync();
    }

    string Signature()
    {
        var state = Outfit.State(wb.Sel);
        var parts = state.Select(s => s.Slot.Id + ":" + s.Piece.Id + "[" + string.Join(",", s.Props.Select(p => p.Key)) + "]");
        // the offered pieces depend on the Estilo (style outfits)
        return Outfit.Mode(wb.Sel).Id + "|" + string.Join(";", parts) + "|" + wb.Sel.Values.GetValueOrDefault("theme");
    }

    public void Sync()
    {
        var sig = Signature();
        if (sig != signature) { Rebuild(); signature = sig; }
        syncing = true;
        var mode = Outfit.Mode(wb.Sel).Id;
        foreach (var r in modes) r.IsChecked = (string)r.Tag == mode;
        foreach (var (combo, current) in combos)
        {
            var id = current();
            Ui.Select(combo, t => t switch { Piece p => p.Id == id, OptionDef o => o.Id == id, _ => false });
        }
        if (lockButton != null) lockButton.IsChecked = wb.Locks.Contains(OutfitEngine.Var);
        syncing = false;
    }

    void Rebuild()
    {
        Children.Clear();
        combos.Clear();
        modes.Clear();

        // mode, as a segmented control, plus dice and lock for the whole outfit
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var m in Outfit.Modes)
        {
            var r = new RadioButton { Content = m.Pt, Tag = m.Id, GroupName = group, Style = Ui.Style("Segment") };
            r.Checked += (_, _) => { if (!syncing) wb.SetOutfit("outfit.mode", m.Id); };
            modes.Add(r);
            bar.Children.Add(r);
        }
        var dice = Ui.Icon(Ui.IconDice, "Sortear as peças e as cores (o tipo fica)", (_, _) => wb.Randomize(OutfitEngine.Var));
        lockButton = Ui.LockToggle("Travar a roupa: não muda ao trocar de pose nem ao sortear");
        lockButton.Click += (_, _) => wb.ToggleLock(OutfitEngine.Var);
        Children.Add(Ui.Row("Roupa", new Border { Style = Ui.Style("SegmentBar"), Child = bar }, dice, lockButton));

        foreach (var slot in Outfit.State(wb.Sel)) Children.Add(SlotCard(slot));
    }

    Border SlotCard(SlotState state)
    {
        var slotId = state.Slot.Id;
        var panel = new StackPanel();

        var pieces = Ui.Combo(Outfit.Offered(slotId, wb.Sel), p => ((Piece)p).Pt + (((Piece)p).Theme is not null ? "  ·  do estilo" : ""));
        CharacterSheet.PassWheel(pieces);
        pieces.SelectionChanged += (_, _) =>
        {
            if (!syncing && pieces.SelectedItem is ComboBoxItem { Tag: Piece p }) wb.SetOutfit("outfit." + slotId, p.Id);
        };
        combos.Add((pieces, () => Outfit.PieceIn(slotId, wb.Sel).Id));
        panel.Children.Add(Line(state.Slot.Pt, pieces, header: true));

        foreach (var prop in state.Props)
        {
            if (prop.Fixed is not null)
            {
                var fixedText = prop.Def.Options.FirstOrDefault(o => o.Id == prop.Fixed)?.Pt ?? prop.Fixed;
                panel.Children.Add(Line(prop.Def.Label, Ui.Text(fixedText + "  (fixo nesta peça)", "Small", new Thickness(0, 8, 0, 0)), header: false));
                continue;
            }
            if (!prop.Editable) continue;
            var combo = prop.Def.IsColor ? Ui.ColorCombo(prop.Options) : Ui.Combo(prop.Options, o => ((OptionDef)o).Pt);
            CharacterSheet.PassWheel(combo);
            var key = $"outfit.{slotId}.{prop.Key}";
            combo.SelectionChanged += (_, _) =>
            {
                if (!syncing && combo.SelectedItem is ComboBoxItem { Tag: OptionDef o }) wb.SetOutfit(key, o.Id);
            };
            combos.Add((combo, () => Outfit.PropValue(slotId, prop, wb.Sel)));
            panel.Children.Add(Line(prop.Def.Label, combo, header: false));
        }

        return new Border
        {
            Background = Ui.Res<Brush>("Surface2"),
            BorderBrush = Ui.Res<Brush>("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 10, 14, 4),
            Margin = new Thickness(0, 0, 0, 8),
            Child = panel
        };
    }

    // inside a slot card: a narrower label column, the slot name in the accent color
    static Grid Line(string label, UIElement control, bool header)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = Ui.Text(label, "Label");
        l.Margin = new Thickness(0, 8, 8, 0);
        l.VerticalAlignment = VerticalAlignment.Top;
        if (header)
        {
            l.Foreground = Ui.Res<Brush>("Ink");
            l.FontWeight = FontWeights.SemiBold;
        }
        g.Children.Add(l);
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }
}
