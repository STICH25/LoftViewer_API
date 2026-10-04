using System.Security.Cryptography;
using LoftViewer.Configuration;
using LoftViewer.Data;
using LoftViewer.Services;
using LoftViewer.Services.Weather;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LoftViewer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.IsConfigured,
                "MongoDB is not configured. Set MongoDBSettings:ConnectionString, or Host, Username and Password.")
            .ValidateOnStart();

        // MongoClient is thread-safe and owns the connection pool: one per process.
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.BuildConnectionString()));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.DatabaseName));

        services.AddSingleton<IBirdRepository, BirdRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.HasStrongSecret,
                $"JwtSettings:Secret must be at least {JwtOptions.MinimumSecretBytes} bytes. Set it with user-secrets or the JwtSettings__Secret environment variable.")
            .ValidateOnStart();

        if (environment.IsDevelopment())
        {
            // Lets a fresh clone run without setup. Tokens stop validating whenever the app restarts.
            var ephemeralSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            services.PostConfigure<JwtOptions>(o =>
            {
                if (string.IsNullOrEmpty(o.Secret))
                {
                    o.Secret = ephemeralSecret;
                }
            });
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false; // keep the short "name" / "role" claim types we issue
                bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters();
            });

        services.AddAuthorization();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IAccountService, AccountService>();
        return services;
    }

    public static IServiceCollection AddWeather(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WeatherOptions>()
            .Bind(configuration.GetSection(WeatherOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<WeatherCallBudget>();
        services.AddSingleton<WeatherCache>();
        services.AddHttpClient<IWeatherClient, OpenWeatherClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.openweathermap.org/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHostedService<WeatherRefreshService>();
        return services;
    }

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins ?? [];

        services.AddCors(options => options.AddPolicy(CorsSettings.PolicyName, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
        return services;
    }

    /// <summary>
    /// The app runs behind Railway's proxy, which terminates TLS. Trust its X-Forwarded-* headers so the
    /// request scheme and client IP (used for rate limiting) are the real ones.
    /// </summary>
    public static IServiceCollection AddProxySupport(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return services;
    }
}
