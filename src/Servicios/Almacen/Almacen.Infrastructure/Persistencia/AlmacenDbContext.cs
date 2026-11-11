using Almacen.Application.Abstracciones;
using Almacen.Domain;
using Shared.Infraestructura.Mensajeria;
using Microsoft.EntityFrameworkCore;

namespace Almacen.Infrastructure.Persistencia;

/// <summary>Contexto de AlmacenDb: inventario, reservas y tablas de Outbox/Inbox.</summary>
public sealed class AlmacenDbContext(DbContextOptions<AlmacenDbContext> opciones)
    : DbContext(opciones), IUnidadTrabajo
{
    public const string NombreCadenaConexion = "AlmacenDb";

    public DbSet<Producto> Productos => Set<Producto>();

    public DbSet<ReservaStock> ReservasStock => Set<ReservaStock>();

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlmacenDbContext).Assembly);
        modelBuilder.AgregarTablasMensajeria();
    }
}
