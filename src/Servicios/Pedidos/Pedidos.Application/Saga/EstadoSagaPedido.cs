using MassTransit;

namespace Pedidos.Application.Saga;

/// <summary>
/// Estado persistido de una instancia de la Saga (una por pedido). Se guarda en la tabla <c>SagasPedido</c>
/// de PedidosDb, de modo que el proceso sobrevive a reinicios del servicio.
/// </summary>
/// <remarks>
/// MassTransit exige propiedades públicas con setter para materializar y actualizar la instancia.
/// La lógica de negocio de las transiciones vive en <see cref="GestorSagaPedidos"/>; aquí solo hay datos
/// y pequeñas operaciones que mantienen coherentes los indicadores de compensación.
/// </remarks>
public class EstadoSagaPedido : SagaStateMachineInstance
{
    /// <summary>Identificador del proceso (coincide con el PedidoId).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Nombre del estado actual de la máquina (p. ej. <c>ProcesandoPago</c>).</summary>
    public string EstadoActual { get; set; } = string.Empty;

    public Guid ClienteId { get; set; }

    public decimal Importe { get; set; }

    public string Moneda { get; set; } = string.Empty;

    public Guid? ReservaId { get; set; }

    public Guid? PagoId { get; set; }

    /// <summary>
    /// Se activa cuando falla técnicamente el procesamiento del pago: no se sabe si llegó a cobrarse,
    /// por lo que la compensación debe incluir <c>CancelarPago</c> además de liberar el stock.
    /// </summary>
    public bool RequiereCancelacionPago { get; set; }

    public bool LiberacionReservaConfirmada { get; set; }

    public bool CancelacionPagoConfirmada { get; set; }

    public bool ReservaConfirmadaEnAlmacen { get; set; }

    public string? MotivoFinalizacion { get; set; }

    /// <summary>Último error técnico recibido (p. ej. una compensación enviada a la cola de errores).</summary>
    public string? UltimoError { get; set; }

    public int FallosCompensacion { get; set; }

    public DateTimeOffset FechaInicioUtc { get; set; }

    public DateTimeOffset FechaActualizacionUtc { get; set; }

    /// <summary>Token de concurrencia optimista (rowversion): evita que dos eventos simultáneos se pisen.</summary>
    public byte[] Version { get; set; } = [];

    /// <summary>La compensación termina solo cuando se han confirmado todas las acciones compensatorias.</summary>
    public bool CompensacionFinalizada =>
        LiberacionReservaConfirmada && (!RequiereCancelacionPago || CancelacionPagoConfirmada);

    /// <summary>Número de transiciones registradas en <c>HistorialSaga</c>; sirve para numerarlas.</summary>
    public int NumeroTransiciones { get; set; }

    internal void Iniciar(PedidoRegistrado mensaje, DateTimeOffset ahora)
    {
        ClienteId = mensaje.ClienteId;
        Importe = mensaje.Importe;
        Moneda = mensaje.Moneda;
        FechaInicioUtc = ahora;
        FechaActualizacionUtc = ahora;
    }

    internal void IniciarCompensacion(string motivo, Guid? pagoId, bool requiereCancelacionPago, DateTimeOffset ahora)
    {
        MotivoFinalizacion = motivo;
        PagoId = pagoId ?? PagoId;
        RequiereCancelacionPago = requiereCancelacionPago;
        LiberacionReservaConfirmada = false;
        CancelacionPagoConfirmada = false;
        FechaActualizacionUtc = ahora;
    }

    internal void RegistrarFalloCompensacion(string error, DateTimeOffset ahora)
    {
        UltimoError = error;
        FallosCompensacion++;
        FechaActualizacionUtc = ahora;
    }

    internal int SiguienteSecuencia() => ++NumeroTransiciones;
}
