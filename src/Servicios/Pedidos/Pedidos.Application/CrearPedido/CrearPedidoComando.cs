using Shared.Aplicacion.Cqrs;

namespace Pedidos.Application.CrearPedido;

/// <summary>Registra un pedido nuevo e inicia su Saga.</summary>
public sealed record CrearPedidoComando(Guid ClienteId, string Moneda, IReadOnlyList<LineaPedidoSolicitada> Lineas)
    : IComando<PedidoCreadoDto>;

/// <summary>Línea solicitada por el cliente.</summary>
public sealed record LineaPedidoSolicitada(Guid ProductoId, int Cantidad, decimal PrecioUnitario);

/// <summary>Respuesta inmediata: el pedido queda registrado y su procesamiento continúa de forma asíncrona.</summary>
public sealed record PedidoCreadoDto(Guid PedidoId, string Estado, decimal Total, string Moneda);
