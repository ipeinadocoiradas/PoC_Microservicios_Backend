using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Pedidos.Application.Abstracciones;
using Pedidos.Domain;

namespace Pedidos.Application.ConsultarPedido;

public sealed class ManejadorObtenerPedido(IPedidoRepositorio repositorio)
    : IManejadorConsulta<ObtenerPedidoConsulta, PedidoDto>
{
    public async Task<Resultado<PedidoDto>> ManejarAsync(ObtenerPedidoConsulta consulta, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(new PedidoId(consulta.PedidoId), cancellationToken);

        return pedido is null
            ? ErroresPedidos.NoEncontrado(consulta.PedidoId)
            : PedidoDto.Desde(pedido);
    }
}

public sealed class ManejadorObtenerEstadoPedido(IPedidoRepositorio repositorio, ILectorEstadoSaga lectorSaga)
    : IManejadorConsulta<ObtenerEstadoPedidoConsulta, EstadoPedidoDto>
{
    public async Task<Resultado<EstadoPedidoDto>> ManejarAsync(ObtenerEstadoPedidoConsulta consulta, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(new PedidoId(consulta.PedidoId), cancellationToken);
        if (pedido is null)
        {
            return ErroresPedidos.NoEncontrado(consulta.PedidoId);
        }

        // La Saga puede no existir todavía durante unos instantes: el mensaje de inicio está en el Outbox.
        var saga = await lectorSaga.ObtenerAsync(consulta.PedidoId, cancellationToken);

        return new EstadoPedidoDto(
            pedido.Id.Valor,
            pedido.Estado,
            saga?.Estado,
            saga?.ReservaId,
            saga?.PagoId,
            pedido.MotivoFinalizacion ?? saga?.Motivo,
            saga?.UltimoError,
            saga?.FechaActualizacionUtc);
    }
}

public sealed class ManejadorObtenerHistorialSaga(IPedidoRepositorio repositorio, ILectorHistorialSaga lectorHistorial)
    : IManejadorConsulta<ObtenerHistorialSagaConsulta, IReadOnlyList<TransicionSagaDto>>
{
    public async Task<Resultado<IReadOnlyList<TransicionSagaDto>>> ManejarAsync(
        ObtenerHistorialSagaConsulta consulta, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(new PedidoId(consulta.PedidoId), cancellationToken);
        if (pedido is null)
        {
            return ErroresPedidos.NoEncontrado(consulta.PedidoId);
        }

        // C# no aplica conversiones implícitas desde interfaces: hay que crear el resultado explícitamente.
        var historial = await lectorHistorial.ObtenerAsync(consulta.PedidoId, cancellationToken);
        return Resultado<IReadOnlyList<TransicionSagaDto>>.Correcto(historial);
    }
}
