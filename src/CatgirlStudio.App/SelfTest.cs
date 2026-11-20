using System.IO;
using CatgirlStudio.App.Studio;
using CatgirlStudio.Core;

namespace CatgirlStudio.App;

/// <summary>--fake --selftest FILE: walks the main flow through the real window and writes what
/// happened to FILE, plus a picture of the window to FILE.png. Nothing is charged (test mode).</summary>
internal static class SelfTest
{
    public static async Task Run(MainWindow window, string report)
    {
        var log = new List<string>();
        var wb = window.Bench;
        var st = window.Studio;
        string Wears() => wb.Result.Text.Split('\n').First(l => l.StartsWith("An adult")).Split("She wears ")[1].Split(". ")[0];
        try
        {
            wb.SelectPrefab("mascote");
            log.Add("format after Mascote: " + wb.Sel.Values["format"]);

            // outfit engine: pieces mode, a hoodie opens hood and sleeves; trousers hide the socks
            wb.SetOutfit("outfit.mode", "pieces");
            wb.SetOutfit("outfit.top", "hoodie");
            wb.SetOutfit("outfit.top.hood", "up");
            wb.SetOutfit("outfit.top.color", "lavender");
            log.Add("pieces, hoodie: slots " + string.Join(",", wb.Outfit.State(wb.Sel).Select(s => s.Slot.Id + "=" + s.Piece.Id + "[" + string.Join("/", s.Props.Select(p => p.Key)) + "]")));
            log.Add("  wears: " + Wears());
            wb.SetOutfit("outfit.bottom", "trousers");
            log.Add("trousers -> socks slot shown: " + wb.Outfit.State(wb.Sel).Any(s => s.Slot.Id == "legs"));
            wb.SetOutfit("outfit.bottom", "pleatedstripe");
            wb.SetOutfit("outfit.legs", "thighstripe");
            log.Add("striped skirt + socks -> wears: " + Wears());
            wb.SetValue("theme", "idol");
            wb.SetOutfit("outfit.mode", "ready");
            log.Add("idol style offers: " + string.Join(",", wb.Outfit.Offered("full", wb.Sel).Where(p => p.Theme != null).Select(p => p.Id)));
            wb.SetOutfit("outfit.full", "theme-idol");
            log.Add("  wears: " + Wears());
            wb.SetValue("theme", "none");
            log.Add("style back to none -> outfit falls back to: " + wb.Outfit.PieceIn("full", wb.Sel).Id);

            var acc = wb.Catalog.Vars["accessory"];
            wb.ToggleOption("accessory", acc.Find("choker")!);
            wb.ToggleOption("accessory", acc.Find("glasses")!);
            wb.ToggleOption("accessory", acc.Find("scarf")!); // same slot as the choker
            log.Add("accessories (choker, glasses, then scarf): " + string.Join(",", wb.Sel.Lists["accessory"]));

            await st.Generate();
            log.Add("generate -> " + st.Message + " | " + st.Current?.Id + " " + st.Current?.ShownSize);
            var first = st.Current!;

            wb.SetValue("eyeColor", "amber");
            wb.SetOutfit("outfit.full.main", "navy");
            log.Add("edit request: " + wb.EditRequest().Text.Replace("\n", " / "));
            await st.ApplyEdit();
            log.Add("apply edit -> " + st.Message + " | parent " + st.Current?.Parent + " | prompt starts: " + st.Current?.Prompt[..30]);

            st.Pick(first);
            log.Add("picked the first image -> eyes " + wb.Sel.Values["eyeColor"] + ", main color " + wb.Sel.Values.GetValueOrDefault("outfit.full.main") + ", nothing to edit: " + (wb.EditRequest().Text.Length == 0));

            wb.SelectPrefab("esc-carteira");
            log.Add("school pose -> format " + wb.Sel.Values["format"] + ", outfit " + wb.Outfit.PieceIn("full", wb.Sel).Id);
            wb.SetValue("format", "wide");
            await st.Generate();
            log.Add("wide wallpaper -> saved " + st.Current?.ShownSize);

            if (Upscaler.Find() is not null)
            {
                st.Pick(first);
                foreach (var (scale, tta) in new[] { (2, false), (4, false), (2, true) })
                {
                    var started = DateTime.Now;
                    await st.UpscaleWith(scale, tta);
                    var file = Path.Combine(Path.GetDirectoryName(first.Path)!, $"{scale}x" + (tta ? "-tta" : ""), Path.GetFileName(first.Path));
                    if (File.Exists(file))
                    {
                        var p = Png.Read(file);
                        log.Add($"{scale}x{(tta ? " TTA" : "")} -> {p.Width}x{p.Height}, alpha {ImageOps.MinAlpha(p.Bgra)}..{ImageOps.MaxAlpha(p.Bgra)}, {(int)(DateTime.Now - started).TotalSeconds} s");
                    }
                    else log.Add($"{scale}x{(tta ? " TTA" : "")} -> " + st.Message);
                }
            }
            else log.Add("upscale -> skipped (Real-ESRGAN not found)");
        }
        catch (Exception e)
        {
            log.Add("EXCEPTION " + e);
        }
        await Task.Delay(500);
        App.Snap(window, report + ".png");
        File.WriteAllLines(report, log);
        window.Close();
    }
}
