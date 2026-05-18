using Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            // Iniciamos el desafío de autenticación indicando que queremos usar Google
            // Redirigimos a GoogleResponse cuando el usuario termine de autenticarse
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            // Recibimos los datos del usuario desde la cookie que generó la autenticación
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (!result.Succeeded || result.Principal == null)
            {
                return Redirect("/?error=auth_failed");
            }
                
            var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(googleId))
            {
                return Redirect("/?error=no_google_id");
            }

            // Redirigimos de vuelta al frontend (raíz del sitio)
            return Redirect("/");
        }

        [HttpGet("current-user")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (!result.Succeeded || result.Principal == null)
            {
                return Ok(new { IsAuthenticated = false });
            }

            var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var nombre = result.Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

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

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }
    }

    public class RegisterRequest
    {
        public string GoogleId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
