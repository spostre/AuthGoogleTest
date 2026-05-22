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
    private static readonly string[] AllowedPictureTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    private readonly IUserService _userService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        IUserService userService,
        JwtTokenService jwtTokenService,
        IWebHostEnvironment environment)
    {
        _userService = userService;
        _jwtTokenService = jwtTokenService;
        _environment = environment;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Vincula Google a la cuenta actual (JWT en query o usuario autenticado).
    /// </summary>
    [HttpGet("link-google")]
    public async Task<IActionResult> LinkGoogle([FromQuery] string? token)
    {
        var userId = _jwtTokenService.TryGetUserIdFromToken(token);
        if (userId == null && User.Identity?.IsAuthenticated == true)
        {
            userId = await GetCurrentUserIdAsync();
        }

        if (userId == null)
        {
            return Redirect("/?error=link_requires_login");
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return Redirect("/?error=link_requires_login");
        }

        if (!string.IsNullOrEmpty(user.GoogleId))
        {
            return Redirect("/?error=google_already_linked");
        }

        var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
        properties.Items["link_user_id"] = userId.Value.ToString();
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

        if (string.IsNullOrWhiteSpace(email))
        {
            return Redirect("/?error=no_email");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = email.Split('@')[0];
        }

        if (result.Properties?.Items.TryGetValue("link_user_id", out var linkUserIdStr) == true
            && int.TryParse(linkUserIdStr, out var linkUserId))
        {
            try
            {
                var linkedUser = await _userService.LinkGoogleAccountAsync(
                    linkUserId, googleId, email, picture);
                var linkToken = _jwtTokenService.GenerateForUser(
                    linkedUser,
                    authProvider: ResolveAuthProvider(linkedUser));
                return Redirect(BuildTokenRedirect(linkToken, linked: true));
            }
            catch (InvalidOperationException)
            {
                return Redirect("/?error=google_link_failed");
            }
        }

        var existingUser = await _userService.ResolveGoogleUserAsync(googleId, email);
        if (existingUser != null)
        {
            var user = await _userService.ApplyGooglePictureIfEmptyAsync(existingUser.Id, picture);
            var token = _jwtTokenService.GenerateForUser(user, authProvider: ResolveAuthProvider(user));
            return Redirect(BuildTokenRedirect(token));
        }

        try
        {
            var newUser = await _userService.RegisterWithGoogleAsync(googleId, nombre, email, picture);
            var token = _jwtTokenService.GenerateForUser(
                newUser,
                authProvider: ResolveAuthProvider(newUser));
            return Redirect(BuildTokenRedirect(token));
        }
        catch (InvalidOperationException)
        {
            return Redirect("/?error=email_already_registered");
        }
    }

    private static string BuildTokenRedirect(string token, bool linked = false)
    {
        var fragment = $"token={Uri.EscapeDataString(token)}";
        if (linked)
        {
            fragment += "&linked=google";
        }

        return $"/#{fragment}";
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
            return Ok(new { token, user = MapUser(user, true) });
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
        return Ok(new { token, user = MapUser(user, true) });
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
        var authProvider = User.FindFirstValue("auth_provider") ?? "google";
        var googleId = User.FindFirstValue("google_id") ?? subject;

        if (string.IsNullOrEmpty(subject))
        {
            return Ok(new { isAuthenticated = false });
        }

        var userId = await _userService.ResolveUserIdFromSubjectAsync(subject);
        if (userId != null)
        {
            var user = await GetAuthenticatedUsuarioAsync(userId.Value);
            if (user != null)
            {
                return Ok(MapAuthenticatedUser(user));
            }
        }

        return Ok(new
        {
            isAuthenticated = true,
            isRegistered = false,
            googleId,
            nombre,
            email,
            pictureUrl = User.FindFirstValue("picture") ?? "",
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
            var pictureUrl = User.FindFirstValue("picture");
            var user = await _userService.RegisterWithGoogleAsync(
                request.GoogleId,
                request.Nombre,
                request.Email,
                pictureUrl);

            if (!string.IsNullOrEmpty(pictureUrl))
            {
                user = await _userService.ApplyGooglePictureIfEmptyAsync(user.Id, pictureUrl);
            }

            var token = _jwtTokenService.GenerateForUser(user, authProvider: ResolveAuthProvider(user));
            return Ok(new { token, user = MapUser(user, true) });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("account-security")]
    public async Task<IActionResult> GetAccountSecurity()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await GetAuthenticatedUsuarioAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(MapSecurityProfile(user));
    }

    [Authorize]
    [HttpPut("profile-picture")]
    public async Task<IActionResult> UpdateProfilePicture([FromBody] UpdateProfilePictureRequest request)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.PictureUrl))
        {
            return BadRequest("La URL de la imagen es requerida.");
        }

        if (!Uri.TryCreate(request.PictureUrl.Trim(), UriKind.Absolute, out _))
        {
            return BadRequest("La URL de la imagen no es válida.");
        }

        var user = await _userService.UpdatePictureUrlAsync(userId.Value, request.PictureUrl);
        return Ok(BuildProfilePictureResponse(user, "Foto de perfil actualizada."));
    }

    [Authorize]
    [HttpPost("profile-picture/upload")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (file.Length == 0)
        {
            return BadRequest("Selecciona una imagen.");
        }

        if (file.Length > 2 * 1024 * 1024)
        {
            return BadRequest("La imagen no puede superar 2 MB.");
        }

        if (!AllowedPictureTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Formato no permitido. Usa JPG, PNG, WEBP o GIF.");
        }

        var extension = file.ContentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        var avatarsDir = AvatarStorage.GetDirectory(_environment.ContentRootPath);
        Directory.CreateDirectory(avatarsDir);

        var fileName = $"{userId.Value}{extension}";
        var filePath = Path.Combine(avatarsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var publicUrl = $"{AvatarStorage.PublicUrlPrefix}/{fileName}";
        var user = await _userService.UpdatePictureUrlAsync(userId.Value, publicUrl);
        return Ok(BuildProfilePictureResponse(user, "Foto de perfil guardada."));
    }

    [Authorize]
    [HttpDelete("profile-picture")]
    public async Task<IActionResult> RemoveProfilePicture()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        var avatarsDir = AvatarStorage.GetDirectory(_environment.ContentRootPath);
        foreach (var extension in new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" })
        {
            var path = Path.Combine(avatarsDir, $"{userId.Value}{extension}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        var user = await _userService.UpdatePictureUrlAsync(userId.Value, null);
        return Ok(BuildProfilePictureResponse(user, "Foto de perfil eliminada."));
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("La nueva contraseña es requerida.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest("La confirmación de contraseña no coincide.");
        }

        try
        {
            await _userService.UpdatePasswordAsync(
                userId.Value,
                request.NewPassword,
                request.CurrentPassword);

            var user = await GetAuthenticatedUsuarioAsync(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            var token = _jwtTokenService.GenerateForUser(user, authProvider: ResolveAuthProvider(user));

            return Ok(new
            {
                message = string.IsNullOrEmpty(request.CurrentPassword)
                    ? "Contraseña configurada. Ya puedes iniciar sesión con tu correo."
                    : "Contraseña actualizada.",
                token,
                user = MapAuthenticatedUser(user)
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Sesión cerrada. Elimina el token del cliente." });
    }

    private async Task<int?> GetCurrentUserIdAsync()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        return await _userService.ResolveUserIdFromSubjectAsync(subject);
    }

    private async Task<Usuario?> GetAuthenticatedUsuarioAsync(int userId)
    {
        var user = await _userService.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var googleIdClaim = User.FindFirstValue("google_id");
        if (!string.IsNullOrEmpty(googleIdClaim) && string.IsNullOrEmpty(user.GoogleId))
        {
            try
            {
                user = await _userService.EnsureGoogleIdLinkedAsync(userId, googleIdClaim);
            }
            catch (InvalidOperationException)
            {
                return user;
            }
        }

        var pictureClaim = User.FindFirstValue("picture");
        if (!string.IsNullOrEmpty(pictureClaim) && string.IsNullOrEmpty(user.PictureUrl))
        {
            user = await _userService.ApplyGooglePictureIfEmptyAsync(userId, pictureClaim);
        }

        return user;
    }

    private static string ResolveAuthProvider(Usuario user)
    {
        var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
        var hasGoogle = !string.IsNullOrEmpty(user.GoogleId);

        if (hasPassword && hasGoogle)
        {
            return "both";
        }

        if (hasGoogle)
        {
            return "google";
        }

        return "local";
    }

    private object BuildProfilePictureResponse(Usuario user, string message) =>
        new
        {
            message,
            token = _jwtTokenService.GenerateForUser(user, authProvider: ResolveAuthProvider(user)),
            user = MapAuthenticatedUser(user)
        };

    private static object MapSecurityProfile(Usuario user) =>
        new
        {
            email = user.Email,
            nombre = user.Nombre,
            googleId = user.GoogleId ?? "",
            pictureUrl = user.PictureUrl ?? "",
            hasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            hasGoogle = !string.IsNullOrEmpty(user.GoogleId)
        };

    private static object MapAuthenticatedUser(Usuario user) =>
        new
        {
            isAuthenticated = true,
            isRegistered = true,
            userId = user.Id,
            googleId = user.GoogleId ?? "",
            nombre = user.Nombre,
            email = user.Email,
            pictureUrl = user.PictureUrl ?? "",
            hasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            hasGoogle = !string.IsNullOrEmpty(user.GoogleId),
            authProvider = ResolveAuthProvider(user)
        };

    private object MapUser(Usuario user, bool isRegistered) =>
        MapAuthenticatedUser(user);

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

public class UpdatePasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UpdateProfilePictureRequest
{
    public string PictureUrl { get; set; } = string.Empty;
}
