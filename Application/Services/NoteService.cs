using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Services;

public class NoteService : INoteService
{
    private readonly IApplicationDbContext _context;

    public NoteService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Nota> CreateNoteAsync(string googleId, string titulo, string contenido)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);

        if (usuario == null)
        {
            throw new System.UnauthorizedAccessException("El usuario no está registrado en el sistema.");
        }

        var nuevaNota = new Nota
        {
            Titulo = titulo,
            Contenido = contenido,
            UsuarioId = usuario.Id
        };

        _context.Notas.Add(nuevaNota);
        await _context.SaveChangesAsync();

        return nuevaNota;
    }

    public async Task<List<Nota>> GetNotesByGoogleIdAsync(string googleId)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);

        if (usuario == null)
        {
            throw new System.UnauthorizedAccessException("El usuario no está registrado en el sistema.");
        }

        return await _context.Notas
            .Where(n => n.UsuarioId == usuario.Id)
            .OrderByDescending(n => n.FechaCreacion)
            .ToListAsync();
    }
}
