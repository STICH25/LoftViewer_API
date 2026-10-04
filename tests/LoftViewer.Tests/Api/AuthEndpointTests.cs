using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LoftViewer.Contracts;
using LoftViewer.Models;
using LoftViewer.Tests.Infrastructure;

namespace LoftViewer.Tests.Api;

public sealed class AuthEndpointTests : IClassFixture<LoftViewerApiFactory>
{
    private readonly LoftViewerApiFactory _factory;

    public AuthEndpointTests(LoftViewerApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_token_that_authorizes_admin_calls()
    {
        var user = _factory.Users.Add($"admin-{Guid.NewGuid():N}", "Correct1!", Roles.Admin);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.UserName, "Correct1!"), Ct);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
        Assert.Equal(user.UserName, login!.UserName);
        Assert.Equal(Roles.Admin, login.Role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var delete = await client.DeleteAsync("/api/birds/000000000000000000000000", Ct);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode); // authorized, the bird just does not exist
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var user = _factory.Users.Add($"user-{Guid.NewGuid():N}", "Correct1!", Roles.User);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(user.UserName, "Wrong1!!"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_user_returns_401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody", "Whatever1!"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("newbie", "newbie@example.test", "weak"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_stores_lowercased_user_with_user_role_and_rejects_duplicates()
    {
        var name = $"Mixed{Guid.NewGuid():N}"[..20];
        var request = new RegisterRequest(name, $"{name}@Example.test", "Strong1!pw");
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/register", request, Ct);
        var second = await client.PostAsJsonAsync("/api/auth/register", request, Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var stored = _factory.Users.Users[name.ToLowerInvariant()];
        Assert.Equal(Roles.User, stored.Role);
        Assert.NotEqual("Strong1!pw", stored.PasswordHash);
    }
}
