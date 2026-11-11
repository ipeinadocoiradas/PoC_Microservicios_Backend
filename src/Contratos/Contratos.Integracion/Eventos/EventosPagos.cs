namespace Contratos.Integracion.Eventos;

/// <summary>
/// El pago se ha autorizado y registrado.
/// <para><b>Publica:</b> Pagos.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record PagoCompletado(Guid CorrelationId, Guid PedidoId, Guid PagoId, decimal Importe, string Moneda);

/// <summary>
/// La autorización ha sido denegada (rechazo de negocio, no un error técnico).
/// <paramref name="PagoId"/> es nulo si la solicitud no llegó a registrarse por ser inválida.
/// <para><b>Publica:</b> Pagos.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record PagoRechazado(Guid CorrelationId, Guid PedidoId, Guid? PagoId, string Motivo);

/// <summary>
/// El pago ha quedado anulado: cancelado si no se había cobrado, reembolsado si sí.
/// <para><b>Publica:</b> Pagos.Api · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
/// <param name="EstadoFinal">Estado del pago tras la anulación (Cancelado, Reembolsado o Rechazado).</param>
public sealed record PagoCancelado(Guid CorrelationId, Guid PedidoId, Guid PagoId, string EstadoFinal);
