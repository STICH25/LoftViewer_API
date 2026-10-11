using LoftViewer.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LoftViewer.Tests.Unit;

/// <summary>
/// The Mongo-backed repository cannot run without a server, so these render the exact filters and
/// documents it sends, without connecting. They guard the properties its safety depends on.
/// </summary>
public sealed class RefreshTokenMappingTests
{
    private static RenderArgs<RefreshToken> Args =>
        new(BsonSerializer.SerializerRegistry.GetSerializer<RefreshToken>(), BsonSerializer.SerializerRegistry);

    [Fact]
    public void Revoking_matches_only_an_active_token_so_a_rotation_can_be_won_by_one_request()
    {
        FilterDefinition<RefreshToken> filter =
            Builders<RefreshToken>.Filter.Where(t => t.TokenHash == "abc" && t.RevokedAt == null);

        var rendered = filter.Render(Args);

        Assert.Equal("abc", rendered["TokenHash"].AsString);
        Assert.True(rendered["RevokedAt"].IsBsonNull);
    }

    [Fact]
    public void Dates_are_stored_as_BSON_dates_so_the_TTL_index_can_expire_them()
    {
        var token = new RefreshToken
        {
            Id = ObjectId.GenerateNewId().ToString(), // the driver assigns this during InsertOne
            TokenHash = "h",
            UserId = "507f1f77bcf86cd799439011",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };

        var document = token.ToBsonDocument();

        Assert.Equal(BsonType.DateTime, document["ExpiresAt"].BsonType);
        Assert.Equal(BsonType.DateTime, document["CreatedAt"].BsonType);
        Assert.Equal(BsonType.ObjectId, document["_id"].BsonType);
    }

    [Fact]
    public void Only_the_hash_of_the_secret_is_persisted()
    {
        var token = new RefreshToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            TokenHash = "hash-only",
            UserId = "u",
            ExpiresAt = DateTime.UtcNow,
        };

        var names = token.ToBsonDocument().Names.ToList();

        Assert.Contains("TokenHash", names);
        Assert.DoesNotContain(names, n => n.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }
}
