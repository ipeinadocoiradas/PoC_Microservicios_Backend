using Almacen.Domain;

namespace Almacen.Application.Abstracciones;

public interface IProductoRepositorio
{
    Task<Producto?> ObtenerPorIdAsync(ProductoId id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<ProductoId, Producto>> ObtenerPorIdsAsync(IEnumerable<ProductoId> ids, CancellationToken cancellationToken);
}

public interface IReservaStockRepositorio
{
    /// <summary>Busca la reserva de un pedido; es la consulta en la que se apoya la idempotencia.</summary>
    Task<ReservaStock?> ObtenerPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken);

    void Agregar(ReservaStock reserva);
}

public interface IUnidadTrabajo
{
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
