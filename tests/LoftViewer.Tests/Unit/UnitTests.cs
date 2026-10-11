using LoftViewer.Configuration;
using LoftViewer.Services;
using LoftViewer.Services.Weather;
using LoftViewer.Tests.Infrastructure;
using LoftViewer.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SixLabors.ImageSharp;

namespace LoftViewer.Tests.Unit;

public sealed class WindDirectionTests
{
    [Theory]
    [InlineData(0, "N")]
    [InlineData(45, "NE")]
    [InlineData(90, "E")]
    [InlineData(200, "SSW")]
    [InlineData(350, "N")]
    [InlineData(360, "N")]
    [InlineData(-90, "W")]
    public void FromDegrees_maps_bearing_to_compass_point(double degrees, string expected) =>
        Assert.Equal(expected, WindDirection.FromDegrees(degrees));
}

public sealed class CredentialRulesTests
{
    [Theory]
    [InlineData("Passw0rd!", true)]
    [InlineData("password1!", false)] // no uppercase
    [InlineData("Password!", false)]  // no digit
    [InlineData("Password1", false)]  // no special character
    [InlineData("Pa1!", false)]       // too short
    [InlineData("Password1!TooLong!", false)]
    public void IsValidPassword_enforces_policy(string password, bool expected) =>
        Assert.Equal(expected, CredentialRules.IsValidPassword(password));

    [Theory]
    [InlineData("someone@example.com", true)]
    [InlineData("someone@example", false)]
    [InlineData("not an email", false)]
    [InlineData("", false)]
    public void IsValidEmail_checks_shape(string email, bool expected) =>
        Assert.Equal(expected, CredentialRules.IsValidEmail(email));
}

public sealed class ImageProcessorTests
{
    [Fact]
    public async Task NormalizeAsync_keeps_small_images_at_original_size()
    {
        using var input = new MemoryStream(TestImages.Png(400, 300));

        using var result = Image.Load(await ImageProcessor.NormalizeAsync(input, TestContext.Current.CancellationToken));

        Assert.Equal((400, 300), (result.Width, result.Height));
    }

    [Fact]
    public async Task NormalizeAsync_rejects_TIFF_because_only_photo_formats_are_decoded()
    {
        using var tiff = new MemoryStream();
        using (var source = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(32, 32))
        {
            await SixLabors.ImageSharp.ImageExtensions.SaveAsTiffAsync(source, tiff, TestContext.Current.CancellationToken);
        }

        tiff.Position = 0;

        await Assert.ThrowsAsync<SixLabors.ImageSharp.UnknownImageFormatException>(
            () => ImageProcessor.NormalizeAsync(tiff, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    [InlineData("gif")]
    [InlineData("bmp")]
    public async Task NormalizeAsync_accepts_every_format_a_phone_or_camera_produces(string format)
    {
        using var source = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(64, 48);
        using var encoded = new MemoryStream();
        var ct = TestContext.Current.CancellationToken;
        switch (format)
        {
            case "jpeg": await SixLabors.ImageSharp.ImageExtensions.SaveAsJpegAsync(source, encoded, ct); break;
            case "png": await SixLabors.ImageSharp.ImageExtensions.SaveAsPngAsync(source, encoded, ct); break;
            case "webp": await SixLabors.ImageSharp.ImageExtensions.SaveAsWebpAsync(source, encoded, ct); break;
            case "gif": await SixLabors.ImageSharp.ImageExtensions.SaveAsGifAsync(source, encoded, ct); break;
            default: await SixLabors.ImageSharp.ImageExtensions.SaveAsBmpAsync(source, encoded, ct); break;
        }

        encoded.Position = 0;
        using var result = Image.Load(await ImageProcessor.NormalizeAsync(encoded, ct));

        Assert.Equal((64, 48), (result.Width, result.Height));
    }

    [Fact]
    public async Task NormalizeAsync_strips_exif_metadata()
    {
        using var input = new MemoryStream(TestImages.Png(200, 200, withExif: true));

        using var result = Image.Load(await ImageProcessor.NormalizeAsync(input, TestContext.Current.CancellationToken));

        Assert.Null(result.Metadata.ExifProfile);
    }
}

public sealed class WeatherCallBudgetTests
{
    [Fact]
    public void TryConsume_refuses_beyond_daily_limit_and_resets_next_day()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 23, 0, 0, TimeSpan.Zero));
        var budget = new WeatherCallBudget(Options.Create(new WeatherOptions { MaxCallsPerDay = 4 }), time);

        Assert.True(budget.TryConsume(2));
        Assert.True(budget.TryConsume(2));
        Assert.False(budget.TryConsume(2));

        time.Advance(TimeSpan.FromHours(2));
        Assert.True(budget.TryConsume(2));
    }
}

public sealed class JwtOptionsTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("short-secret", false)]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    public void HasStrongSecret_requires_256_bits(string secret, bool expected) =>
        Assert.Equal(expected, new JwtOptions { Secret = secret }.HasStrongSecret);
}
