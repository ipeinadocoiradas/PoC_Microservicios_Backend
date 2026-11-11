namespace Almacen.Domain;

/// <summary>
/// Producto del inventario. Distingue entre stock físico (unidades en el almacén) y stock reservado
/// (unidades comprometidas con pedidos aún no confirmados).
/// </summary>
/// <remarks>
/// Invariante: <c>0 ≤ StockReservado ≤ StockFisico</c>. Se protege aquí y, además, con una restricción
/// CHECK en la base de datos y un token de concurrencia que evita la sobreventa con peticiones simultáneas.
/// </remarks>
public sealed class Producto
{
    private Producto()
    {
        // Requerido por EF Core.
    }

    private Producto(ProductoId id, string nombre, int stockFisico)
    {
        Id = id;
        Nombre = nombre;
        StockFisico = stockFisico;
    }

    public ProductoId Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public int StockFisico { get; private set; }

    public int StockReservado { get; private set; }

    public int StockDisponible => StockFisico - StockReservado;

    public static Producto Crear(ProductoId id, string nombre, int stockInicial)
    {
        if (id.Valor == Guid.Empty)
        {
            throw new ExcepcionDominio("El producto necesita un identificador.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ExcepcionDominio("El producto necesita un nombre.");
        }

        if (stockInicial < 0)
        {
            throw new ExcepcionDominio("El stock inicial no puede ser negativo.");
        }

        return new Producto(id, nombre.Trim(), stockInicial);
    }

    public bool PuedeReservar(int cantidad) => cantidad > 0 && StockDisponible >= cantidad;

    /// <summary>Compromete unidades disponibles para un pedido.</summary>
    public void Reservar(int cantidad)
    {
        ValidarCantidad(cantidad);

        if (!PuedeReservar(cantidad))
        {
            throw new ExcepcionDominio($"Stock insuficiente para '{Nombre}': disponibles {StockDisponible}, solicitadas {cantidad}.");
        }

        StockReservado += cantidad;
    }

    /// <summary>Devuelve al stock disponible unidades previamente reservadas (compensación).</summary>
    public void LiberarReserva(int cantidad)
    {
        ValidarCantidad(cantidad);

        if (cantidad > StockReservado)
        {
            throw new ExcepcionDominio($"No se pueden liberar {cantidad} unidades de '{Nombre}': solo hay {StockReservado} reservadas.");
        }

        StockReservado -= cantidad;
    }

    /// <summary>Convierte unidades reservadas en una salida definitiva del almacén.</summary>
    public void ConfirmarSalida(int cantidad)
    {
        ValidarCantidad(cantidad);

        if (cantidad > StockReservado)
        {
            throw new ExcepcionDominio($"No se pueden confirmar {cantidad} unidades de '{Nombre}': solo hay {StockReservado} reservadas.");
        }

        StockReservado -= cantidad;
        StockFisico -= cantidad;
    }

    private static void ValidarCantidad(int cantidad)
    {
        if (cantidad <= 0)
        {
            throw new ExcepcionDominio("La cantidad debe ser mayor que cero.");
        }
    }
}
