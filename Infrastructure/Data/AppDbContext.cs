using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Nota> Notas => Set<Nota>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de la entidad Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Nombre).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.Property(u => u.GoogleId).HasMaxLength(100);
            entity.Property(u => u.PasswordHash).HasMaxLength(500);
            entity.HasIndex(u => u.Email).IsUnique();
            
            // Un usuario tiene muchas notas
            entity.HasMany(u => u.Notas)
                  .WithOne(n => n.Usuario)
                  .HasForeignKey(n => n.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de la entidad Nota
        modelBuilder.Entity<Nota>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Titulo).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Contenido).IsRequired();
            entity.Property(n => n.FechaCreacion).IsRequired();
        });
    }
}
