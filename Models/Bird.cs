using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace LoftViewer.Models;

public class Bird : IBird
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = String.Empty;
    public string BirdName { get; set; } = String.Empty;
    public string BirdNumber { get; set; } = String.Empty;
    public string? BirdColor { get; set; }  
    public string? BirdFather { get; set; } 
    public string? BirdMother { get; set; }
    public string? Champion { get; set; }
    public IFormFile? Image { get; set; }
    public string? ImagePath { get; set; }
    public byte[]? ImageBytes { get; set; }
}