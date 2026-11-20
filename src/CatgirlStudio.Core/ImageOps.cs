namespace CatgirlStudio.Core;

/// <summary>
/// Pixel work for the 2x post-process, on 32-bit BGRA buffers (no image library: the app
/// decodes and encodes the PNGs). Ported from the old tools/postprocess.py.
/// </summary>
public static class ImageOps
{
    /// <summary>Raw GPT alpha: a residue of 1-4 in the background, the character never above ~249-254.</summary>
    public const int AlphaLow = 4, AlphaHigh = 249;
    /// <summary>The upscaled alpha rings a little at both ends.</summary>
    public const int SnapLow = 5, SnapHigh = 252;

    public static byte MaxAlpha(ReadOnlySpan<byte> bgra)
    {
        byte max = 0;
        for (var i = 3; i < bgra.Length; i += 4) if (bgra[i] > max) max = bgra[i];
        return max;
    }

    public static byte MinAlpha(ReadOnlySpan<byte> bgra)
    {
        byte min = 255;
        for (var i = 3; i < bgra.Length; i += 4) if (bgra[i] < min) min = bgra[i];
        return min;
    }

    /// <summary>alpha' = clamp((a - 4) / (249 - 4)): drops the background residue and makes the character solid.</summary>
    public static void CleanAlpha(Span<byte> bgra)
    {
        Span<byte> lut = stackalloc byte[256];
        for (var v = 0; v < 256; v++)
            lut[v] = (byte)Math.Clamp((int)Math.Round((v - AlphaLow) * 255.0 / (AlphaHigh - AlphaLow)), 0, 255);
        for (var i = 3; i < bgra.Length; i += 4) bgra[i] = lut[bgra[i]];
    }

    /// <summary>Snaps the faint ringing an upscaler leaves: up to 5 becomes 0, from 252 becomes 255.</summary>
    public static byte Snap(byte a) => a <= SnapLow ? (byte)0 : a >= SnapHigh ? (byte)255 : a;

    /// <summary>
    /// Fills the color under fully transparent pixels from the visible ones (pull-push: average
    /// down a pyramid of the visible colors, then push the averages back up into the holes), so
    /// an upscaler does not drag the junk under alpha 0 into the edges. Visible pixels keep their
    /// exact color. Returns an opaque BGRA copy.
    /// </summary>
    public static byte[] Bleed(ReadOnlySpan<byte> bgra, int width, int height)
    {
        var n = width * height;
        var output = bgra.ToArray();
        var known = 0;
        for (var i = 0; i < n; i++) if (bgra[i * 4 + 3] > 0) known++;
        if (known == 0 || known == n)
        {
            for (var i = 0; i < n; i++) output[i * 4 + 3] = 255;
            return output;
        }

        // level 0: premultiplied-by-visibility color and the visibility weight
        var levels = new List<(float[] C, float[] W, int Width, int Height)>();
        var c0 = new float[n * 3];
        var w0 = new float[n];
        double sb = 0, sg = 0, sr = 0;
        for (var i = 0; i < n; i++)
        {
            if (bgra[i * 4 + 3] == 0) continue;
            w0[i] = 1;
            c0[i * 3] = bgra[i * 4]; c0[i * 3 + 1] = bgra[i * 4 + 1]; c0[i * 3 + 2] = bgra[i * 4 + 2];
            sb += bgra[i * 4]; sg += bgra[i * 4 + 1]; sr += bgra[i * 4 + 2];
        }
        levels.Add((c0, w0, width, height));
        while (levels[^1].Width > 1 || levels[^1].Height > 1)
        {
            var (c, w, lw, lh) = levels[^1];
            int nw = (lw + 1) / 2, nh = (lh + 1) / 2;
            var c2 = new float[nw * nh * 3];
            var w2 = new float[nw * nh];
            for (var y = 0; y < nh; y++)
                for (var x = 0; x < nw; x++)
                {
                    float cb = 0, cg = 0, cr = 0, ww = 0; var count = 0;
                    for (var dy = 0; dy < 2; dy++)
                        for (var dx = 0; dx < 2; dx++)
                        {
                            int sx = x * 2 + dx, sy = y * 2 + dy;
                            if (sx >= lw || sy >= lh) continue;
                            var j = sy * lw + sx;
                            cb += c[j * 3]; cg += c[j * 3 + 1]; cr += c[j * 3 + 2]; ww += w[j]; count++;
                        }
                    var k = y * nw + x;
                    c2[k * 3] = cb / count; c2[k * 3 + 1] = cg / count; c2[k * 3 + 2] = cr / count; w2[k] = ww / count;
                }
            levels.Add((c2, w2, nw, nh));
        }

        // the top starts as the mean visible color; each level below mixes its own visible
        // average with the coarser fill: fill = C + (1 - W) * up
        float[] fill = [(float)(sb / known), (float)(sg / known), (float)(sr / known)];
        int fw = 1, fh = 1;
        for (var li = levels.Count - 1; li >= 0; li--)
        {
            var (c, w, lw, lh) = levels[li];
            var up = Upsample(fill, fw, fh, lw, lh);
            var next = new float[lw * lh * 3];
            for (var k = 0; k < lw * lh; k++)
                for (var ch = 0; ch < 3; ch++)
                    next[k * 3 + ch] = c[k * 3 + ch] + (1 - w[k]) * up[k * 3 + ch];
            fill = next; fw = lw; fh = lh;
        }
        for (var i = 0; i < n; i++)
        {
            if (bgra[i * 4 + 3] == 0)
                for (var ch = 0; ch < 3; ch++) output[i * 4 + ch] = (byte)Math.Clamp((int)Math.Round(fill[i * 3 + ch]), 0, 255);
            output[i * 4 + 3] = 255;
        }
        return output;
    }

    static float[] Upsample(float[] src, int sw, int sh, int dw, int dh)
    {
        if (sw == dw && sh == dh) return src;
        var dst = new float[dw * dh * 3];
        for (var y = 0; y < dh; y++)
        {
            var fy = Math.Clamp((y + 0.5f) * sh / dh - 0.5f, 0, sh - 1);
            int y0 = (int)fy, y1 = Math.Min(y0 + 1, sh - 1);
            var ty = fy - y0;
            for (var x = 0; x < dw; x++)
            {
                var fx = Math.Clamp((x + 0.5f) * sw / dw - 0.5f, 0, sw - 1);
                int x0 = (int)fx, x1 = Math.Min(x0 + 1, sw - 1);
                var tx = fx - x0;
                for (var ch = 0; ch < 3; ch++)
                {
                    var a = src[(y0 * sw + x0) * 3 + ch] * (1 - tx) + src[(y0 * sw + x1) * 3 + ch] * tx;
                    var b = src[(y1 * sw + x0) * 3 + ch] * (1 - tx) + src[(y1 * sw + x1) * 3 + ch] * tx;
                    dst[(y * dw + x) * 3 + ch] = a * (1 - ty) + b * ty;
                }
            }
        }
        return dst;
    }

    /// <summary>A gray, opaque BGRA image of the alpha channel (the upscaler only takes color).</summary>
    public static byte[] AlphaAsGray(ReadOnlySpan<byte> bgra)
    {
        var o = new byte[bgra.Length];
        for (var i = 0; i < bgra.Length; i += 4) { o[i] = o[i + 1] = o[i + 2] = bgra[i + 3]; o[i + 3] = 255; }
        return o;
    }

    /// <summary>Color from one image, alpha from the gray one (snapped), into a new BGRA buffer.</summary>
    public static byte[] Merge(ReadOnlySpan<byte> color, ReadOnlySpan<byte> gray)
    {
        var o = color.ToArray();
        for (var i = 0; i < o.Length; i += 4)
        {
            // the gray image went through the upscaler as color: use its luminance
            var l = (gray[i] * 114 + gray[i + 1] * 587 + gray[i + 2] * 299 + 500) / 1000;
            o[i + 3] = Snap((byte)l);
        }
        return o;
    }
}
