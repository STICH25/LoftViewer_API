using LoftViewer.Models;
using MongoDB.Driver;

namespace LoftViewer.Data;

/// <summary>Creates the refresh-token indexes at startup. Failure is logged, never fatal.</summary>
public sealed partial class MongoIndexInitializer(IMongoDatabase database, ILogger<MongoIndexInitializer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var tokens = database.GetCollection<RefreshToken>(RefreshTokenRepository.CollectionName);
            await tokens.Indexes.CreateManyAsync(
                [
                    // Lookups by the hash of the presented token.
                    new CreateIndexModel<RefreshToken>(
                        Builders<RefreshToken>.IndexKeys.Ascending(t => t.TokenHash),
                        new CreateIndexOptions { Unique = true }),
                    // "Sign this user out everywhere" after a replayed token.
                    new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.UserId)),
                    // Expired tokens delete themselves.
                    new CreateIndexModel<RefreshToken>(
                        Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt),
                        new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
                ],
                stoppingToken);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            LogIndexCreationFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the refreshTokens indexes; refresh will work but lookups may be slow")]
    private static partial void LogIndexCreationFailed(ILogger logger, Exception exception);
}
