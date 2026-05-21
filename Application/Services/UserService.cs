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
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
    }

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

    public async Task<Usuario> RegisterWithGoogleAsync(string googleId, string nombre, string email)
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
            Email = email.Trim().ToLowerInvariant()
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
            Email = email.Trim().ToLowerInvariant(),
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
}
