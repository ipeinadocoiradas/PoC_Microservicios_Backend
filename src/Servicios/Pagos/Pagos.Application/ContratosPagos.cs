using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Pagos.Domain;

namespace Pagos.Application;

/// <summary>Procesa el cobro de un pedido. Idempotente por <see cref="PedidoId"/>.</summary>
public sealed record ProcesarPagoComando(Guid PedidoId, Guid ClienteId, decimal Importe, string Moneda) : IComando<PagoDto>;

/// <summary>Compensación: cancela o reembolsa el pago de un pedido. Idempotente.</summary>
public sealed record CancelarPagoComando(Guid PedidoId, Guid ClienteId, decimal Importe, string Moneda, string Motivo) : IComando<PagoDto>;

public sealed record ObtenerPagoConsulta(Guid PagoId) : IConsulta<PagoDto>;

public sealed record ObtenerPagoPorPedidoConsulta(Guid PedidoId) : IConsulta<PagoDto>;

public sealed record PagoDto(
    Guid PagoId,
    Guid PedidoId,
    Guid ClienteId,
    decimal Importe,
    string Moneda,
    EstadoPago Estado,
    string? CodigoAutorizacion,
    string? Motivo,
    DateTimeOffset FechaCreacionUtc,
    DateTimeOffset FechaActualizacionUtc)
{
    public static PagoDto Desde(Pago pago) => new(
        pago.Id.Valor,
        pago.PedidoId,
        pago.ClienteId,
        pago.Importe.Cantidad,
        pago.Importe.Moneda,
        pago.Estado,
        pago.CodigoAutorizacion,
        pago.Motivo,
        pago.FechaCreacionUtc,
        pago.FechaActualizacionUtc);
}

public static class ErroresPagos
{
    public static readonly Error SolicitudNoValida =
        Error.Validacion("Pago.SolicitudNoValida", "El pago necesita pedido, cliente, un importe positivo y una moneda ISO 4217.");

    public static Error NoEncontrado(Guid id) =>
        Error.NoEncontrado("Pago.NoEncontrado", $"No existe el pago {id}.");

    public static Error NoEncontradoParaPedido(Guid pedidoId) =>
        Error.NoEncontrado("Pago.NoEncontrado", $"No existe ningún pago para el pedido {pedidoId}.");

    internal static Error? Validar(Guid pedidoId, Guid clienteId, decimal importe, string moneda) =>
        pedidoId == Guid.Empty || clienteId == Guid.Empty || importe <= 0 || string.IsNullOrWhiteSpace(moneda) || moneda.Trim().Length != 3
            ? SolicitudNoValida
            : null;
}
