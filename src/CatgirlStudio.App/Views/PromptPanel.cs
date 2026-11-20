using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CatgirlStudio.App.Views;

/// <summary>The prompt ticket: the prompt, copy / open in ChatGPT, the quick edit and favorites.</summary>
internal sealed class PromptPanel : UserControl
{
    static readonly Dictionary<string, string> SizeLabels = new()
    {
        ["portrait"] = "1024×1536", ["mascot"] = "1024×1536", ["landscape"] = "1536×1024", ["square"] = "1024×1024",
        ["wide"] = "2560×1080", ["fhd"] = "1920×1080", ["vertical"] = "1080×1920"
    };

    readonly Workbench wb;
    readonly TextBlock count = Ui.Text("", "Small", new Thickness(10, 5, 0, 0));
    readonly TextBlock tags = Ui.Text("", "Small", new Thickness(0, 4, 0, 10));
    readonly TextBox prompt = new() { IsEnabled = true, Style = Ui.Style("Code") };
    readonly TextBlock status = Ui.Text("", "Small", new Thickness(0, 0, 0, 4));
    readonly TextBlock editHint = Ui.Text("", "Small", new Thickness(0, 2, 0, 8));
    readonly TextBox edit = new() { Style = Ui.Style("Code"), Height = 150 };
    readonly WrapPanel editActions = new() { Margin = new Thickness(0, 10, 0, 0) };
    readonly TextBox favName = Ui.Input("Nome da combinação");
    readonly ListBox favs = new();
    readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };

    /// <summary>"Aplicar nesta imagem": the studio panel wires and labels it.</summary>
    public Button ApplyButton { get; } = Ui.IconTextButton(Ui.IconSparkle, "Aplicar nesta imagem", primary: true);

    public PromptPanel(Workbench wb)
    {
        this.wb = wb;
        favs.Style = Ui.Style("PlainList");
        favs.ItemContainerStyle = Ui.Style("FavItem");
        favs.MouseDoubleClick += (_, _) => LoadFavorite();
        status.Foreground = Ui.Res<Brush>("Ok");

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(Ui.Text("Prompt", "H2"));
        head.Children.Add(count);

        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        actions.Children.Add(Ui.IconTextButton(Ui.IconCopy, "Copiar prompt", primary: true, (_, _) => Copy(wb.Result.Text, "Prompt copiado. Cole no ChatGPT.")));
        actions.Children.Add(Ui.IconTextButton(Ui.IconOpen, "Abrir no ChatGPT", click: (_, _) => OpenChatGpt()));

        editActions.Children.Add(ApplyButton);
        editActions.Children.Add(Ui.Button("Copiar ajuste", click: (_, _) => Copy(wb.EditRequest().Text, "Pedido de ajuste copiado.")));
        editActions.Children.Add(Ui.Button("Usar o atual como base", click: (_, _) => { wb.MarkSent(); Say("O prompt atual virou a nova base."); }));

        favName.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) SaveFavorite(); };
        var favForm = new DockPanel { Margin = new Thickness(0, 8, 0, 8) };
        var save = Ui.Button("Salvar", click: (_, _) => SaveFavorite());
        save.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(save, Dock.Right);
        favForm.Children.Add(save);
        favForm.Children.Add(favName);
        var favActions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        favActions.Children.Add(Ui.Button("Carregar", click: (_, _) => LoadFavorite()));
        favActions.Children.Add(Ui.Button("Apagar", click: (_, _) => DeleteFavorite()));

        var grid = new Grid();
        var rows = new (UIElement El, GridLength H)[]
        {
            (head, GridLength.Auto), (tags, GridLength.Auto), (prompt, new GridLength(1, GridUnitType.Star)), (actions, GridLength.Auto), (status, GridLength.Auto),
            (Ui.Text("Ajuste rápido", "H2", new Thickness(0, 14, 0, 0)), GridLength.Auto), (editHint, GridLength.Auto),
            (edit, GridLength.Auto), (editActions, GridLength.Auto),
            (Ui.Text("Favoritos", "H2", new Thickness(0, 16, 0, 0)), GridLength.Auto), (favForm, GridLength.Auto),
            (favs, new GridLength(150)), (favActions, GridLength.Auto)
        };
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = rows[i].H });
            Grid.SetRow(rows[i].El, i);
            grid.Children.Add(rows[i].El);
        }
        Content = grid;

        statusTimer.Tick += (_, _) => { status.Text = ""; statusTimer.Stop(); };
        wb.Changed += Render;
        Render();
    }

    void Render()
    {
        var text = wb.Result.Text;
        if (prompt.Text != text) prompt.Text = text;
        count.Text = text.Length.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.') + " caracteres";
        var format = wb.Sel.Values.GetValueOrDefault("format") ?? "";
        var formatName = wb.Catalog.Vars["format"].Find(format)?.Pt.Split(" (")[0] ?? "Formato livre";
        tags.Text = $"{wb.Prefab.Name}   ·   {SizeLabels.GetValueOrDefault(format, "tamanho livre")}   ·   {formatName}   ·   {wb.Result.Used.Count} campos";

        var req = wb.EditRequest();
        if (wb.Baseline is null) editHint.Text = "Depois de copiar, abrir ou gerar um prompt, mude o que quiser e eu monto o pedido de ajuste, que mantém o resto da imagem igual.";
        else if (req.Changes.Count == 0) editHint.Text = "Nada mudou desde o último prompt enviado. Troque uma opção para montar o pedido de ajuste.";
        else editHint.Text = "Mudou: " + string.Join(", ", req.Changes.Select(c => c.LabelPt)) + ".";
        edit.Text = req.Text;
        edit.Visibility = editActions.Visibility = req.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var selected = (favs.SelectedItem as ListBoxItem)?.Tag;
        favs.Items.Clear();
        foreach (var f in wb.Favorites) favs.Items.Add(FavoriteItem(f));
        if (selected != null) favs.SelectedItem = favs.Items.Cast<ListBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, selected));
    }

    ListBoxItem FavoriteItem(Favorite f)
    {
        var pose = Ui.Text(wb.Engine.FindPrefab(f.Selection.PrefabId).Name, "Small");
        pose.TextWrapping = TextWrapping.NoWrap;
        DockPanel.SetDock(pose, Dock.Right);
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(pose);
        dock.Children.Add(new TextBlock { Text = f.Name, Foreground = Ui.Res<Brush>("Ink"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) });
        return new ListBoxItem { Tag = f, Content = dock };
    }

    void Copy(string text, string message)
    {
        if (text.Length == 0) return;
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.COMException) { Say("Não consegui copiar: outro programa está usando a área de transferência. Tente de novo."); return; }
        wb.MarkSent();
        Say(message);
    }

    void OpenChatGpt()
    {
        var text = wb.Result.Text;
        Copy(text, "Abrindo o ChatGPT. O prompt também foi copiado.");
        Process.Start(new ProcessStartInfo("https://chatgpt.com/?q=" + Uri.EscapeDataString(text)) { UseShellExecute = true });
    }

    public void Say(string message)
    {
        status.Text = message;
        statusTimer.Stop();
        statusTimer.Start();
    }

    void SaveFavorite()
    {
        wb.SaveFavorite(favName.Text);
        favName.Text = "";
        Say("Salvo nos favoritos.");
    }

    void LoadFavorite()
    {
        if ((favs.SelectedItem as ListBoxItem)?.Tag is not Favorite f) return;
        wb.LoadSelection(f.Selection);
        Say($"Favorito “{f.Name}” carregado.");
    }

    void DeleteFavorite()
    {
        if ((favs.SelectedItem as ListBoxItem)?.Tag is not Favorite f) return;
        if (MessageBox.Show(Window.GetWindow(this)!, $"Apagar o favorito “{f.Name}”?", "Apagar favorito", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        wb.DeleteFavorite(f);
        Say("Favorito apagado.");
    }
}
