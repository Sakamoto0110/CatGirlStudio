using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatgirlStudio.App.Studio;
using CatgirlStudio.App.Theme;
using CatgirlStudio.Core;

namespace CatgirlStudio.App;

/// <summary>Where the app keeps its state (selection, favorites, studio settings).</summary>
internal static class AppPaths
{
    public static string Data { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatgirlStudio");
}

/// <summary>
///   CatgirlStudio.exe                    the app
///   --fake                               test mode: images are drawn locally, nothing is charged
///   --out DIR                            save images in DIR instead of Pictures\Catgirls\api
///   --data DIR                           keep state and favorites in DIR instead of %AppData%\CatgirlStudio
///   --accent #RRGGBB                     try another accent color (the default is in Theme.xaml)
///   --screenshot FILE                    save a picture of the window once it is shown, then quit
///   --fake --selftest FILE               walk the main flow and write what happened to FILE (+ FILE.png)
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        System.Diagnostics.Debug.WriteLine("Starting CatGirlStudio.");
        var args = e.Args;
        if (Arg(args, "--data") is { } data) AppPaths.Data = data;
        ThemeColors.Apply(Resources, Arg(args, "--accent"));

        var catalog = Catalog.Load(Resource("CatgirlStudio.catalog.json"), Resource("CatgirlStudio.prefabs.json"));
        var outfit = new OutfitEngine(catalog, Resource("CatgirlStudio.outfits.json"));
        var engine = new PromptEngine(catalog, outfit);
        IImageApi api = args.Contains("--fake") ? new FakeImages() : new OpenAiImages();
        var window = new MainWindow(new Workbench(engine), api, new ImageStore(Arg(args, "--out") ?? ImageStore.DefaultFolder));
        MainWindow = window;

        if (Arg(args, "--screenshot") is { } shot)
            window.ContentRendered += async (_, _) => { await Task.Delay(800); Snap(window, shot); window.Close(); };
        if (Arg(args, "--selftest") is { } report && api.Fake)
            window.ContentRendered += async (_, _) => await SelfTest.Run(window, report);
        window.Show();
    }

    static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"embedded resource {name} is missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The window draws itself into a bitmap; the screen is never read.</summary>
    internal static void Snap(Window window, string file)
    {
        if (window.Content is not FrameworkElement root) return;
        var dpi = VisualTreeHelper.GetDpi(root);
        var bmp = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(root);
        File.WriteAllBytes(file, Png.Encode(bmp));
    }
}
