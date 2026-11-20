using System.Text.Json;

namespace CatgirlStudio.Core;

/// <summary>One choice of a variable. Besides id/pt/en an option may carry extra text fields
/// (slot, socks, bg, outfit...) that {{var.field}} and list items read.</summary>
public sealed class OptionDef
{
    public required string Id { get; init; }
    public required string Pt { get; init; }
    public required string En { get; init; }
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();

    public string? Slot => Fields.TryGetValue("slot", out var s) ? s : null;

    /// <summary>A field by name: en, pt, id or an extra field. Null when the option has none.</summary>
    public string? Field(string name) => name switch
    {
        "id" => Id,
        "pt" => Pt,
        "en" => En,
        _ => Fields.TryGetValue(name, out var v) ? v : null
    };
}

public sealed class VarDef
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    /// <summary>English name, used in the "change only..." edit request.</summary>
    public required string Name { get; init; }
    public required string Group { get; init; }
    public bool Multi { get; init; }
    /// <summary>Default option id (single-choice variables).</summary>
    public string? Default { get; init; }
    /// <summary>Default option ids (multi-choice variables).</summary>
    public IReadOnlyList<string> DefaultMany { get; init; } = [];
    public required IReadOnlyList<OptionDef> Options { get; init; }
    /// <summary>A color: its options carry a "hex" swatch and it also takes a picked "#RRGGBB"
    /// (the shared colors list, or "picker": true).</summary>
    public bool IsColor { get; init; }

    public OptionDef? Find(string? id) => id is null ? null : Options.FirstOrDefault(o => o.Id == id);
}

public sealed class Prefab
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Desc { get; init; }
    public required string Pose { get; init; }
    /// <summary>Values applied when the prefab is picked: an option id, or a list for multi variables.</summary>
    public IReadOnlyDictionary<string, JsonElement> Defaults { get; init; } = new Dictionary<string, JsonElement>();
}

/// <summary>data/catalog.json + data/prefabs.json, the same files the web page reads.</summary>
public sealed class Catalog
{
    public required IReadOnlyList<string> Groups { get; init; }
    public required OrderedDictionary<string, string> Blocks { get; init; }
    /// <summary>Shared option lists (colors, patterns), also used by the outfit engine.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OptionDef>> Lists { get; init; } = new Dictionary<string, IReadOnlyList<OptionDef>>();
    public required OrderedDictionary<string, VarDef> Vars { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<Prefab> Prefabs { get; init; }

    public static Catalog Load(string catalogJson, string prefabsJson)
    {
        using var cat = JsonDocument.Parse(catalogJson);
        using var pre = JsonDocument.Parse(prefabsJson);
        var root = cat.RootElement;

        var lists = new Dictionary<string, IReadOnlyList<OptionDef>>();
        if (root.TryGetProperty("lists", out var listsEl))
            foreach (var l in listsEl.EnumerateObject()) lists[l.Name] = l.Value.EnumerateArray().Select(ReadOption).ToList();

        var blocks = new OrderedDictionary<string, string>();
        foreach (var b in root.GetProperty("blocks").EnumerateObject()) blocks[b.Name] = b.Value.GetString() ?? "";

        var vars = new OrderedDictionary<string, VarDef>();
        foreach (var v in root.GetProperty("vars").EnumerateObject())
        {
            var d = v.Value;
            var multi = d.TryGetProperty("multi", out var m) && m.ValueKind == JsonValueKind.True;
            var optEl = d.GetProperty("options");
            IReadOnlyList<OptionDef> options = optEl.ValueKind == JsonValueKind.String
                ? lists.GetValueOrDefault(optEl.GetString()!) ?? []
                : optEl.EnumerateArray().Select(ReadOption).ToList();
            var def = d.TryGetProperty("default", out var de) ? de : default;
            vars[v.Name] = new VarDef
            {
                Key = v.Name,
                Label = d.GetProperty("label").GetString()!,
                Name = d.GetProperty("name").GetString()!,
                Group = d.GetProperty("group").GetString()!,
                Multi = multi,
                Default = def.ValueKind == JsonValueKind.String ? def.GetString() : null,
                DefaultMany = def.ValueKind == JsonValueKind.Array ? def.EnumerateArray().Select(e => e.GetString()!).ToList() : [],
                Options = options,
                IsColor = !multi && ((optEl.ValueKind == JsonValueKind.String && optEl.GetString() == "colors")
                    || (d.TryGetProperty("picker", out var pk) && pk.ValueKind == JsonValueKind.True))
            };
        }

        var prefabs = pre.RootElement.GetProperty("prefabs").EnumerateArray().Select(p => new Prefab
        {
            Id = p.GetProperty("id").GetString()!,
            Name = p.GetProperty("name").GetString()!,
            Category = p.GetProperty("category").GetString()!,
            Desc = p.GetProperty("desc").GetString()!,
            Pose = p.GetProperty("pose").GetString()!,
            Defaults = p.TryGetProperty("defaults", out var dd)
                ? dd.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
                : new Dictionary<string, JsonElement>()
        }).ToList();

        return new Catalog
        {
            Groups = root.GetProperty("groups").EnumerateArray().Select(g => g.GetString()!).ToList(),
            Blocks = blocks,
            Lists = lists,
            Vars = vars,
            Categories = pre.RootElement.GetProperty("categories").EnumerateArray().Select(c => c.GetString()!).ToList(),
            Prefabs = prefabs
        };
    }

    internal static OptionDef ReadOption(JsonElement o)
    {
        var fields = new Dictionary<string, string>();
        foreach (var p in o.EnumerateObject())
            if (p.Name is not ("id" or "pt" or "en") && p.Value.ValueKind == JsonValueKind.String) fields[p.Name] = p.Value.GetString()!;
        return new OptionDef
        {
            Id = o.GetProperty("id").GetString()!,
            Pt = o.GetProperty("pt").GetString()!,
            En = o.GetProperty("en").GetString()!,
            Fields = fields
        };
    }
}
