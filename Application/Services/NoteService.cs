using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class NoteService : INoteService
{
    private readonly IApplicationDbContext _context;

    public NoteService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Nota> CreateNoteAsync(int userId, string titulo, string contenido)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == userId);
        if (usuario == null)
        {
            throw new UnauthorizedAccessException("El usuario no está registrado en el sistema.");
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

    public async Task<List<Nota>> GetNotesByUserIdAsync(int userId)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == userId);
        if (usuario == null)
        {
            throw new UnauthorizedAccessException("El usuario no está registrado en el sistema.");
        }

        return await _context.Notas
            .Where(n => n.UsuarioId == usuario.Id)
            .OrderByDescending(n => n.FechaCreacion)
            .ToListAsync();
    }

    public async Task<Nota?> UpdateNoteAsync(int noteId, int userId, string titulo, string contenido)
    {
        var nota = await _context.Notas
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UsuarioId == userId);

        if (nota == null)
        {
            return null;
        }

        nota.Titulo = titulo;
        nota.Contenido = contenido;
        await _context.SaveChangesAsync();

        return nota;
    }

    public async Task<bool> DeleteNoteAsync(int noteId, int userId)
    {
        var nota = await _context.Notas
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UsuarioId == userId);

        if (nota == null)
        {
            return false;
        }

        _context.Notas.Remove(nota);
        await _context.SaveChangesAsync();

        return true;
    }
}
