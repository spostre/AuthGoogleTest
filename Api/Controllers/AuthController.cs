using Api.Services;
using Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(IUserService userService, JwtTokenService jwtTokenService)
        {
            _userService = userService;
            _jwtTokenService = jwtTokenService;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!result.Succeeded || result.Principal == null)
            {
                return Redirect("/?error=auth_failed");
            }

            var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(googleId))
            {
                return Redirect("/?error=no_google_id");
            }

            var nombre = result.Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            var token = _jwtTokenService.GenerateToken(googleId, nombre, email);

            return Redirect($"/#token={token}");
        }

        [HttpGet("current-user")]
        public async Task<IActionResult> GetCurrentUser()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Ok(new { IsAuthenticated = false });
            }

            var googleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var nombre = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            if (string.IsNullOrEmpty(googleId))
            {
                return Ok(new { IsAuthenticated = false });
            }

            var usuarioExistente = await _userService.GetByGoogleIdAsync(googleId);

            return Ok(new
            {
                IsAuthenticated = true,
                IsRegistered = usuarioExistente != null,
                GoogleId = googleId,
                Nombre = nombre,
                Email = email
            });
        }

        [Authorize]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.GoogleId) || string.IsNullOrEmpty(request.Nombre) || string.IsNullOrEmpty(request.Email))
            {
                return BadRequest("Todos los campos (GoogleId, Nombre, Email) son requeridos.");
            }

            var authenticatedGoogleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (authenticatedGoogleId != request.GoogleId)
            {
                return Forbid("No tienes permiso para registrar una cuenta a nombre de otra persona.");
            }

            var usuario = await _userService.RegisterAsync(request.GoogleId, request.Nombre, request.Email);
            return Ok(usuario);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { Message = "Sesión cerrada. Elimina el token del cliente." });
        }
    }

    public class RegisterRequest
    {
        public string GoogleId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
