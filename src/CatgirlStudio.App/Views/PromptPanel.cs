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
    // both texts can be edited by hand (Code is read-only by default)
    readonly TextBox prompt = new() { Style = Ui.Style("Code"), IsReadOnly = false, AcceptsReturn = true };
    readonly DockPanel manualBar = new() { Margin = new Thickness(0, 8, 0, 0) };
    readonly TextBlock manualNote = Ui.Text("", "Small");
    bool rendering;
    string lastBuiltEdit = "";
    readonly TextBlock status = Ui.Text("", "Small", new Thickness(0, 0, 0, 4));
    readonly TextBlock editHint = Ui.Text("", "Small", new Thickness(0, 2, 0, 8));
    readonly TextBox edit = new() { Style = Ui.Style("Code"), IsReadOnly = false, AcceptsReturn = true, MinHeight = 70 };
    RowDefinition? editRow;
    readonly WrapPanel editActions = new() { Margin = new Thickness(0, 10, 0, 0) };
    readonly TextBox favName = Ui.Input("Nome da combinação");
    readonly ListBox favs = new();
    readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };

    /// <summary>"Aplicar nesta imagem": the studio panel wires and labels it.</summary>
    public Button ApplyButton { get; } = Ui.IconTextButton(Ui.IconSparkle, "Aplicar nesta imagem", primary: true);

    /// <summary>The quick-edit request as it reads in the box (edited by hand or not).</summary>
    public string EditText => edit.Visibility == Visibility.Visible ? edit.Text.Trim() : "";

    /// <summary>For the self-test: types into the quick-edit box.</summary>
    internal void TypeEdit(string text) => edit.Text = text;

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
        actions.Children.Add(Ui.IconTextButton(Ui.IconCopy, "Copiar prompt", primary: true, (_, _) => Copy(wb.PromptText, "Prompt copiado. Cole no ChatGPT.")));
        actions.Children.Add(Ui.IconTextButton(Ui.IconOpen, "Abrir no ChatGPT", click: (_, _) => OpenChatGpt()));

        // typing in the prompt makes it a hand edit; the bar says so and goes back to the built one
        prompt.ToolTip = "Dá para editar à mão: copiar, abrir no ChatGPT e gerar usam o texto como está aqui.";
        prompt.TextChanged += (_, _) => { if (!rendering) wb.EditPrompt(prompt.Text); };
        manualNote.TextWrapping = TextWrapping.Wrap;
        manualNote.VerticalAlignment = VerticalAlignment.Center;
        var back = Ui.Button("Voltar ao gerado", click: (_, _) => { wb.UseBuiltPrompt(); Say("O prompt voltou a ser o montado pelos campos."); });
        back.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(back, Dock.Right);
        manualBar.Children.Add(back);
        manualBar.Children.Add(manualNote);
        edit.ToolTip = "Dá para editar à mão antes de aplicar ou copiar; volta ao montado quando você muda outro campo.";

        editActions.Children.Add(ApplyButton);
        editActions.Children.Add(Ui.Button("Copiar ajuste", click: (_, _) => Copy(EditText, "Pedido de ajuste copiado.")));
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

        // the prompt above, the quick edit and favorites below; the line between them drags
        var star = new GridLength(1, GridUnitType.Star);
        var top = Ui.Rows((head, GridLength.Auto), (tags, GridLength.Auto), (prompt, star), (manualBar, GridLength.Auto), (actions, GridLength.Auto), (status, GridLength.Auto));
        var bottom = Ui.Rows(
            (Ui.Text("Ajuste rápido", "H2"), GridLength.Auto), (editHint, GridLength.Auto),
            (edit, GridLength.Auto), (editActions, GridLength.Auto),
            (Ui.Text("Favoritos", "H2", new Thickness(0, 16, 0, 0)), GridLength.Auto), (favForm, GridLength.Auto),
            (favs, star), (favActions, GridLength.Auto));
        favs.MinHeight = 60;
        // the edit text and the favorites list share what the lower part has
        editRow = bottom.RowDefinitions[2];
        Content = Layout.SplitRows("promptSplit", top, bottom, defaultTop: 0.45, minTop: 220, minBottom: 340);

        statusTimer.Tick += (_, _) => { status.Text = ""; statusTimer.Stop(); };
        wb.Changed += Render;
        Render();
    }

    void Render()
    {
        rendering = true;
        var text = wb.PromptText;
        if (prompt.Text != text) prompt.Text = text;
        var manual = wb.ManualPrompt is not null;
        count.Text = text.Length.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.') + " caracteres" + (manual ? "  ·  editado à mão" : "");
        count.Foreground = manual ? Ui.Res<Brush>("Accent") : Ui.Res<Brush>("Muted");
        manualBar.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        manualNote.Text = wb.ManualOutdated
            ? "Você mudou campos depois de editar: o texto editado não acompanha. Copiar e gerar usam ele assim mesmo."
            : "Editado à mão: copiar, abrir no ChatGPT e gerar usam este texto.";
        manualNote.Foreground = wb.ManualOutdated ? Ui.Res<Brush>("Error") : Ui.Res<Brush>("Muted");
        var format = wb.Sel.Values.GetValueOrDefault("format") ?? "";
        var formatName = wb.Catalog.Vars["format"].Find(format)?.Pt.Split(" (")[0] ?? "Formato livre";
        tags.Text = $"{wb.Prefab.Name}   ·   {SizeLabels.GetValueOrDefault(format, "tamanho livre")}   ·   {formatName}   ·   {wb.Result.Used.Count} campos";

        var req = wb.EditRequest();
        if (wb.Baseline is null) editHint.Text = "Depois de copiar, abrir ou gerar um prompt, mude o que quiser e eu monto o pedido de ajuste, que mantém o resto da imagem igual.";
        else if (req.Changes.Count == 0) editHint.Text = "Nada mudou desde o último prompt enviado. Troque uma opção para montar o pedido de ajuste.";
        else editHint.Text = "Mudou: " + string.Join(", ", req.Changes.Select(c => c.LabelPt)) + ".";
        // a hand edit of the request stays until the built request changes
        if (req.Text != lastBuiltEdit) { edit.Text = req.Text; lastBuiltEdit = req.Text; }
        var hasEdit = req.Text.Length > 0;
        edit.Visibility = editActions.Visibility = hasEdit ? Visibility.Visible : Visibility.Collapsed;
        if (editRow != null) editRow.Height = hasEdit ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;

        var selected = (favs.SelectedItem as ListBoxItem)?.Tag;
        favs.Items.Clear();
        foreach (var f in wb.Favorites) favs.Items.Add(FavoriteItem(f));
        if (selected != null) favs.SelectedItem = favs.Items.Cast<ListBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, selected));
        rendering = false;
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
        var text = wb.PromptText;
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
        wb.LoadSelection(f.Selection, f.ManualPrompt);
        Say($"Favorito “{f.Name}” carregado." + (f.ManualPrompt is not null ? " O prompt editado à mão veio junto." : ""));
    }

    void DeleteFavorite()
    {
        if ((favs.SelectedItem as ListBoxItem)?.Tag is not Favorite f) return;
        if (MessageBox.Show(Window.GetWindow(this)!, $"Apagar o favorito “{f.Name}”?", "Apagar favorito", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        wb.DeleteFavorite(f);
        Say("Favorito apagado.");
    }
}
