using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LoftViewer.interfaces;
using LoftViewer.Models;
using LoftViewer.Utilities;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace LoftViewer.Services;

public class JwtAuthenticationService : IJwtAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly string _tokenFilePath = "app_jwt_token.json";
    private readonly int TokenExpirationMinutes;
    private string _secretKey;

    public JwtAuthenticationService(IConfiguration configuration)
    {
        _configuration = configuration;
        TokenExpirationMinutes = Convert.ToInt32(_configuration["JwtSettings:ExpirationMinutes"] ?? "60");
        
        // Load token from file
        LoadTokenFromFile();
    }

    private void LoadTokenFromFile()
    {
        if (File.Exists(_tokenFilePath))
        {
            try
            {
                var json = File.ReadAllText(_tokenFilePath);
                var tokenData = JsonConvert.DeserializeObject<TokenModel>(json);
                
                if (tokenData != null && !IsTokenExpired(tokenData.CreatedAt))
                {
                    _secretKey = tokenData.Token;
                    Console.WriteLine($"Loaded existing JWT Token: {_secretKey}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading token file: {ex.Message}");
            }
        }

        // If token is expired or doesn't exist, generate a new one
        _secretKey = KeyGenerator.GetSecret();
        SaveTokenToFile(_secretKey);
    }

    private bool IsTokenExpired(DateTime expiration)
    {
        return DateTime.UtcNow >= expiration;
    }

    private void SaveTokenToFile(string token)
    {
        var expiration = DateTime.UtcNow.AddMinutes(TokenExpirationMinutes);
        var tokenData = new TokenModel()
        {
            Token = token,
            CreatedAt = expiration
        };

        File.WriteAllText(_tokenFilePath, JsonConvert.SerializeObject(tokenData));
        Console.WriteLine("New JWT Token saved.");
    }

    public TokenValidationParameters GetTokenValidationParameters()
    {
        var key = Encoding.UTF8.GetBytes(_secretKey);
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidAudience = _configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    }

    public string GetUsernameFromToken(string authorizationHeader)
    {
        var token = authorizationHeader.Split(" ").Last();
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, GetTokenValidationParameters(), out _);

        return principal.Identity?.Name ?? string.Empty;
    }

    public string GetRoleFromToken(string authorizationHeader)
    {
        var token = authorizationHeader.Split(" ").Last();
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, GetTokenValidationParameters(), out _);

        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        return roleClaim?.Value ?? "User";
    }

    public string GenerateJwtToken(UserModel user)
    {
        if (IsTokenExpired(GetTokenExpirationFromFile()))
        {
            _secretKey = KeyGenerator.GetSecret();
            SaveTokenToFile(_secretKey);
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddMinutes(TokenExpirationMinutes),
            Issuer = _configuration["JwtSettings:Issuer"],
            Audience = _configuration["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private DateTime GetTokenExpirationFromFile()
    {
        if (File.Exists(_tokenFilePath))
        {
            try
            {
                var json = File.ReadAllText(_tokenFilePath);
                var tokenData = JsonConvert.DeserializeObject<TokenModel>(json);
                return tokenData?.CreatedAt ?? DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading token expiration: {ex.Message}");
            }
        }

        return DateTime.UtcNow;
    }
}


