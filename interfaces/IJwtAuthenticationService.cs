using LoftViewer.Models;
using Microsoft.IdentityModel.Tokens;

namespace LoftViewer.interfaces;

public interface IJwtAuthenticationService
{
    string GenerateJwtToken(UserModel user);
    
    // Provides JWT validation parameters for authentication
    TokenValidationParameters GetTokenValidationParameters();
    
    // Extract username from token
    string GetUsernameFromToken(string authorizationHeader);
    
    // Extract role from token
    string GetRoleFromToken(string authorizationHeader);
}