using Contratos.Integracion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Abstracciones;
using Pedidos.Application.ConsultarPedido;
using Pedidos.Application.Saga;
using Pedidos.Domain;

namespace Pedidos.Infrastructure.Persistencia;

internal sealed class RepositorioPedidos(PedidosDbContext contexto) : IPedidoRepositorio
{
    public Task<Pedido?> ObtenerPorIdAsync(PedidoId id, CancellationToken cancellationToken) =>
        contexto.Pedidos.FirstOrDefaultAsync(pedido => pedido.Id == id, cancellationToken);

    public void Agregar(Pedido pedido) => contexto.Pedidos.Add(pedido);
}

internal sealed class LectorEstadoSaga(PedidosDbContext contexto) : ILectorEstadoSaga
{
    public Task<InstantaneaSaga?> ObtenerAsync(Guid pedidoId, CancellationToken cancellationToken) =>
        contexto.SagasPedido
            .AsNoTracking()
            .Where(saga => saga.CorrelationId == pedidoId)
            .Select(saga => new InstantaneaSaga(
                saga.EstadoActual,
                saga.ReservaId,
                saga.PagoId,
                saga.MotivoFinalizacion,
                saga.UltimoError,
                saga.FechaActualizacionUtc))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Publica <see cref="PedidoRegistrado"/> a través del <b>Bus Outbox</b>: fuera de un consumidor,
/// el <see cref="IPublishEndpoint"/> con ámbito no envía a RabbitMQ sino que inserta el mensaje en
/// <c>BandejaSalidaMensajes</c> del mismo <c>DbContext</c>. Un servicio en segundo plano lo entrega después
/// de que la transacción se haya confirmado.
/// </summary>
internal sealed class IniciadorProcesoPedido(IPublishEndpoint publicador) : IIniciadorProcesoPedido
{
    public Task IniciarAsync(Pedido pedido, CancellationToken cancellationToken) =>
        publicador.Publish(
            new PedidoRegistrado(
                CorrelationId: pedido.Id.Valor,
                PedidoId: pedido.Id.Valor,
                ClienteId: pedido.ClienteId,
                Importe: pedido.Total,
                Moneda: pedido.Moneda,
                Lineas: [.. pedido.Lineas.Select(linea => new LineaReserva(linea.ProductoId, linea.Cantidad))]),
            cancellationToken);
}

internal sealed class RegistroHistorialSaga(PedidosDbContext contexto) : IRegistroHistorialSaga
{
    // Solo añade al contexto: lo confirma el repositorio de la Saga en su SaveChanges.
    public void Registrar(TransicionSagaPedido transicion) => contexto.HistorialSaga.Add(transicion);
}

internal sealed class LectorHistorialSaga(PedidosDbContext contexto) : ILectorHistorialSaga
{
    public async Task<IReadOnlyList<TransicionSagaDto>> ObtenerAsync(Guid pedidoId, CancellationToken cancellationToken) =>
        await contexto.HistorialSaga
            .AsNoTracking()
            .Where(transicion => transicion.CorrelationId == pedidoId)
            .OrderBy(transicion => transicion.Secuencia)
            .Select(transicion => new TransicionSagaDto(
                transicion.Secuencia, transicion.EstadoAnterior, transicion.EstadoNuevo, transicion.Evento, transicion.FechaUtc))
            .ToListAsync(cancellationToken);
}
