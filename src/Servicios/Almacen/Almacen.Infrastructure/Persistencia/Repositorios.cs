using Almacen.Application.Abstracciones;
using Almacen.Domain;
using Microsoft.EntityFrameworkCore;

namespace Almacen.Infrastructure.Persistencia;

internal sealed class RepositorioProductos(AlmacenDbContext contexto) : IProductoRepositorio
{
    public Task<Producto?> ObtenerPorIdAsync(ProductoId id, CancellationToken cancellationToken) =>
        contexto.Productos.FirstOrDefaultAsync(producto => producto.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<ProductoId, Producto>> ObtenerPorIdsAsync(IEnumerable<ProductoId> ids, CancellationToken cancellationToken)
    {
        var identificadores = ids.Distinct().ToList();

        var productos = await contexto.Productos
            .Where(producto => identificadores.Contains(producto.Id))
            .ToListAsync(cancellationToken);

        return productos.ToDictionary(producto => producto.Id);
    }
}

internal sealed class RepositorioReservasStock(AlmacenDbContext contexto) : IReservaStockRepositorio
{
    public Task<ReservaStock?> ObtenerPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken) =>
        contexto.ReservasStock.FirstOrDefaultAsync(reserva => reserva.PedidoId == pedidoId, cancellationToken);

    public void Agregar(ReservaStock reserva) => contexto.ReservasStock.Add(reserva);
}
