using System.Text;
using SkiaSharp;

namespace LoftViewer.Tests.Infrastructure;

/// <summary>Builds small real images for tests, including JPEGs with hand-written EXIF.</summary>
public static class TestImages
{
    /// <summary>A solid-colour PNG.</summary>
    public static byte[] Png(int width, int height) => Encode(Solid(width, height, new SKColor(120, 160, 200)), SKEncodedImageFormat.Png);

    /// <summary>A solid-colour JPEG.</summary>
    public static byte[] Jpeg(int width, int height) => Encode(Solid(width, height, new SKColor(120, 160, 200)), SKEncodedImageFormat.Jpeg);

    public static byte[] Encoded(int width, int height, SKEncodedImageFormat format) =>
        Encode(Solid(width, height, new SKColor(120, 160, 200)), format);

    /// <summary>
    /// A 24-bit BMP built by hand: Skia can decode BMP but has no BMP encoder, so a fixture cannot be
    /// produced the way the other formats are.
    /// </summary>
    public static byte[] Bmp(int width, int height)
    {
        var rowBytes = (width * 3 + 3) / 4 * 4;
        var pixelBytes = rowBytes * height;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + pixelBytes); // file size
        writer.Write(0); // reserved
        writer.Write(54); // pixel data offset
        writer.Write(40); // DIB header size
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1); // planes
        writer.Write((short)24); // bits per pixel
        writer.Write(0); // no compression
        writer.Write(pixelBytes);
        writer.Write(2835); // 72 dpi
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < rowBytes; x++)
            {
                writer.Write((byte)(x % 3 == 0 ? 200 : 120)); // B, G, R repeating: a flat colour
            }
        }

        return stream.ToArray();
    }

    /// <summary>An .ico file wrapping a PNG. Skia can decode it, but it is not a photo format.</summary>
    public static byte[] Ico(int size)
    {
        var png = Png(size, size);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)0); // reserved
        writer.Write((short)1); // type: icon
        writer.Write((short)1); // one image
        writer.Write((byte)size);
        writer.Write((byte)size);
        writer.Write((byte)0); // palette size
        writer.Write((byte)0); // reserved
        writer.Write((short)1); // colour planes
        writer.Write((short)32); // bits per pixel
        writer.Write(png.Length);
        writer.Write(22); // offset: 6 byte header + 16 byte entry
        writer.Write(png);
        return stream.ToArray();
    }

    /// <summary>A JPEG whose left half is red and right half is blue, so rotations are observable.</summary>
    public static byte[] RedBlueJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2f, height, red);
            canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        }

        return Encode(bitmap, SKEncodedImageFormat.Jpeg, 95);
    }

    /// <summary>
    /// Inserts an EXIF APP1 segment carrying an orientation tag (1 to 8), optionally followed by extra
    /// text that stands in for metadata such as GPS coordinates.
    /// </summary>
    public static byte[] WithExif(byte[] jpeg, int orientation, string? embeddedText = null)
    {
        // Little-endian TIFF header, then one IFD entry: tag 0x0112 (Orientation), SHORT, count 1.
        byte[] tiff =
        [
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, (byte)orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        var payload = new List<byte>(Encoding.ASCII.GetBytes("Exif\0\0"));
        payload.AddRange(tiff);
        if (embeddedText is not null)
        {
            payload.AddRange(Encoding.ASCII.GetBytes(embeddedText));
        }

        var length = payload.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) };
        segment.AddRange(payload);

        // Right after the two-byte start-of-image marker.
        var result = new List<byte>(jpeg.Length + segment.Count);
        result.AddRange(jpeg.Take(2));
        result.AddRange(segment);
        result.AddRange(jpeg.Skip(2));
        return result.ToArray();
    }

    /// <summary>Reads size and format from encoded bytes without needing the code under test.</summary>
    public static (int Width, int Height, SKEncodedImageFormat Format) Inspect(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes)) ?? throw new InvalidOperationException("Not an image.");
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat);
    }

    /// <summary>The colour of the pixel at (x, y) in decoded bytes.</summary>
    public static SKColor PixelAt(byte[] bytes, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        return bitmap.GetPixel(x, y);
    }

    private static SKBitmap Solid(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 90)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(format, quality))
        {
            return data.ToArray();
        }
    }
}
