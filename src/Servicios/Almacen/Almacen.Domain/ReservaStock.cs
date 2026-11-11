namespace Almacen.Domain;

/// <summary>Ciclo de vida de una reserva de stock.</summary>
public enum EstadoReserva
{
    /// <summary>Unidades comprometidas, pendientes de confirmar o liberar.</summary>
    Reservada = 1,

    /// <summary>Salida definitiva de stock tras el cobro. Estado final.</summary>
    Confirmada = 2,

    /// <summary>Unidades devueltas al stock disponible (compensación). Estado final.</summary>
    Liberada = 3,

    /// <summary>
    /// No había stock suficiente; no se reservó nada. Se persiste para que un mensaje duplicado reciba
    /// la misma respuesta aunque, entretanto, haya entrado stock nuevo. Estado final.
    /// </summary>
    Rechazada = 4
}

/// <summary>Objeto de valor: unidades de un producto incluidas en una reserva.</summary>
public sealed class LineaReservaStock
{
    private LineaReservaStock()
    {
        // Requerido por EF Core.
    }

    private LineaReservaStock(ProductoId productoId, int cantidad)
    {
        ProductoId = productoId;
        Cantidad = cantidad;
    }

    public ProductoId ProductoId { get; private set; }

    public int Cantidad { get; private set; }

    public static LineaReservaStock Crear(ProductoId productoId, int cantidad)
    {
        if (productoId.Valor == Guid.Empty)
        {
            throw new ExcepcionDominio("La línea de reserva debe referenciar un producto.");
        }

        if (cantidad <= 0)
        {
            throw new ExcepcionDominio("La cantidad a reservar debe ser mayor que cero.");
        }

        return new LineaReservaStock(productoId, cantidad);
    }
}

/// <summary>
/// Agregado que representa las unidades comprometidas para un pedido. Existe como máximo una reserva por
/// pedido (restricción única sobre <see cref="PedidoId"/>), lo que respalda la idempotencia del consumidor.
/// </summary>
/// <remarks>
/// Las transiciones que afectan al stock se realizan a través de <see cref="ServicioReservaStock"/>,
/// que mantiene coherentes la reserva y los productos implicados; por eso los métodos de cambio son internos.
/// </remarks>
public sealed class ReservaStock
{
    private readonly List<LineaReservaStock> _lineas = [];

    private ReservaStock()
    {
        // Requerido por EF Core.
    }

    private ReservaStock(Guid pedidoId, IEnumerable<LineaReservaStock> lineas, EstadoReserva estado, string? motivoRechazo, DateTimeOffset fechaUtc)
    {
        Id = ReservaId.Nueva();
        PedidoId = pedidoId;
        Estado = estado;
        MotivoRechazo = motivoRechazo;
        FechaCreacionUtc = fechaUtc;
        FechaActualizacionUtc = fechaUtc;
        _lineas.AddRange(lineas);
    }

    public ReservaId Id { get; private set; }

    /// <summary>Pedido propietario. Es un identificador externo: Almacén no conoce el modelo de Pedidos.</summary>
    public Guid PedidoId { get; private set; }

    public EstadoReserva Estado { get; private set; }

    public string? MotivoRechazo { get; private set; }

    public DateTimeOffset FechaCreacionUtc { get; private set; }

    public DateTimeOffset FechaActualizacionUtc { get; private set; }

    public IReadOnlyCollection<LineaReservaStock> Lineas => _lineas.AsReadOnly();

    internal static ReservaStock CrearReservada(Guid pedidoId, IEnumerable<LineaReservaStock> lineas, DateTimeOffset fechaUtc) =>
        new(pedidoId, lineas, EstadoReserva.Reservada, motivoRechazo: null, fechaUtc);

    internal static ReservaStock CrearRechazada(Guid pedidoId, IEnumerable<LineaReservaStock> lineas, string motivo, DateTimeOffset fechaUtc) =>
        new(pedidoId, lineas, EstadoReserva.Rechazada, motivo, fechaUtc);

    internal void MarcarLiberada(DateTimeOffset fechaUtc) => CambiarDesdeReservada(EstadoReserva.Liberada, fechaUtc);

    internal void MarcarConfirmada(DateTimeOffset fechaUtc) => CambiarDesdeReservada(EstadoReserva.Confirmada, fechaUtc);

    private void CambiarDesdeReservada(EstadoReserva destino, DateTimeOffset fechaUtc)
    {
        if (Estado is not EstadoReserva.Reservada)
        {
            throw new ExcepcionDominio($"La reserva {Id} está {Estado} y no puede pasar a {destino}.");
        }

        Estado = destino;
        FechaActualizacionUtc = fechaUtc;
    }
}
