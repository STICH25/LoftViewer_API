using LoftViewer.Models;

namespace LoftViewer.Data;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the token revoked only if it is still active. Returns true for exactly one caller, so two
    /// requests presenting the same token at once cannot both be granted a new one.
    /// </summary>
    Task<bool> TryRevokeAsync(string tokenHash, DateTime revokedAt, string? replacedByHash, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken);
}
