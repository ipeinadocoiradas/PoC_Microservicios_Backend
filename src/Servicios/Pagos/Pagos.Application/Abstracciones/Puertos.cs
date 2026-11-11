using Pagos.Domain;

namespace Pagos.Application.Abstracciones;

public interface IPagoRepositorio
{
    Task<Pago?> ObtenerPorIdAsync(PagoId id, CancellationToken cancellationToken);

    /// <summary>Busca el pago de un pedido; es la consulta en la que se apoya la idempotencia.</summary>
    Task<Pago?> ObtenerPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken);

    void Agregar(Pago pago);
}

public interface IUnidadTrabajo
{
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Pasarela de autorización de pagos. Es una interfaz porque aporta desacoplamiento real: en la PoC la
/// implementa un simulador y en producción la implementaría el adaptador de un proveedor de pagos.
/// </summary>
public interface IAutorizadorPagos
{
    /// <exception cref="Exception">Un fallo técnico de la pasarela se propaga como excepción para que se reintente.</exception>
    Task<ResultadoAutorizacion> AutorizarAsync(SolicitudAutorizacion solicitud, CancellationToken cancellationToken);
}

public sealed record SolicitudAutorizacion(Guid PagoId, Guid ClienteId, decimal Importe, string Moneda);

/// <summary>Respuesta de negocio de la pasarela: aprobada (con código) o denegada (con motivo).</summary>
public sealed record ResultadoAutorizacion(bool Aprobada, string? CodigoAutorizacion, string? MotivoRechazo)
{
    public static ResultadoAutorizacion Aprobar(string codigoAutorizacion) => new(true, codigoAutorizacion, null);

    public static ResultadoAutorizacion Denegar(string motivo) => new(false, null, motivo);
}
