using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Usuario?> GetByIdAsync(int id)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<Usuario?> GetByEmailAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    public async Task<Usuario?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }

    public async Task<int?> ResolveUserIdFromSubjectAsync(string subject)
    {
        if (int.TryParse(subject, out var userId))
        {
            var byId = await GetByIdAsync(userId);
            return byId?.Id;
        }

        var byGoogle = await GetByGoogleIdAsync(subject);
        return byGoogle?.Id;
    }

    public async Task<Usuario> RegisterWithGoogleAsync(
        string googleId,
        string nombre,
        string email,
        string? pictureUrl = null)
    {
        var existingUser = await GetByGoogleIdAsync(googleId);
        if (existingUser != null)
        {
            return existingUser;
        }

        var existingEmail = await GetByEmailAsync(email);
        if (existingEmail != null)
        {
            throw new InvalidOperationException("Ya existe una cuenta con ese correo electrónico.");
        }

        var newUser = new Usuario
        {
            GoogleId = googleId,
            Nombre = nombre.Trim(),
            Email = NormalizeEmail(email),
            PictureUrl = NormalizePictureUrl(pictureUrl)
        };

        _context.Usuarios.Add(newUser);
        await _context.SaveChangesAsync();

        return newUser;
    }

    public async Task<Usuario> RegisterWithPasswordAsync(string nombre, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        }

        var existingEmail = await GetByEmailAsync(email);
        if (existingEmail != null)
        {
            throw new InvalidOperationException("Ya existe una cuenta con ese correo electrónico.");
        }

        var newUser = new Usuario
        {
            Nombre = nombre.Trim(),
            Email = NormalizeEmail(email),
            PasswordHash = _passwordHasher.Hash(password)
        };

        _context.Usuarios.Add(newUser);
        await _context.SaveChangesAsync();

        return newUser;
    }

    public async Task<Usuario?> LoginWithPasswordAsync(string email, string password)
    {
        var user = await GetByEmailAsync(email);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return null;
        }

        return _passwordHasher.Verify(user.PasswordHash, password) ? user : null;
    }

    public async Task UpdatePasswordAsync(int userId, string newPassword, string? currentPassword = null)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        }

        var user = await GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                throw new ArgumentException("La contraseña actual es requerida.");
            }

            if (!_passwordHasher.Verify(user.PasswordHash, currentPassword))
            {
                throw new UnauthorizedAccessException("La contraseña actual no es correcta.");
            }
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await _context.SaveChangesAsync();
    }

    public async Task<Usuario> EnsureGoogleIdLinkedAsync(int userId, string googleId)
    {
        if (string.IsNullOrWhiteSpace(googleId))
        {
            throw new ArgumentException("El identificador de Google es requerido.");
        }

        var user = await GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (!string.IsNullOrEmpty(user.GoogleId))
        {
            return user;
        }

        var other = await GetByGoogleIdAsync(googleId);
        if (other != null && other.Id != userId)
        {
            throw new InvalidOperationException(
                "Ese identificador de Google ya está vinculado a otra cuenta.");
        }

        user.GoogleId = googleId.Trim();
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<Usuario> LinkGoogleAccountAsync(
        int userId,
        string googleId,
        string googleEmail,
        string? pictureUrl = null)
    {
        var user = await GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (!string.IsNullOrEmpty(user.GoogleId))
        {
            if (user.GoogleId == googleId.Trim())
            {
                return user;
            }

            throw new InvalidOperationException("Tu cuenta ya tiene Google vinculado.");
        }

        if (NormalizeEmail(user.Email) != NormalizeEmail(googleEmail))
        {
            throw new InvalidOperationException(
                "El correo de Google debe ser el mismo que el de tu cuenta.");
        }

        user = await EnsureGoogleIdLinkedAsync(userId, googleId);
        return await ApplyGooglePictureIfEmptyAsync(userId, pictureUrl);
    }

    public async Task<Usuario?> ResolveGoogleUserAsync(string googleId, string email)
    {
        var byGoogle = await GetByGoogleIdAsync(googleId);
        if (byGoogle != null)
        {
            return byGoogle;
        }

        var byEmail = await GetByEmailAsync(email);
        if (byEmail == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(byEmail.GoogleId))
        {
            return await EnsureGoogleIdLinkedAsync(byEmail.Id, googleId);
        }

        return byEmail.GoogleId == googleId ? byEmail : null;
    }

    public async Task<Usuario> UpdatePictureUrlAsync(int userId, string? pictureUrl)
    {
        var user = await GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        user.PictureUrl = NormalizePictureUrl(pictureUrl);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<Usuario> ApplyGooglePictureIfEmptyAsync(int userId, string? pictureUrl)
    {
        var user = await GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (string.IsNullOrWhiteSpace(pictureUrl) || !string.IsNullOrEmpty(user.PictureUrl))
        {
            return user;
        }

        user.PictureUrl = NormalizePictureUrl(pictureUrl);
        await _context.SaveChangesAsync();
        return user;
    }

    private static string? NormalizePictureUrl(string? pictureUrl)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl))
        {
            return null;
        }

        var trimmed = pictureUrl.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
