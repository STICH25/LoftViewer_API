using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LoftViewer.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "JwtSettings";
    public const int MinimumSecretBytes = 32;
    public const string RoleClaim = "role";

    /// <summary>HMAC signing key. Supply it through user-secrets or the JwtSettings__Secret environment variable.</summary>
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 24 * 60)]
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// How long a native app stays signed in without being opened. Each refresh restarts the clock, so
    /// anyone who uses the app at least this often never has to sign in again.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;

    public bool HasStrongSecret => Encoding.UTF8.GetByteCount(Secret) >= MinimumSecretBytes;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Secret));

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(),
        NameClaimType = JwtRegisteredClaimNames.Name,
        RoleClaimType = RoleClaim,
        ClockSkew = TimeSpan.FromMinutes(1),
    };
}
