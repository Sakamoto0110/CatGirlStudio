using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatgirlStudio.App.Studio;

/// <summary>A message for the user: the request failed in a way they can act on.</summary>
internal sealed class StudioException(string message) : Exception(message);

internal sealed record ApiResult(List<byte[]> Images, JsonNode? Usage, string? RequestId);

internal interface IImageApi
{
    bool Fake { get; }
    bool HasKey { get; }
    Task<ApiResult> PostAsync(string endpoint, JsonObject body, CancellationToken ct);
}

/// <summary>The OpenAI Images API (generations and edits). The key comes from the
/// OpenAI_CatGirl_API environment variable and only ever goes into the Authorization header.</summary>
internal sealed class OpenAiImages : IImageApi
{
    public const string KeyVariable = "OpenAI_CatGirl_API";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5.5) };

    public bool Fake => false;
    public bool HasKey => ReadKey() is not null;

    // the user-level variable is read straight from the registry too, so a key saved after
    // the app (or the terminal that started it) opened still works
    static string? ReadKey()
    {
        var key = Environment.GetEnvironmentVariable(KeyVariable) ?? Environment.GetEnvironmentVariable(KeyVariable, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public async Task<ApiResult> PostAsync(string endpoint, JsonObject body, CancellationToken ct)
    {
        var key = ReadKey() ?? throw new StudioException($"Não achei a chave: crie a variável de ambiente {KeyVariable} com a sua chave da OpenAI.");
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/" + endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        HttpResponseMessage res;
        try { res = await Http.SendAsync(req, ct); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new StudioException("A OpenAI demorou mais de 5 minutos para responder."); }
        catch (HttpRequestException e) { throw new StudioException("Sem conexão com a OpenAI: " + e.Message); }
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync(ct);
            JsonNode? json = null;
            try { json = JsonNode.Parse(text); } catch (JsonException) { }
            if (!res.IsSuccessStatusCode) throw new StudioException(Explain((int)res.StatusCode, json?["error"]));
            var images = json?["data"]?.AsArray().Select(d => Convert.FromBase64String(d!["b64_json"]!.GetValue<string>())).ToList() ?? [];
            if (images.Count == 0) throw new StudioException("A OpenAI respondeu sem imagem.");
            res.Headers.TryGetValues("x-request-id", out var ids);
            return new ApiResult(images, json!["usage"]?.DeepClone(), ids?.FirstOrDefault());
        }
    }

    /// <summary>Turns an API error into a message for the user (keeping the API's own text).</summary>
    static string Explain(int status, JsonNode? err)
    {
        var message = err?["message"]?.GetValue<string>()?.Trim() ?? "";
        if (err?["code"]?.GetValue<string>() == "moderation_blocked")
        {
            var details = err["moderation_details"];
            var stage = details?["moderation_stage"]?.GetValue<string>() switch { "input" => " no pedido", "output" => " na imagem gerada", _ => "" };
            var cats = string.Join(", ", details?["categories"]?.AsArray().Select(c => c!.GetValue<string>()) ?? []);
            return $"A OpenAI bloqueou{stage} pelo filtro de conteúdo{(cats.Length > 0 ? " (" + cats + ")" : "")}. Mude o prompt e tente de novo.";
        }
        return status switch
        {
            401 => $"A OpenAI recusou a chave em {KeyVariable}. Confira se ela está certa e ativa.",
            403 => $"A OpenAI negou o acesso: {message} (o modelo pode exigir verificar a organização no painel da OpenAI).",
            429 => $"Limite de uso ou de crédito atingido: {message}",
            _ => $"Erro {status} da OpenAI: {(message.Length > 0 ? message : "sem detalhes")}"
        };
    }
}

/// <summary>Answers like the API with drawn images, so the app can be tried without spending.
/// Start the app with --fake to use it.</summary>
internal sealed class FakeImages : IImageApi
{
    public bool Fake => true;
    public bool HasKey => true;

    public async Task<ApiResult> PostAsync(string endpoint, JsonObject body, CancellationToken ct)
    {
        await Task.Delay(1200, ct);
        var size = body["size"]!.GetValue<string>().Split('x').Select(int.Parse).ToArray();
        var transparent = body["background"]?.GetValue<string>() == "transparent";
        var prompt = body["prompt"]!.GetValue<string>();
        var n = body["n"]?.GetValue<int>() ?? 1;
        var images = new List<byte[]>();
        for (var i = 0; i < n; i++)
        {
            int w = size[0], h = size[1];
            var visual = new DrawingVisual();
            using (var g = visual.RenderOpen())
            {
                if (endpoint == "edits")
                {
                    var url = body["images"]![0]!["image_url"]!.GetValue<string>();
                    var parent = Png.Decode(Convert.FromBase64String(url[(url.IndexOf(',') + 1)..]));
                    (w, h) = (parent.Width, parent.Height);
                    g.DrawImage(BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, parent.Bgra, w * 4), new Rect(0, 0, w, h));
                }
                else
                {
                    if (!transparent) g.DrawRectangle(new SolidColorBrush(Color.FromRgb(238, 232, 244)), null, new Rect(0, 0, w, h));
                    g.DrawEllipse(new SolidColorBrush(Color.FromRgb(244, 214, 170)), null, new Point(w * .5, h * .23), w * .2, h * .15);
                    g.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(201, 58, 100)), null, new Rect(w * .22, h * .36, w * .56, h * .59), 40, 40);
                }
                var band = endpoint == "edits" ? h * .8 : h * .4;
                g.DrawRectangle(new SolidColorBrush(Color.FromArgb(230, 20, 20, 30)), null, new Rect(0, band, w, 120));
                var label = (endpoint == "edits" ? "EDIT: " : $"FAKE {i + 1}  ") + prompt[..Math.Min(prompt.Length, 220)];
                var text = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 18, Brushes.White, 1.0) { MaxTextWidth = w - 32, MaxTextHeight = 110 };
                g.DrawText(text, new Point(16, band + 8));
            }
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            images.Add(Png.Encode(bmp));
        }
        var usage = new JsonObject
        {
            ["input_tokens"] = 600,
            ["output_tokens"] = Pricing.EstimateTokens(body["quality"]!.GetValue<string>(), body["size"]!.GetValue<string>()) * images.Count,
            ["input_tokens_details"] = new JsonObject { ["text_tokens"] = 500, ["image_tokens"] = endpoint == "edits" ? 1000 : 0 }
        };
        return new ApiResult(images, usage, "fake");
    }
}
