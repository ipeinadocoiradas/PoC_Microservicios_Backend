using Almacen.Application.Abstracciones;
using Almacen.Domain;
using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;

namespace Almacen.Application.Productos;

public sealed record ConsultarDisponibilidadConsulta(Guid ProductoId) : IConsulta<DisponibilidadProductoDto>;

public sealed record DisponibilidadProductoDto(Guid ProductoId, string Nombre, int StockFisico, int StockReservado, int StockDisponible);

public sealed class ManejadorConsultarDisponibilidad(IProductoRepositorio productos)
    : IManejadorConsulta<ConsultarDisponibilidadConsulta, DisponibilidadProductoDto>
{
    public async Task<Resultado<DisponibilidadProductoDto>> ManejarAsync(ConsultarDisponibilidadConsulta consulta, CancellationToken cancellationToken)
    {
        var producto = await productos.ObtenerPorIdAsync(new ProductoId(consulta.ProductoId), cancellationToken);

        return producto is null
            ? Error.NoEncontrado("Producto.NoEncontrado", $"No existe el producto {consulta.ProductoId}.")
            : new DisponibilidadProductoDto(producto.Id.Valor, producto.Nombre, producto.StockFisico, producto.StockReservado, producto.StockDisponible);
    }
}
