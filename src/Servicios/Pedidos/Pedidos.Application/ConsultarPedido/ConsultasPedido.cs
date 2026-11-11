using Shared.Aplicacion.Cqrs;
using Pedidos.Domain;

namespace Pedidos.Application.ConsultarPedido;

/// <summary>Obtiene el detalle de negocio de un pedido.</summary>
public sealed record ObtenerPedidoConsulta(Guid PedidoId) : IConsulta<PedidoDto>;

/// <summary>Obtiene el estado del pedido junto al estado interno de su Saga.</summary>
public sealed record ObtenerEstadoPedidoConsulta(Guid PedidoId) : IConsulta<EstadoPedidoDto>;

public sealed record LineaPedidoDto(Guid ProductoId, int Cantidad, decimal PrecioUnitario, decimal Subtotal);

/// <summary>Obtiene las transiciones de la Saga de un pedido, en orden.</summary>
public sealed record ObtenerHistorialSagaConsulta(Guid PedidoId) : IConsulta<IReadOnlyList<TransicionSagaDto>>;

/// <summary>Una transición de la Saga tal como la expone <c>GET /api/pedidos/{id}/historial</c>.</summary>
public sealed record TransicionSagaDto(int Secuencia, string? EstadoAnterior, string EstadoNuevo, string Evento, DateTimeOffset FechaUtc);

public sealed record PedidoDto(
    Guid PedidoId,
    Guid ClienteId,
    EstadoPedido Estado,
    decimal Total,
    string Moneda,
    string? MotivoFinalizacion,
    DateTimeOffset FechaCreacionUtc,
    DateTimeOffset? FechaFinalizacionUtc,
    IReadOnlyList<LineaPedidoDto> Lineas)
{
    public static PedidoDto Desde(Pedido pedido) => new(
        pedido.Id.Valor,
        pedido.ClienteId,
        pedido.Estado,
        pedido.Total,
        pedido.Moneda,
        pedido.MotivoFinalizacion,
        pedido.FechaCreacionUtc,
        pedido.FechaFinalizacionUtc,
        [.. pedido.Lineas.Select(linea => new LineaPedidoDto(linea.ProductoId, linea.Cantidad, linea.PrecioUnitario, linea.Subtotal))]);
}

/// <summary>
/// Vista de seguimiento que muestra explícitamente la diferencia entre el estado de negocio del pedido
/// (<see cref="EstadoPedido"/>) y el estado técnico de la Saga (<see cref="EstadoSaga"/>).
/// </summary>
public sealed record EstadoPedidoDto(
    Guid PedidoId,
    EstadoPedido EstadoPedido,
    string? EstadoSaga,
    Guid? ReservaId,
    Guid? PagoId,
    string? Motivo,
    string? UltimoErrorSaga,
    DateTimeOffset? FechaActualizacionSagaUtc);
