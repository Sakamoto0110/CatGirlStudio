using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatgirlStudio.Core;

/// <summary>What is picked on the page: the prefab, one option id per single-choice variable
/// (or <see cref="PromptEngine.Custom"/> with text in <see cref="Custom"/>), and a list of ids
/// per multi-choice variable.</summary>
public sealed class Selection
{
    public string PrefabId { get; set; } = "";
    public Dictionary<string, string> Values { get; init; } = [];
    public Dictionary<string, List<string>> Lists { get; init; } = [];
    public Dictionary<string, string> Custom { get; init; } = [];

    public Selection Clone() => new()
    {
        PrefabId = PrefabId,
        Values = new(Values),
        Lists = Lists.ToDictionary(p => p.Key, p => new List<string>(p.Value)),
        Custom = new(Custom)
    };

    /// <summary>Same JSON shape as the web page and the old studio .json files:
    /// {"prefabId": "...", "values": {"var": "id" | ["ids"]}, "custom": {"var": "text"}}.</summary>
    public JsonObject ToJson()
    {
        var values = new JsonObject();
        foreach (var (k, v) in Values) values[k] = v;
        foreach (var (k, v) in Lists) values[k] = new JsonArray(v.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
        var custom = new JsonObject();
        foreach (var (k, v) in Custom) custom[k] = v;
        return new JsonObject { ["prefabId"] = PrefabId, ["values"] = values, ["custom"] = custom };
    }

    /// <summary>Reads that JSON shape; values that do not fit today's catalog are dropped,
    /// and an old single accessory id becomes a one-item list (see <see cref="PromptEngine.Normalize"/>).</summary>
    public static Selection FromJson(JsonElement json, PromptEngine engine)
    {
        var s = new Selection();
        if (json.ValueKind != JsonValueKind.Object) return s;
        if (json.TryGetProperty("prefabId", out var p) && p.ValueKind == JsonValueKind.String) s.PrefabId = p.GetString()!;
        if (json.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Object)
        {
            foreach (var v in values.EnumerateObject())
            {
                var raw = v.Value.ValueKind switch
                {
                    JsonValueKind.String => (object)v.Value.GetString()!,
                    JsonValueKind.Array => v.Value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList(),
                    _ => null
                };
                engine.Normalize(v.Name, raw, s);
            }
        }
        if (json.TryGetProperty("custom", out var custom) && custom.ValueKind == JsonValueKind.Object)
            foreach (var c in custom.EnumerateObject())
                if (c.Value.ValueKind == JsonValueKind.String && engine.Catalog.Vars.ContainsKey(c.Name)) s.Custom[c.Name] = c.Value.GetString()!;
        return s;
    }
}
