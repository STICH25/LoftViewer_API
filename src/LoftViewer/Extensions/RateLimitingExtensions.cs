using System.Threading.RateLimiting;

namespace LoftViewer.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>Applied to login and registration to slow down password guessing.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Applied to token refresh and sign-out. More generous than <see cref="AuthPolicy"/>: a refresh
    /// secret is 256 random bits, so it cannot be guessed, and many phones can share one carrier IP.
    /// </summary>
    public const string RefreshPolicy = "refresh";

    public static IServiceCollection AddLoftViewerRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicy, context => PerClientIp(context, "RateLimiting:AuthPermitsPerMinute", 10));
            options.AddPolicy(RefreshPolicy, context => PerClientIp(context, "RateLimiting:RefreshPermitsPerMinute", 30));
        });
        return services;
    }

    /// <summary>
    /// Reads the limit from configuration when a request arrives rather than at startup, so a value
    /// supplied through the host's configuration (and tests) is always honoured.
    /// </summary>
    private static RateLimitPartition<string> PerClientIp(HttpContext context, string settingName, int defaultLimit)
    {
        var limit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue(settingName, defaultLimit);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{settingName}:{context.Connection.RemoteIpAddress}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    }
}
