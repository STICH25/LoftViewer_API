using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoftViewer.Models;

/// <summary>A pigeon as stored in the <c>Birds</c> collection. Field names match existing documents.</summary>
[BsonIgnoreExtraElements] // older documents carry fields this model no longer has (Image, ImagePath)
public sealed class Bird
{
    public const string NotAvailable = "N/A";

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string BirdName { get; set; } = string.Empty;
    public string BirdNumber { get; set; } = string.Empty;
    public string? BirdColor { get; set; }
    public string? BirdFather { get; set; }
    public string? BirdMother { get; set; }
    public string? Champion { get; set; }

    /// <summary>JPEG bytes. Excluded from list and detail queries; served by the image endpoint.</summary>
    public byte[]? ImageBytes { get; set; }
}
