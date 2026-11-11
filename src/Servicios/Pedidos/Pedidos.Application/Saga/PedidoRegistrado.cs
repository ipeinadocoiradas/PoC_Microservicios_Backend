using Contratos.Integracion;

namespace Pedidos.Application.Saga;

/// <summary>
/// Evento interno de Pedidos que crea la instancia de la Saga. No pertenece a los contratos compartidos
/// porque solo lo produce y lo consume este servicio.
/// <para><b>Publica:</b> Pedidos.Api (vía Outbox al crear el pedido) · <b>Consume:</b> Pedidos.Api (Saga).</para>
/// </summary>
public sealed record PedidoRegistrado(
    Guid CorrelationId,
    Guid PedidoId,
    Guid ClienteId,
    decimal Importe,
    string Moneda,
    IReadOnlyList<LineaReserva> Lineas);
