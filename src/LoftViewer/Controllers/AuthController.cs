using LoftViewer.Contracts;
using LoftViewer.Extensions;
using LoftViewer.Services;
using LoftViewer.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LoftViewer.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public sealed class AuthController(IAccountService accounts, ITokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await accounts.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (user is null)
        {
            return Problem("Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var issued = tokens.CreateToken(user);
        return Ok(new LoginResponse(issued.Token, user.UserName, user.Role, issued.ExpiresAt));
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!CredentialRules.IsValidEmail(request.Email))
        {
            return Problem("Invalid email address.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!CredentialRules.IsValidPassword(request.Password))
        {
            return Problem(CredentialRules.PasswordRequirements, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await accounts.RegisterAsync(
            request.UserName.Trim().ToLowerInvariant(),
            request.Email.Trim().ToLowerInvariant(),
            request.Password,
            cancellationToken);

        return result == RegistrationResult.Created
            ? StatusCode(StatusCodes.Status201Created)
            : Problem("That user name or email is already registered.", statusCode: StatusCodes.Status409Conflict);
    }
}
