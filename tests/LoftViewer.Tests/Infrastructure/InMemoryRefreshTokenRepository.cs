using System.Collections.Concurrent;
using LoftViewer.Data;
using LoftViewer.Models;

namespace LoftViewer.Tests.Infrastructure;

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly Lock _gate = new();

    public ConcurrentDictionary<string, RefreshToken> Tokens { get; } = new();

    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        Tokens[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.GetValueOrDefault(tokenHash));

    public Task<bool> TryRevokeAsync(
        string tokenHash, DateTime revokedAt, string? replacedByHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!Tokens.TryGetValue(tokenHash, out var token) || token.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            token.RevokedAt = revokedAt;
            token.ReplacedByHash = replacedByHash;
            return Task.FromResult(true);
        }
    }

    public Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var token in Tokens.Values.Where(t => t.UserId == userId && t.RevokedAt is null))
            {
                token.RevokedAt = revokedAt;
            }
        }

        return Task.CompletedTask;
    }
}
