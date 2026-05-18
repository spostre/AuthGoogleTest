using System.Collections.Generic;

namespace Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GoogleId { get; set; } = string.Empty; // Identificador único de Google

    // Relación: Un usuario puede tener muchas notas
    public ICollection<Nota> Notas { get; set; } = new List<Nota>();
}
