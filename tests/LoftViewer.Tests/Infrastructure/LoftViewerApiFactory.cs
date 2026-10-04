using System.Net.Http.Headers;
using LoftViewer.Data;
using LoftViewer.Models;
using LoftViewer.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoftViewer.Tests.Infrastructure;

/// <summary>Hosts the real API pipeline with in-memory repositories in place of MongoDB.</summary>
public sealed class LoftViewerApiFactory : WebApplicationFactory<Program>
{
    public InMemoryBirdRepository Birds { get; } = new();
    public InMemoryUserRepository Users { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Never contacted: the repositories are replaced below. The client connects lazily.
            ["MongoDBSettings:ConnectionString"] = "mongodb://localhost:27017",
            ["MongoDBSettings:DatabaseName"] = "LoftViewerTests",
            ["JwtSettings:Secret"] = "test-signing-key-that-is-long-enough-for-hmac-sha256",
            ["WeatherSettings:ApiKey"] = "",
            ["RateLimiting:AuthPermitsPerMinute"] = "10000",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IBirdRepository>(Birds);
            services.AddSingleton<IUserRepository>(Users);
        });
    }

    /// <summary>A client that sends a valid bearer token for a user in <paramref name="role"/>.</summary>
    public HttpClient CreateClientAs(string role)
    {
        var client = CreateClient();
        var user = Users.Add($"{role.ToLowerInvariant()}-{Guid.NewGuid():N}", "Irrelevant1!", role);
        var token = Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateAdminClient() => CreateClientAs(Roles.Admin);
}
