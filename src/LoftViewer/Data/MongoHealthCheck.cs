using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LoftViewer.Data;

/// <summary>Reports unhealthy when the database does not answer a ping.</summary>
public sealed class MongoHealthCheck(IMongoDatabase database) : IHealthCheck
{
    private static readonly BsonDocumentCommand<BsonDocument> Ping = new(new BsonDocument("ping", 1));

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync(Ping, cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("MongoDB did not respond to ping.", ex);
        }
    }
}
