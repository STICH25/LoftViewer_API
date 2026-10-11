using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LoftViewer.Contracts;

/// <param name="IssueRefreshToken">
/// Set by native apps, which keep the refresh token in secure storage. The website leaves it off and
/// never receives one.
/// </param>
public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password,
    bool IssueRefreshToken = false);

public sealed record LoginResponse(string Token, string UserName, string Role, DateTimeOffset ExpiresAt)
{
    /// <summary>Present only when the client asked for one at login, or on a refresh.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? RefreshTokenExpiresAt { get; init; }
}

public sealed record RefreshRequest([Required] string RefreshToken);

public sealed record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string UserName,
    [Required, EmailAddress] string Email,
    [Required] string Password);
