using Microsoft.AspNetCore.Mvc;
using LoftViewer.Models;
using LoftViewer.Services;
using LoftViewer.interfaces;
using LoftViewer.Utilities;

namespace LoftViewer.Controllers
{
    [Route("api/auth"), ApiController]
    public class UserAuthController : ControllerBase
    {
        private readonly DbServices _dbServices;
        private readonly IConfiguration _configuration;
        private readonly IJwtAuthenticationService _jwtAuthService;
        private readonly EmailAndPasswordValidation _emailAndPasswordValidation;

        public UserAuthController(DbServices dbServices, IConfiguration configuration, IJwtAuthenticationService jwtAuthService)
        {
            _dbServices = dbServices;
            _configuration = configuration;
            _jwtAuthService = jwtAuthService;
        }

        // Login action (validate credentials and issue token)
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _dbServices.AuthenticateUserAsync(request.Username, request.Password);
            if (user == null || !user.VerifyPassword(request.Password))
            {
                return Unauthorized("Invalid username or password.");
            }

            var token = _jwtAuthService.GenerateJwtToken(user);
            return Ok(new { Token = token });
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> RegisterNewUser([FromBody] AddNewUserModel request)
        {
            EmailAndPasswordValidation _emailAndPasswordValidation = new EmailAndPasswordValidation();
            bool isRegistered = false;
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            if (_emailAndPasswordValidation.IsValidEmail(request.Email) &&
                _emailAndPasswordValidation.IsValidPassword(request.Password))
            {
                isRegistered = await _dbServices.RegisterUserAsync(request.UserName.ToLower(), request.Email.ToLower(), request.Password);
            }
            else
            {
                return BadRequest("Invalid email or password.");
            }
            
            if (!isRegistered)
            {
                return Conflict("Email already in use.");
            }

            return Ok("User registered successfully.");
        }
    }
}
