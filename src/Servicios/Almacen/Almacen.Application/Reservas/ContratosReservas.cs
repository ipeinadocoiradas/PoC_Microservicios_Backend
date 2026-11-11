using Almacen.Domain;
using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;

namespace Almacen.Application.Reservas;

public sealed record LineaSolicitadaDto(Guid ProductoId, int Cantidad);

/// <summary>Reserva el stock de un pedido. Idempotente por <see cref="PedidoId"/>.</summary>
public sealed record ReservarStockComando(Guid PedidoId, IReadOnlyList<LineaSolicitadaDto> Lineas) : IComando<ReservaDto>;

/// <summary>Compensación: libera la reserva de un pedido. Idempotente.</summary>
public sealed record LiberarReservaComando(Guid PedidoId) : IComando<ReservaDto>;

/// <summary>Confirma la reserva de un pedido como salida de stock. Idempotente.</summary>
public sealed record ConfirmarReservaComando(Guid PedidoId) : IComando<ReservaDto>;

public sealed record ObtenerReservaPorPedidoConsulta(Guid PedidoId) : IConsulta<ReservaDto>;

public sealed record LineaReservaDto(Guid ProductoId, int Cantidad);

public sealed record ReservaDto(
    Guid ReservaId,
    Guid PedidoId,
    EstadoReserva Estado,
    string? MotivoRechazo,
    DateTimeOffset FechaActualizacionUtc,
    IReadOnlyList<LineaReservaDto> Lineas)
{
    public static ReservaDto Desde(ReservaStock reserva) => new(
        reserva.Id.Valor,
        reserva.PedidoId,
        reserva.Estado,
        reserva.MotivoRechazo,
        reserva.FechaActualizacionUtc,
        [.. reserva.Lineas.Select(linea => new LineaReservaDto(linea.ProductoId.Valor, linea.Cantidad))]);
}

public static class ErroresReservas
{
    public static readonly Error PedidoObligatorio =
        Error.Validacion("Reserva.PedidoObligatorio", "Debe indicarse el pedido.");

    public static readonly Error LineasNoValidas =
        Error.Validacion("Reserva.LineasNoValidas", "La reserva necesita líneas con producto, cantidad positiva y sin productos repetidos.");

    public static Error NoEncontrada(Guid pedidoId) =>
        Error.NoEncontrado("Reserva.NoEncontrada", $"No existe ninguna reserva para el pedido {pedidoId}.");
}
