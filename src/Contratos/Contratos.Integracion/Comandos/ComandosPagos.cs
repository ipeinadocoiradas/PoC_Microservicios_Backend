namespace Contratos.Integracion.Comandos;

/// <summary>
/// Solicita el cobro de un pedido cuyo stock ya está reservado.
/// <para><b>Envía:</b> Pedidos.Api (Saga) · <b>Consume:</b> Pagos.Api.</para>
/// </summary>
/// <param name="CorrelationId">Identificador del proceso distribuido.</param>
/// <param name="PedidoId">Pedido a cobrar. Es la clave de idempotencia en Pagos.</param>
/// <param name="ClienteId">Cliente al que se realiza el cargo.</param>
/// <param name="Importe">Importe total del pedido.</param>
/// <param name="Moneda">Código ISO 4217 de la moneda.</param>
public sealed record ProcesarPago(Guid CorrelationId, Guid PedidoId, Guid ClienteId, decimal Importe, string Moneda);

/// <summary>
/// Acción compensatoria: cancela (o reembolsa si ya se cobró) el pago de un pedido.
/// Se usa cuando no se sabe con certeza si el cobro llegó a producirse.
/// <para><b>Envía:</b> Pedidos.Api (Saga) · <b>Consume:</b> Pagos.Api.</para>
/// </summary>
/// <param name="CorrelationId">Identificador del proceso distribuido.</param>
/// <param name="PedidoId">Pedido cuyo pago debe anularse.</param>
/// <param name="ClienteId">Cliente del pedido.</param>
/// <param name="Importe">Importe del pedido; permite registrar una cancelación preventiva si el pago aún no existe.</param>
/// <param name="Moneda">Código ISO 4217 de la moneda.</param>
/// <param name="Motivo">Causa de la cancelación.</param>
public sealed record CancelarPago(Guid CorrelationId, Guid PedidoId, Guid ClienteId, decimal Importe, string Moneda, string Motivo);
