using Shared.Infraestructura.Mensajeria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pagos.Application.Abstracciones;
using Pagos.Domain;

namespace Pagos.Infrastructure.Persistencia;

/// <summary>Contexto de PagosDb: pagos y tablas de Outbox/Inbox.</summary>
public sealed class PagosDbContext(DbContextOptions<PagosDbContext> opciones)
    : DbContext(opciones), IUnidadTrabajo
{
    public const string NombreCadenaConexion = "PagosDb";

    public DbSet<Pago> Pagos => Set<Pago>();

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PagosDbContext).Assembly);
        modelBuilder.AgregarTablasMensajeria();
    }
}

internal sealed class ConfiguracionPago : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> builder)
    {
        builder.ToTable("Pagos");
        builder.HasKey(pago => pago.Id);

        builder.Property(pago => pago.Id)
            .HasColumnName("IdPago")
            .HasConversion(id => id.Valor, valor => new PagoId(valor))
            .ValueGeneratedNever();

        // Idempotencia respaldada por la base de datos: como máximo un pago por pedido.
        builder.Property(pago => pago.PedidoId).HasColumnName("IdPedido");
        builder.HasIndex(pago => pago.PedidoId).IsUnique().HasDatabaseName("UX_Pagos_IdPedido");

        builder.Property(pago => pago.ClienteId).HasColumnName("IdCliente");

        // Objeto de valor como complex type: se aplana en columnas de la propia tabla Pagos.
        builder.ComplexProperty(pago => pago.Importe, importe =>
        {
            importe.Property(dinero => dinero.Cantidad).HasColumnName("Importe").HasPrecision(18, 2);
            importe.Property(dinero => dinero.Moneda).HasColumnName("Moneda").HasMaxLength(3).IsFixedLength();
        });

        builder.Property(pago => pago.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(pago => pago.CodigoAutorizacion).HasMaxLength(64);
        builder.Property(pago => pago.Motivo).HasMaxLength(500);
    }
}

internal sealed class RepositorioPagos(PagosDbContext contexto) : IPagoRepositorio
{
    public Task<Pago?> ObtenerPorIdAsync(PagoId id, CancellationToken cancellationToken) =>
        contexto.Pagos.FirstOrDefaultAsync(pago => pago.Id == id, cancellationToken);

    public Task<Pago?> ObtenerPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken) =>
        contexto.Pagos.FirstOrDefaultAsync(pago => pago.PedidoId == pedidoId, cancellationToken);

    public void Agregar(Pago pago) => contexto.Pagos.Add(pago);
}
