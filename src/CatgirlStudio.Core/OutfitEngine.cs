using System.Text.Json;
using System.Text.RegularExpressions;

namespace CatgirlStudio.Core;

/// <summary>Something a prompt placeholder can come from besides the catalog variables
/// ({{outfit}} comes from the <see cref="OutfitEngine"/>).</summary>
public interface IPromptProvider
{
    bool Handles(string name);
    /// <summary>Whether the placeholder is a field the user picks (shown, rolled, locked). A text the
    /// provider derives from other choices, like the outfit's modesty rule, is not.</summary>
    bool IsField(string name);
    /// <summary>Raw text; {{catalog}} placeholders in it are expanded by the prompt engine.</summary>
    string Text(string name, Selection s);
    /// <summary>The text split into parts, so a quick edit can name only the part that changed.</summary>
    IReadOnlyList<PromptPart> Parts(string name, Selection s);
    /// <summary>Takes a value (including ones saved before this provider existed).</summary>
    bool Normalize(string key, object? raw, Selection into);
    void AddDefaults(Selection s);
}

public sealed record PromptPart(string Key, string Name, string LabelPt, string Text);

/// <summary>A customizable property of a piece (color, pattern, sleeves...).</summary>
public sealed class PropDef
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Name { get; init; }
    public string? Default { get; init; }
    public required IReadOnlyList<OptionDef> Options { get; init; }
    public bool IsColor { get; init; }
}

/// <summary>A property as one piece offers it: its own default, the options it allows, or a fixed value.</summary>
public sealed class PieceProp
{
    public required PropDef Def { get; init; }
    public string? Default { get; init; }
    public IReadOnlyList<string>? Allow { get; init; }
    public string? Fixed { get; init; }

    public string Key => Def.Key;
    public IReadOnlyList<OptionDef> Options => Allow is null ? Def.Options : Def.Options.Where(o => Allow.Contains(o.Id)).ToList();
    public bool Editable => Fixed is null && Options.Count > 1;
}

public sealed class Piece
{
    public required string Slot { get; init; }
    public required string Id { get; init; }
    public required string Pt { get; init; }
    public required string En { get; init; }
    public string? Group { get; init; }
    /// <summary>Offered only while this Estilo (theme) is picked.</summary>
    public string? Theme { get; init; }
    /// <summary>The legs show, so the Meias slot is offered.</summary>
    public bool Legs { get; init; }
    /// <summary>Slots this piece already takes care of (a ready outfit with shoes covers feet).</summary>
    public IReadOnlySet<string> Covers { get; init; } = new HashSet<string>();
    /// <summary>At-home piece (a little midriff, thin straps): switches the modesty rule to the relaxed one.</summary>
    public bool Relaxed { get; init; }
    public required IReadOnlyList<PieceProp> Props { get; init; }
}

public sealed class OutfitSlot
{
    public required string Id { get; init; }
    public required string Pt { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<Piece> Pieces { get; init; }
    public Piece? Find(string? id) => id is null ? null : Pieces.FirstOrDefault(p => p.Id == id);
}

public sealed record OutfitMode(string Id, string Pt, IReadOnlyList<string> Slots, string Main);

/// <summary>A slot as shown right now: the piece in it and the properties that piece offers.</summary>
public sealed record SlotState(OutfitSlot Slot, Piece Piece, IReadOnlyList<PieceProp> Props);

/// <summary>
/// The outfit, from data/outfits.json. Every piece declares what can be customized on it
/// (and how), the mode decides which body slots are in use, and the engine builds both the
/// fields to show and the text of {{outfit}}. Values live in the selection under
/// "outfit.mode", "outfit.&lt;slot&gt;" and "outfit.&lt;slot&gt;.&lt;prop&gt;".
/// </summary>
public sealed partial class OutfitEngine : IPromptProvider
{
    public const string Var = "outfit";
    /// <summary>{{outfitModesty}}: the house modesty rule, relaxed while an at-home piece is worn.</summary>
    public const string ModestyVar = "outfitModesty";
    const string ThemePiece = "@theme";

    [GeneratedRegex(@"(?<!\{)\{([A-Za-z]+)\}(?!\})")] private static partial Regex PropRx();

    readonly Catalog catalog;
    public IReadOnlyList<OutfitMode> Modes { get; }
    public OrderedDictionary<string, OutfitSlot> Slots { get; }
    public IReadOnlyDictionary<string, PropDef> Props { get; }
    /// <summary>The modesty rule: "default", and "relaxed" for at-home pieces.</summary>
    public IReadOnlyDictionary<string, string> Modesty { get; }
    readonly Dictionary<string, string> defaults = [];

    public OutfitEngine(Catalog catalog, string outfitsJson)
    {
        this.catalog = catalog;
        using var doc = JsonDocument.Parse(outfitsJson);
        var root = doc.RootElement;

        var props = new Dictionary<string, PropDef>();
        foreach (var p in root.GetProperty("props").EnumerateObject())
        {
            var d = p.Value;
            var list = d.TryGetProperty("list", out var l) ? l.GetString() : null;
            props[p.Name] = new PropDef
            {
                Key = p.Name,
                Label = d.GetProperty("label").GetString()!,
                Name = d.GetProperty("name").GetString()!,
                Default = d.TryGetProperty("default", out var def) ? def.GetString() : null,
                Options = list is not null
                    ? catalog.Lists.GetValueOrDefault(list) ?? throw new InvalidDataException($"outfits.json: prop {p.Name} names unknown list {list}")
                    : d.GetProperty("options").EnumerateArray().Select(Catalog.ReadOption).ToList(),
                IsColor = list == "colors"
            };
        }
        Props = props;

        Modes = root.GetProperty("modes").EnumerateArray().Select(m => new OutfitMode(
            m.GetProperty("id").GetString()!,
            m.GetProperty("pt").GetString()!,
            m.GetProperty("slots").EnumerateArray().Select(x => x.GetString()!).ToList(),
            m.GetProperty("main").GetString()!)).ToList();

        foreach (var d in root.GetProperty("defaults").EnumerateObject()) defaults[d.Name] = d.Value.GetString()!;
        Modesty = root.GetProperty("modesty").EnumerateObject().ToDictionary(m => m.Name, m => m.Value.GetString()!);
        foreach (var k in new[] { "default", "relaxed" })
            if (!Modesty.ContainsKey(k)) throw new InvalidDataException($"outfits.json: modesty.{k} is missing");

        Slots = [];
        foreach (var s in root.GetProperty("slots").EnumerateObject())
        {
            var slotProps = s.Value.TryGetProperty("props", out var sp) ? sp : default;
            var pieces = s.Value.GetProperty("pieces").EnumerateArray().Select(p => new Piece
            {
                Slot = s.Name,
                Id = p.GetProperty("id").GetString()!,
                Pt = p.GetProperty("pt").GetString()!,
                En = p.GetProperty("en").GetString()!,
                Group = p.TryGetProperty("group", out var g) ? g.GetString() : null,
                Theme = p.TryGetProperty("theme", out var t) ? t.GetString() : null,
                Legs = p.TryGetProperty("legs", out var legs) && legs.ValueKind == JsonValueKind.True,
                Covers = p.TryGetProperty("covers", out var c) ? c.EnumerateArray().Select(x => x.GetString()!).ToHashSet() : new HashSet<string>(),
                Relaxed = p.TryGetProperty("relaxed", out var r) && r.ValueKind == JsonValueKind.True,
                Props = MergeProps(slotProps, p.TryGetProperty("props", out var pp) ? pp : default, s.Name, p.GetProperty("id").GetString()!)
            }).ToList();
            Slots[s.Name] = new OutfitSlot
            {
                Id = s.Name,
                Pt = s.Value.GetProperty("pt").GetString()!,
                Name = s.Value.GetProperty("name").GetString()!,
                Pieces = pieces
            };
        }
    }

    // a slot's props apply to every piece; a piece adds or overrides them, and null removes one
    List<PieceProp> MergeProps(JsonElement slotProps, JsonElement pieceProps, string slot, string piece)
    {
        var merged = new OrderedDictionary<string, JsonElement?>();
        foreach (var src in new[] { slotProps, pieceProps })
        {
            if (src.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in src.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Null) merged.Remove(p.Name);
                else merged[p.Name] = p.Value;
            }
        }
        return merged.Select(p =>
        {
            if (!Props.TryGetValue(p.Key, out var def)) throw new InvalidDataException($"outfits.json: {slot}.{piece} uses unknown prop {p.Key}");
            var o = p.Value!.Value;
            return new PieceProp
            {
                Def = def,
                Default = o.TryGetProperty("default", out var d) ? d.GetString() : null,
                Allow = o.TryGetProperty("allow", out var a) ? a.EnumerateArray().Select(x => x.GetString()!).ToList() : null,
                Fixed = o.TryGetProperty("fixed", out var f) ? f.GetString() : null
            };
        }).ToList();
    }

    /* ----------------------------------------------------------- reading */

    public OutfitMode Mode(Selection s) =>
        Modes.FirstOrDefault(m => m.Id == s.Values.GetValueOrDefault("outfit.mode")) ?? Modes.First(m => m.Id == defaults["mode"]);

    public bool Offered(Piece p, Selection s) => p.Theme is null || s.Values.GetValueOrDefault("theme") == p.Theme;

    /// <summary>The pieces a slot offers right now (style pieces only with their Estilo).</summary>
    public IEnumerable<Piece> Offered(string slot, Selection s) => Slots[slot].Pieces.Where(p => Offered(p, s));

    public Piece PieceIn(string slot, Selection s)
    {
        var def = Slots[slot];
        var id = s.Values.GetValueOrDefault("outfit." + slot);
        // A missing bra gets a visible basic fallback whenever the pieces mode has no top.
        if (slot == "underTop" && Mode(s).Id == "pieces" && PieceIn("top", s).Id == "none" &&
            (id is null or "none" || def.Find(id) is null))
            id = "softbra";
        if (id == ThemePiece) id = def.Pieces.FirstOrDefault(p => p.Theme is not null && p.Theme == s.Values.GetValueOrDefault("theme"))?.Id;
        var piece = def.Find(id);
        if (piece is not null && Offered(piece, s)) return piece;
        return def.Find(defaults.GetValueOrDefault(slot)) is { } d && Offered(d, s) ? d : Offered(slot, s).First();
    }

    /// <summary>The slots in use: the mode's slots, minus Meias when the legs do not show and
    /// minus anything a piece in use already covers.</summary>
    public IReadOnlyList<SlotState> State(Selection s)
    {
        var mode = Mode(s);
        var pieces = mode.Slots.ToDictionary(x => x, x => PieceIn(x, s));
        var covered = pieces.Values.SelectMany(p => p.Covers).ToHashSet();
        var mainPiece = covered.Contains(mode.Main)
            ? pieces.Values.FirstOrDefault(p => p.Covers.Contains(mode.Main)) ?? pieces[mode.Main]
            : pieces[mode.Main];
        var list = new List<SlotState>();
        foreach (var slot in mode.Slots)
        {
            if (covered.Contains(slot)) continue;
            if (slot == "legs" && !mainPiece.Legs) continue;
            list.Add(new SlotState(Slots[slot], pieces[slot], pieces[slot].Props));
        }
        return list;
    }

    /// <summary>A color property the piece does not narrow down also takes a picked "#RRGGBB".</summary>
    public static bool AcceptsHex(PieceProp p) => p.Def.IsColor && p.Allow is null && p.Fixed is null;

    /// <summary>The value of a property: fixed, or the chosen one if the piece allows it (a picked
    /// color too), or its default.</summary>
    public string PropValue(string slot, PieceProp p, Selection s)
    {
        if (p.Fixed is not null) return p.Fixed;
        var options = p.Options;
        var chosen = s.Values.GetValueOrDefault($"outfit.{slot}.{p.Key}");
        if (chosen is not null && options.Any(o => o.Id == chosen)) return chosen;
        if (AcceptsHex(p) && ColorWords.IsHex(chosen)) return chosen!;
        var def = p.Default ?? p.Def.Default;
        if (def is not null && def.StartsWith('@')) def = s.Values.GetValueOrDefault(def[1..]);
        if (AcceptsHex(p) && ColorWords.IsHex(def)) return def!; // following a picked accent color
        return options.Any(o => o.Id == def) ? def! : options[0].Id;
    }

    public string PieceText(string slot, Piece piece, Selection s)
    {
        var template = slot == "underTop" && Mode(s).Id == "pieces" && PieceIn("top", s).Id == "none"
            ? piece.En.Replace(" under her top", "") : piece.En;
        return PropRx().Replace(template, m =>
        {
            var p = piece.Props.FirstOrDefault(x => x.Key == m.Groups[1].Value);
            if (p is null) return m.Value; // stays visible so the check notices it
            var id = PropValue(slot, p, s);
            if (ColorWords.IsHex(id)) return ColorWords.Phrase(id);
            var en = p.Def.Options.First(o => o.Id == id).En;
            return en.Length > 0 ? en : PromptEngine.EmptyMarker; // "with {neckwear}" + nothing -> the "with" goes too
        });
    }

    /* ---------------------------------------------------- IPromptProvider */

    public bool Handles(string name) => name is Var or ModestyVar;

    public bool IsField(string name) => name == Var;

    /// <summary>True while an at-home piece is worn (in a slot that is in use).</summary>
    public bool IsRelaxed(Selection s) => State(s).Any(x => x.Piece.Relaxed);

    // the modesty rule has no parts: a quick edit names the pieces that changed, not the rule
    public IReadOnlyList<PromptPart> Parts(string name, Selection s) => name == ModestyVar ? [] :
        State(s).Select(x => new PromptPart("outfit." + x.Slot.Id, x.Slot.Name, x.Slot.Pt, PieceText(x.Slot.Id, x.Piece, s))).ToList();

    public string Text(string name, Selection s)
    {
        if (name == ModestyVar) return Modesty[IsRelaxed(s) ? "relaxed" : "default"];
        var items = Parts(name, s).Select(p => PromptEngine.Tidy(p.Text)).Where(t => t.Length > 0).ToList();
        return items.Count < 2 ? string.Concat(items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];
    }

    public void AddDefaults(Selection s)
    {
        foreach (var (k, v) in defaults) s.Values.TryAdd("outfit." + k, v);
    }

    // values saved before the outfit engine (favorites, image .json files, prefab defaults)
    static readonly Dictionary<string, string[]> Legacy = new()
    {
        ["top"] = ["outfit.top"], ["dress"] = ["outfit.dress"], ["bottom"] = ["outfit.bottom"],
        ["outerwear"] = ["outfit.outer"], ["legwear"] = ["outfit.legs"], ["shoes"] = ["outfit.feet"],
        ["topColor"] = ["outfit.top.color", "outfit.dress.color"], ["topPattern"] = ["outfit.top.pattern", "outfit.dress.pattern"],
        ["bottomColor"] = ["outfit.bottom.color"], ["bottomPattern"] = ["outfit.bottom.pattern"],
        ["outerColor"] = ["outfit.outer.color"], ["shoesColor"] = ["outfit.feet.color"],
        ["sleeves"] = ["outfit.top.sleeves", "outfit.dress.sleeves"], ["hood"] = ["outfit.top.hood", "outfit.dress.hood"],
        ["neckwear"] = ["outfit.top.neckwear"], ["skirtLength"] = ["outfit.bottom.skirtLength"], ["mainColor"] = ["outfit.full.main"]
    };

    public bool Normalize(string key, object? raw, Selection into)
    {
        if (raw is not string value) return false;
        if (key == Var)
        {
            switch (value)
            {
                case "build": into.Values["outfit.mode"] = "pieces"; return true;
                case "builddress": into.Values["outfit.mode"] = "dress"; return true;
                case "styled": into.Values["outfit.mode"] = "ready"; into.Values["outfit.full"] = ThemePiece; return true;
            }
            if (Slots["full"].Find(value) is null) return false;
            into.Values["outfit.mode"] = "ready";
            into.Values["outfit.full"] = value;
            return true;
        }
        if (Legacy.TryGetValue(key, out var targets))
        {
            foreach (var t in targets) into.Values[t] = value;
            return true;
        }
        if (!key.StartsWith("outfit.")) return false;
        var parts = key.Split('.');
        if (parts.Length == 2 && parts[1] == "mode" && Modes.Any(m => m.Id == value)) { into.Values[key] = value; return true; }
        if (parts.Length == 2 && Slots.TryGetValue(parts[1], out var slot) && (slot.Find(value) is not null || value == ThemePiece)) { into.Values[key] = value; return true; }
        if (parts.Length == 3 && Slots.ContainsKey(parts[1]) && Props.TryGetValue(parts[2], out var prop))
        {
            if (prop.Options.Any(o => o.Id == value)) { into.Values[key] = value; return true; }
            if (prop.IsColor && ColorWords.IsHex(value)) { into.Values[key] = ColorWords.Canonical(value); return true; }
        }
        return false;
    }

    /* ------------------------------------------------------------ dice */

    /// <summary>New pieces and properties in every slot in use (the mode stays).</summary>
    public void Randomize(Selection s, Random random)
    {
        for (var pass = 0; pass < 2; pass++) // a new piece may open other slots (a skirt opens Meias)
        {
            foreach (var slot in State(s))
            {
                var offered = Offered(slot.Slot.Id, s).ToList();
                var piece = offered[random.Next(offered.Count)];
                s.Values["outfit." + slot.Slot.Id] = piece.Id;
                foreach (var p in piece.Props.Where(p => p.Editable))
                    s.Values[$"outfit.{slot.Slot.Id}.{p.Key}"] = p.Options[random.Next(p.Options.Count)].Id;
            }
        }
    }
}
