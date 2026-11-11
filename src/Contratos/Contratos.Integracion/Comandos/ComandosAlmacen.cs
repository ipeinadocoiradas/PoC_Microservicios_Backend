namespace Contratos.Integracion.Comandos;

// Todos los mensajes incluyen CorrelationId: identifica el proceso de negocio (la Saga del pedido)
// y permite a MassTransit enrutar las respuestas a la instancia correcta. En esta PoC coincide con el PedidoId.

/// <summary>
/// Solicita reservar el stock de un pedido.
/// <para><b>Envía:</b> Pedidos.Api (Saga) · <b>Consume:</b> Almacen.Api.</para>
/// </summary>
/// <param name="CorrelationId">Identificador del proceso distribuido.</param>
/// <param name="PedidoId">Pedido al que pertenece la reserva. Es la clave de idempotencia en Almacén.</param>
/// <param name="Lineas">Productos y cantidades a reservar.</param>
public sealed record SolicitarReservaStock(Guid CorrelationId, Guid PedidoId, IReadOnlyList<LineaReserva> Lineas);

/// <summary>
/// Acción compensatoria: libera las unidades reservadas para un pedido que no puede completarse.
/// <para><b>Envía:</b> Pedidos.Api (Saga) · <b>Consume:</b> Almacen.Api.</para>
/// </summary>
/// <param name="CorrelationId">Identificador del proceso distribuido.</param>
/// <param name="PedidoId">Pedido cuya reserva debe liberarse.</param>
/// <param name="Motivo">Causa que provoca la compensación (trazabilidad).</param>
public sealed record LiberarReservaStock(Guid CorrelationId, Guid PedidoId, string Motivo);

/// <summary>
/// Convierte la reserva en una salida definitiva de stock una vez cobrado el pedido.
/// <para><b>Envía:</b> Pedidos.Api (Saga) · <b>Consume:</b> Almacen.Api.</para>
/// </summary>
/// <param name="CorrelationId">Identificador del proceso distribuido.</param>
/// <param name="PedidoId">Pedido cuya reserva se confirma.</param>
public sealed record ConfirmarReservaStock(Guid CorrelationId, Guid PedidoId);
