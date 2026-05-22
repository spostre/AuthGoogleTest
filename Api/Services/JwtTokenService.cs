using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateForUser(Usuario user, string? pictureUrlOverride = null, string authProvider = "local")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Nombre),
            new(ClaimTypes.Email, user.Email),
            new("auth_provider", authProvider)
        };

        if (!string.IsNullOrEmpty(user.GoogleId))
        {
            claims.Add(new Claim("google_id", user.GoogleId));
        }

        var pictureUrl = pictureUrlOverride ?? user.PictureUrl;
        if (!string.IsNullOrEmpty(pictureUrl))
        {
            claims.Add(new Claim("picture", pictureUrl));
        }

        return WriteToken(claims);
    }

    public string GenerateForGoogleSession(string googleId, string name, string email, string? pictureUrl = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, googleId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new("auth_provider", "google")
        };

        if (!string.IsNullOrEmpty(pictureUrl))
        {
            claims.Add(new Claim("picture", pictureUrl));
        }

        return WriteToken(claims);
    }

    public int? TryGetUserIdFromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                token,
                BuildValidationParameters(),
                out _);

            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(subject, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    private TokenValidationParameters BuildValidationParameters() =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!))
        };

    private string WriteToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
