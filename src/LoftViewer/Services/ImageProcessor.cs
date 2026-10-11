using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LoftViewer.Services;

/// <summary>Normalises uploaded photos before they are stored inline in MongoDB.</summary>
public static class ImageProcessor
{
    /// <summary>Largest upload accepted by the bird endpoints.</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>Longest edge of a stored image. Cards render at a few hundred pixels.</summary>
    public const int MaxDimension = 1600;

    private const int JpegQuality = 85;

    /// <summary>
    /// Only the formats a phone or camera actually produces are decoded. ImageSharp ships TIFF, TGA,
    /// QOI and others by default; none are needed here, and each decoder is attack surface for a file
    /// an uploader controls. Anything outside this list is rejected as "not a supported image".
    /// </summary>
    private static readonly DecoderOptions DecodeOptions = new()
    {
        Configuration = new SixLabors.ImageSharp.Configuration(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new WebpConfigurationModule(),
            new GifConfigurationModule(),
            new BmpConfigurationModule()),
    };

    /// <summary>
    /// Re-encodes any supported image as a JPEG no larger than <see cref="MaxDimension"/> on its long edge,
    /// applies the EXIF orientation and strips metadata (phone photos carry GPS coordinates).
    /// </summary>
    /// <exception cref="UnknownImageFormatException">The stream is not a supported image.</exception>
    /// <exception cref="InvalidImageContentException">The stream is a corrupt image.</exception>
    public static async Task<byte[]> NormalizeAsync(Stream input, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(DecodeOptions, input, cancellationToken);

        image.Mutate(x => x.AutoOrient());
        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxDimension, MaxDimension),
            }));
        }

        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = JpegQuality }, cancellationToken);
        return output.ToArray();
    }
}
