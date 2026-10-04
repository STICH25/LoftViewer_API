using System.Threading.RateLimiting;

namespace LoftViewer.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>Applied to login and registration to slow down password guessing.</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddLoftViewerRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Requests per client IP per minute on the auth endpoints.
        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
        return services;
    }
}
