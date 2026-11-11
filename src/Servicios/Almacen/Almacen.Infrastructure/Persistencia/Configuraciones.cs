using Almacen.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Almacen.Infrastructure.Persistencia;

internal sealed class ConfiguracionProducto : IEntityTypeConfiguration<Producto>
{
    /// <summary>Token de concurrencia en propiedad sombra: el dominio no necesita conocerlo.</summary>
    public const string ColumnaVersion = "Version";

    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        // La restricción CHECK es la última defensa contra la sobreventa, incluso ante un bug en el código.
        builder.ToTable("Productos", tabla => tabla.HasCheckConstraint(
            "CK_Productos_Stock",
            "[StockReservado] >= 0 AND [StockReservado] <= [StockFisico]"));

        builder.HasKey(producto => producto.Id);
        builder.Property(producto => producto.Id)
            .HasColumnName("IdProducto")
            .HasConversion(id => id.Valor, valor => new ProductoId(valor))
            .ValueGeneratedNever();

        builder.Property(producto => producto.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(producto => producto.StockFisico);
        builder.Property(producto => producto.StockReservado);
        builder.Ignore(producto => producto.StockDisponible);

        builder.Property<byte[]>(ColumnaVersion).IsRowVersion();
    }
}

internal sealed class ConfiguracionReservaStock : IEntityTypeConfiguration<ReservaStock>
{
    public void Configure(EntityTypeBuilder<ReservaStock> builder)
    {
        builder.ToTable("ReservasStock");
        builder.HasKey(reserva => reserva.Id);

        builder.Property(reserva => reserva.Id)
            .HasColumnName("IdReserva")
            .HasConversion(id => id.Valor, valor => new ReservaId(valor))
            .ValueGeneratedNever();

        // Idempotencia respaldada por la base de datos: como máximo una reserva por pedido.
        builder.Property(reserva => reserva.PedidoId).HasColumnName("IdPedido");
        builder.HasIndex(reserva => reserva.PedidoId).IsUnique().HasDatabaseName("UX_ReservasStock_IdPedido");

        builder.Property(reserva => reserva.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(reserva => reserva.MotivoRechazo).HasMaxLength(500);

        builder.OwnsMany(reserva => reserva.Lineas, lineas =>
        {
            lineas.ToTable("LineasReservaStock");
            lineas.Property<ReservaId>("IdReserva").HasConversion(id => id.Valor, valor => new ReservaId(valor));
            lineas.WithOwner().HasForeignKey("IdReserva");
            lineas.Property<int>("IdLinea").ValueGeneratedOnAdd();
            lineas.HasKey("IdLinea");
            lineas.Property(linea => linea.ProductoId)
                .HasColumnName("IdProducto")
                .HasConversion(id => id.Valor, valor => new ProductoId(valor));
            lineas.Property(linea => linea.Cantidad);
        });

        builder.Navigation(reserva => reserva.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
