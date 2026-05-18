using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Services;

public interface INoteService
{
    Task<Nota> CreateNoteAsync(string googleId, string titulo, string contenido);
    Task<List<Nota>> GetNotesByGoogleIdAsync(string googleId);
}
