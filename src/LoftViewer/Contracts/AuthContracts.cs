using System.ComponentModel.DataAnnotations;

namespace LoftViewer.Contracts;

public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public sealed record LoginResponse(string Token, string UserName, string Role, DateTimeOffset ExpiresAt);

public sealed record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string UserName,
    [Required, EmailAddress] string Email,
    [Required] string Password);
