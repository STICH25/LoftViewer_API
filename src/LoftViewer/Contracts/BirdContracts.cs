using LoftViewer.Models;

namespace LoftViewer.Contracts;

/// <summary>The bird form posted as multipart/form-data by the add and update screens.</summary>
public sealed class BirdForm
{
    public string? BirdName { get; set; }
    public string? BirdNumber { get; set; }
    public string? BirdColor { get; set; }
    public string? BirdFather { get; set; }
    public string? BirdMother { get; set; }
    public string? Champion { get; set; }
    public IFormFile? Image { get; set; }
}

/// <summary>One entry of a bulk JSON import.</summary>
public sealed record BirdImportItem(
    string? BirdName,
    string? BirdNumber,
    string? BirdColor,
    string? BirdFather,
    string? BirdMother,
    string? Champion,
    byte[]? ImageBytes);

public sealed record BirdImportResult(
    string Message,
    int AddedCount,
    int SkippedCount,
    IReadOnlyList<SkippedBird> SkippedBirds);

public sealed record SkippedBird(string? BirdName, string? BirdNumber);

/// <summary>A bird as returned to clients. Never carries image bytes.</summary>
public sealed record BirdResponse(
    string Id,
    string BirdName,
    string BirdNumber,
    string? BirdColor,
    string? BirdFather,
    string? BirdMother,
    string? Champion)
{
    public static BirdResponse From(Bird bird) => new(
        bird.Id, bird.BirdName, bird.BirdNumber, bird.BirdColor, bird.BirdFather, bird.BirdMother, bird.Champion);
}
