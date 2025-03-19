using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using BCrypt.Net;
using LoftViewer.interfaces;

namespace LoftViewer.Models;

[BsonIgnoreExtraElements]
public class UserModel : IUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = String.Empty;

    public required string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Store hashed password
    public string Role { get; set; } = "User"; // User role, can be "Admin", "User"
    
    // **SetPassword Method (Hashes Password)**
    public void SetPassword(string password)
    {
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        Console.WriteLine(PasswordHash);
    }

    // **VerifyPassword Method (Compares Hash with Input)**
    public bool VerifyPassword(string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }
    
}