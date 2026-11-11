using Microsoft.Extensions.Logging;
using Pedidos.Application.Abstracciones;
using Pedidos.Domain;

namespace Pedidos.Application.Saga;

/// <summary>
/// Aplica al agregado <see cref="Pedido"/> las decisiones tomadas por la Saga.
/// </summary>
/// <remarks>
/// Es la frontera entre la orquestación (Saga) y el dominio: la Saga decide <i>cuándo</i>, el agregado
/// valida <i>si</i> la transición es posible. Existe como interfaz para poder probar la máquina de estados
/// de forma aislada con un doble de prueba.
/// </remarks>
public interface IServicioEstadoPedido
{
    Task CompletarAsync(Guid pedidoId, CancellationToken cancellationToken);

    Task CancelarAsync(Guid pedidoId, string motivo, CancellationToken cancellationToken);

    Task MarcarFallidoAsync(Guid pedidoId, string motivo, CancellationToken cancellationToken);
}

/// <inheritdoc />
/// <remarks>
/// No llama a <c>SaveChanges</c>: el repositorio de la Saga comparte el mismo <c>DbContext</c> y confirma
/// en una sola transacción el estado de la Saga, el cambio del pedido y los mensajes del Outbox.
/// </remarks>
public sealed class ServicioEstadoPedido(
    IPedidoRepositorio repositorio,
    TimeProvider reloj,
    ILogger<ServicioEstadoPedido> registro) : IServicioEstadoPedido
{
    public Task CompletarAsync(Guid pedidoId, CancellationToken cancellationToken) =>
        AplicarAsync(pedidoId, (pedido, ahora) => pedido.Completar(ahora), cancellationToken);

    public Task CancelarAsync(Guid pedidoId, string motivo, CancellationToken cancellationToken) =>
        AplicarAsync(pedidoId, (pedido, ahora) => pedido.Cancelar(motivo, ahora), cancellationToken);

    public Task MarcarFallidoAsync(Guid pedidoId, string motivo, CancellationToken cancellationToken) =>
        AplicarAsync(pedidoId, (pedido, ahora) => pedido.MarcarFallido(motivo, ahora), cancellationToken);

    private async Task AplicarAsync(Guid pedidoId, Action<Pedido, DateTimeOffset> transicion, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(new PedidoId(pedidoId), cancellationToken)
            ?? throw new InvalidOperationException($"La Saga referencia el pedido {pedidoId}, que no existe.");

        if (pedido.EstaFinalizado)
        {
            // Idempotencia: un reintento tras un fallo al confirmar no debe intentar finalizarlo dos veces.
            registro.LogInformation("El pedido {PedidoId} ya estaba {Estado}; no se modifica", pedidoId, pedido.Estado);
            return;
        }

        transicion(pedido, reloj.GetUtcNow());
        registro.LogInformation("Pedido {PedidoId} pasa a {Estado}", pedidoId, pedido.Estado);
    }
}
