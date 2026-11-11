using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pedidos.Application.Saga;
using Pedidos.Domain;

namespace Pedidos.Infrastructure.Persistencia;

/// <summary>Mapeo del agregado Pedido a las tablas <c>Pedidos</c> y <c>LineasPedido</c>.</summary>
internal sealed class ConfiguracionPedido : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("Pedidos");
        builder.HasKey(pedido => pedido.Id);

        builder.Property(pedido => pedido.Id)
            .HasColumnName("IdPedido")
            .HasConversion(id => id.Valor, valor => new PedidoId(valor))
            .ValueGeneratedNever();

        builder.Property(pedido => pedido.ClienteId).HasColumnName("IdCliente");
        builder.Property(pedido => pedido.Moneda).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(pedido => pedido.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(pedido => pedido.MotivoFinalizacion).HasMaxLength(500);
        builder.Property(pedido => pedido.FechaCreacionUtc);
        builder.Property(pedido => pedido.FechaFinalizacionUtc);

        builder.Ignore(pedido => pedido.Total);
        builder.Ignore(pedido => pedido.EstaFinalizado);

        builder.OwnsMany(pedido => pedido.Lineas, lineas =>
        {
            lineas.ToTable("LineasPedido");
            // Clave foránea sombra con el mismo conversor que la clave del propietario.
            lineas.Property<PedidoId>("IdPedido").HasConversion(id => id.Valor, valor => new PedidoId(valor));
            lineas.WithOwner().HasForeignKey("IdPedido");
            lineas.Property<int>("IdLinea").ValueGeneratedOnAdd();
            lineas.HasKey("IdLinea");
            lineas.Property(linea => linea.ProductoId).HasColumnName("IdProducto");
            lineas.Property(linea => linea.Cantidad);
            lineas.Property(linea => linea.PrecioUnitario).HasPrecision(18, 2);
            lineas.Ignore(linea => linea.Subtotal);
        });

        builder.Navigation(pedido => pedido.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(pedido => pedido.Estado);
    }
}

/// <summary>
/// Mapeo de la instancia de la Saga a la tabla <c>SagasPedido</c>.
/// La columna <c>Version</c> (rowversion) habilita la concurrencia optimista del repositorio de MassTransit.
/// </summary>
internal sealed class MapaEstadoSagaPedido : SagaClassMap<EstadoSagaPedido>
{
    protected override void Configure(EntityTypeBuilder<EstadoSagaPedido> entidad, ModelBuilder modelo)
    {
        entidad.ToTable("SagasPedido");

        entidad.Property(saga => saga.CorrelationId).HasColumnName("IdCorrelacion");
        entidad.Property(saga => saga.EstadoActual).HasMaxLength(64).IsRequired();
        entidad.Property(saga => saga.ClienteId).HasColumnName("IdCliente");
        entidad.Property(saga => saga.Importe).HasPrecision(18, 2);
        entidad.Property(saga => saga.Moneda).HasMaxLength(3).IsFixedLength();
        entidad.Property(saga => saga.ReservaId).HasColumnName("IdReserva");
        entidad.Property(saga => saga.PagoId).HasColumnName("IdPago");
        entidad.Property(saga => saga.MotivoFinalizacion).HasMaxLength(500);
        entidad.Property(saga => saga.UltimoError).HasMaxLength(2000);
        entidad.Property(saga => saga.Version).IsRowVersion();

        entidad.Ignore(saga => saga.CompensacionFinalizada);
        entidad.HasIndex(saga => saga.EstadoActual);
    }
}

/// <summary>Mapeo del historial de la Saga a la tabla <c>HistorialSaga</c>.</summary>
internal sealed class ConfiguracionTransicionSagaPedido : IEntityTypeConfiguration<TransicionSagaPedido>
{
    public void Configure(EntityTypeBuilder<TransicionSagaPedido> builder)
    {
        builder.ToTable("HistorialSaga");

        builder.HasKey(transicion => transicion.Id);
        builder.Property(transicion => transicion.Id).HasColumnName("IdTransicion").ValueGeneratedOnAdd();

        builder.Property(transicion => transicion.CorrelationId).HasColumnName("IdCorrelacion");
        builder.Property(transicion => transicion.Secuencia);
        builder.Property(transicion => transicion.EstadoAnterior).HasMaxLength(64);
        builder.Property(transicion => transicion.EstadoNuevo).HasMaxLength(64).IsRequired();
        builder.Property(transicion => transicion.Evento).HasMaxLength(128).IsRequired();
        builder.Property(transicion => transicion.FechaUtc);

        // Una transición por posición: protege frente a un registro duplicado del mismo paso.
        builder.HasIndex(transicion => new { transicion.CorrelationId, transicion.Secuencia })
            .IsUnique()
            .HasDatabaseName("UX_HistorialSaga_IdCorrelacion_Secuencia");
    }
}
