using SkiaSharp;

namespace LoftViewer.Services;

/// <summary>The upload is not an image this service accepts (wrong format, corrupt, or too large).</summary>
public sealed class UnsupportedImageException(string message) : Exception(message);

/// <summary>Normalises uploaded photos before they are stored inline in MongoDB.</summary>
public static class ImageProcessor
{
    /// <summary>Largest upload accepted by the bird endpoints.</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>Longest edge of a stored image. Cards render at a few hundred pixels.</summary>
    public const int MaxDimension = 1600;

    /// <summary>Refuses decompression bombs: a small file that declares an enormous canvas.</summary>
    private const long MaxPixels = 120_000_000;

    private const int JpegQuality = 85;

    /// <summary>
    /// Only formats a phone or camera produces are decoded. Everything else Skia can read is rejected
    /// as "not a supported image": each decoder is attack surface for a file an uploader controls.
    /// </summary>
    private static readonly HashSet<SKEncodedImageFormat> AllowedFormats =
    [
        SKEncodedImageFormat.Jpeg,
        SKEncodedImageFormat.Png,
        SKEncodedImageFormat.Webp,
        SKEncodedImageFormat.Gif,
        SKEncodedImageFormat.Bmp,
    ];

    /// <summary>
    /// Re-encodes a supported image as a JPEG no larger than <see cref="MaxDimension"/> on its long edge.
    /// Applies the EXIF orientation, flattens transparency onto white, and drops all metadata: Skia's
    /// encoder writes none, so GPS coordinates in a phone photo never reach the database.
    /// </summary>
    /// <exception cref="UnsupportedImageException">Not a supported format, corrupt, or too large.</exception>
    public static async Task<byte[]> NormalizeAsync(Stream input, CancellationToken cancellationToken)
    {
        // SKCodec needs a seekable, synchronous stream; the upload is at most MaxUploadBytes.
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        buffer.Position = 0;

        return Normalize(buffer);
    }

    private static byte[] Normalize(Stream stream)
    {
        using var codec = SKCodec.Create(stream)
            ?? throw new UnsupportedImageException("The uploaded file is not a supported image.");

        if (!AllowedFormats.Contains(codec.EncodedFormat))
        {
            throw new UnsupportedImageException("The uploaded file is not a supported image.");
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
        {
            throw new UnsupportedImageException("The image is too large.");
        }

        // JPEGs can be decoded straight to 1/2, 1/4 or 1/8 size, so a 48-megapixel phone photo never
        // has to exist in memory at full resolution.
        // The codec rounds a requested scale to what it supports, sometimes down, which would leave
        // the result under MaxDimension; step up until the decoded size reaches the wanted size.
        var longEdge = Math.Max(info.Width, info.Height);
        var wanted = Math.Min(MaxDimension, longEdge);
        var scale = wanted / (float)longEdge;
        var decodeSize = codec.GetScaledDimensions(scale);
        while (Math.Max(decodeSize.Width, decodeSize.Height) < wanted && scale < 1f)
        {
            scale = Math.Min(1f, scale + 0.125f);
            decodeSize = codec.GetScaledDimensions(scale);
        }

        using var source = SKBitmap.Decode(codec, new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new UnsupportedImageException("The uploaded image could not be read.");

        var (width, height) = OrientedSize(codec.EncodedOrigin, source.Width, source.Height);
        var ratio = Math.Min(1f, MaxDimension / (float)Math.Max(width, height));
        var outWidth = Math.Max(1, (int)Math.Round(width * ratio));
        var outHeight = Math.Max(1, (int)Math.Round(height * ratio));

        using var target = new SKBitmap(outWidth, outHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.White); // JPEG has no alpha; transparent PNG/WebP/GIF areas become white
            canvas.Scale(outWidth / (float)width, outHeight / (float)height);
            canvas.Concat(OrientationMatrix(codec.EncodedOrigin, source.Width, source.Height));
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        }

        using var image = SKImage.FromBitmap(target);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
            ?? throw new UnsupportedImageException("The uploaded image could not be processed.");
        return data.ToArray();
    }

    /// <summary>Width and height after the EXIF orientation is applied (the sideways origins swap them).</summary>
    internal static (int Width, int Height) OrientedSize(SKEncodedOrigin origin, int width, int height) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom
            ? (height, width)
            : (width, height);

    /// <summary>Maps source pixels into the upright image, for a source of the given size.</summary>
    internal static SKMatrix OrientationMatrix(SKEncodedOrigin origin, int width, int height)
    {
        float w = width, h = height;
        return origin switch
        {
            SKEncodedOrigin.TopRight => Matrix(-1, 0, w, 0, 1, 0),
            SKEncodedOrigin.BottomRight => Matrix(-1, 0, w, 0, -1, h),
            SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, h),
            SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => Matrix(0, -1, h, 1, 0, 0),
            SKEncodedOrigin.RightBottom => Matrix(0, -1, h, -1, 0, w),
            SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, w),
            _ => SKMatrix.Identity,
        };
    }

    private static SKMatrix Matrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY) =>
        new(scaleX, skewX, transX, skewY, scaleY, transY, 0, 0, 1);
}
