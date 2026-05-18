using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Application.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _context;

    public UserService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }

    public async Task<Usuario> RegisterAsync(string googleId, string nombre, string email)
    {
        var existingUser = await GetByGoogleIdAsync(googleId);
        if (existingUser != null)
        {
            return existingUser;
        }

        var newUser = new Usuario
        {
            GoogleId = googleId,
            Nombre = nombre,
            Email = email
        };

        _context.Usuarios.Add(newUser);
        await _context.SaveChangesAsync();

        return newUser;
    }
}
