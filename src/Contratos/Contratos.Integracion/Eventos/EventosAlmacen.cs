namespace Contratos.Integracion.Eventos;

/// <summary>
/// Las unidades del pedido han quedado reservadas.
/// <para><b>Publica:</b> Almacen.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record StockReservado(Guid CorrelationId, Guid PedidoId, Guid ReservaId);

/// <summary>
/// No hay stock suficiente (o algún producto no existe); no se ha reservado nada.
/// <para><b>Publica:</b> Almacen.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record StockNoDisponible(Guid CorrelationId, Guid PedidoId, string Motivo);

/// <summary>
/// La reserva se ha liberado (compensación completada). <paramref name="ReservaId"/> es nulo
/// cuando no existía reserva que liberar: el objetivo de la compensación se considera alcanzado igualmente.
/// <para><b>Publica:</b> Almacen.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record ReservaLiberada(Guid CorrelationId, Guid PedidoId, Guid? ReservaId);

/// <summary>
/// La reserva se ha convertido en salida definitiva de stock.
/// <para><b>Publica:</b> Almacen.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record ReservaConfirmada(Guid CorrelationId, Guid PedidoId, Guid ReservaId);
