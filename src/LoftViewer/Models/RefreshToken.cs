using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoftViewer.Models;

/// <summary>
/// A long-lived credential a mobile app exchanges for new access tokens. Only the SHA-256 hash of the
/// secret is stored, so a database leak does not hand out working sessions.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class RefreshToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Hex SHA-256 of the token the client holds.</summary>
    public required string TokenHash { get; set; }

    public required string UserId { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    /// <summary>The document is removed by a TTL index shortly after this time.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the token was rotated, signed out, or revoked after suspected theft.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one when it was rotated.</summary>
    public string? ReplacedByHash { get; set; }
}
