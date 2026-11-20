using System.Diagnostics;
using System.IO;
using CatgirlStudio.Core;

namespace CatgirlStudio.App.Studio;

/// <summary>
/// The upscale: cleans the GPT's dirty transparency, spreads color under the transparent pixels
/// and runs Real-ESRGAN (realesr-animevideov3, x2/x3/x4, optionally with TTA) on the color and on
/// the alpha separately, then merges them. Real-ESRGAN is the ncnn-vulkan build, a separate
/// program run on the GPU.
/// </summary>
internal static class Upscaler
{
    const string Exe = "realesrgan-ncnn-vulkan.exe";
    public const string DownloadUrl = "https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0";

    /// <summary>Looks next to the app (realesrgan\) and then up the folders for the project's
    /// tools\bin\realesrgan-ncnn-vulkan\ (where the development copy lives).</summary>
    public static string? Find()
    {
        var here = AppContext.BaseDirectory;
        var beside = Path.Combine(here, "realesrgan", Exe);
        if (File.Exists(beside)) return beside;
        for (var dir = new DirectoryInfo(here); dir != null; dir = dir.Parent)
        {
            var dev = Path.Combine(dir.FullName, "tools", "bin", "realesrgan-ncnn-vulkan", Exe);
            if (File.Exists(dev)) return dev;
        }
        return null;
    }

    /// <summary>Writes outDir\name.png at 2, 3 or 4 times the size and returns its path. TTA runs
    /// the model on 8 flipped/rotated copies and averages them: cleaner edges, about 8x slower.</summary>
    public static async Task<string> Upscale(string pngPath, string outDir, int scale, bool tta, CancellationToken ct = default)
    {
        if (scale is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(scale), "realesr-animevideov3 has x2, x3 and x4 models");
        var exe = Find() ?? throw new StudioException($"Não achei o Real-ESRGAN. Baixe o realesrgan-ncnn-vulkan-20220424-windows.zip de {DownloadUrl} e extraia numa pasta \"realesrgan\" ao lado do app.");
        var src = Png.Read(pngPath);
        var hasAlpha = ImageOps.MinAlpha(src.Bgra) < 255;
        if (hasAlpha && ImageOps.MaxAlpha(src.Bgra) < 255) ImageOps.CleanAlpha(src.Bgra); // raw GPT output

        var tmp = Directory.CreateTempSubdirectory("catgirl-2x-");
        try
        {
            var input = tmp.CreateSubdirectory("in");
            var output = tmp.CreateSubdirectory("out");
            var color = hasAlpha ? ImageOps.Bleed(src.Bgra, src.Width, src.Height) : src.Bgra;
            await File.WriteAllBytesAsync(Path.Combine(input.FullName, "color.png"), Png.Encode(new Pixels(color, src.Width, src.Height)), ct);
            if (hasAlpha)
                await File.WriteAllBytesAsync(Path.Combine(input.FullName, "alpha.png"), Png.Encode(new Pixels(ImageOps.AlphaAsGray(src.Bgra), src.Width, src.Height)), ct);

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var a in new[] { "-i", input.FullName, "-o", output.FullName, "-n", "realesr-animevideov3", "-s", scale.ToString(), "-f", "png", "-m", Path.Combine(Path.GetDirectoryName(exe)!, "models") })
                psi.ArgumentList.Add(a);
            if (tta) psi.ArgumentList.Add("-x");
            using var process = Process.Start(psi) ?? throw new StudioException("Não consegui abrir o Real-ESRGAN.");
            var errors = process.StandardError.ReadToEndAsync(ct);
            _ = process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var colorOut = Path.Combine(output.FullName, "color.png");
            var alphaOut = Path.Combine(output.FullName, "alpha.png");
            if (process.ExitCode != 0 || !File.Exists(colorOut) || (hasAlpha && !File.Exists(alphaOut)))
            {
                var tail = string.Join(" / ", (await errors).Trim().Split('\n').TakeLast(3));
                throw new StudioException($"O Real-ESRGAN falhou (código {process.ExitCode}): {tail}");
            }

            var big = Png.Read(colorOut);
            var merged = hasAlpha ? ImageOps.Merge(big.Bgra, Png.Read(alphaOut).Bgra) : big.Bgra;
            Directory.CreateDirectory(outDir);
            var dest = Path.Combine(outDir, Path.GetFileName(pngPath));
            await File.WriteAllBytesAsync(dest, Png.Encode(new Pixels(merged, big.Width, big.Height)), ct);
            return dest;
        }
        finally
        {
            try { tmp.Delete(recursive: true); } catch (IOException) { }
        }
    }
}
