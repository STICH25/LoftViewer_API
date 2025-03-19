namespace LoftViewer.Models;

public interface IBird
{
    public string Id { get; set; }
    string BirdName { get; set; }
    string BirdNumber { get; set; }
    string? BirdColor { get; set; }
    string? BirdFather { get; set; }
    string? BirdMother { get; set; }
    string? Champion { get; set; }
    public IFormFile? Image { get; set; }
    public string? ImagePath { get; set; }
    public byte[]? ImageBytes { get; set; }
}