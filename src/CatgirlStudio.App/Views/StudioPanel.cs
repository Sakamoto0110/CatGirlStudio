using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CatgirlStudio.App.Studio;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Views;

/// <summary>Generate and edit through the OpenAI API, show the image, the 2x and the recent ones.</summary>
internal sealed class StudioPanel : UserControl
{
    static string SettingsFile => Path.Combine(AppPaths.Data, "studio.json");
    const int GalleryLimit = 60;

    readonly Workbench wb;
    readonly IImageApi api;
    readonly ImageStore store;
    readonly PromptPanel promptPanel;
    readonly ComboBox model, quality, count;
    readonly Button generate;
    readonly TextBlock message = Ui.Text("", "Small", new Thickness(0, 2, 0, 10));
    readonly Image picture = new() { Stretch = Stretch.Uniform };
    readonly TextBlock caption = Ui.Text("", "Small", new Thickness(0, 10, 0, 6));
    readonly WrapPanel captionActions = new();
    // upscale: scale (2x/3x/4x), TTA and the button
    readonly WrapPanel upscaleBar = new() { Margin = new Thickness(0, 2, 0, 0) };
    readonly List<RadioButton> scales = [];
    readonly ToggleButton ttaChip;
    readonly Button upscale;
    int scale = 2;
    readonly WrapPanel thumbs = new();
    readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    StoredImage? current;
    List<StoredImage> gallery = [];
    bool busy;
    double spent;
    Action? tick;

    // for the --selftest run
    internal StoredImage? Current => current;
    internal string Message => message.Text;

    public StudioPanel(Workbench wb, IImageApi api, ImageStore store, PromptPanel promptPanel)
    {
        this.wb = wb;
        this.api = api;
        this.store = store;
        this.promptPanel = promptPanel;

        model = Ui.Combo(Pricing.Models.Cast<object>(), o => ((KeyValuePair<string, string>)o).Value);
        quality = Ui.Combo(Pricing.Qualities, o => (string)o);
        count = Ui.Combo(new object[] { 1, 2, 3, 4 }, o => (int)o == 1 ? "1 imagem" : $"{o} imagens");
        model.SelectedIndex = 0;
        quality.SelectedIndex = 1;
        count.SelectedIndex = 0;
        foreach (var c in new[] { model, quality, count }) c.SelectionChanged += (_, _) => { RefreshLabels(); SaveSettings(); };

        var options = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        for (var i = 0; i < 3; i++) options.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var (label, combo, col) in new[] { ("Modelo", model, 0), ("Qualidade", quality, 1), ("Quantidade", count, 2) })
        {
            var cell = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 5, 0, col == 2 ? 0 : 5, 0) };
            cell.Children.Add(Ui.Text(label, "Label", new Thickness(0, 0, 0, 5)));
            cell.Children.Add(combo);
            Grid.SetColumn(cell, col);
            options.Children.Add(cell);
        }

        generate = Ui.IconTextButton(Ui.IconSparkle, "Gerar imagem", primary: true, async (_, _) => await Generate());
        generate.HorizontalAlignment = HorizontalAlignment.Stretch;
        generate.Margin = new Thickness(0, 14, 0, 8);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = Checker(),
            BorderBrush = Ui.Res<Brush>("Line"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = picture,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Clique duas vezes para abrir em tamanho real"
        };
        frame.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2 && current != null) Open(current.Path); };

        captionActions.Children.Add(Ui.IconTextButton(Ui.IconOpen, "Abrir", click: (_, _) => { if (current != null) Open(current.Path); }));
        captionActions.Children.Add(Ui.IconTextButton(Ui.IconFolder, "Mostrar na pasta", click: (_, _) => ShowInFolder()));

        var scaleBar = new StackPanel { Orientation = Orientation.Horizontal };
        var group = "scale-" + Guid.NewGuid().ToString("N");
        foreach (var s in new[] { 2, 3, 4 })
        {
            var r = new RadioButton { Content = $"{s}x", Tag = s, GroupName = group, Style = Ui.Style("Segment"), IsChecked = s == scale };
            r.Checked += (_, _) => { scale = s; RefreshLabels(); SaveSettings(); };
            scales.Add(r);
            scaleBar.Children.Add(r);
        }
        ttaChip = Ui.Chip("TTA");
        ttaChip.Margin = new Thickness(8, 0, 8, 8);
        ttaChip.VerticalAlignment = VerticalAlignment.Center;
        ttaChip.ToolTip = "Mais qualidade: roda o modelo em 8 cópias viradas da imagem e tira a média. Bordas um pouco mais limpas, umas 8 vezes mais lento.";
        ttaChip.Click += (_, _) => { RefreshLabels(); SaveSettings(); };
        upscale = Ui.Button("Ampliar", click: async (_, _) => await Upscale());
        upscaleBar.Children.Add(Ui.Text("Ampliar", "Label", new Thickness(0, 0, 10, 8)));
        upscaleBar.Children.Add(new Border { Style = Ui.Style("SegmentBar"), Child = scaleBar, Margin = new Thickness(0, 0, 0, 8) });
        upscaleBar.Children.Add(ttaChip);
        upscaleBar.Children.Add(upscale);

        var thumbsScroll = new ScrollViewer { Content = thumbs, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0) };

        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(Ui.Text("Gerar pela API", "H2"));
        if (api.Fake)
            title.Children.Add(new Border
            {
                Background = Ui.Res<Brush>("AccentSoft"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 3),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "modo de teste", FontSize = 11, Foreground = Ui.Res<Brush>("Accent") }
            });

        var grid = new Grid();
        var rows = new (UIElement El, GridLength H)[]
        {
            (title, GridLength.Auto), (options, GridLength.Auto), (generate, GridLength.Auto), (message, GridLength.Auto),
            (frame, new GridLength(1, GridUnitType.Star)), (caption, GridLength.Auto), (captionActions, GridLength.Auto), (upscaleBar, GridLength.Auto),
            (Ui.Text("Geradas recentemente", "H2", new Thickness(0, 12, 0, 0)), GridLength.Auto), (thumbsScroll, new GridLength(236))
        };
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = rows[i].H });
            Grid.SetRow(rows[i].El, i);
            grid.Children.Add(rows[i].El);
        }
        Content = grid;

        promptPanel.ApplyButton.Click += async (_, _) => await ApplyEdit();
        ticker.Tick += (_, _) => tick?.Invoke();
        wb.Changed += RefreshLabels;

        LoadSettings();
        LoadGallery();
        if (!api.HasKey) Say($"Não achei a chave da OpenAI: crie a variável de ambiente {OpenAiImages.KeyVariable} e abra o app de novo.", error: true);
        else if (api.Fake) Say("Modo de teste: as imagens são desenhadas pelo app, nada é cobrado.");
        RefreshLabels();
    }

    static string Chosen(ComboBox c) => c.SelectedItem is ComboBoxItem { Tag: var t } ? t switch
    {
        KeyValuePair<string, string> kv => kv.Key,
        _ => t?.ToString() ?? ""
    } : "";

    string FormatNow => wb.Sel.Values.GetValueOrDefault("format") is { } f && Pricing.Sizes.ContainsKey(f) ? f : "portrait";

    static void SetLabel(Button b, string text)
    {
        if (b.Content is StackPanel { Children: [_, TextBlock t] }) t.Text = text;
        else b.Content = text;
    }

    void RefreshLabels()
    {
        var n = int.Parse(Chosen(count) is { Length: > 0 } s ? s : "1");
        SetLabel(generate, (n == 1 ? "Gerar imagem" : $"Gerar {n} imagens") + "  ·  ~" + Pricing.Money(Pricing.Estimate(Chosen(quality), Pricing.Sizes[FormatNow], n), api.Fake));
        generate.IsEnabled = !busy && api.HasKey;
        var apply = promptPanel.ApplyButton;
        SetLabel(apply, "Aplicar nesta imagem  ·  ~" + Pricing.Money(Pricing.Estimate(Chosen(quality), current?.Size ?? Pricing.Sizes[FormatNow], 1), api.Fake) + " + entrada");
        apply.IsEnabled = current != null && !busy && api.HasKey;
        apply.ToolTip = current != null ? "Manda a imagem escolhida e o pedido de ajuste para a API" : "Gere ou escolha uma imagem primeiro";
        var tta = ttaChip.IsChecked == true;
        var done = current is null ? null : store.UpscaledPath(current, scale, tta);
        var name = $"{scale}x" + (tta ? " com TTA" : "");
        SetLabel(upscale, done is null ? "Ampliar " + name : "Abrir " + name);
        upscale.IsEnabled = !busy && current != null;
        var size = current is null ? "" : SizeTimes(current.ShownSize, scale);
        upscale.ToolTip = done ?? $"Limpa a transparência e amplia {name}{size} com o Real-ESRGAN, no seu PC (não custa nada)";
    }

    void Say(string text, bool error = false)
    {
        message.Text = text;
        message.Foreground = Ui.Res<Brush>(error ? "Error" : "Muted");
    }

    /* ------------------------------------------------------------- actions */

    internal async Task Generate()
    {
        if (busy) return;
        var snap = wb.Sel.Clone();
        var fmt = FormatNow;
        var body = new JsonObject
        {
            ["model"] = Chosen(model),
            ["prompt"] = wb.Result.Text,
            ["size"] = Pricing.Sizes[fmt],
            ["quality"] = Chosen(quality),
            ["background"] = snap.Values.GetValueOrDefault("background") == "transparent" ? "transparent" : "opaque",
            ["output_format"] = "png",
            ["n"] = int.Parse(Chosen(count))
        };
        await Run("Gerando", "generations", body, "generate", snap, null, Pricing.Crops.TryGetValue(fmt, out var c) ? c : null);
    }

    internal async Task ApplyEdit()
    {
        if (busy) return;
        if (current is null) { Say("Gere ou escolha uma imagem primeiro.", error: true); return; }
        var req = wb.EditRequest();
        if (req.Text.Length == 0) { Say("Nada mudou desde esta imagem: troque alguma opção primeiro.", error: true); return; }
        const string Lead = "Edit the last image.";
        var prompt = req.Text.StartsWith(Lead) ? "Edit this image." + req.Text[Lead.Length..] : req.Text;
        var body = new JsonObject
        {
            ["model"] = Chosen(model),
            ["prompt"] = prompt,
            ["size"] = current.Size,
            ["quality"] = Chosen(quality),
            ["background"] = current.Background,
            ["output_format"] = "png",
            ["n"] = 1,
            ["images"] = new JsonArray(new JsonObject { ["image_url"] = "data:image/png;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(current.Path)) })
        };
        await Run("Ajustando", "edits", body, "edit", wb.Sel.Clone(), current.Id, current.Crop);
    }

    async Task Run(string label, string endpoint, JsonObject body, string kind, Selection snap, string? parent, (int W, int H)? crop)
    {
        busy = true;
        RefreshLabels();
        var started = DateTime.Now;
        tick = () => Say($"{label}… {(int)(DateTime.Now - started).TotalSeconds} s (pode levar até 2 minutos)");
        tick();
        ticker.Start();
        try
        {
            var result = await api.PostAsync(endpoint, body, CancellationToken.None);
            var total = Pricing.CostOf(result.Usage);
            var each = total is { } t ? Math.Round(t / result.Images.Count, 5) : (double?)null;
            spent += total ?? 0;
            StoredImage? first = null;
            foreach (var png in result.Images)
            {
                var meta = new JsonObject
                {
                    ["kind"] = kind,
                    ["created"] = DateTime.Now.ToString("s"),
                    ["model"] = body["model"]!.GetValue<string>(),
                    ["quality"] = body["quality"]!.GetValue<string>(),
                    ["size"] = body["size"]!.GetValue<string>(),
                    ["background"] = body["background"]!.GetValue<string>(),
                    ["prompt"] = body["prompt"]!.GetValue<string>(),
                    ["selection"] = snap.ToJson(),
                    ["parent"] = parent,
                    ["costUsd"] = each,
                    ["usage"] = result.Usage?.DeepClone(),
                    ["requestId"] = result.RequestId,
                    ["crop"] = crop is { } c ? new JsonArray(c.W, c.H) : null
                };
                var bytes = crop is { } size ? Png.CenterCrop(png, size.W, size.H) : png;
                var saved = store.Save(bytes, meta, snap.PrefabId);
                first ??= saved;
            }
            LoadGallery();
            SetCurrent(first);
            wb.SetBaseline(snap);
            var many = result.Images.Count > 1 ? $" pelas {result.Images.Count} imagens" : "";
            Say($"Pronto: {Pricing.Money(total, api.Fake)}{many}. Nesta sessão: {Pricing.Money(spent, api.Fake)}.");
        }
        catch (StudioException e) { Say(e.Message, error: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or FormatException or NotSupportedException)
        {
            Say("Não deu para salvar ou ler a imagem: " + e.Message, error: true);
        }
        finally
        {
            ticker.Stop();
            tick = null;
            busy = false;
            RefreshLabels();
        }
    }

    static string SizeTimes(string shown, int scale)
    {
        var parts = shown.Split('×');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) ? $" ({w * scale}×{h * scale})" : "";
    }

    // for the --selftest run
    internal Task UpscaleWith(int s, bool tta)
    {
        scales.First(r => (int)r.Tag == s).IsChecked = true;
        ttaChip.IsChecked = tta;
        return Upscale();
    }

    internal async Task Upscale()
    {
        if (busy || current is null) return;
        var tta = ttaChip.IsChecked == true;
        if (store.UpscaledPath(current, scale, tta) is { } existing) { Open(existing); return; }
        busy = true;
        RefreshLabels();
        var name = $"{scale}x" + (tta ? " com TTA" : "");
        var started = DateTime.Now;
        tick = () => Say($"Limpando a transparência e ampliando {name}… {(int)(DateTime.Now - started).TotalSeconds} s");
        tick();
        ticker.Start();
        try
        {
            var dest = await Upscaler.Upscale(current.Path, store.UpscaleFolder(scale, tta), scale, tta);
            Say($"{name} pronto em {(int)(DateTime.Now - started).TotalSeconds} s: {dest}");
        }
        catch (StudioException e) { Say(e.Message, error: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            Say("A ampliação não saiu: " + e.Message, error: true);
        }
        finally
        {
            ticker.Stop();
            tick = null;
            busy = false;
            RefreshLabels();
        }
    }

    /* ------------------------------------------------------------- gallery */

    void LoadGallery()
    {
        gallery = store.Recent(GalleryLimit);
        thumbs.Children.Clear();
        if (gallery.Count == 0)
        {
            thumbs.Children.Add(Ui.Text("Nenhuma imagem ainda.", "Small"));
            return;
        }
        foreach (var img in gallery)
        {
            var thumb = new Border
            {
                Width = 82,
                Height = 112,
                Margin = new Thickness(0, 0, 8, 8),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Background = Checker(),
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = img,
                ToolTip = (img.Kind == "edit" ? "Ajuste" : "Gerada") + " · " + img.ShownSize + "\nClique para usar: os campos voltam para como ela foi feita",
                Child = new Image { Source = Png.Load(store.ThumbPath(img), 180), Stretch = Stretch.UniformToFill }
            };
            thumb.MouseLeftButtonUp += (_, _) => Pick(img);
            thumbs.Children.Add(thumb);
        }
        MarkCurrent();
    }

    void MarkCurrent()
    {
        foreach (var b in thumbs.Children.OfType<Border>())
            b.BorderBrush = current?.Id == ((StoredImage)b.Tag).Id ? Ui.Res<Brush>("Accent") : Brushes.Transparent;
    }

    internal void Pick(StoredImage img)
    {
        if (busy) return;
        if (img.Selection is JsonNode sel)
        {
            using var doc = JsonDocument.Parse(sel.ToJsonString());
            wb.LoadSelection(Selection.FromJson(doc.RootElement, wb.Engine));
            wb.MarkSent();
        }
        SetCurrent(img);
        Say("Imagem escolhida. Os campos voltaram para como ela foi feita; mude algo e use o Ajuste rápido.");
    }

    void SetCurrent(StoredImage? img)
    {
        current = img;
        picture.Source = img is null ? null : Png.Load(img.Path);
        caption.Text = img is null ? "" :
            $"{(img.Kind == "edit" ? "Ajuste" : "Gerada")}  ·  {Pricing.Models.GetValueOrDefault(img.Model, img.Model)}  ·  {img.Quality}  ·  {img.ShownSize}" +
            (img.CostUsd is { } cost ? "  ·  " + Pricing.Money(cost, api.Fake) : "");
        captionActions.Visibility = upscaleBar.Visibility = img is null ? Visibility.Collapsed : Visibility.Visible;
        MarkCurrent();
        SaveSettings();
        RefreshLabels();
    }

    static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    void ShowInFolder()
    {
        if (current is null) Process.Start(new ProcessStartInfo(store.Folder) { UseShellExecute = true });
        else Process.Start("explorer.exe", $"/select,\"{current.Path}\"");
    }

    static DrawingBrush Checker()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Ui.Res<Brush>("Surface"), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        var squares = new GeometryGroup();
        squares.Children.Add(new RectangleGeometry(new Rect(0, 0, 8, 8)));
        squares.Children.Add(new RectangleGeometry(new Rect(8, 8, 8, 8)));
        group.Children.Add(new GeometryDrawing(Ui.Res<Brush>("Surface2"), null, squares));
        var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute };
        brush.Freeze();
        return brush;
    }

    /* ------------------------------------------------------------ settings */

    void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile));
            var r = doc.RootElement;
            if (r.TryGetProperty("model", out var m)) Ui.Select(model, t => t is KeyValuePair<string, string> kv && kv.Key == m.GetString());
            if (r.TryGetProperty("quality", out var q)) Ui.Select(quality, t => (string?)t == q.GetString());
            if (r.TryGetProperty("scale", out var sc) && sc.TryGetInt32(out var s) && scales.FirstOrDefault(x => (int)x.Tag == s) is { } radio) radio.IsChecked = true;
            if (r.TryGetProperty("tta", out var tt)) ttaChip.IsChecked = tt.ValueKind == JsonValueKind.True;
            if (r.TryGetProperty("current", out var cur) && store.Get(cur.GetString()) is { } img) SetCurrent(img);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { }
    }

    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            var json = new JsonObject { ["model"] = Chosen(model), ["quality"] = Chosen(quality), ["scale"] = scale, ["tta"] = ttaChip.IsChecked == true, ["current"] = current?.Id };
            File.WriteAllText(SettingsFile, json.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
