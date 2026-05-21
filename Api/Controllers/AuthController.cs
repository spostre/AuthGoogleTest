using Api.Auth;
using Api.Services;
using Application.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Api.Controllers;

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
        var result = await HttpContext.AuthenticateAsync(AuthSchemes.External);

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
        var picture = await GetGooglePictureAsync(result);

        await HttpContext.SignOutAsync(AuthSchemes.External);

        var existingUser = await _userService.GetByGoogleIdAsync(googleId);
        if (existingUser != null)
        {
            var token = _jwtTokenService.GenerateForUser(existingUser, picture, "google");
            return Redirect($"/#token={token}");
        }

        var pendingToken = _jwtTokenService.GenerateForGoogleSession(googleId, nombre, email, picture);
        return Redirect($"/#token={pendingToken}");
    }

    [AllowAnonymous]
    [HttpPost("register-local")]
    public async Task<IActionResult> RegisterLocal([FromBody] LocalAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Nombre, correo y contraseña son requeridos.");
        }

        try
        {
            var user = await _userService.RegisterWithPasswordAsync(
                request.Nombre, request.Email, request.Password);

            var token = _jwtTokenService.GenerateForUser(user, authProvider: "local");
            return Ok(new { token, user = MapUser(user, true, "local") });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login-local")]
    public async Task<IActionResult> LoginLocal([FromBody] LocalLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Correo y contraseña son requeridos.");
        }

        var user = await _userService.LoginWithPasswordAsync(request.Email, request.Password);
        if (user == null)
        {
            return Unauthorized(new { message = "Correo o contraseña incorrectos." });
        }

        var token = _jwtTokenService.GenerateForUser(user, authProvider: "local");
        return Ok(new { token, user = MapUser(user, true, "local") });
    }

    [HttpGet("current-user")]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new { isAuthenticated = false });
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var nombre = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var pictureUrl = User.FindFirstValue("picture");
        var authProvider = User.FindFirstValue("auth_provider") ?? "google";
        var googleId = User.FindFirstValue("google_id") ?? subject;

        if (string.IsNullOrEmpty(subject))
        {
            return Ok(new { isAuthenticated = false });
        }

        var userId = await _userService.ResolveUserIdFromSubjectAsync(subject);
        if (userId != null)
        {
            var user = await _userService.GetByIdAsync(userId.Value);
            if (user != null)
            {
                return Ok(new
                {
                    isAuthenticated = true,
                    isRegistered = true,
                    userId = user.Id,
                    googleId = user.GoogleId ?? "",
                    nombre = user.Nombre,
                    email = user.Email,
                    pictureUrl,
                    authProvider = user.PasswordHash != null ? "local" : "google"
                });
            }
        }

        return Ok(new
        {
            isAuthenticated = true,
            isRegistered = false,
            googleId,
            nombre,
            email,
            pictureUrl,
            authProvider
        });
    }

    [Authorize]
    [HttpPost("register-google")]
    public async Task<IActionResult> RegisterGoogle([FromBody] GoogleRegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.GoogleId) ||
            string.IsNullOrEmpty(request.Nombre) ||
            string.IsNullOrEmpty(request.Email))
        {
            return BadRequest("GoogleId, Nombre y Email son requeridos.");
        }

        var authenticatedSubject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (authenticatedSubject != request.GoogleId)
        {
            return Forbid("No tienes permiso para registrar una cuenta a nombre de otra persona.");
        }

        try
        {
            var user = await _userService.RegisterWithGoogleAsync(
                request.GoogleId, request.Nombre, request.Email);

            var pictureUrl = User.FindFirstValue("picture");
            var token = _jwtTokenService.GenerateForUser(user, pictureUrl, "google");
            return Ok(new { token, user = MapUser(user, true, "google", pictureUrl) });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Sesión cerrada. Elimina el token del cliente." });
    }

    private static object MapUser(Usuario user, bool isRegistered, string authProvider, string? pictureUrl = null) =>
        new
        {
            isAuthenticated = true,
            isRegistered,
            userId = user.Id,
            googleId = user.GoogleId ?? "",
            nombre = user.Nombre,
            email = user.Email,
            pictureUrl,
            authProvider
        };

    private static async Task<string?> GetGooglePictureAsync(AuthenticateResult result)
    {
        var picture = result.Principal?.FindFirstValue("picture")
            ?? result.Principal?.FindFirstValue("urn:google:picture");

        if (!string.IsNullOrEmpty(picture))
        {
            return picture;
        }

        var accessToken = result.Properties?.GetTokenValue("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        try
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://www.googleapis.com/oauth2/v3/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var userInfo = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (userInfo.TryGetProperty("picture", out var pictureElement))
            {
                return pictureElement.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}

public class LocalAuthRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LocalLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class GoogleRegisterRequest
{
    public string GoogleId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
