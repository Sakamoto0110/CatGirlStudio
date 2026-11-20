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
            // at home: a cropped top switches the modesty rule, a regular one switches it back
            string Rule() => wb.Result.Text.Split('\n').First(l => l.Contains("non-revealing") || l.Contains("homewear"));
            wb.SetOutfit("outfit.top", "croptee");
            wb.SetOutfit("outfit.bottom", "highwaist");
            log.Add("cropped tee + sweatpants -> wears: " + Wears());
            log.Add("  rule: " + Rule());
            wb.SetOutfit("outfit.top", "hoodie");
            log.Add("back to the hoodie -> rule: " + Rule() + " | fields counted: " + wb.Result.Used.Count + ", modesty is a field: " + wb.Result.Used.Contains(OutfitEngine.ModestyVar));
            wb.SetOutfit("outfit.mode", "ready");
            wb.SetOutfit("outfit.full", "cropjamas");
            log.Add("ready cropjamas -> wears: " + Wears() + " | socks slot: " + wb.Outfit.State(wb.Sel).Any(s => s.Slot.Id == "legs"));
            log.Add("  rule: " + Rule());
            wb.SetOutfit("outfit.mode", "pieces");
            wb.SetOutfit("outfit.bottom", "pleatedstripe");
            wb.SetOutfit("outfit.legs", "thighstripe");
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

            for (var i = 0; i < 20; i++) wb.RandomizeAll(); // must not trip over the modesty placeholder
            log.Add("roll everything 20 times -> ok");

            // picked colors: "name (#HEX)" in the prompt, named in the quick edit, kept by a favorite
            wb.SelectPrefab("mascote");
            wb.SetOutfit("outfit.mode", "pieces");
            wb.SetOutfit("outfit.top", "hoodie");
            wb.SetValue("accentColor", "teal");
            wb.MarkSent();
            wb.SetOutfit("outfit.top.color", "#C8A2C8");
            wb.SetValue("hairColor", "#7FB3D5");
            log.Add("picked colors -> wears: " + Wears());
            log.Add("  character: " + wb.Result.Text.Split('\n').First(l => l.StartsWith("An adult")).Split(". ")[0]);
            log.Add("  edit: " + wb.EditRequest().Text.Split('\n').Where(l => l.StartsWith("- ")).Aggregate((a, b) => a + " / " + b));
            wb.SaveFavorite("cores escolhidas");
            wb.SetValue("hairColor", "black");
            wb.LoadSelection(wb.Favorites[0].Selection);
            log.Add("  favorite back -> hair " + wb.Sel.Values["hairColor"] + ", top " + wb.Sel.Values["outfit.top.color"]);
            wb.DeleteFavorite(wb.Favorites[0]);

            // hand-edited prompt: generate sends it, a field change marks it outdated, the image brings it back
            const string Extra = "Extra: a tiny star sticker on her cheek.";
            wb.EditPrompt(wb.Result.Text + "\n\n" + Extra);
            log.Add("hand edit -> manual: " + (wb.ManualPrompt != null) + ", outdated: " + wb.ManualOutdated);
            await st.Generate();
            var edited = st.Current!;
            log.Add("  generated with it: " + edited.Prompt.EndsWith(Extra) + ", marked hand-edited: " + edited.HandEdited);
            wb.SetValue("eyeColor", "red");
            log.Add("  eyes changed -> outdated: " + wb.ManualOutdated + ", still the edited text: " + wb.PromptText.EndsWith(Extra));
            wb.UseBuiltPrompt();
            log.Add("  back to built -> manual: " + (wb.ManualPrompt != null) + ", red eyes in it: " + wb.PromptText.Contains("crimson"));
            st.Pick(edited);
            log.Add("  picked that image -> edited text back: " + (wb.ManualPrompt == edited.Prompt) + ", outdated: " + wb.ManualOutdated);
            wb.UseBuiltPrompt();
            wb.EditPrompt(wb.Result.Text); // typing it back to the built text is no edit at all
            log.Add("  typed back to the built text -> manual: " + (wb.ManualPrompt != null));

            // a hand edit of the quick-edit request is what gets applied
            wb.SetValue("eyeColor", wb.Sel.Values["eyeColor"] == "amber" ? "violet" : "amber"); // always a change, whatever the dice left
            log.Add("  baseline eyes " + wb.Baseline?.Values.GetValueOrDefault("eyeColor") + ", now " + wb.Sel.Values["eyeColor"] + ", request length " + wb.EditRequest().Text.Length);
            window.Prompt.TypeEdit(wb.EditRequest().Text.Replace("Do not change anything else.", "- eye shine: add a small heart-shaped highlight\nDo not change anything else."));
            await st.ApplyEdit();
            log.Add("  applied hand-edited request -> " + st.Current!.Prompt.Contains("heart-shaped highlight") + " | " + st.Message + " | box: " + window.Prompt.EditText.Replace("\n", " / "));

            // layout: a wide window splits the character sheet in two blocks
            log.Add("window " + (int)window.ActualWidth + " px -> two blocks: " + window.Sheet.TwoBlocks);
            window.Width = 2400;
            await Task.Delay(700);
            log.Add("window " + (int)window.ActualWidth + " px -> two blocks: " + window.Sheet.TwoBlocks);
            App.Snap(window, report + "-wide.png");
            var picker = Views.ColorPicker.ForTest(window, "#C8A2C8");
            picker.Show();
            await Task.Delay(500);
            App.Snap(picker, report + "-picker.png");
            picker.Close();
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
