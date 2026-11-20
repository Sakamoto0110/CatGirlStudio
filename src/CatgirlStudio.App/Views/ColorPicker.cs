using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CatgirlStudio.App.Theme;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Views;

/// <summary>
/// The color picker: a saturation/brightness square, a hue bar, the hex and what the prompt will
/// say. Opaque colors only (a color is the color of the thing, not how see-through it is).
/// </summary>
internal sealed class ColorPicker : Window
{
    const double SquareW = 280, SquareH = 190;

    double hue, sat, val;
    readonly Rectangle hueLayer = new() { Width = SquareW, Height = SquareH, RadiusX = 10, RadiusY = 10 };
    readonly Ellipse svMark = new() { Width = 16, Height = 16, Stroke = Brushes.White, StrokeThickness = 2.5, IsHitTestVisible = false };
    readonly Border hueMark = new() { Width = 8, Height = 22, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.White, BorderThickness = new Thickness(2.5), IsHitTestVisible = false };
    readonly Border preview = new() { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), BorderBrush = Ui.Res<Brush>("Line"), BorderThickness = new Thickness(1) };
    readonly TextBox hexBox = new() { Width = 110, Style = Ui.Style("Input"), Tag = "#RRGGBB" };
    readonly TextBlock phrase = Ui.Text("", "Small", new Thickness(0, 10, 0, 0));
    bool typing;

    /// <summary>The picked color ("#RRGGBB"), or null when the dialog was closed without one.</summary>
    public string? Result { get; private set; }

    ColorPicker(Window owner, string title, string? start)
    {
        Owner = owner;
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Ui.Res<Brush>("Surface");
        Foreground = Ui.Res<Brush>("Ink");
        FontFamily = Ui.Res<FontFamily>("UiFont");
        FontSize = 13;
        UseLayoutRounding = true;
        SourceInitialized += (_, _) => ThemeColors.DarkTitleBar(this);

        // saturation goes left to right (white -> hue), brightness top to bottom (-> black)
        var square = new Grid { Width = SquareW, Height = SquareH, Cursor = Cursors.Cross, Background = Brushes.Transparent };
        square.Children.Add(hueLayer);
        square.Children.Add(new Rectangle
        {
            RadiusX = 10, RadiusY = 10,
            Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0)
        });
        square.Children.Add(new Rectangle
        {
            RadiusX = 10, RadiusY = 10,
            Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90),
            Stroke = Ui.Res<Brush>("Line"), StrokeThickness = 1
        });
        var svCanvas = new Canvas { IsHitTestVisible = false };
        svCanvas.Children.Add(svMark);
        square.Children.Add(svCanvas);
        Drag(square, p =>
        {
            sat = Math.Clamp(p.X / SquareW, 0, 1);
            val = 1 - Math.Clamp(p.Y / SquareH, 0, 1);
            Refresh();
        });

        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (var i = 0; i <= 6; i++) rainbow.GradientStops.Add(new GradientStop(FromHsv(i * 60 % 360, 1, 1), i / 6.0));
        var hueBar = new Grid { Width = SquareW, Height = 22, Margin = new Thickness(0, 12, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        hueBar.Children.Add(new Border { Height = 14, CornerRadius = new CornerRadius(7), Background = rainbow, VerticalAlignment = VerticalAlignment.Center });
        var hueCanvas = new Canvas { IsHitTestVisible = false };
        hueCanvas.Children.Add(hueMark);
        hueBar.Children.Add(hueCanvas);
        Drag(hueBar, p =>
        {
            hue = Math.Clamp(p.X / SquareW, 0, 1) * 359.9;
            Refresh();
        });

        hexBox.FontFamily = Ui.Res<FontFamily>("MonoFont");
        hexBox.VerticalAlignment = VerticalAlignment.Center;
        hexBox.Margin = new Thickness(12, 0, 0, 0);
        hexBox.TextChanged += (_, _) =>
        {
            var t = hexBox.Text.Trim();
            if (!t.StartsWith('#')) t = "#" + t;
            if (!ColorWords.IsHex(t)) return;
            typing = true;
            SetFrom(t);
            typing = false;
        };
        var hexRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        hexRow.Children.Add(preview);
        hexRow.Children.Add(hexBox);

        phrase.TextWrapping = TextWrapping.Wrap;
        phrase.MaxWidth = SquareW;

        var ok = Ui.Button("Usar esta cor", primary: true, click: (_, _) => { Result = Hex(); DialogResult = true; });
        ok.IsDefault = true;
        ok.Margin = new Thickness(0);
        var cancel = Ui.Button("Cancelar", click: (_, _) => DialogResult = false);
        cancel.IsCancel = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        Content = new Border
        {
            Padding = new Thickness(22, 18, 22, 18),
            Background = Ui.Res<Brush>("Surface"),
            Child = new StackPanel { Children = { square, hueBar, hexRow, phrase, buttons } }
        };

        SetFrom(ColorWords.IsHex(start) ? start! : "#F5B1C8");
    }

    /// <summary>Asks for a color, starting from <paramref name="start"/>; null when canceled.</summary>
    public static string? Pick(DependencyObject from, string title, string? start)
    {
        var owner = GetWindow(from) ?? Application.Current.MainWindow;
        var dialog = new ColorPicker(owner, title, start);
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    /// <summary>For the self-test: the dialog, not shown yet.</summary>
    internal static Window ForTest(Window owner, string start) => new ColorPicker(owner, "Parte de cima: cor", start);

    static void Drag(FrameworkElement area, Action<Point> at)
    {
        area.MouseLeftButtonDown += (_, e) => { area.CaptureMouse(); at(e.GetPosition(area)); };
        area.MouseMove += (_, e) => { if (area.IsMouseCaptured) at(e.GetPosition(area)); };
        area.MouseLeftButtonUp += (_, _) => area.ReleaseMouseCapture();
    }

    void SetFrom(string hex)
    {
        var (r, g, b) = ColorWords.Rgb(hex);
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf)), d = max - min;
        if (d > 0)
        {
            var h = max == rf ? 60 * (((gf - bf) / d) % 6) : max == gf ? 60 * ((bf - rf) / d + 2) : 60 * ((rf - gf) / d + 4);
            hue = h < 0 ? h + 360 : h;
        }
        sat = max == 0 ? 0 : d / max;
        val = max;
        Refresh();
    }

    string Hex()
    {
        var c = FromHsv(hue, sat, val);
        return ColorWords.Hex(c.R, c.G, c.B);
    }

    void Refresh()
    {
        hueLayer.Fill = new SolidColorBrush(FromHsv(hue, 1, 1));
        Canvas.SetLeft(svMark, sat * SquareW - 8);
        Canvas.SetTop(svMark, (1 - val) * SquareH - 8);
        Canvas.SetLeft(hueMark, hue / 359.9 * SquareW - 4);
        var hex = Hex();
        preview.Background = new SolidColorBrush(FromHsv(hue, sat, val));
        svMark.Stroke = val > 0.6 && sat < 0.4 ? Brushes.Black : Brushes.White;
        if (!typing) hexBox.Text = hex;
        phrase.Text = "No prompt: " + ColorWords.Phrase(hex);
    }

    static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
