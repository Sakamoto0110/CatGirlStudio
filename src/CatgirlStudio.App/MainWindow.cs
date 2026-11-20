using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatgirlStudio.App.Studio;
using CatgirlStudio.App.Theme;
using CatgirlStudio.App.Views;

namespace CatgirlStudio.App;

/// <summary>The workbench: poses | character sheet | prompt | image (API), as four cards.</summary>
internal sealed class MainWindow : Window
{
    readonly Workbench wb;

    // for the --selftest run
    internal Workbench Bench => wb;
    internal StudioPanel Studio { get; }
    internal CharacterSheet Sheet { get; }
    internal PromptPanel Prompt { get; }

    public MainWindow(Workbench wb, IImageApi api, ImageStore store)
    {
        this.wb = wb;
        Title = "Catgirl Studio" + (api.Fake ? " · modo de teste" : "");
        Width = 1760;
        Height = 1000;
        MinWidth = 1280;
        MinHeight = 760;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Res<Brush>("Bg");
        Foreground = Ui.Res<Brush>("Ink");
        FontFamily = Ui.Res<FontFamily>("UiFont");
        FontSize = 13;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/neko-purple-light.ico"));

        var top = new DockPanel { Margin = new Thickness(26, 18, 26, 14), LastChildFill = false };
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Resources/neko-purple-light.ico")), Width = 30, Height = 30, Margin = new Thickness(0, 0, 12, 0) });
        brand.Children.Add(new StackPanel
        {
            Children =
            {
                Ui.Text("Catgirl Studio", "Title"),
                Ui.Text("monte a personagem, gere e ajuste a imagem", "Small")
            }
        });
        DockPanel.SetDock(brand, Dock.Left);
        top.Children.Add(brand);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(Ui.IconTextButton(Ui.IconDice, "Sortear campos", click: (_, _) => wb.RandomizeAll()));
        actions.Children.Add(Ui.Button("Pose aleatória", click: (_, _) => wb.RandomPose()));
        actions.Children.Add(Ui.Button("Restaurar padrão", click: (_, _) => Reset()));
        DockPanel.SetDock(actions, Dock.Right);
        top.Children.Add(actions);

        var prompt = Prompt = new PromptPanel(wb);
        Studio = new StudioPanel(wb, api, store, prompt);

        // four cards with draggable gaps between them; the character sheet takes what is left
        var grid = new Grid { Margin = new Thickness(26, 6, 26, 26) };
        Sheet = new CharacterSheet(wb);
        var cards = new UIElement[] { new PoseShelf(wb), Sheet, prompt, Studio };
        for (var i = 0; i < cards.Length; i++)
        {
            if (i > 0)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                var splitter = Layout.Splitter(betweenColumns: true, SaveColumns);
                splitter.MouseDoubleClick += (_, _) => ResetColumns();
                Grid.SetColumn(splitter, grid.ColumnDefinitions.Count - 1);
                grid.Children.Add(splitter);
            }
            var column = Columns[i];
            var def = new ColumnDefinition
            {
                Width = column.Default is { } px ? new GridLength(Layout.Get(column.Key, px, column.Min, column.Max)) : new GridLength(1, GridUnitType.Star),
                MinWidth = column.Min
            };
            grid.ColumnDefinitions.Add(def);
            columns.Add(def);
            var card = Ui.Card(cards[i]);
            Grid.SetColumn(card, grid.ColumnDefinitions.Count - 1);
            grid.Children.Add(card);
        }

        var root = new DockPanel { Background = Ui.Res<Brush>("Bg") };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(grid);
        Content = root;
        SourceInitialized += (_, _) => ThemeColors.DarkTitleBar(this);
    }

    // poses | character sheet (takes the rest) | prompt | image
    static readonly (string Key, double? Default, double Min, double Max)[] Columns =
    [
        ("poses", 268, 220, 520), ("sheet", null, 440, double.PositiveInfinity), ("prompt", 438, 340, 1000), ("studio", 458, 360, 1100)
    ];
    readonly List<ColumnDefinition> columns = [];

    void SaveColumns() => Layout.Set(Columns.Select((c, i) => (c.Key, columns[i].ActualWidth)).Where((_, i) => Columns[i].Default is not null).ToArray());

    void ResetColumns()
    {
        for (var i = 0; i < Columns.Length; i++)
            if (Columns[i].Default is { } px) columns[i].Width = new GridLength(px);
        SaveColumns();
    }

    void Reset()
    {
        if (MessageBox.Show(this, "Voltar todos os campos ao padrão e destravar tudo?", "Restaurar padrão", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            wb.Reset();
    }
}
