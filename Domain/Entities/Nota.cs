using System;

namespace Domain.Entities;

public class Nota
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Relación: Una nota pertenece a un usuario
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
