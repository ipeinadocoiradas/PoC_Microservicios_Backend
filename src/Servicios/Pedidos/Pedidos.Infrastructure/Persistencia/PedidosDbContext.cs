using Shared.Infraestructura.Mensajeria;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Abstracciones;
using Pedidos.Application.Saga;
using Pedidos.Domain;

namespace Pedidos.Infrastructure.Persistencia;

/// <summary>
/// Contexto de PedidosDb. Contiene en el mismo modelo los pedidos, las instancias de la Saga y las tablas
/// del Outbox/Inbox: así un único <c>SaveChanges</c> confirma todo en una transacción local.
/// </summary>
public sealed class PedidosDbContext(DbContextOptions<PedidosDbContext> opciones)
    : DbContext(opciones), IUnidadTrabajo
{
    /// <summary>Clave de <c>ConnectionStrings</c> (coincide con el recurso de base de datos de Aspire).</summary>
    public const string NombreCadenaConexion = "PedidosDb";

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<EstadoSagaPedido> SagasPedido => Set<EstadoSagaPedido>();

    public DbSet<TransicionSagaPedido> HistorialSaga => Set<TransicionSagaPedido>();

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PedidosDbContext).Assembly);

        ISagaClassMap mapaSaga = new MapaEstadoSagaPedido();
        mapaSaga.Configure(modelBuilder);

        modelBuilder.AgregarTablasMensajeria();
    }
}
