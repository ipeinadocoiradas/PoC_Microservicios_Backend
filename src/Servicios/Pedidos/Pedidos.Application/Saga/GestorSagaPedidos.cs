using Contratos.Integracion;
using Contratos.Integracion.Comandos;
using Contratos.Integracion.Eventos;
using MassTransit;

namespace Pedidos.Application.Saga;

/// <summary>
/// Máquina de estados que orquesta la compra distribuida (Saga Orchestration).
/// </summary>
/// <remarks>
/// <para>
/// Cada evento recibido se procesa en una transacción local de PedidosDb que incluye: el estado de la Saga,
/// el cambio del agregado Pedido y los comandos salientes (Transactional Outbox). O se persiste todo, o nada.
/// Cada cambio de estado queda registrado en <c>HistorialSaga</c> mediante <see cref="ObservadorHistorialSaga"/>
/// </para>
/// <para><b>Estados</b> (distintos del <c>EstadoPedido</c> de negocio):</para>
/// <list type="table">
///   <item><term>PedidoPendiente</term><description>Estado de entrada: la instancia acaba de crearse con los datos del pedido. Se abandona en la misma transacción al enviar <c>SolicitarReservaStock</c>.</description></item>
///   <item><term>ReservandoStock</term><description>Esperando la respuesta de Almacén (<c>StockReservado</c> o <c>StockNoDisponible</c>).</description></item>
///   <item><term>ProcesandoPago</term><description>Stock reservado; esperando a Pagos (<c>PagoCompletado</c>, <c>PagoRechazado</c> o un fallo técnico).</description></item>
///   <item><term>CompensandoOperaciones</term><description>Se han enviado acciones compensatorias y se espera su confirmación. No se sale de aquí hasta recibirlas todas.</description></item>
///   <item><term>Completado</term><description>Final: pedido cobrado; se ha solicitado confirmar la reserva.</description></item>
///   <item><term>Cancelado</term><description>Final: cancelación por causa de negocio, con compensaciones confirmadas.</description></item>
///   <item><term>Fallido</term><description>Final: fallo técnico no recuperable en el pago; compensaciones confirmadas.</description></item>
/// </list>

/// </remarks>
public sealed class GestorSagaPedidos : MassTransitStateMachine<EstadoSagaPedido>
{
    private const string MotivoNoIndicado = "Motivo no indicado";

    private readonly TimeProvider _reloj;

    public GestorSagaPedidos(TimeProvider reloj)
    {
        _reloj = reloj;

        InstanceState(instancia => instancia.EstadoActual);

        ConfigurarCorrelacion();
        ConfigurarInicio();
        ConfigurarReservaStock();
        ConfigurarProcesamientoPago();
        ConfigurarCompensacion();
        ConfigurarEstadosFinales();

        // Historial de transiciones (tabla HistorialSaga), en la misma transacción que el estado de la Saga.
        ConnectStateObserver(new ObservadorHistorialSaga(reloj));
    }

    // ── Estados ────────────────────────────────────────────────────────────────
    public State PedidoPendiente { get; private set; } = null!;
    public State ReservandoStock { get; private set; } = null!;
    public State ProcesandoPago { get; private set; } = null!;
    public State CompensandoOperaciones { get; private set; } = null!;
    public State Completado { get; private set; } = null!;
    public State Cancelado { get; private set; } = null!;
    public State Fallido { get; private set; } = null!;

    // ── Eventos ────────────────────────────────────────────────────────────────
    public Event<PedidoRegistrado> PedidoRegistrado { get; private set; } = null!;
    public Event<StockReservado> StockReservado { get; private set; } = null!;
    public Event<StockNoDisponible> StockNoDisponible { get; private set; } = null!;
    public Event<ReservaLiberada> ReservaLiberada { get; private set; } = null!;
    public Event<ReservaConfirmada> ReservaConfirmada { get; private set; } = null!;
    public Event<PagoCompletado> PagoCompletado { get; private set; } = null!;
    public Event<PagoRechazado> PagoRechazado { get; private set; } = null!;
    public Event<PagoCancelado> PagoCancelado { get; private set; } = null!;

    /// <summary>Pagos agotó los reintentos de <c>ProcesarPago</c>: no se sabe si llegó a cobrar.</summary>
    public Event<Fault<ProcesarPago>> ProcesamientoPagoFallido { get; private set; } = null!;

    /// <summary>Almacén agotó los reintentos de la compensación: el mensaje está en la cola de errores.</summary>
    public Event<Fault<LiberarReservaStock>> LiberacionReservaFallida { get; private set; } = null!;

    /// <summary>Pagos agotó los reintentos de la compensación: el mensaje está en la cola de errores.</summary>
    public Event<Fault<CancelarPago>> CancelacionPagoFallida { get; private set; } = null!;

    private DateTimeOffset Ahora => _reloj.GetUtcNow();

    private void ConfigurarCorrelacion()
    {
        // Todos los mensajes llevan el CorrelationId del proceso; es lo que enlaza la respuesta con su instancia.
        Event(() => PedidoRegistrado, evento => evento.CorrelateById(contexto => contexto.Message.CorrelationId));

        Event(() => StockReservado, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));
        Event(() => StockNoDisponible, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));
        Event(() => ReservaLiberada, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));
        Event(() => ReservaConfirmada, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));
        Event(() => PagoCompletado, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));
        Event(() => PagoRechazado, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));
        Event(() => PagoCancelado, evento => CorrelacionarExistente(evento, contexto => contexto.Message.CorrelationId));

        Event(() => ProcesamientoPagoFallido, evento => CorrelacionarExistente(evento, contexto => contexto.Message.Message.CorrelationId));
        Event(() => LiberacionReservaFallida, evento => CorrelacionarExistente(evento, contexto => contexto.Message.Message.CorrelationId));
        Event(() => CancelacionPagoFallida, evento => CorrelacionarExistente(evento, contexto => contexto.Message.Message.CorrelationId));
    }

    private void ConfigurarInicio()
    {
        Initially(
            When(PedidoRegistrado)
                .Then(contexto => contexto.Saga.Iniciar(contexto.Message, Ahora))
                .TransitionTo(PedidoPendiente)
                .Send(ColasIntegracion.DireccionComandosAlmacen, contexto => new SolicitarReservaStock(
                    contexto.Saga.CorrelationId,
                    contexto.Message.PedidoId,
                    contexto.Message.Lineas))
                .TransitionTo(ReservandoStock));

        // Un PedidoRegistrado duplicado (reentrega del Outbox) no debe reiniciar un proceso en curso.
        DuringAny(Ignore(PedidoRegistrado));
    }

    private void ConfigurarReservaStock()
    {
        During(ReservandoStock,
            When(StockReservado)
                .Then(contexto =>
                {
                    contexto.Saga.ReservaId = contexto.Message.ReservaId;
                    contexto.Saga.FechaActualizacionUtc = Ahora;
                })
                .Send(ColasIntegracion.DireccionComandosPagos, contexto => new ProcesarPago(
                    contexto.Saga.CorrelationId,
                    contexto.Saga.CorrelationId,
                    contexto.Saga.ClienteId,
                    contexto.Saga.Importe,
                    contexto.Saga.Moneda))
                .TransitionTo(ProcesandoPago),

            // Escenario B: no se reservó nada, así que no hay nada que compensar y el pago nunca se inicia.
            When(StockNoDisponible)
                .Then(contexto => RegistrarMotivo(contexto.Saga, $"Stock no disponible: {contexto.Message.Motivo}"))
                .ThenAsync(contexto => ActualizarPedidoAsync(contexto,
                    (servicio, pedidoId, cancelacion) => servicio.CancelarAsync(pedidoId, contexto.Saga.MotivoFinalizacion!, cancelacion)))
                .TransitionTo(Cancelado));
    }

    private void ConfigurarProcesamientoPago()
    {
        During(ProcesandoPago,
            // Escenario A: pago cobrado → el pedido se completa y se pide a Almacén consolidar la reserva.
            When(PagoCompletado)
                .Then(contexto =>
                {
                    contexto.Saga.PagoId = contexto.Message.PagoId;
                    contexto.Saga.FechaActualizacionUtc = Ahora;
                })
                .ThenAsync(contexto => ActualizarPedidoAsync(contexto,
                    (servicio, pedidoId, cancelacion) => servicio.CompletarAsync(pedidoId, cancelacion)))
                .Send(ColasIntegracion.DireccionComandosAlmacen, contexto => new ConfirmarReservaStock(
                    contexto.Saga.CorrelationId,
                    contexto.Saga.CorrelationId))
                .TransitionTo(Completado),

            // Escenario C: rechazo de negocio → compensar la reserva de stock.
            When(PagoRechazado)
                .Then(contexto => contexto.Saga.IniciarCompensacion(
                    $"Pago rechazado: {contexto.Message.Motivo}",
                    contexto.Message.PagoId,
                    requiereCancelacionPago: false,
                    Ahora))
                .Send(ColasIntegracion.DireccionComandosAlmacen, contexto => CrearLiberacionReserva(contexto.Saga))
                .TransitionTo(CompensandoOperaciones),

            // Fallo técnico: se desconoce si el cargo llegó a producirse, así que se compensan ambos servicios.
            When(ProcesamientoPagoFallido)
                .Then(contexto => contexto.Saga.IniciarCompensacion(
                    $"Fallo técnico procesando el pago: {DescribirFallo(contexto.Message)}",
                    pagoId: null,
                    requiereCancelacionPago: true,
                    Ahora))
                .Send(ColasIntegracion.DireccionComandosPagos, contexto => new CancelarPago(
                    contexto.Saga.CorrelationId,
                    contexto.Saga.CorrelationId,
                    contexto.Saga.ClienteId,
                    contexto.Saga.Importe,
                    contexto.Saga.Moneda,
                    contexto.Saga.MotivoFinalizacion!))
                .Send(ColasIntegracion.DireccionComandosAlmacen, contexto => CrearLiberacionReserva(contexto.Saga))
                .TransitionTo(CompensandoOperaciones),

            Ignore(StockReservado));
    }

    private void ConfigurarCompensacion()
    {
        // Escenario D: la Saga permanece aquí hasta recibir la confirmación de TODAS las compensaciones.
        // Si Almacén está caído, LiberarReservaStock espera en su cola duradera; al volver, se procesa.
        During(CompensandoOperaciones,
            When(ReservaLiberada)
                .Then(contexto =>
                {
                    contexto.Saga.LiberacionReservaConfirmada = true;
                    contexto.Saga.FechaActualizacionUtc = Ahora;
                })
                .If(contexto => contexto.Saga.CompensacionFinalizada, binder => FinalizarCompensacion(binder)),

            When(PagoCancelado)
                .Then(contexto =>
                {
                    contexto.Saga.CancelacionPagoConfirmada = true;
                    contexto.Saga.FechaActualizacionUtc = Ahora;
                })
                .If(contexto => contexto.Saga.CompensacionFinalizada, binder => FinalizarCompensacion(binder)),

            // La compensación agotó sus reintentos y está en la DLQ: se registra, pero NO se finaliza la Saga.
            // Un operador puede reinyectar el mensaje desde la cola _error cuando el problema se resuelva.
            When(LiberacionReservaFallida)
                .Then(contexto => contexto.Saga.RegistrarFalloCompensacion(
                    $"LiberarReservaStock en cola de errores: {DescribirFallo(contexto.Message)}", Ahora)),

            When(CancelacionPagoFallida)
                .Then(contexto => contexto.Saga.RegistrarFalloCompensacion(
                    $"CancelarPago en cola de errores: {DescribirFallo(contexto.Message)}", Ahora)),

            Ignore(StockReservado),
            Ignore(PagoRechazado),
            Ignore(PagoCompletado),
            Ignore(ProcesamientoPagoFallido));
    }

    private void ConfigurarEstadosFinales()
    {
        During(Completado,
            When(ReservaConfirmada)
                .Then(contexto =>
                {
                    contexto.Saga.ReservaConfirmadaEnAlmacen = true;
                    contexto.Saga.FechaActualizacionUtc = Ahora;
                }),
            Ignore(StockReservado),
            Ignore(PagoCompletado));

        // Los estados finales absorben duplicados y respuestas tardías sin efectos secundarios.
        During(Cancelado, Fallido,
            Ignore(StockReservado),
            Ignore(StockNoDisponible),
            Ignore(ReservaLiberada),
            Ignore(PagoCompletado),
            Ignore(PagoRechazado),
            Ignore(PagoCancelado),
            Ignore(ProcesamientoPagoFallido),
            Ignore(LiberacionReservaFallida),
            Ignore(CancelacionPagoFallida));
    }

    private EventActivityBinder<EstadoSagaPedido, TMensaje> FinalizarCompensacion<TMensaje>(
        EventActivityBinder<EstadoSagaPedido, TMensaje> binder)
        where TMensaje : class =>
        binder.IfElse(contexto => contexto.Saga.RequiereCancelacionPago,
            fallo => fallo
                .ThenAsync(contexto => ActualizarPedidoAsync(contexto,
                    (servicio, pedidoId, cancelacion) => servicio.MarcarFallidoAsync(pedidoId, contexto.Saga.MotivoFinalizacion ?? MotivoNoIndicado, cancelacion)))
                .TransitionTo(Fallido),
            cancelacion => cancelacion
                .ThenAsync(contexto => ActualizarPedidoAsync(contexto,
                    (servicio, pedidoId, token) => servicio.CancelarAsync(pedidoId, contexto.Saga.MotivoFinalizacion ?? MotivoNoIndicado, token)))
                .TransitionTo(Cancelado));

    private static LiberarReservaStock CrearLiberacionReserva(EstadoSagaPedido saga) =>
        new(saga.CorrelationId, saga.CorrelationId, saga.MotivoFinalizacion ?? MotivoNoIndicado);

    /// <summary>
    /// Resuelve <see cref="IServicioEstadoPedido"/> desde el ámbito del mensaje en curso, de modo que comparte
    /// el <c>DbContext</c> (y la transacción) con el repositorio de la Saga.
    /// </summary>
    private static Task ActualizarPedidoAsync(
        BehaviorContext<EstadoSagaPedido> contexto,
        Func<IServicioEstadoPedido, Guid, CancellationToken, Task> accion) =>
        accion(contexto.GetServiceOrCreateInstance<IServicioEstadoPedido>(), contexto.Saga.CorrelationId, contexto.CancellationToken);

    private void RegistrarMotivo(EstadoSagaPedido saga, string motivo)
    {
        saga.MotivoFinalizacion = motivo;
        saga.FechaActualizacionUtc = Ahora;
    }

    private static void CorrelacionarExistente<TMensaje>(
        IEventCorrelationConfigurator<EstadoSagaPedido, TMensaje> evento,
        Func<ConsumeContext<TMensaje>, Guid> selector)
        where TMensaje : class
    {
        evento.CorrelateById(selector);

        // Un evento sin instancia (p. ej. de un proceso ya purgado) se descarta en lugar de ir a la cola de errores.
        evento.OnMissingInstance(instancia => instancia.Discard());
    }

    private static string DescribirFallo<TMensaje>(Fault<TMensaje> fallo)
        where TMensaje : class =>
        fallo.Exceptions?.FirstOrDefault()?.Message ?? "error no especificado";
}
