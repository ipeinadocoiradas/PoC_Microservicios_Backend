using Almacen.Application.Abstracciones;
using Almacen.Domain;
using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging;

namespace Almacen.Application.Reservas;

/// <summary>
/// Compensación: libera la reserva del pedido. Si ya estaba liberada no hace nada, de modo que un
/// <c>LiberarReservaStock</c> reentregado nunca devuelve dos veces las mismas unidades.
/// </summary>
public sealed class ManejadorLiberarReserva(
    IReservaStockRepositorio reservas,
    IProductoRepositorio productos,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj,
    ILogger<ManejadorLiberarReserva> registro) : IManejadorComando<LiberarReservaComando, ReservaDto>
{
    public async Task<Resultado<ReservaDto>> ManejarAsync(LiberarReservaComando comando, CancellationToken cancellationToken)
    {
        var reserva = await reservas.ObtenerPorPedidoAsync(comando.PedidoId, cancellationToken);
        if (reserva is null)
        {
            return ErroresReservas.NoEncontrada(comando.PedidoId);
        }

        var productosAfectados = await productos.ObtenerPorIdsAsync(reserva.Lineas.Select(linea => linea.ProductoId), cancellationToken);

        if (ServicioReservaStock.Liberar(reserva, productosAfectados, reloj.GetUtcNow()))
        {
            await unidadTrabajo.GuardarCambiosAsync(cancellationToken);
            registro.LogInformation("Reserva {ReservaId} del pedido {PedidoId} liberada", reserva.Id.Valor, comando.PedidoId);
        }
        else
        {
            registro.LogInformation("La reserva del pedido {PedidoId} ya estaba {Estado}; no se modifica el stock", comando.PedidoId, reserva.Estado);
        }

        return ReservaDto.Desde(reserva);
    }
}

/// <summary>Confirma la reserva tras el cobro. Idempotente.</summary>
public sealed class ManejadorConfirmarReserva(
    IReservaStockRepositorio reservas,
    IProductoRepositorio productos,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj,
    ILogger<ManejadorConfirmarReserva> registro) : IManejadorComando<ConfirmarReservaComando, ReservaDto>
{
    public async Task<Resultado<ReservaDto>> ManejarAsync(ConfirmarReservaComando comando, CancellationToken cancellationToken)
    {
        var reserva = await reservas.ObtenerPorPedidoAsync(comando.PedidoId, cancellationToken);
        if (reserva is null)
        {
            return ErroresReservas.NoEncontrada(comando.PedidoId);
        }

        var productosAfectados = await productos.ObtenerPorIdsAsync(reserva.Lineas.Select(linea => linea.ProductoId), cancellationToken);

        if (ServicioReservaStock.Confirmar(reserva, productosAfectados, reloj.GetUtcNow()))
        {
            await unidadTrabajo.GuardarCambiosAsync(cancellationToken);
            registro.LogInformation("Reserva {ReservaId} del pedido {PedidoId} confirmada", reserva.Id.Valor, comando.PedidoId);
        }

        return ReservaDto.Desde(reserva);
    }
}

public sealed class ManejadorObtenerReservaPorPedido(IReservaStockRepositorio reservas)
    : IManejadorConsulta<ObtenerReservaPorPedidoConsulta, ReservaDto>
{
    public async Task<Resultado<ReservaDto>> ManejarAsync(ObtenerReservaPorPedidoConsulta consulta, CancellationToken cancellationToken)
    {
        var reserva = await reservas.ObtenerPorPedidoAsync(consulta.PedidoId, cancellationToken);
        return reserva is null ? ErroresReservas.NoEncontrada(consulta.PedidoId) : ReservaDto.Desde(reserva);
    }
}
