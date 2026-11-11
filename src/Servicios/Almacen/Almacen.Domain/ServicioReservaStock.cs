namespace Almacen.Domain;

/// <summary>
/// Servicio de dominio que coordina una reserva con los productos que afecta. Es la única vía para crear,
/// confirmar o liberar reservas, garantizando que el stock de los productos y el estado de la reserva
/// cambian siempre juntos.
/// </summary>
/// <remarks>
/// Las operaciones de compensación son <b>idempotentes</b>: liberar una reserva ya liberada (o rechazada)
/// no modifica el stock y devuelve <c>false</c>. Así una reentrega de <c>LiberarReservaStock</c> nunca
/// devuelve dos veces las mismas unidades.
/// </remarks>
public static class ServicioReservaStock
{
    /// <summary>Reserva todas las líneas o ninguna (todo o nada dentro de la transacción local).</summary>
    /// <returns>Una reserva <see cref="EstadoReserva.Reservada"/> o, si falta stock, <see cref="EstadoReserva.Rechazada"/>.</returns>
    public static ReservaStock Reservar(
        Guid pedidoId,
        IReadOnlyCollection<LineaReservaStock> lineas,
        IReadOnlyDictionary<ProductoId, Producto> productos,
        DateTimeOffset fechaUtc)
    {
        ValidarSolicitud(pedidoId, lineas);

        if (BuscarMotivoRechazo(lineas, productos) is { } motivo)
        {
            return ReservaStock.CrearRechazada(pedidoId, lineas, motivo, fechaUtc);
        }

        foreach (var linea in lineas)
        {
            productos[linea.ProductoId].Reservar(linea.Cantidad);
        }

        return ReservaStock.CrearReservada(pedidoId, lineas, fechaUtc);
    }

    /// <summary>Devuelve al stock disponible las unidades de la reserva.</summary>
    /// <returns><c>true</c> si se liberó; <c>false</c> si no había nada que liberar (operación idempotente).</returns>
    /// <exception cref="ExcepcionDominio">Si la reserva ya está confirmada: la mercancía ya salió del almacén.</exception>
    public static bool Liberar(ReservaStock reserva, IReadOnlyDictionary<ProductoId, Producto> productos, DateTimeOffset fechaUtc)
    {
        switch (reserva.Estado)
        {
            case EstadoReserva.Liberada or EstadoReserva.Rechazada:
                return false;
            case EstadoReserva.Confirmada:
                throw new ExcepcionDominio($"La reserva {reserva.Id} ya está confirmada y no puede liberarse.");
        }

        foreach (var linea in reserva.Lineas)
        {
            ObtenerProducto(productos, linea.ProductoId).LiberarReserva(linea.Cantidad);
        }

        reserva.MarcarLiberada(fechaUtc);
        return true;
    }

    /// <summary>Consolida la reserva como salida definitiva de stock.</summary>
    /// <returns><c>true</c> si se confirmó; <c>false</c> si ya estaba confirmada (operación idempotente).</returns>
    public static bool Confirmar(ReservaStock reserva, IReadOnlyDictionary<ProductoId, Producto> productos, DateTimeOffset fechaUtc)
    {
        switch (reserva.Estado)
        {
            case EstadoReserva.Confirmada:
                return false;
            case EstadoReserva.Liberada or EstadoReserva.Rechazada:
                throw new ExcepcionDominio($"La reserva {reserva.Id} está {reserva.Estado} y no puede confirmarse.");
        }

        foreach (var linea in reserva.Lineas)
        {
            ObtenerProducto(productos, linea.ProductoId).ConfirmarSalida(linea.Cantidad);
        }

        reserva.MarcarConfirmada(fechaUtc);
        return true;
    }

    private static void ValidarSolicitud(Guid pedidoId, IReadOnlyCollection<LineaReservaStock> lineas)
    {
        if (pedidoId == Guid.Empty)
        {
            throw new ExcepcionDominio("La reserva debe pertenecer a un pedido.");
        }

        if (lineas.Count == 0)
        {
            throw new ExcepcionDominio("La reserva debe incluir al menos una línea.");
        }

        if (lineas.DistinctBy(linea => linea.ProductoId).Count() != lineas.Count)
        {
            throw new ExcepcionDominio("Un producto no puede aparecer en varias líneas de la misma reserva.");
        }
    }

    private static string? BuscarMotivoRechazo(IEnumerable<LineaReservaStock> lineas, IReadOnlyDictionary<ProductoId, Producto> productos)
    {
        foreach (var linea in lineas)
        {
            if (!productos.TryGetValue(linea.ProductoId, out var producto))
            {
                return $"El producto {linea.ProductoId} no existe en el catálogo.";
            }

            if (!producto.PuedeReservar(linea.Cantidad))
            {
                return $"Stock insuficiente para '{producto.Nombre}': disponibles {producto.StockDisponible}, solicitadas {linea.Cantidad}.";
            }
        }

        return null;
    }

    private static Producto ObtenerProducto(IReadOnlyDictionary<ProductoId, Producto> productos, ProductoId id) =>
        productos.TryGetValue(id, out var producto)
            ? producto
            : throw new ExcepcionDominio($"El producto {id} de la reserva no se ha cargado.");
}
