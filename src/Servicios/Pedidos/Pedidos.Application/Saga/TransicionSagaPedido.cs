namespace Pedidos.Application.Saga;

/// <summary>
/// Una transición de la máquina de estados de la Saga, registrada en la tabla <c>HistorialSaga</c>.
/// </summary>
/// <remarks>
/// La escribe <see cref="ObservadorHistorialSaga"/> en la misma transacción que el estado de la Saga, por lo
/// que el historial siempre es coherente con <c>SagasPedido</c>. Es un registro de solo inserción: nunca se modifica.
/// </remarks>
public sealed class TransicionSagaPedido
{
    private TransicionSagaPedido()
    {
        // Requerido por EF Core.
    }

    public long Id { get; private set; }

    /// <summary>Proceso al que pertenece (coincide con el PedidoId).</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Orden de la transición dentro del proceso (1, 2, 3…). Único por proceso.</summary>
    public int Secuencia { get; private set; }

    /// <summary>Estado de origen; nulo en la primera transición (la Saga venía del estado inicial de MassTransit).</summary>
    public string? EstadoAnterior { get; private set; }

    public string EstadoNuevo { get; private set; } = string.Empty;

    /// <summary>Evento de la máquina de estados que provocó la transición (p. ej. <c>PagoRechazado</c>).</summary>
    public string Evento { get; private set; } = string.Empty;

    public DateTimeOffset FechaUtc { get; private set; }

    public static TransicionSagaPedido Crear(
        Guid correlationId, int secuencia, string? estadoAnterior, string estadoNuevo, string evento, DateTimeOffset fechaUtc) =>
        new()
        {
            CorrelationId = correlationId,
            Secuencia = secuencia,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo,
            Evento = evento,
            FechaUtc = fechaUtc,
        };
}