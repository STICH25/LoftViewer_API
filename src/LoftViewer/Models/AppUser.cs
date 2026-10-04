using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoftViewer.Models;

/// <summary>An account as stored in the <c>users</c> collection. Field names match existing documents.</summary>
[BsonIgnoreExtraElements]
public sealed class AppUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public required string UserName { get; set; }
    public string? UserEmail { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.User;
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}
