namespace Pagos.Domain;

/// <summary>Identificador fuertemente tipado del pago.</summary>
public readonly record struct PagoId(Guid Valor)
{
    public static PagoId Nuevo() => new(Guid.CreateVersion7());

    public override string ToString() => Valor.ToString();
}

/// <summary>Ciclo de vida de un pago.</summary>
public enum EstadoPago
{
    /// <summary>Registrado, pendiente de la respuesta de la pasarela.</summary>
    Pendiente = 1,

    /// <summary>Autorizado y cobrado.</summary>
    Completado = 2,

    /// <summary>Denegado por la pasarela. Estado final, sin cargo.</summary>
    Rechazado = 3,

    /// <summary>Anulado antes de cobrarse (incluye la cancelación preventiva). Estado final, sin cargo.</summary>
    Cancelado = 4,

    /// <summary>Cobrado y posteriormente devuelto (compensación). Estado final.</summary>
    Reembolsado = 5
}

/// <summary>
/// Agregado Pago. Existe como máximo un pago por pedido (restricción única), lo que impide cobrar dos veces
/// aunque <c>ProcesarPago</c> se reciba repetido.
/// </summary>
public sealed class Pago
{
    private const int LongitudMaximaMotivo = 500;

    private Pago()
    {
        // Requerido por EF Core.
    }

    private Pago(PagoId id, Guid pedidoId, Guid clienteId, Dinero importe, DateTimeOffset fechaUtc)
    {
        Id = id;
        PedidoId = pedidoId;
        ClienteId = clienteId;
        Importe = importe;
        Estado = EstadoPago.Pendiente;
        FechaCreacionUtc = fechaUtc;
        FechaActualizacionUtc = fechaUtc;
    }

    public PagoId Id { get; private set; }

    public Guid PedidoId { get; private set; }

    public Guid ClienteId { get; private set; }

    public Dinero Importe { get; private set; } = null!;

    public EstadoPago Estado { get; private set; }

    public string? CodigoAutorizacion { get; private set; }

    /// <summary>Motivo del rechazo o de la cancelación.</summary>
    public string? Motivo { get; private set; }

    public DateTimeOffset FechaCreacionUtc { get; private set; }

    public DateTimeOffset FechaActualizacionUtc { get; private set; }

    public static Pago Iniciar(Guid pedidoId, Guid clienteId, Dinero importe, DateTimeOffset fechaUtc)
    {
        ArgumentNullException.ThrowIfNull(importe);

        if (pedidoId == Guid.Empty)
        {
            throw new ExcepcionDominio("El pago debe pertenecer a un pedido.");
        }

        if (clienteId == Guid.Empty)
        {
            throw new ExcepcionDominio("El pago debe pertenecer a un cliente.");
        }

        return new Pago(PagoId.Nuevo(), pedidoId, clienteId, importe, fechaUtc);
    }

    /// <summary>
    /// Registra la anulación de un pago que nunca llegó a procesarse. Si después se recibe un
    /// <c>ProcesarPago</c> tardío (p. ej. reinyectado desde la cola de errores), encontrará este registro
    /// y no cobrará.
    /// </summary>
    public static Pago RegistrarCancelacionPreventiva(Guid pedidoId, Guid clienteId, Dinero importe, string motivo, DateTimeOffset fechaUtc)
    {
        var pago = Iniciar(pedidoId, clienteId, importe, fechaUtc);
        pago.Cancelar(motivo, fechaUtc);
        return pago;
    }

    public void Completar(string codigoAutorizacion, DateTimeOffset fechaUtc)
    {
        if (string.IsNullOrWhiteSpace(codigoAutorizacion))
        {
            throw new ExcepcionDominio("Un pago completado requiere código de autorización.");
        }

        AsegurarPendiente(EstadoPago.Completado);
        Estado = EstadoPago.Completado;
        CodigoAutorizacion = codigoAutorizacion;
        FechaActualizacionUtc = fechaUtc;
    }

    public void Rechazar(string motivo, DateTimeOffset fechaUtc)
    {
        AsegurarPendiente(EstadoPago.Rechazado);
        Estado = EstadoPago.Rechazado;
        Motivo = Recortar(motivo);
        FechaActualizacionUtc = fechaUtc;
    }

    /// <summary>
    /// Anula el pago: si estaba pendiente queda <see cref="EstadoPago.Cancelado"/>; si estaba cobrado,
    /// <see cref="EstadoPago.Reembolsado"/>. En estados finales no hace nada (idempotente).
    /// </summary>
    /// <returns><c>true</c> si el estado cambió.</returns>
    public bool Cancelar(string motivo, DateTimeOffset fechaUtc)
    {
        var nuevoEstado = Estado switch
        {
            EstadoPago.Pendiente => EstadoPago.Cancelado,
            EstadoPago.Completado => EstadoPago.Reembolsado,
            _ => (EstadoPago?)null
        };

        if (nuevoEstado is null)
        {
            return false;
        }

        Estado = nuevoEstado.Value;
        Motivo = Recortar(motivo);
        FechaActualizacionUtc = fechaUtc;
        return true;
    }

    private void AsegurarPendiente(EstadoPago destino)
    {
        if (Estado is not EstadoPago.Pendiente)
        {
            throw new ExcepcionDominio($"El pago {Id} está {Estado} y no puede pasar a {destino}.");
        }
    }

    private static string Recortar(string motivo)
    {
        var limpio = string.IsNullOrWhiteSpace(motivo) ? "Sin motivo indicado" : motivo.Trim();
        return limpio.Length <= LongitudMaximaMotivo ? limpio : limpio[..LongitudMaximaMotivo];
    }
}
