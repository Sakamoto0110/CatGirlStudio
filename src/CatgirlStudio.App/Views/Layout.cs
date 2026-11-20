using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CatgirlStudio.App.Views;

/// <summary>The sizes the user dragged (column widths, panel splits), kept in layout.json.</summary>
internal static class Layout
{
    static string FilePath => Path.Combine(AppPaths.Data, "layout.json");
    static Dictionary<string, double>? values;

    static Dictionary<string, double> Values
    {
        get
        {
            if (values != null) return values;
            values = [];
            try
            {
                if (File.Exists(FilePath))
                    values = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(FilePath)) ?? [];
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
            return values;
        }
    }

    public static double Get(string key, double fallback, double min, double max) =>
        Values.TryGetValue(key, out var v) && double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;

    public static void Set(params (string Key, double Value)[] entries)
    {
        foreach (var (k, v) in entries) Values[k] = Math.Round(v, 3);
        try
        {
            Directory.CreateDirectory(AppPaths.Data);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Values));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static void Forget()
    {
        Values.Clear();
        try { File.Delete(FilePath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>A splitter between two grid columns (vertical bar) or rows (horizontal bar):
    /// invisible until hovered, then a thin accent line.</summary>
    public static GridSplitter Splitter(bool betweenColumns, Action dragged)
    {
        var s = new GridSplitter
        {
            Style = Ui.Style(betweenColumns ? "SplitterV" : "SplitterH"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = betweenColumns ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Cursor = betweenColumns ? Cursors.SizeWE : Cursors.SizeNS,
            ToolTip = "Arraste para ajustar · clique duas vezes para voltar ao padrão"
        };
        s.DragCompleted += (_, _) => dragged();
        return s;
    }

    /// <summary>Two rows sharing the height in a proportion the user drags; returns the ratio of
    /// the top one when asked to save.</summary>
    public static Grid SplitRows(string key, UIElement top, UIElement bottom, double defaultTop, double minTop, double minBottom)
    {
        var grid = new Grid();
        var ratio = Get(key, defaultTop, 0.15, 0.85);
        var a = new RowDefinition { Height = new GridLength(ratio, GridUnitType.Star), MinHeight = minTop };
        var b = new RowDefinition { Height = new GridLength(1 - ratio, GridUnitType.Star), MinHeight = minBottom };
        grid.RowDefinitions.Add(a);
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        grid.RowDefinitions.Add(b);
        var splitter = Splitter(betweenColumns: false, () =>
        {
            var total = a.ActualHeight + b.ActualHeight;
            if (total > 0) Set((key, a.ActualHeight / total));
        });
        splitter.MouseDoubleClick += (_, _) =>
        {
            a.Height = new GridLength(defaultTop, GridUnitType.Star);
            b.Height = new GridLength(1 - defaultTop, GridUnitType.Star);
            Set((key, defaultTop));
        };
        Grid.SetRow(top, 0);
        Grid.SetRow(splitter, 1);
        Grid.SetRow(bottom, 2);
        grid.Children.Add(top);
        grid.Children.Add(splitter);
        grid.Children.Add(bottom);
        return grid;
    }
}
