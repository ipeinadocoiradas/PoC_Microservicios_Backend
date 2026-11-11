namespace Pedidos.Domain;

/// <summary>
/// Objeto de valor: un producto, sus unidades y el precio acordado. Inmutable una vez creado.
/// </summary>
/// <remarks>
/// El precio se recibe con el pedido (simplificación de la PoC: Pedidos no consulta un catálogo de precios).
/// </remarks>
public sealed class LineaPedido
{
    public const int CantidadMaximaPorLinea = 1_000;

    private LineaPedido()
    {
        // Requerido por EF Core para materializar el objeto.
    }

    private LineaPedido(Guid productoId, int cantidad, decimal precioUnitario)
    {
        ProductoId = productoId;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
    }

    public Guid ProductoId { get; private set; }

    public int Cantidad { get; private set; }

    public decimal PrecioUnitario { get; private set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;

    /// <summary>Crea una línea validando sus invariantes.</summary>
    /// <exception cref="ExcepcionDominio">Si algún dato no es válido.</exception>
    public static LineaPedido Crear(Guid productoId, int cantidad, decimal precioUnitario)
    {
        if (productoId == Guid.Empty)
        {
            throw new ExcepcionDominio("La línea debe referenciar un producto.");
        }

        if (cantidad is <= 0 or > CantidadMaximaPorLinea)
        {
            throw new ExcepcionDominio($"La cantidad debe estar entre 1 y {CantidadMaximaPorLinea}.");
        }

        if (precioUnitario <= 0)
        {
            throw new ExcepcionDominio("El precio unitario debe ser mayor que cero.");
        }

        if (decimal.Round(precioUnitario, 2) != precioUnitario)
        {
            throw new ExcepcionDominio("El precio unitario admite como máximo dos decimales.");
        }

        return new LineaPedido(productoId, cantidad, precioUnitario);
    }
}
