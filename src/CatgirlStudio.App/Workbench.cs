using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using CatgirlStudio.Core;

namespace CatgirlStudio.App;

/// <summary>A saved favorite: a name, a selection and, when it was typed by hand, the prompt.</summary>
internal sealed record Favorite(string Name, Selection Selection, DateTimeOffset At, string? ManualPrompt = null);

/// <summary>
/// Everything the page keeps in app.js, without UI: the selection, locked fields, the base
/// for quick edits, favorites and the dice. Saved in %AppData%\CatgirlStudio.
/// </summary>
internal sealed class Workbench
{
    static string Folder => AppPaths.Data;
    static string StateFile => Path.Combine(Folder, "state.json");
    static string FavoritesFile => Path.Combine(Folder, "favorites.json");
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    const int MaxFavorites = 60;

    readonly Random random = new();

    public PromptEngine Engine { get; }
    public Catalog Catalog => Engine.Catalog;
    /// <summary>The outfit rules (data/outfits.json); {{outfit}} comes from here.</summary>
    public OutfitEngine Outfit => (OutfitEngine)Engine.Provider!;
    public Selection Sel { get; private set; }
    public HashSet<string> Locks { get; } = [];
    /// <summary>What was last sent (copied, opened or generated): the quick edit compares against it.</summary>
    public Selection? Baseline { get; private set; }
    public BuildResult Result { get; private set; }
    public List<Favorite> Favorites { get; } = [];

    /// <summary>The prompt as edited by hand, or null while the built one is used.</summary>
    public string? ManualPrompt { get; private set; }
    // the built prompt when the hand edit started: if it differs now, fields changed after the edit
    string? manualBase;

    /// <summary>The prompt that copy, open and generate use: the hand-edited one, or the built one.</summary>
    public string PromptText => ManualPrompt ?? Result.Text;

    /// <summary>Fields changed after the prompt was edited by hand (the edited text does not follow them).</summary>
    public bool ManualOutdated => ManualPrompt is not null && manualBase != Result.Text;

    /// <summary>Raised after any change to the selection, locks or baseline.</summary>
    public event Action? Changed;

    public Workbench(PromptEngine engine)
    {
        Engine = engine;
        Sel = engine.DefaultSelection();
        Restore();
        Result = engine.Build(Sel);
    }

    public Prefab Prefab => Engine.FindPrefab(Sel.PrefabId);

    void Update(bool save = true)
    {
        Result = Engine.Build(Sel);
        if (save) Save();
        Changed?.Invoke();
    }

    /* ------------------------------------------------------------ changes */

    public void SelectPrefab(string id)
    {
        Sel.PrefabId = Engine.FindPrefab(id).Id;
        ApplyPrefabDefaults(Prefab);
        Update();
    }

    void ApplyPrefabDefaults(Prefab prefab)
    {
        // the mascot format belongs to the mascot prefab; leaving it goes back to the portrait format
        if (!prefab.Defaults.ContainsKey("format") && Sel.Values.GetValueOrDefault("format") == "mascot" && !Locks.Contains("format"))
            Sel.Values["format"] = "portrait";
        foreach (var (name, value) in prefab.Defaults)
            if (!Locks.Contains(name)) Engine.ApplyDefault(name, value, Sel);
    }

    public void SetValue(string name, string id)
    {
        Sel.Values[name] = id;
        Update();
    }

    /// <summary>An outfit value ("outfit.mode", "outfit.top", "outfit.top.color"...): the outfit
    /// editor only offers what the piece allows, and the engine re-checks it when reading.</summary>
    public void SetOutfit(string key, string value)
    {
        Sel.Values[key] = value;
        Update();
    }

    /// <summary>The prompt typed by hand. Typing it back to the built text drops the edit.</summary>
    public void EditPrompt(string text)
    {
        if (text == Result.Text) DropManual();
        else
        {
            if (ManualPrompt is null) manualBase = Result.Text;
            ManualPrompt = text;
        }
        Save();
        Changed?.Invoke();
    }

    /// <summary>Back to the prompt built from the fields.</summary>
    public void UseBuiltPrompt()
    {
        DropManual();
        Save();
        Changed?.Invoke();
    }

    void DropManual() => (ManualPrompt, manualBase) = (null, null);

    public void SetCustom(string name, string text)
    {
        Sel.Custom[name] = text;
        Update();
    }

    /// <summary>Multi-choice: turns an option on or off; options sharing a slot exclude each other.</summary>
    public void ToggleOption(string name, OptionDef opt)
    {
        var def = Catalog.Vars[name];
        var current = Sel.Lists.GetValueOrDefault(name) ?? [];
        var next = current.Where(id => id != opt.Id).ToList();
        if (next.Count == current.Count)
        {
            if (opt.Slot is { } slot) next = next.Where(id => def.Find(id)?.Slot != slot).ToList();
            next.Add(opt.Id);
        }
        Sel.Lists[name] = next;
        Update();
    }

    public void ToggleLock(string name)
    {
        if (!Locks.Remove(name)) Locks.Add(name);
        Update();
    }

    public void Randomize(string name)
    {
        SetRandom(name);
        Update();
    }

    /// <summary>Re-rolls every reached, unlocked field (three passes, so fields a new choice opens get rolled too).</summary>
    public void RandomizeAll()
    {
        var touched = new HashSet<string>();
        for (var pass = 0; pass < 3; pass++)
            foreach (var name in Engine.Build(Sel).Used)
                if (!Locks.Contains(name) && touched.Add(name)) SetRandom(name);
        Update();
    }

    void SetRandom(string name)
    {
        if (name == OutfitEngine.Var)
        {
            Outfit.Randomize(Sel, random);
            return;
        }
        var def = Catalog.Vars[name];
        if (!def.Multi)
        {
            Sel.Values[name] = def.Options[random.Next(def.Options.Count)].Id;
            return;
        }
        // up to two, never two from the same slot
        var count = random.Next(3);
        var picked = new List<string>();
        var slots = new HashSet<string>();
        foreach (var o in def.Options.OrderBy(_ => random.Next()))
        {
            if (picked.Count >= count) break;
            if (o.Slot is { } s && !slots.Add(s)) continue;
            picked.Add(o.Id);
        }
        Sel.Lists[name] = picked;
    }

    public void RandomPose()
    {
        var others = Catalog.Prefabs.Where(p => p.Id != Sel.PrefabId).ToList();
        SelectPrefab(others[random.Next(others.Count)].Id);
    }

    public void Reset()
    {
        Locks.Clear();
        DropManual();
        Sel = Engine.DefaultSelection();
        ApplyPrefabDefaults(Prefab);
        Update();
    }

    /// <summary>Loads a saved selection (favorite, image). A hand-edited prompt saved with it comes
    /// back too; otherwise the prompt goes back to the built one.</summary>
    public void LoadSelection(Selection s, string? manualPrompt = null)
    {
        DropManual();
        var next = Engine.DefaultSelection(Engine.FindPrefab(s.PrefabId).Id);
        // the outfit starts from its defaults: a property the saved selection left at its
        // default (and so did not store) must not keep the value picked since
        foreach (var (k, v) in Sel.Values) if (!k.StartsWith("outfit.")) next.Values[k] = v;
        foreach (var (k, v) in Sel.Lists) next.Lists[k] = [.. v];
        foreach (var (k, v) in s.Values) next.Values[k] = v;
        foreach (var (k, v) in s.Lists) next.Lists[k] = [.. v];
        next.Custom.Clear();
        foreach (var (k, v) in s.Custom) next.Custom[k] = v;
        Sel = next;
        Result = Engine.Build(Sel);
        if (manualPrompt is not null && manualPrompt != Result.Text) (ManualPrompt, manualBase) = (manualPrompt, Result.Text);
        Update();
    }

    /// <summary>The current selection becomes the base the quick edit compares against.</summary>
    public void MarkSent() => SetBaseline(Sel);

    public void SetBaseline(Selection s)
    {
        Baseline = s.Clone();
        Update();
    }

    public EditRequestResult EditRequest() => Baseline is null ? new EditRequestResult([], "") : Engine.EditRequest(Baseline, Sel);

    /* ---------------------------------------------------------- favorites */

    public void SaveFavorite(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = Prefab.Name + " " + DateTime.Now.ToString("dd/MM/yyyy");
        Favorites.Insert(0, new Favorite(name.Trim(), Sel.Clone(), DateTimeOffset.Now, ManualPrompt));
        if (Favorites.Count > MaxFavorites) Favorites.RemoveRange(MaxFavorites, Favorites.Count - MaxFavorites);
        SaveFavorites();
        Changed?.Invoke();
    }

    public void DeleteFavorite(Favorite f)
    {
        Favorites.Remove(f);
        SaveFavorites();
        Changed?.Invoke();
    }

    /* -------------------------------------------------------------- disk */

    void Restore()
    {
        try
        {
            if (File.Exists(StateFile))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(StateFile));
                var root = doc.RootElement;
                if (root.TryGetProperty("selection", out var sel)) LoadInto(Selection.FromJson(sel, Engine));
                if (root.TryGetProperty("locks", out var locks))
                    foreach (var l in locks.EnumerateArray()) if (l.GetString() is { } n && Catalog.Vars.ContainsKey(n)) Locks.Add(n);
                if (root.TryGetProperty("baseline", out var b) && b.ValueKind == JsonValueKind.Object) Baseline = Selection.FromJson(b, Engine);
                if (root.TryGetProperty("manualPrompt", out var mp) && mp.ValueKind == JsonValueKind.String)
                {
                    ManualPrompt = mp.GetString();
                    manualBase = root.TryGetProperty("manualBase", out var mb) && mb.ValueKind == JsonValueKind.String ? mb.GetString() : null;
                }
            }
            else ApplyPrefabDefaults(Prefab);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            ApplyPrefabDefaults(Prefab); // a broken state file just means starting fresh
        }
        try
        {
            if (File.Exists(FavoritesFile))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FavoritesFile));
                foreach (var f in doc.RootElement.EnumerateArray())
                    Favorites.Add(new Favorite(
                        f.GetProperty("name").GetString() ?? "",
                        Selection.FromJson(f.GetProperty("selection"), Engine),
                        f.TryGetProperty("at", out var at) && at.TryGetDateTimeOffset(out var when) ? when : DateTimeOffset.Now,
                        f.TryGetProperty("prompt", out var fp) && fp.ValueKind == JsonValueKind.String ? fp.GetString() : null));
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or KeyNotFoundException)
        {
            // unreadable favorites: start with none rather than fail to open
        }
    }

    void LoadInto(Selection s)
    {
        Sel = Engine.DefaultSelection(Engine.FindPrefab(s.PrefabId).Id);
        foreach (var (k, v) in s.Values) Sel.Values[k] = v;
        foreach (var (k, v) in s.Lists) Sel.Lists[k] = v;
        foreach (var (k, v) in s.Custom) Sel.Custom[k] = v;
    }

    void Save()
    {
        var state = new JsonObject
        {
            ["selection"] = Sel.ToJson(),
            ["locks"] = new JsonArray(Locks.Select(l => (JsonNode)JsonValue.Create(l)).ToArray()),
            ["baseline"] = Baseline?.ToJson(),
            ["manualPrompt"] = ManualPrompt,
            ["manualBase"] = manualBase
        };
        Write(StateFile, state.ToJsonString(Pretty));
    }

    void SaveFavorites()
    {
        var list = new JsonArray(Favorites.Select(f => (JsonNode)new JsonObject
        {
            ["name"] = f.Name,
            ["selection"] = f.Selection.ToJson(),
            ["at"] = f.At.ToString("o"),
            ["prompt"] = f.ManualPrompt
        }).ToArray());
        Write(FavoritesFile, list.ToJsonString(Pretty));
    }

    static void Write(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // saving is a convenience; the app keeps working without it
        }
    }
}
