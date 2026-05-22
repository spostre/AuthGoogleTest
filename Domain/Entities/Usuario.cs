using System.Collections.Generic;

namespace Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? GoogleId { get; set; }
    public string? PasswordHash { get; set; }
    public string? PictureUrl { get; set; }

    public ICollection<Nota> Notas { get; set; } = new List<Nota>();
}
