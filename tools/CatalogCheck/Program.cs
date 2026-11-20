using System.Text.Json;
using System.Text.RegularExpressions;
using CatgirlStudio.Core;
using Jint;

// Checks the app's catalog (data/catalog.json, prefabs.json, outfits.json).
//
//   dotnet run --project tools/CatalogCheck                  # checks, exit code 1 on problems
//   dotnet run --project tools/CatalogCheck -- --show        # also prints a sample prompt
//   dotnet run --project tools/CatalogCheck -- --compare-web # how the app's prompts differ from the
//                                                            # frozen web page (web/) for the same old selections
var root = FindRoot();
string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));
var catalogJson = Read("data/catalog.json");
var prefabsJson = Read("data/prefabs.json");
var outfitsJson = Read("data/outfits.json");
var problems = new List<string>();

// duplicate keys: JSON allows them and the last one silently wins
foreach (var (file, json, sections) in new[] { ("catalog.json", catalogJson, new[] { "blocks", "lists", "vars" }), ("outfits.json", outfitsJson, new[] { "props", "slots" }) })
{
    using var doc = JsonDocument.Parse(json);
    foreach (var section in sections)
        foreach (var dup in doc.RootElement.GetProperty(section).EnumerateObject().GroupBy(p => p.Name).Where(g => g.Count() > 1))
            problems.Add($"{file} declares {section}.{dup.Key} {dup.Count()} times (the last one wins)");
}

var catalog = Catalog.Load(catalogJson, prefabsJson);
var outfit = new OutfitEngine(catalog, outfitsJson);
var engine = new PromptEngine(catalog, outfit);
var glitch = new Regex(@"\s[.,]|\.\.|,,|, ,| {2}|\b(?:and|with)[.,]||\{\{|\{[A-Za-z]+\}");
bool Bad(string text) => text.Split('\n').Any(glitch.IsMatch);

Selection Base(Prefab p)
{
    var s = engine.DefaultSelection(p.Id);
    foreach (var (k, v) in p.Defaults)
        if (!engine.ApplyDefault(k, v, s)) problems.Add($"prefab {p.Id}: invalid default {k}={v}");
    return s;
}

// prefabs
var lengths = new List<int>();
foreach (var dup in catalog.Prefabs.GroupBy(p => p.Id).Where(g => g.Count() > 1)) problems.Add("duplicate prefab id " + dup.Key);
foreach (var p in catalog.Prefabs)
{
    if (!catalog.Categories.Contains(p.Category)) problems.Add($"prefab {p.Id}: unknown category {p.Category}");
    var text = engine.Build(Base(p)).Text;
    if (Bad(text)) problems.Add($"prefab {p.Id}: placeholder or spacing glitch in the prompt");
    lengths.Add(text.Length);
}

// {{var.field}}: every option of var carries the field
var texts = catalog.Blocks.Values.Concat(catalog.Prefabs.Select(p => p.Pose)).Concat(catalog.Vars.Values.SelectMany(v => v.Options.Select(o => o.En)));
foreach (var t in texts)
    foreach (Match m in Regex.Matches(t, @"\{\{(\w+)\.(\w+)\}\}"))
    {
        if (!catalog.Vars.TryGetValue(m.Groups[1].Value, out var def)) { problems.Add($"{m.Value}: unknown variable"); continue; }
        foreach (var o in def.Options.Where(o => o.Field(m.Groups[2].Value) is null)) problems.Add($"var {def.Key}={o.Id}: missing field \"{m.Groups[2].Value}\"");
    }

// every option of every variable, in a selection that reaches the variable
bool Mentions(string text, string name) => Regex.Matches(text, @"\{\{(?:list:)?([\w,.]+)\}\}").Any(m => m.Groups[1].Value.Split(',').Contains(name));
Selection? ContextFor(string name, HashSet<string> seen)
{
    var start = Base(catalog.Prefabs[0]);
    if (engine.Build(start).Used.Contains(name)) return start;
    foreach (var p in catalog.Prefabs.Where(p => Mentions(p.Pose, name)))
        if (Base(p) is var s && engine.Build(s).Used.Contains(name)) return s;
    if (!seen.Add(name)) return null;
    foreach (var parent in catalog.Vars.Values)
        foreach (var o in parent.Options.Where(o => Mentions(o.En, name) || o.Fields.Values.Any(f => Mentions(f, name))))
        {
            if (ContextFor(parent.Key, seen) is not { } c) continue;
            var s = c.Clone();
            s.Values[parent.Key] = o.Id;
            if (engine.Build(s).Used.Contains(name)) return s;
        }
    return null;
}
foreach (var def in catalog.Vars.Values)
{
    var ctx = ContextFor(def.Key, []);
    if (ctx is null) { problems.Add($"var {def.Key}: no selection reaches it, so it never shows up"); continue; }
    foreach (var dup in def.Options.GroupBy(o => o.Id).Where(g => g.Count() > 1)) problems.Add($"var {def.Key}: duplicate option id {dup.Key}");
    foreach (var o in def.Options)
    {
        var s = ctx.Clone();
        if (def.Multi) s.Lists[def.Key] = [o.Id]; else s.Values[def.Key] = o.Id;
        if (Bad(engine.Build(s).Text)) problems.Add($"var {def.Key}={o.Id}: placeholder or spacing glitch");
        foreach (Match m in Regex.Matches(o.En, @"\{\{list:([\w,.]+)\}\}"))
            foreach (var item in m.Groups[1].Value.Split(',').Where(i => catalog.Vars.ContainsKey(i)))
                foreach (var io in catalog.Vars[item].Options.Where(x => x.En.Contains(',')))
                    problems.Add($"var {item}={io.Id}: comma inside a list item");
    }
    if (def.Multi)
    {
        foreach (var d in def.DefaultMany.Where(d => def.Find(d) is null)) problems.Add($"var {def.Key}: default {d} is not an option");
        var all = ctx.Clone();
        var slots = new HashSet<string>();
        all.Lists[def.Key] = def.Options.Where(o => o.Slot is null || slots.Add(o.Slot)).Select(o => o.Id).ToList();
        if (Bad(engine.Build(all).Text)) problems.Add($"var {def.Key}: glitch with several options at once");
    }
    else if (def.Find(def.Default) is null) problems.Add($"var {def.Key}: default {def.Default} is not an option");
}

// outfits: every piece of every slot, in every mode that uses the slot, with every option of its props
var themes = catalog.Vars["theme"].Options.Select(o => o.Id).ToHashSet();
foreach (var mode in outfit.Modes)
    foreach (var slotId in mode.Slots)
    {
        if (!outfit.Slots.ContainsKey(slotId)) { problems.Add($"outfits.json: mode {mode.Id} uses unknown slot {slotId}"); continue; }
        foreach (var piece in outfit.Slots[slotId].Pieces)
        {
            var s = Base(catalog.Prefabs[0]);
            s.Values["outfit.mode"] = mode.Id;
            if (piece.Theme is not null) s.Values["theme"] = piece.Theme;
            // legs and feet only show under a main piece that leaves them in view
            if (slotId == "legs") s.Values["outfit." + mode.Main] = outfit.Slots[mode.Main].Pieces.First(p => p.Legs && p.Theme is null).Id;
            if (slotId == "feet") s.Values["outfit." + mode.Main] = outfit.Slots[mode.Main].Pieces.First(p => !p.Covers.Contains("feet") && p.Theme is null).Id;
            s.Values["outfit." + slotId] = piece.Id;
            var where = $"outfit {mode.Id}/{slotId}={piece.Id}";
            if (!outfit.State(s).Any(x => x.Piece.Id == piece.Id && x.Slot.Id == slotId)) { problems.Add($"{where}: does not show up"); continue; }
            if (Bad(engine.Build(s).Text)) problems.Add($"{where}: placeholder or spacing glitch");
            foreach (var p in piece.Props)
                foreach (var o in p.Options)
                {
                    var v = s.Clone();
                    v.Values[$"outfit.{slotId}.{p.Key}"] = o.Id;
                    if (Bad(engine.Build(v).Text)) problems.Add($"{where} {p.Key}={o.Id}: placeholder or spacing glitch");
                }
        }
    }
foreach (var slot in outfit.Slots.Values)
    foreach (var piece in slot.Pieces)
    {
        var where = $"outfit {slot.Id}={piece.Id}";
        var used = Regex.Matches(piece.En, @"(?<!\{)\{([A-Za-z]+)\}(?!\})").Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var u in used.Where(u => piece.Props.All(p => p.Key != u))) problems.Add($"{where}: text uses {{{u}}} but the piece declares no such prop");
        foreach (var p in piece.Props.Where(p => !used.Contains(p.Key))) problems.Add($"{where}: prop {p.Key} is declared but the text never uses it");
        foreach (var p in piece.Props)
        {
            if (p.Options.Count == 0) problems.Add($"{where}: prop {p.Key} allows no option");
            var def = p.Default ?? p.Def.Default;
            if (def is not null && !def.StartsWith('@') && p.Options.All(o => o.Id != def)) problems.Add($"{where}: prop {p.Key} default {def} is not allowed");
            if (p.Fixed is not null && p.Def.Options.All(o => o.Id != p.Fixed)) problems.Add($"{where}: prop {p.Key} fixed to unknown {p.Fixed}");
        }
        if (slot.Id != "full" && piece.En.Contains(',')) problems.Add($"{where}: comma inside a list item");
        if (piece.Theme is not null && !themes.Contains(piece.Theme)) problems.Add($"{where}: unknown theme {piece.Theme}");
    }

Console.WriteLine($"prefabs: {catalog.Prefabs.Count} | variables: {catalog.Vars.Count} | outfit pieces: {outfit.Slots.Values.Sum(s => s.Pieces.Count)} | prompt length {lengths.Min()}-{lengths.Max()} chars");
Console.WriteLine($"problems: {problems.Count}");
foreach (var p in problems.Distinct()) Console.WriteLine("  - " + p);
if (args.Contains("--show")) Console.WriteLine("\n=== sample (" + catalog.Prefabs[0].Id + ") ===\n" + engine.Build(Base(catalog.Prefabs[0])).Text);
if (args.Contains("--compare-web")) CompareWeb();
return problems.Count > 0 ? 1 : 0;

// The web page (web/) is frozen on the catalog from before the outfit engine. For the same old
// selections (every prefab, every option, random combinations), print how many prompts the app
// now writes differently, and a few examples, so wording changes are seen on purpose.
void CompareWeb()
{
    var js = new Engine(o => o.LimitRecursion(2000));
    js.Execute("var window = globalThis;");
    js.Execute(Read("web/engine.js"));
    js.SetValue("catalogJson", Read("web/data/catalog.json"));
    js.SetValue("prefabsJson", Read("web/data/prefabs.json"));
    js.Execute("var CATALOG = JSON.parse(catalogJson), P = JSON.parse(prefabsJson), PREFABS = P.prefabs, CATEGORIES = P.categories;");
    js.Execute(Read("tools/CatalogCheck/checks.js"));
    using var cases = JsonDocument.Parse(js.Evaluate("parityCases()").AsString());
    int same = 0, differ = 0;
    var kinds = new Dictionary<string, int>();
    foreach (var c in cases.RootElement.GetProperty("cases").EnumerateArray())
    {
        var app = engine.Build(Selection.FromJson(c.GetProperty("sel"), engine)).Text;
        var web = c.GetProperty("text").GetString()!;
        if (app == web) { same++; continue; }
        differ++;
        // group by what changed: the differing stretch of text, from the first different character
        var at = Enumerable.Range(0, Math.Min(app.Length, web.Length)).FirstOrDefault(k => app[k] != web[k]);
        var start = Math.Max(0, at - 60);
        var key = $"    web: …{Cut(web[start..], 170)}\n    app: …{Cut(app[start..], 170)}";
        kinds[key] = kinds.GetValueOrDefault(key) + 1;
    }
    Console.WriteLine($"\ncompared with the web page: {same} prompts identical, {differ} different, in {kinds.Count} distinct ways:");
    foreach (var (k, n) in kinds.OrderByDescending(x => x.Value).Take(40)) Console.WriteLine($"  {n}x\n{k}");
}

static string Cut(string s, int n = 300) => (s.Length > n ? s[..n] + "…" : s).Replace("\n", " / ");

static string FindRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CatgirlStudio.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("CatgirlStudio.slnx not found above the tool");
}
