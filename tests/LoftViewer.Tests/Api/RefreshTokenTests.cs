using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoftViewer.Contracts;
using LoftViewer.Models;
using LoftViewer.Services;
using LoftViewer.Tests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;

namespace LoftViewer.Tests.Api;

public sealed class RefreshTokenTests : IClassFixture<LoftViewerApiFactory>
{
    private const string Password = "Correct1!";

    private readonly LoftViewerApiFactory _factory;

    public RefreshTokenTests(LoftViewerApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AppUser NewUser(string role = Roles.User) => _factory.Users.Add($"u-{Guid.NewGuid():N}", Password, role);

    private async Task<LoginResponse> LoginAsync(AppUser user, bool issueRefreshToken = true)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(user.UserName, Password, issueRefreshToken), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(Ct))!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken), Ct);

    [Fact]
    public async Task Login_without_opt_in_returns_the_original_shape_with_no_refresh_token()
    {
        var user = NewUser();

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { username = user.UserName, password = Password }, Ct);

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(json.RootElement.TryGetProperty("token", out _));
        Assert.False(json.RootElement.TryGetProperty("refreshToken", out _));
        Assert.DoesNotContain(_factory.RefreshTokens.Tokens.Values, t => t.UserId == user.UserId);
    }

    [Fact]
    public async Task Login_with_opt_in_returns_a_refresh_token_that_is_stored_only_as_a_hash()
    {
        var user = NewUser();

        var login = await LoginAsync(user);

        Assert.False(string.IsNullOrEmpty(login.RefreshToken));
        Assert.NotNull(login.RefreshTokenExpiresAt);
        var stored = Assert.Single(_factory.RefreshTokens.Tokens.Values, t => t.UserId == user.UserId);
        Assert.NotEqual(login.RefreshToken, stored.TokenHash);
        Assert.Equal(RefreshTokenService.Hash(login.RefreshToken!), stored.TokenHash);
    }

    [Fact]
    public async Task Refresh_returns_new_tokens_and_the_new_access_token_works()
    {
        var admin = NewUser(Roles.Admin);
        var login = await LoginAsync(admin);

        var response = await RefreshAsync(login.RefreshToken!);

        response.EnsureSuccessStatusCode();
        var refreshed = (await response.Content.ReadFromJsonAsync<LoginResponse>(Ct))!;
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrEmpty(refreshed.Token));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Token);
        var delete = await client.DeleteAsync("/api/birds/000000000000000000000000", Ct);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode); // authorized as admin; the bird just is not there
    }

    [Fact]
    public async Task A_rotated_token_cannot_be_used_again_and_replaying_it_ends_every_session()
    {
        var user = NewUser();
        var first = await LoginAsync(user);
        var second = (await (await RefreshAsync(first.RefreshToken!)).Content.ReadFromJsonAsync<LoginResponse>(Ct))!;

        var replay = await RefreshAsync(first.RefreshToken!);
        var newerNowDead = await RefreshAsync(second.RefreshToken!);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, newerNowDead.StatusCode);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_rejected()
    {
        var user = NewUser();
        var login = await LoginAsync(user);
        _factory.RefreshTokens.Tokens[RefreshTokenService.Hash(login.RefreshToken!)].ExpiresAt =
            DateTime.UtcNow.AddMinutes(-1);

        var response = await RefreshAsync(login.RefreshToken!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected()
    {
        var response = await RefreshAsync("not-a-token-we-issued");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_token_and_never_reveals_whether_it_existed()
    {
        var user = NewUser();
        var login = await LoginAsync(user);
        var client = _factory.CreateClient();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(login.RefreshToken!), Ct);
        var unknown = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest("never-issued"), Ct);
        var refreshAfter = await RefreshAsync(login.RefreshToken!);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfter.StatusCode);
    }

    [Fact]
    public async Task Refresh_picks_up_a_role_change()
    {
        var user = NewUser(Roles.Admin);
        var login = await LoginAsync(user);
        user.Role = Roles.User; // demoted after the app signed in

        var refreshed = (await (await RefreshAsync(login.RefreshToken!)).Content.ReadFromJsonAsync<LoginResponse>(Ct))!;

        Assert.Equal(Roles.User, refreshed.Role);
        var payload = new JsonWebToken(refreshed.Token);
        Assert.Equal(Roles.User, payload.GetClaim("role").Value);
    }

    [Fact]
    public async Task Refresh_for_a_deleted_account_is_rejected()
    {
        var user = NewUser();
        var login = await LoginAsync(user);
        _factory.Users.Users.TryRemove(user.UserName, out _);

        var response = await RefreshAsync(login.RefreshToken!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
