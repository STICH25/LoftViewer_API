using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LoftViewer.Utilities;

public class ImageResizerHelper
{
    #region ImagePresets

    private const long MaxFileSizeInBytes = 16 * 1024 * 1024; // 16 MB
    private const int MaxIterations = 10;
    private const int QualityDecrement = 10;

    #endregion
    
    public static async Task<byte[]> ResizeImageAsync(IFormFile image)
    {
        using var img = await Image.LoadAsync(image.OpenReadStream());
        int originalWidth = img.Width;
        int originalHeight = img.Height;
        int targetWidth = originalWidth;
        int targetHeight = originalHeight;
        int quality = 100;
        byte[] imageBytes = new byte[] { };

        for (int i = 0; i < MaxIterations; i++)
        {
            using var memoryStream = new MemoryStream();
            var encoder = new JpegEncoder { Quality = quality };

            var width = targetWidth;
            var height = targetHeight;
            img.Mutate(x => x.Resize(width, height));
            await img.SaveAsync(memoryStream, encoder);
            imageBytes = memoryStream.ToArray();

            if (imageBytes.Length <= MaxFileSizeInBytes)
            {
                return imageBytes;
            }

            // Reduce dimensions and quality for the next iteration
            targetWidth = (int)(targetWidth * 0.9);
            targetHeight = (int)(targetHeight * 0.9);
            quality -= QualityDecrement;

            // Reset image to original dimensions for the next iteration
            img.Mutate(x => x.Resize(originalWidth, originalHeight));

        }
        return imageBytes.ToArray();
    }

    public static async Task<string> SaveImageAsync(byte[] imageBytes)
    {
        var fileName = Guid.NewGuid().ToString() + ".jpg";
        var filePath = Path.Combine("wwwroot", "images", fileName);

        await File.WriteAllBytesAsync(filePath, imageBytes);

        return filePath;
    }
}