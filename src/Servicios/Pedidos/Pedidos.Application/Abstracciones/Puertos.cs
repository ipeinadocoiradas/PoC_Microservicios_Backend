using Pedidos.Domain;
using Pedidos.Application.ConsultarPedido;
using Pedidos.Application.Saga;

namespace Pedidos.Application.Abstracciones;

/// <summary>Acceso persistente al agregado <see cref="Pedido"/>.</summary>
public interface IPedidoRepositorio
{
    Task<Pedido?> ObtenerPorIdAsync(PedidoId id, CancellationToken cancellationToken);

    void Agregar(Pedido pedido);
}

/// <summary>
/// Confirma en una única transacción local los cambios pendientes: el pedido, el estado de la Saga
/// y los mensajes del Outbox comparten el mismo <c>DbContext</c>.
/// </summary>
public interface IUnidadTrabajo
{
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Arranca el proceso distribuido de un pedido recién creado. La implementación escribe el mensaje en el
/// Outbox, de modo que solo se publicará si el pedido se guarda correctamente.
/// </summary>
public interface IIniciadorProcesoPedido
{
    Task IniciarAsync(Pedido pedido, CancellationToken cancellationToken);
}

/// <summary>Lectura del estado interno de la Saga (solo para consultas).</summary>
public interface ILectorEstadoSaga
{
    Task<InstantaneaSaga?> ObtenerAsync(Guid pedidoId, CancellationToken cancellationToken);
}

/// <summary>Escritura del historial de la Saga. La implementación lo añade a la transacción en curso.</summary>
public interface IRegistroHistorialSaga
{
    void Registrar(TransicionSagaPedido transicion);
}

/// <summary>Lectura del historial de la Saga (solo para consultas).</summary>
public interface ILectorHistorialSaga
{
    Task<IReadOnlyList<TransicionSagaDto>> ObtenerAsync(Guid pedidoId, CancellationToken cancellationToken);
}

/// <summary>Proyección de solo lectura del estado de la Saga de un pedido.</summary>
public sealed record InstantaneaSaga(
    string Estado,
    Guid? ReservaId,
    Guid? PagoId,
    string? Motivo,
    string? UltimoError,
    DateTimeOffset FechaActualizacionUtc);
