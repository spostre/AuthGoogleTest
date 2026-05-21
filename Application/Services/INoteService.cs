using Domain.Entities;

namespace Application.Services;

public interface INoteService
{
    Task<Nota> CreateNoteAsync(int userId, string titulo, string contenido);
    Task<List<Nota>> GetNotesByUserIdAsync(int userId);
    Task<Nota?> UpdateNoteAsync(int noteId, int userId, string titulo, string contenido);
    Task<bool> DeleteNoteAsync(int noteId, int userId);
}
