using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace LoftViewer.Tests.Infrastructure;

public static class TestImages
{
    public static byte[] Png(int width, int height, bool withExif = false)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(120, 160, 200));
        if (withExif)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Artist, "Test Camera");
        }

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
