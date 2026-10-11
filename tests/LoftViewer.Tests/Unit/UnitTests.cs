using LoftViewer.Configuration;
using LoftViewer.Services;
using LoftViewer.Services.Weather;
using LoftViewer.Tests.Infrastructure;
using LoftViewer.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SkiaSharp;

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
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<byte[]> NormalizeAsync(byte[] bytes) => ImageProcessor.NormalizeAsync(new MemoryStream(bytes), Ct);

    [Fact]
    public async Task Keeps_small_images_at_their_size_and_returns_a_JPEG()
    {
        var result = TestImages.Inspect(await NormalizeAsync(TestImages.Png(400, 300)));

        Assert.Equal((400, 300, SKEncodedImageFormat.Jpeg), result);
    }

    [Fact]
    public async Task Scales_large_images_down_to_the_longest_edge_keeping_proportions()
    {
        var result = TestImages.Inspect(await NormalizeAsync(TestImages.Jpeg(4000, 3000)));

        Assert.Equal((ImageProcessor.MaxDimension, 1200, SKEncodedImageFormat.Jpeg), result);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task Accepts_every_format_a_phone_or_camera_produces(SKEncodedImageFormat format)
    {
        var result = TestImages.Inspect(await NormalizeAsync(TestImages.Encoded(64, 48, format)));

        Assert.Equal((64, 48, SKEncodedImageFormat.Jpeg), result);
    }

    [Fact]
    public async Task Accepts_BMP()
    {
        var result = TestImages.Inspect(await NormalizeAsync(TestImages.Bmp(64, 48)));

        Assert.Equal((64, 48, SKEncodedImageFormat.Jpeg), result);
    }

    [Theory]
    [InlineData("not an image at all")]
    [InlineData("")]
    public async Task Rejects_bytes_that_are_not_an_image(string text)
    {
        await Assert.ThrowsAsync<UnsupportedImageException>(() => NormalizeAsync(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public async Task Rejects_a_truncated_image()
    {
        var truncated = TestImages.Png(200, 200).Take(40).ToArray();

        await Assert.ThrowsAsync<UnsupportedImageException>(() => NormalizeAsync(truncated));
    }

    [Fact]
    public async Task Rejects_a_format_that_is_not_a_photo_format()
    {
        // Skia can decode an .ico, but it is not something a camera produces.
        var icon = TestImages.Ico(32);

        await Assert.ThrowsAsync<UnsupportedImageException>(() => NormalizeAsync(icon));
    }

    [Theory]
    [InlineData(1, 40, 20)] // as shot
    [InlineData(3, 40, 20)] // upside down: same size
    [InlineData(6, 20, 40)] // phone held upright: stored sideways, comes out portrait
    [InlineData(8, 20, 40)]
    public async Task Applies_the_EXIF_orientation_to_the_pixels_and_the_size(int orientation, int width, int height)
    {
        var tilted = TestImages.WithExif(TestImages.RedBlueJpeg(40, 20), orientation);

        var result = TestImages.Inspect(await NormalizeAsync(tilted));

        Assert.Equal((width, height), (result.Width, result.Height));
    }

    [Fact]
    public async Task Rotates_content_a_quarter_turn_clockwise_for_orientation_6()
    {
        // Source: red left half, blue right half. A 90 degree clockwise turn puts red on top.
        var output = await NormalizeAsync(TestImages.WithExif(TestImages.RedBlueJpeg(40, 20), orientation: 6));

        var top = TestImages.PixelAt(output, 10, 5);
        var bottom = TestImages.PixelAt(output, 10, 35);

        Assert.True(top.Red > 200 && top.Blue < 60, $"expected red on top, got {top}");
        Assert.True(bottom.Blue > 200 && bottom.Red < 60, $"expected blue below, got {bottom}");
    }

    [Fact]
    public async Task Flips_content_for_orientation_3()
    {
        var output = await NormalizeAsync(TestImages.WithExif(TestImages.RedBlueJpeg(40, 20), orientation: 3));

        var left = TestImages.PixelAt(output, 5, 10);
        var right = TestImages.PixelAt(output, 35, 10);

        Assert.True(left.Blue > 200 && left.Red < 60, $"expected blue on the left, got {left}");
        Assert.True(right.Red > 200 && right.Blue < 60, $"expected red on the right, got {right}");
    }

    [Fact]
    public async Task Strips_embedded_metadata_such_as_GPS_coordinates()
    {
        var withGps = TestImages.WithExif(TestImages.Jpeg(120, 80), orientation: 1, embeddedText: "GPSLatitude=27.9506N;GPSLongitude=82.4572W");
        Assert.Contains("GPSLatitude", System.Text.Encoding.ASCII.GetString(withGps), StringComparison.Ordinal); // the fixture really carries it

        var output = System.Text.Encoding.ASCII.GetString(await NormalizeAsync(withGps));

        Assert.DoesNotContain("GPSLatitude", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Exif", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Orientation_matrix_sends_the_source_corner_to_the_right_place()
    {
        // 4x2 source, rotated a quarter turn clockwise (RightTop): source top-left becomes upright top-right.
        var matrix = ImageProcessor.OrientationMatrix(SKEncodedOrigin.RightTop, 4, 2);

        var topLeft = matrix.MapPoint(0, 0);
        var bottomRight = matrix.MapPoint(4, 2);

        Assert.Equal((2f, 0f), (topLeft.X, topLeft.Y));
        Assert.Equal((0f, 4f), (bottomRight.X, bottomRight.Y));
        Assert.Equal((2, 4), ImageProcessor.OrientedSize(SKEncodedOrigin.RightTop, 4, 2));
        Assert.Equal((4, 2), ImageProcessor.OrientedSize(SKEncodedOrigin.TopLeft, 4, 2));
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
