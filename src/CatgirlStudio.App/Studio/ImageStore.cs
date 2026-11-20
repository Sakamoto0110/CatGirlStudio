using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CatgirlStudio.App.Studio;

/// <summary>A generated image and its .json (prompt, selection, model, cost...).</summary>
internal sealed class StoredImage(string id, string path, JsonObject meta)
{
    public string Id { get; } = id;
    public string Path { get; } = path;
    public JsonObject Meta { get; } = meta;

    string? S(string key) => Meta[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public string Kind => S("kind") ?? "generate";
    public string Model => S("model") ?? "";
    public string Quality => S("quality") ?? "";
    public string Size => S("size") ?? "";
    public string ShownSize => (S("savedSize") ?? Size).Replace('x', '×');
    public string Background => S("background") ?? "auto";
    public string? Parent => S("parent");
    public string Prompt => S("prompt") ?? "";
    public double? CostUsd => Meta["costUsd"] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    public JsonNode? Selection => Meta["selection"];
    public (int W, int H)? Crop => Meta["crop"] is JsonArray a && a.Count == 2 ? (a[0]!.GetValue<int>(), a[1]!.GetValue<int>()) : null;
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>The image folder (Pictures\Catgirls\api by default), same layout as before, so
/// earlier images show up too. Upscaled copies go to the "2x" subfolder.</summary>
internal sealed partial class ImageStore
{
    [GeneratedRegex(@"^\d{8}-\d{6}-[\w-]{1,60}$")] private static partial Regex IdRx();
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    readonly Lock reserve = new();

    public static string DefaultFolder =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Catgirls", "api");

    public string Folder { get; }

    /// <summary>Where upscaled copies go: 2x, 3x, 4x, with "-tta" when made with TTA.</summary>
    public string UpscaleFolder(int scale, bool tta) => System.IO.Path.Combine(Folder, $"{scale}x" + (tta ? "-tta" : ""));

    public ImageStore(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(System.IO.Path.Combine(folder, "thumbs"));
    }

    string NewId(string? prefab)
    {
        var clean = Regex.Replace(prefab ?? "", @"[^\w-]", "");
        var basename = DateTime.Now.ToString("yyyyMMdd-HHmmss-") + (clean.Length > 0 ? clean[..Math.Min(40, clean.Length)] : "img");
        lock (reserve)
        {
            var name = basename;
            for (var i = 2; File.Exists(System.IO.Path.Combine(Folder, name + ".json")); i++) name = $"{basename}-{i}";
            File.WriteAllText(System.IO.Path.Combine(Folder, name + ".json"), "{}"); // reserve the name
            return name;
        }
    }

    public StoredImage Save(byte[] png, JsonObject meta, string? prefabId)
    {
        var id = NewId(prefabId);
        var (w, h) = Png.SizeOf(png);
        meta["id"] = id;
        meta["file"] = id + ".png";
        meta["savedSize"] = $"{w}x{h}";
        var path = System.IO.Path.Combine(Folder, id + ".png");
        File.WriteAllBytes(path, png);
        File.WriteAllText(System.IO.Path.Combine(Folder, id + ".json"), meta.ToJsonString(Pretty));
        var img = new StoredImage(id, path, meta);
        ThumbPath(img);
        return img;
    }

    public StoredImage? Get(string? id) => id is not null && IdRx().IsMatch(id) ? Read(System.IO.Path.Combine(Folder, id + ".json")) : null;

    StoredImage? Read(string jsonPath)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(jsonPath)) is not JsonObject meta) return null;
            var file = meta["file"]?.GetValue<string>();
            var id = meta["id"]?.GetValue<string>();
            if (file is null || id is null) return null;
            var path = System.IO.Path.Combine(Folder, file);
            return File.Exists(path) ? new StoredImage(id, path, meta) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Newest first.</summary>
    public List<StoredImage> Recent(int limit)
    {
        var list = new List<StoredImage>();
        foreach (var json in Directory.EnumerateFiles(Folder, "*.json").OrderDescending())
        {
            if (Read(json) is { } img) list.Add(img);
            if (list.Count >= limit) break;
        }
        return list;
    }

    /// <summary>A small PNG in thumbs\, made on first use.</summary>
    public string ThumbPath(StoredImage img)
    {
        var thumb = System.IO.Path.Combine(Folder, "thumbs", img.Id + ".png");
        if (File.Exists(thumb)) return thumb;
        try { File.WriteAllBytes(thumb, Png.Thumbnail(File.ReadAllBytes(img.Path), 260)); }
        catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException) { return img.Path; }
        return thumb;
    }

    public string? UpscaledPath(StoredImage img, int scale, bool tta)
    {
        var path = System.IO.Path.Combine(UpscaleFolder(scale, tta), img.FileName);
        return File.Exists(path) ? path : null;
    }
}
