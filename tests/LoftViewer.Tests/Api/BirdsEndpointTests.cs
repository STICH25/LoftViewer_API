using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LoftViewer.Contracts;
using LoftViewer.Models;
using LoftViewer.Tests.Infrastructure;

namespace LoftViewer.Tests.Api;

public sealed class BirdsEndpointTests : IClassFixture<LoftViewerApiFactory>
{
    private readonly LoftViewerApiFactory _factory;

    public BirdsEndpointTests(LoftViewerApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAll_returns_birds_without_image_bytes()
    {
        var bird = _factory.Birds.Add(new Bird { BirdName = Unique("Blue"), BirdNumber = Unique("N"), ImageBytes = [1, 2, 3] });

        var response = await _factory.CreateClient().GetAsync("/api/birds", Ct);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains(bird.BirdName, json, StringComparison.Ordinal);
        Assert.DoesNotContain("imageBytes", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_with_malformed_id_returns_404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/birds/not-an-object-id", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetImage_without_stored_image_returns_placeholder_jpeg()
    {
        var bird = _factory.Birds.Add(new Bird { BirdName = Unique("NoPhoto"), BirdNumber = Unique("N") });

        var response = await _factory.CreateClient().GetAsync($"/api/birds/{bird.Id}/image", Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetImage_is_cacheable_and_a_repeat_request_with_the_etag_gets_304()
    {
        var bird = _factory.Birds.Add(new Bird
        {
            BirdName = Unique("Cached"),
            BirdNumber = Unique("N"),
            ImageBytes = TestImages.Png(40, 40),
        });
        var client = _factory.CreateClient();

        var first = await client.GetAsync($"/api/birds/{bird.Id}/image", Ct);
        var etag = first.Headers.ETag;

        first.EnsureSuccessStatusCode();
        Assert.NotNull(etag);
        Assert.Contains("max-age=300", first.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, $"/api/birds/{bird.Id}/image");
        conditional.Headers.IfNoneMatch.Add(etag);
        var second = await client.SendAsync(conditional, Ct);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task AddBird_without_token_returns_401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/birds/addBird", BirdForm(Unique("A"), Unique("1")), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddBird_as_regular_user_returns_403()
    {
        var response = await _factory.CreateClientAs(Roles.User).PostAsync("/api/birds/addBird", BirdForm(Unique("A"), Unique("1")), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddBird_as_admin_stores_bird_with_downscaled_jpeg()
    {
        var name = Unique("Big");
        var form = BirdForm(name, Unique("1"), image: TestImages.Png(3200, 1600));

        var response = await _factory.CreateAdminClient().PostAsync("/api/birds/addBird", form, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BirdResponse>(Ct);
        var stored = _factory.Birds.Birds[created!.Id];
        Assert.Equal(name, stored.BirdName);
        Assert.Equal(Bird.NotAvailable, stored.BirdColor);

        var (width, height, format) = TestImages.Inspect(stored.ImageBytes!);
        Assert.Equal(SkiaSharp.SKEncodedImageFormat.Jpeg, format);
        Assert.Equal(1600, width);
        Assert.Equal(800, height);
    }

    [Fact]
    public async Task AddBird_with_existing_number_returns_409()
    {
        var number = Unique("Dup");
        _factory.Birds.Add(new Bird { BirdName = Unique("First"), BirdNumber = number });

        var response = await _factory.CreateAdminClient().PostAsync("/api/birds/addBird", BirdForm(Unique("Second"), number), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddBird_with_non_image_file_returns_400()
    {
        var form = BirdForm(Unique("Bad"), Unique("1"), image: "definitely not an image"u8.ToArray());

        var response = await _factory.CreateAdminClient().PostAsync("/api/birds/addBird", form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_without_new_image_keeps_stored_image()
    {
        var bird = _factory.Birds.Add(new Bird { BirdName = Unique("Keep"), BirdNumber = Unique("N"), ImageBytes = [9, 9, 9] });
        var form = new MultipartFormDataContent { { new StringContent("Red Check"), "birdColor" } };

        var response = await _factory.CreateAdminClient().PutAsync($"/api/birds/{bird.Id}", form, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = _factory.Birds.Birds[bird.Id];
        Assert.Equal("Red Check", stored.BirdColor);
        Assert.Equal(bird.BirdName, stored.BirdName);
        Assert.Equal([9, 9, 9], stored.ImageBytes);
    }

    [Fact]
    public async Task Delete_as_admin_removes_bird()
    {
        var bird = _factory.Birds.Add(new Bird { BirdName = Unique("Gone"), BirdNumber = Unique("N") });
        var client = _factory.CreateAdminClient();

        var first = await client.DeleteAsync($"/api/birds/{bird.Id}", Ct);
        var second = await client.DeleteAsync($"/api/birds/{bird.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static MultipartFormDataContent BirdForm(string name, string number, byte[]? image = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "birdName" },
            { new StringContent(number), "birdNumber" },
        };

        if (image is not null)
        {
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "image", "upload.bin");
        }

        return form;
    }
}
