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
public sealed class AuthController(
    IAccountService accounts,
    ITokenService tokens,
    IRefreshTokenService refreshTokens) : ControllerBase
{
    /// <summary>
    /// Signs in. Native apps pass <c>issueRefreshToken: true</c> to also receive a refresh token for
    /// <c>POST /api/auth/refresh</c>; the website does not and gets exactly the response it always did.
    /// </summary>
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
        var response = new LoginResponse(issued.Token, user.UserName, user.Role, issued.ExpiresAt);

        if (request.IssueRefreshToken)
        {
            var refresh = await refreshTokens.IssueAsync(user, cancellationToken);
            response = response with { RefreshToken = refresh.Token, RefreshTokenExpiresAt = refresh.ExpiresAt };
        }

        return Ok(response);
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token and a new refresh token. The presented token
    /// stops working; presenting it again signs the account out everywhere.
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingExtensions.RefreshPolicy)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var session = await refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);
        if (session is null)
        {
            return Problem("The refresh token is invalid or has expired. Sign in again.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var issued = tokens.CreateToken(session.User);
        return Ok(new LoginResponse(issued.Token, session.User.UserName, session.User.Role, issued.ExpiresAt)
        {
            RefreshToken = session.RefreshToken.Token,
            RefreshTokenExpiresAt = session.RefreshToken.ExpiresAt,
        });
    }

    /// <summary>Ends a native app session by revoking its refresh token. Always 204, known token or not.</summary>
    [HttpPost("logout")]
    [EnableRateLimiting(RateLimitingExtensions.RefreshPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await refreshTokens.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
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
