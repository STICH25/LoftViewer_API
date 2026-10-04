using System.Text.Json;
using LoftViewer.Contracts;
using LoftViewer.Data;
using LoftViewer.Models;
using LoftViewer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SixLabors.ImageSharp;

namespace LoftViewer.Controllers;

[ApiController]
[Route("api/birds")]
[Produces("application/json")]
public sealed partial class BirdsController(
    IBirdRepository birds,
    ILogger<BirdsController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions ImportJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string PlaceholderImagePath =
        Path.Combine(AppContext.BaseDirectory, "images", "tempImage.jpg");

    [HttpGet]
    [ProducesResponseType<IEnumerable<BirdResponse>>(StatusCodes.Status200OK)]
    public async Task<IEnumerable<BirdResponse>> GetAll(CancellationToken cancellationToken) =>
        (await birds.GetAllAsync(cancellationToken)).Select(BirdResponse.From);

    [HttpGet("{id}")]
    [ProducesResponseType<BirdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BirdResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        if (!IsObjectId(id))
        {
            return NotFound();
        }

        var bird = await birds.GetByIdAsync(id, cancellationToken);
        return bird is null ? NotFound() : BirdResponse.From(bird);
    }

    /// <summary>The bird's photo, or a placeholder when it has none.</summary>
    [HttpGet("{id}/image")]
    [Produces("image/jpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(string id, CancellationToken cancellationToken)
    {
        var bytes = IsObjectId(id) ? await birds.GetImageAsync(id, cancellationToken) : null;
        if (bytes is { Length: > 0 })
        {
            return File(bytes, "image/jpeg");
        }

        return System.IO.File.Exists(PlaceholderImagePath)
            ? PhysicalFile(PlaceholderImagePath, "image/jpeg")
            : NotFound();
    }

    [HttpPost("addBird")]
    [Authorize(Roles = Roles.Admin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageProcessor.MaxUploadBytes + 64 * 1024)]
    [ProducesResponseType<BirdResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromForm] BirdForm form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.BirdName) || string.IsNullOrWhiteSpace(form.BirdNumber))
        {
            return BadRequestProblem("Bird name and bird number are required.");
        }

        if (await birds.FindByNameOrNumberAsync(form.BirdName, form.BirdNumber, cancellationToken) is not null)
        {
            return Problem("A bird with the same name or number already exists.", statusCode: StatusCodes.Status409Conflict);
        }

        var (image, imageError) = await ReadImageAsync(form.Image, cancellationToken);
        if (imageError is not null)
        {
            return imageError;
        }

        var bird = new Bird
        {
            BirdName = form.BirdName,
            BirdNumber = form.BirdNumber,
            BirdColor = form.BirdColor ?? Bird.NotAvailable,
            BirdFather = form.BirdFather ?? Bird.NotAvailable,
            BirdMother = form.BirdMother ?? Bird.NotAvailable,
            Champion = form.Champion ?? Bird.NotAvailable,
            ImageBytes = image,
        };

        await birds.CreateAsync(bird, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = bird.Id }, BirdResponse.From(bird));
    }

    /// <summary>Bulk-imports birds from a JSON array. Birds whose name or number already exists are skipped.</summary>
    [HttpPost("upload-json")]
    [Authorize(Roles = Roles.Admin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [ProducesResponseType<BirdImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile jsonFile, CancellationToken cancellationToken)
    {
        if (jsonFile is not { Length: > 0 })
        {
            return BadRequestProblem("A non-empty JSON file is required.");
        }

        List<BirdImportItem>? items;
        try
        {
            await using var stream = jsonFile.OpenReadStream();
            items = await JsonSerializer.DeserializeAsync<List<BirdImportItem>>(stream, ImportJsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            LogImportRejected(logger, ex);
            return BadRequestProblem("The file is not a valid JSON array of birds.");
        }

        if (items is not { Count: > 0 })
        {
            return BadRequestProblem("The JSON file is empty.");
        }

        var added = 0;
        var skipped = new List<SkippedBird>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.BirdName) || string.IsNullOrWhiteSpace(item.BirdNumber)
                || await birds.FindByNameOrNumberAsync(item.BirdName, item.BirdNumber, cancellationToken) is not null)
            {
                skipped.Add(new SkippedBird(item.BirdName, item.BirdNumber));
                continue;
            }

            await birds.CreateAsync(new Bird
            {
                BirdName = item.BirdName,
                BirdNumber = item.BirdNumber,
                BirdColor = item.BirdColor,
                BirdFather = item.BirdFather,
                BirdMother = item.BirdMother,
                Champion = item.Champion,
                ImageBytes = item.ImageBytes,
            }, cancellationToken);
            added++;
        }

        return Ok(new BirdImportResult("Bird data processing completed.", added, skipped.Count, skipped));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Admin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageProcessor.MaxUploadBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromForm] BirdForm form, CancellationToken cancellationToken)
    {
        if (!IsObjectId(id))
        {
            return BadRequestProblem("Invalid bird id.");
        }

        var existing = await birds.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var (image, imageError) = await ReadImageAsync(form.Image, cancellationToken);
        if (imageError is not null)
        {
            return imageError;
        }

        var updated = await birds.UpdateAsync(new Bird
        {
            Id = existing.Id,
            BirdName = form.BirdName ?? existing.BirdName,
            BirdNumber = form.BirdNumber ?? existing.BirdNumber,
            BirdColor = form.BirdColor ?? Bird.NotAvailable,
            BirdFather = form.BirdFather ?? Bird.NotAvailable,
            BirdMother = form.BirdMother ?? Bird.NotAvailable,
            Champion = form.Champion ?? Bird.NotAvailable,
            ImageBytes = image, // null keeps the stored image
        }, cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!IsObjectId(id))
        {
            return NotFound();
        }

        return await birds.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private static bool IsObjectId(string id) => ObjectId.TryParse(id, out _);

    private ObjectResult BadRequestProblem(string detail) =>
        Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");

    /// <summary>Normalises an optional upload. Returns (null, null) when no file was sent.</summary>
    private async Task<(byte[]? Image, IActionResult? Error)> ReadImageAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 })
        {
            return (null, null);
        }

        if (file.Length > ImageProcessor.MaxUploadBytes)
        {
            return (null, BadRequestProblem($"Images must be {ImageProcessor.MaxUploadBytes / (1024 * 1024)} MB or smaller."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return (await ImageProcessor.NormalizeAsync(stream, cancellationToken), null);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            LogImageRejected(logger, ex, file.FileName);
            return (null, BadRequestProblem("The uploaded file is not a supported image."));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected bird import file")]
    private static partial void LogImportRejected(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected image upload {FileName}")]
    private static partial void LogImageRejected(ILogger logger, Exception exception, string fileName);
}
