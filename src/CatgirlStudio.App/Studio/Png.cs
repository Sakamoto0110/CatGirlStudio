using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CatgirlStudio.App.Studio;

/// <summary>A decoded image as 32-bit BGRA bytes.</summary>
internal sealed record Pixels(byte[] Bgra, int Width, int Height);

/// <summary>PNG in and out with WPF's codecs (no System.Drawing).</summary>
internal static class Png
{
    public static Pixels Decode(byte[] data)
    {
        using var ms = new MemoryStream(data);
        var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var buffer = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(buffer, bgra.PixelWidth * 4, 0);
        return new Pixels(buffer, bgra.PixelWidth, bgra.PixelHeight);
    }

    public static Pixels Read(string path) => Decode(File.ReadAllBytes(path));

    public static byte[] Encode(Pixels p) => Encode(BitmapSource.Create(p.Width, p.Height, 96, 96, PixelFormats.Bgra32, null, p.Bgra, p.Width * 4));

    public static byte[] Encode(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public static (int Width, int Height) SizeOf(byte[] data)
    {
        using var ms = new MemoryStream(data);
        var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    /// <summary>Crops around the center to the target size; leaves the image alone if it is not larger.</summary>
    public static byte[] CenterCrop(byte[] data, int width, int height)
    {
        var p = Decode(data);
        int left = (p.Width - width) / 2, top = (p.Height - height) / 2;
        if ((p.Width == width && p.Height == height) || left < 0 || top < 0) return data;
        var source = BitmapSource.Create(p.Width, p.Height, 96, 96, PixelFormats.Bgra32, null, p.Bgra, p.Width * 4);
        return Encode(new CroppedBitmap(source, new Int32Rect(left, top, width, height)));
    }

    /// <summary>A small PNG that fits in max×max.</summary>
    public static byte[] Thumbnail(byte[] data, int max)
    {
        using var ms = new MemoryStream(data);
        var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var scale = Math.Min((double)max / frame.PixelWidth, (double)max / frame.PixelHeight);
        return Encode(new TransformedBitmap(frame, new ScaleTransform(scale, scale)));
    }

    /// <summary>An image for display that does not keep the file open.</summary>
    public static BitmapImage? Load(string path, int decodeWidth = 0)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (decodeWidth > 0) img.DecodePixelWidth = decodeWidth;
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UriFormatException or FileFormatException)
        {
            return null;
        }
    }
}
