using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using LoftViewer.Configuration;
using LoftViewer.Data;
using LoftViewer.Models;
using Microsoft.Extensions.Options;

namespace LoftViewer.Services;

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);

public sealed record RefreshedSession(AppUser User, IssuedRefreshToken RefreshToken);

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(AppUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for a new one (the old one stops working). Returns null when the token
    /// is unknown, expired, revoked, or belongs to a user that no longer exists.
    /// </summary>
    Task<RefreshedSession?> RotateAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Signs out the session the token belongs to. Unknown tokens are ignored.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed class RefreshTokenService(
    IRefreshTokenRepository tokens,
    IUserRepository users,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IRefreshTokenService
{
    private const int SecretBytes = 32;

    public async Task<IssuedRefreshToken> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        var (secret, hash) = NewToken();
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(options.Value.RefreshTokenDays);

        await tokens.CreateAsync(new RefreshToken
        {
            TokenHash = hash,
            UserId = user.UserId,
            CreatedAt = now.UtcDateTime,
            ExpiresAt = expiresAt.UtcDateTime,
        }, cancellationToken);

        return new IssuedRefreshToken(secret, expiresAt);
    }

    public async Task<RefreshedSession?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var presentedHash = Hash(refreshToken);
        var current = await tokens.FindByHashAsync(presentedHash, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (current.RevokedAt is not null)
        {
            // A token that was already rotated or signed out is being used again: it was copied, or a
            // client replayed it. Treat the account's sessions as compromised and end all of them.
            await tokens.RevokeAllForUserAsync(current.UserId, now.UtcDateTime, cancellationToken);
            return null;
        }

        if (current.ExpiresAt <= now.UtcDateTime)
        {
            return null;
        }

        // Load the user fresh so a role change or a deleted account takes effect at the next refresh.
        var user = await users.FindByIdAsync(current.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var (secret, hash) = NewToken();
        if (!await tokens.TryRevokeAsync(presentedHash, now.UtcDateTime, hash, cancellationToken))
        {
            return null; // another request rotated this token a moment ago
        }

        // Sliding window: every use pushes the expiry out, so an active app stays signed in.
        var expiresAt = now.AddDays(options.Value.RefreshTokenDays);
        await tokens.CreateAsync(new RefreshToken
        {
            TokenHash = hash,
            UserId = user.UserId,
            CreatedAt = now.UtcDateTime,
            ExpiresAt = expiresAt.UtcDateTime,
        }, cancellationToken);

        return new RefreshedSession(user, new IssuedRefreshToken(secret, expiresAt));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken) =>
        await tokens.TryRevokeAsync(Hash(refreshToken), timeProvider.GetUtcNow().UtcDateTime, null, cancellationToken);

    internal static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static (string Secret, string Hash) NewToken()
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        return (secret, Hash(secret));
    }
}
