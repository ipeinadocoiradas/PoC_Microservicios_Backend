using Almacen.Application.Abstracciones;
using Almacen.Domain;
using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging;

namespace Almacen.Application.Reservas;

/// <summary>
/// Reserva el stock solicitado aplicando idempotencia de negocio.
/// </summary>
/// <remarks>
/// Si ya existe una reserva para el pedido (mensaje duplicado), se devuelve tal cual sin tocar el stock.
/// Ante dos duplicados simultáneos, el índice único sobre <c>IdPedido</c> hace fallar a uno de ellos;
/// el reintento de MassTransit lo vuelve a procesar y entonces encuentra la reserva existente.
/// </remarks>
public sealed class ManejadorReservarStock(
    IReservaStockRepositorio reservas,
    IProductoRepositorio productos,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj,
    ILogger<ManejadorReservarStock> registro) : IManejadorComando<ReservarStockComando, ReservaDto>
{
    public async Task<Resultado<ReservaDto>> ManejarAsync(ReservarStockComando comando, CancellationToken cancellationToken)
    {
        if (Validar(comando) is { } error)
        {
            return error;
        }

        var existente = await reservas.ObtenerPorPedidoAsync(comando.PedidoId, cancellationToken);
        if (existente is not null)
        {
            registro.LogInformation("Solicitud duplicada: el pedido {PedidoId} ya tiene una reserva {Estado}", comando.PedidoId, existente.Estado);
            return ReservaDto.Desde(existente);
        }

        var lineas = comando.Lineas
            .Select(linea => LineaReservaStock.Crear(new ProductoId(linea.ProductoId), linea.Cantidad))
            .ToList();

        var productosAfectados = await productos.ObtenerPorIdsAsync(lineas.Select(linea => linea.ProductoId), cancellationToken);
        var reserva = ServicioReservaStock.Reservar(comando.PedidoId, lineas, productosAfectados, reloj.GetUtcNow());

        reservas.Agregar(reserva);
        await unidadTrabajo.GuardarCambiosAsync(cancellationToken);

        registro.LogInformation("Reserva {ReservaId} del pedido {PedidoId} registrada como {Estado}",
            reserva.Id.Valor, comando.PedidoId, reserva.Estado);

        return ReservaDto.Desde(reserva);
    }

    private static Error? Validar(ReservarStockComando comando)
    {
        if (comando.PedidoId == Guid.Empty)
        {
            return ErroresReservas.PedidoObligatorio;
        }

        var lineasValidas = comando.Lineas is { Count: > 0 }
            && comando.Lineas.All(linea => linea.ProductoId != Guid.Empty && linea.Cantidad > 0)
            && comando.Lineas.DistinctBy(linea => linea.ProductoId).Count() == comando.Lineas.Count;

        return lineasValidas ? null : ErroresReservas.LineasNoValidas;
    }
}
