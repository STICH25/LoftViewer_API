using LoftViewer.Models;
using MongoDB.Driver;

namespace LoftViewer.Data;

public sealed class RefreshTokenRepository(IMongoDatabase database) : IRefreshTokenRepository
{
    public const string CollectionName = "refreshTokens";

    private readonly IMongoCollection<RefreshToken> _tokens = database.GetCollection<RefreshToken>(CollectionName);

    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken) =>
        _tokens.InsertOneAsync(token, cancellationToken: cancellationToken);

    public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        await _tokens.Find(t => t.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryRevokeAsync(
        string tokenHash, DateTime revokedAt, string? replacedByHash, CancellationToken cancellationToken)
    {
        // The RevokedAt == null filter makes this a compare-and-set: only one concurrent caller matches.
        var result = await _tokens.UpdateOneAsync(
            t => t.TokenHash == tokenHash && t.RevokedAt == null,
            Builders<RefreshToken>.Update
                .Set(t => t.RevokedAt, revokedAt)
                .Set(t => t.ReplacedByHash, replacedByHash),
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    public Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken) =>
        _tokens.UpdateManyAsync(
            t => t.UserId == userId && t.RevokedAt == null,
            Builders<RefreshToken>.Update.Set(t => t.RevokedAt, revokedAt),
            cancellationToken: cancellationToken);
}
