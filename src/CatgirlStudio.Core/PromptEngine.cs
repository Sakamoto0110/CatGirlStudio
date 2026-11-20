using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CatgirlStudio.Core;

/// <summary>A change in the "Edit the last image, change only..." request.</summary>
public sealed record EditChange(string Slot, string Label, string LabelPt, string After);

public sealed record EditRequestResult(IReadOnlyList<EditChange> Changes, string Text);

public sealed record BuildResult(string Text, IReadOnlySet<string> Used);

/// <summary>
/// Builds prompts from the catalog. This is a line-by-line port of engine.js (the web page's
/// engine); tools/CatalogCheck compares the two on every prefab and option, so keep them in step.
/// </summary>
public sealed partial class PromptEngine
{
    public const string Custom = "__custom";
    // stands where an option said nothing, until Tidy() removes it with its joining word
    const string Empty = "";
    /// <summary>What a provider writes where a property says nothing ("Na gola: Nada"), so
    /// Tidy() can drop the joining word before it.</summary>
    public const string EmptyMarker = Empty;

    // engine.js uses JavaScript's \w, which is ASCII only
    [GeneratedRegex(@"\{\{([A-Za-z0-9_]+)\}\}")] private static partial Regex Placeholder();
    [GeneratedRegex(@"\{\{list:([A-Za-z0-9_.]+(?:,[A-Za-z0-9_.]+)*)\}\}")] private static partial Regex ListRx();
    [GeneratedRegex(@"\{\{([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)\}\}")] private static partial Regex FieldRx();
    [GeneratedRegex(@"\{\{(?:list:)?([A-Za-z0-9_.]+(?:,[A-Za-z0-9_.]+)*)\}\}")] private static partial Regex PartsRx();
    [GeneratedRegex(",?\\s+(?:and|with|over)\\s*")] private static partial Regex Dangling();
    [GeneratedRegex(@"[ \t]+")] private static partial Regex Spaces();
    [GeneratedRegex(@" +([.,;:!?])")] private static partial Regex SpaceBeforePunct();
    [GeneratedRegex(@"\.\.(?!\.)")] private static partial Regex DoubleDot();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex ManyNewlines();

    public Catalog Catalog { get; }
    /// <summary>Supplies placeholders that are not catalog variables ({{outfit}}).</summary>
    public IPromptProvider? Provider { get; }
    /// <summary>Top-level slots: "pose" plus the variables written directly in the blocks.</summary>
    public IReadOnlyList<string> Slots { get; }

    public PromptEngine(Catalog catalog, IPromptProvider? provider = null)
    {
        Catalog = catalog;
        Provider = provider;
        var names = new List<string>();
        foreach (var text in catalog.Blocks.Values)
            foreach (Match m in Placeholder().Matches(text))
            {
                var n = m.Groups[1].Value;
                if ((catalog.Vars.ContainsKey(n) || provider?.Handles(n) == true) && !names.Contains(n)) names.Add(n);
            }
        Slots = ["pose", .. names];
    }

    bool Provided(string name) => Provider?.Handles(name) == true;

    OrderedDictionary<string, VarDef> Vars => Catalog.Vars;

    public Prefab FindPrefab(string? id) => Catalog.Prefabs.FirstOrDefault(p => p.Id == id) ?? Catalog.Prefabs[0];

    public bool IsMulti(string name) => Vars.TryGetValue(name, out var d) && d.Multi;

    public Selection DefaultSelection(string? prefabId = null)
    {
        var s = new Selection { PrefabId = prefabId ?? Catalog.Prefabs[0].Id };
        foreach (var (name, def) in Vars)
        {
            if (def.Multi) s.Lists[name] = [.. def.DefaultMany];
            else if (def.Default is not null) s.Values[name] = def.Default;
        }
        Provider?.AddDefaults(s);
        return s;
    }

    /// <summary>Puts a raw value (an id, "__custom", or a list of ids) into the selection in the
    /// shape its variable keeps. Returns false (and changes nothing) when it is not valid there.
    /// A multi-choice variable turns an old single id, or "none", into a list.</summary>
    public bool Normalize(string name, object? raw, Selection into)
    {
        if (!Vars.TryGetValue(name, out var def)) return Provider?.Normalize(name, raw, into) == true;
        if (def.Multi)
        {
            var list = raw switch { string one => [one], IEnumerable<string> many => many.ToList(), _ => (List<string>?)null };
            if (list is null) return false;
            into.Lists[name] = list.Where(id => def.Find(id) is not null).Distinct().ToList();
            return true;
        }
        if (raw is string hex && def.IsColor && ColorWords.IsHex(hex))
        {
            into.Values[name] = ColorWords.Canonical(hex); // a color picked by hand
            return true;
        }
        if (raw is not string value || (value != Custom && def.Find(value) is null)) return false;
        into.Values[name] = value;
        return true;
    }

    /// <summary>Applies a prefab default (a JSON string or array) to the selection.</summary>
    public bool ApplyDefault(string name, JsonElement value, Selection into) => Normalize(name, value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Array => value.EnumerateArray().Select(e => e.GetString() ?? "").ToList(),
        _ => null
    }, into);

    public string OptionText(string name, Selection s)
    {
        var def = Vars[name];
        if (def.Multi)
        {
            // catalog order, not click order, so the same choices always read the same
            var chosen = s.Lists.GetValueOrDefault(name) ?? [];
            return string.Join(" ", def.Options.Where(o => chosen.Contains(o.Id)).Select(o => o.En));
        }
        var value = s.Values.GetValueOrDefault(name);
        if (value == Custom) return (s.Custom.GetValueOrDefault(name) ?? "").Trim();
        if (def.IsColor && ColorWords.IsHex(value)) return ColorWords.Phrase(value!);
        var opt = def.Find(value) ?? def.Find(def.Default);
        return opt?.En ?? "";
    }

    // a custom (typed) value has no extra fields, so it falls back to the default option's
    string OptionField(string name, string field, Selection s)
    {
        var def = Vars[name];
        var chosen = def.Multi ? null : def.Find(s.Values.GetValueOrDefault(name));
        var fallback = def.Find(def.Default);
        var opt = chosen?.Field(field) is not null ? chosen : fallback;
        return opt?.Field(field) ?? "";
    }

    static string JoinList(List<string> items) =>
        items.Count < 2 ? string.Concat(items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    public string Expand(string text, Selection s, HashSet<string>? used, int depth = 0)
    {
        text = ListRx().Replace(text, m =>
        {
            if (depth > 12) return "";
            var items = m.Groups[1].Value.Split(',')
                .Select(n => Tidy(Expand("{{" + n + "}}", s, used, depth + 1)))
                .Where(t => t.Length > 0).ToList();
            return items.Count > 0 ? JoinList(items) : Empty; // an all-empty list also drops its joining word
        });
        text = FieldRx().Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            if (depth > 12) return "";
            if (!Vars.ContainsKey(name)) return m.Value;
            used?.Add(name);
            return Expand(OptionField(name, m.Groups[2].Value, s), s, used, depth + 1);
        });
        return Placeholder().Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            if (depth > 12) return "";
            if (name == "pose") return Expand(FindPrefab(s.PrefabId).Pose, s, used, depth + 1);
            if (Catalog.Blocks.TryGetValue(name, out var block)) return Expand(block, s, used, depth + 1);
            if (Provided(name))
            {
                if (Provider!.IsField(name)) used?.Add(name);
                var p = Expand(Provider!.Text(name, s), s, used, depth + 1);
                return p.Trim().Length > 0 ? p : Empty;
            }
            if (Vars.ContainsKey(name))
            {
                used?.Add(name);
                var t = Expand(OptionText(name, s), s, used, depth + 1);
                return t.Trim().Length > 0 ? t : Empty;
            }
            return m.Value; // unknown placeholder stays visible so it gets noticed
        });
    }

    public static string Tidy(string text)
    {
        var lines = text.Split('\n').Select(line =>
        {
            // an option left empty ("none") takes its joining word with it: "shorts and {{legwear}}." -> "shorts."
            line = Dangling().Replace(line, "");
            line = line.Replace(Empty, "");
            line = Spaces().Replace(line, " ");
            line = SpaceBeforePunct().Replace(line, "$1");
            line = DoubleDot().Replace(line, ".");
            return line.Trim();
        });
        return ManyNewlines().Replace(string.Join("\n", lines), "\n\n").Trim();
    }

    /// <summary>Full prompt for a selection, plus the variables it reached (the fields to show).</summary>
    public BuildResult Build(Selection s)
    {
        var used = new HashSet<string>();
        var text = Tidy(Expand(Catalog.Blocks["prompt"], s, used));
        return new BuildResult(text, used);
    }

    string SlotText(string slot, Selection s) => slot == "pose"
        ? Tidy(Expand(FindPrefab(s.PrefabId).Pose, s, null))
        : Provided(slot) ? Tidy(Expand(Provider!.Text(slot, s), s, null))
        : Tidy(Expand(OptionText(slot, s), s, null));

    // A slot whose chosen option is built from parts (it holds a {{list:}}) and did not change
    // between the two selections is compared part by part, so the edit names only that part.
    List<string> SubSlots(string slot, Selection a, Selection b)
    {
        // multi-choice values are lists; engine.js compares them by reference, so they always "differ"
        if (slot == "pose" || IsMulti(slot) || Provided(slot)) return [slot];
        var va = a.Values.GetValueOrDefault(slot);
        if (va != b.Values.GetValueOrDefault(slot) || va == Custom) return [slot];
        var opt = Vars[slot].Find(va);
        if (opt is null || !opt.En.Contains("{{list:")) return [slot];
        var parts = new List<string>();
        foreach (Match m in PartsRx().Matches(opt.En))
        {
            foreach (var n in m.Groups[1].Value.Split(','))
            {
                if (Vars.ContainsKey(n)) { parts.Add(n); continue; }
                // a field item ("bottom.socks" -> "{{legwear}}"): the variables it brings in are parts too
                var dot = n.Split('.');
                if (dot.Length < 2 || !Vars.ContainsKey(dot[0])) continue;
                foreach (var sel in new[] { a, b })
                    foreach (Match mm in Placeholder().Matches(OptionField(dot[0], dot[1], sel)))
                        if (Vars.ContainsKey(mm.Groups[1].Value)) parts.Add(mm.Groups[1].Value);
            }
        }
        return parts.Count > 0 ? parts.Distinct().ToList() : [slot];
    }

    // For a multi-choice slot whose set of choices changed, say what to add and what to take off.
    string? MultiChange(string slot, Selection before, Selection now)
    {
        var was = before.Lists.GetValueOrDefault(slot) ?? [];
        var nowIds = now.Lists.GetValueOrDefault(slot) ?? [];
        string Sentences(IEnumerable<OptionDef> opts) => Tidy(string.Join(" ", opts.Select(o => Expand(o.En, now, null))));
        var added = Vars[slot].Options.Where(o => nowIds.Contains(o.Id) && !was.Contains(o.Id)).ToList();
        var removed = Vars[slot].Options.Where(o => was.Contains(o.Id) && !nowIds.Contains(o.Id)).ToList();
        if (added.Count == 0 && removed.Count == 0) return null;
        var parts = new List<string>();
        if (added.Count > 0) parts.Add("add: " + Sentences(added));
        if (removed.Count > 0) parts.Add("take off: " + Sentences(removed));
        return string.Join(" ", parts);
    }

    /// <summary>"Edit the last image, change only..." request between two selections.</summary>
    public EditRequestResult EditRequest(Selection before, Selection now)
    {
        var changes = new List<EditChange>();
        foreach (var slot in Slots.SelectMany(sl => SubSlots(sl, before, now)).Distinct())
        {
            if (Provided(slot))
            {
                // the provider's parts (outfit: top, bottom, socks...) are compared one by one
                var a = Provider!.Parts(slot, before).ToDictionary(p => p.Key);
                var b = Provider.Parts(slot, now);
                foreach (var key in a.Keys.Concat(b.Select(p => p.Key)).Distinct())
                {
                    var pa = a.GetValueOrDefault(key);
                    var pb = b.FirstOrDefault(p => p.Key == key);
                    var t0 = pa is null ? "" : Tidy(Expand(pa.Text, before, null));
                    var t1 = pb is null ? "" : Tidy(Expand(pb.Text, now, null));
                    if (t0 != t1) changes.Add(new EditChange(key, (pb ?? pa)!.Name, (pb ?? pa)!.LabelPt, t1));
                }
                continue;
            }
            var was = SlotText(slot, before);
            var after = SlotText(slot, now);
            if (was == after) continue;
            if (IsMulti(slot)) after = MultiChange(slot, before, now) ?? after;
            changes.Add(new EditChange(
                slot,
                slot == "pose" ? "pose" : Vars[slot].Name,
                slot == "pose" ? "Pose: " + FindPrefab(now.PrefabId).Name : Vars[slot].Label,
                after));
        }
        if (changes.Count == 0) return new EditRequestResult(changes, "");
        var text = new StringBuilder()
            .Append("Edit the last image. Keep everything else exactly the same: the same character, face, outfit, pose, framing, art style, colors and background.\n")
            .Append("Change only the following:\n");
        foreach (var c in changes) text.Append("- ").Append(c.Label).Append(": ").Append(c.After.Length > 0 ? c.After : "remove it").Append('\n');
        text.Append("Do not change anything else.");
        return new EditRequestResult(changes, text.ToString());
    }
}
