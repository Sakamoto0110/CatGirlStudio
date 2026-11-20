namespace CatgirlStudio.App.Studio;

/// <summary>Image sizes per Formato and the prices used for estimates.</summary>
internal static class Pricing
{
    public static readonly IReadOnlyDictionary<string, string> Models = new Dictionary<string, string>
    {
        ["gpt-image-2.6-sunburst"] = "Sunburst",
        ["gpt-image-2.6-flare"] = "Flare"
    };

    public static readonly string[] Qualities = ["medium", "high", "xhigh"];

    /// <summary>Formato -> size sent to the API. Monitor sizes are requested 8 px larger, because
    /// the API wants both sides in multiples of 16, and cropped to the screen size when saved.</summary>
    public static readonly IReadOnlyDictionary<string, string> Sizes = new Dictionary<string, string>
    {
        ["portrait"] = "1024x1536", ["mascot"] = "1024x1536", ["landscape"] = "1536x1024", ["square"] = "1024x1024",
        ["wide"] = "2560x1088", ["fhd"] = "1920x1088", ["vertical"] = "1088x1920"
    };

    public static readonly IReadOnlyDictionary<string, (int W, int H)> Crops = new Dictionary<string, (int W, int H)>
    {
        ["wide"] = (2560, 1080), ["fhd"] = (1920, 1080), ["vertical"] = (1080, 1920)
    };

    // USD per token and output tokens per image, from OpenAI's image generation guide and its
    // calculator for GPT-Image-2.6 (checked 2026-11-17). Only for estimates: the cost saved
    // with each image comes from the usage the API reports.
    public const double OutputPerToken = 30e-6, TextInPerToken = 5e-6, ImageInPerToken = 8e-6;

    static readonly Dictionary<string, int> OutTokens = new()
    {
        ["medium 1024x1536"] = 1132, ["high 1024x1536"] = 1372, ["xhigh 1024x1536"] = 2459,
        ["medium 1536x1024"] = 1132, ["high 1536x1024"] = 1372, ["xhigh 1536x1024"] = 2459,
        ["medium 1024x1024"] = 1449, ["high 1024x1024"] = 1756, ["xhigh 1024x1024"] = 3122,
        ["medium 2560x1088"] = 950, ["high 2560x1088"] = 1149, ["xhigh 2560x1088"] = 2068,
        ["medium 1920x1088"] = 1135, ["high 1920x1088"] = 1325, ["xhigh 1920x1088"] = 2356,
        ["medium 1088x1920"] = 1135, ["high 1088x1920"] = 1325, ["xhigh 1088x1920"] = 2356
    };

    public static int EstimateTokens(string quality, string size) => OutTokens.GetValueOrDefault(quality + " " + size, 1400);

    public static double Estimate(string quality, string size, int n) => EstimateTokens(quality, size) * OutputPerToken * n;

    /// <summary>Cost from the usage the API returns.</summary>
    public static double? CostOf(System.Text.Json.Nodes.JsonNode? usage)
    {
        if (usage is null) return null;
        var details = usage["input_tokens_details"];
        double text = details?["text_tokens"]?.GetValue<int>() ?? (details is null ? usage["input_tokens"]?.GetValue<int>() ?? 0 : 0);
        double image = details?["image_tokens"]?.GetValue<int>() ?? 0;
        double output = usage["output_tokens"]?.GetValue<int>() ?? 0;
        return Math.Round(output * OutputPerToken + text * TextInPerToken + image * ImageInPerToken, 5);
    }

    // InvariantGlobalization is on (no pt-BR culture data), so the decimal comma by hand
    public static string Money(double? usd, bool simulated = false) =>
        "US$ " + (usd ?? 0).ToString("0.00#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + (simulated ? " (simulado)" : "");
}
