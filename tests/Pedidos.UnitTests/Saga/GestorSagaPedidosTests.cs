using Contratos.Integracion;
using Contratos.Integracion.Comandos;
using Contratos.Integracion.Eventos;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Application.Abstracciones;
using Pedidos.Application.Saga;

namespace Pedidos.UnitTests.Saga;

/// <summary>
/// Pruebas de las transiciones de la máquina de estados con el arnés en memoria de MassTransit.
/// El agregado Pedido se sustituye por un doble (<see cref="IServicioEstadoPedido"/>) para aislar la orquestación.
/// </summary>
public sealed class GestorSagaPedidosTests : IAsyncLifetime
{
    private readonly IServicioEstadoPedido _servicioEstado = Substitute.For<IServicioEstadoPedido>();
    private ServiceProvider _proveedor = null!;
    private ITestHarness _arnes = null!;
    private ISagaStateMachineTestHarness<GestorSagaPedidos, EstadoSagaPedido> _arnesSaga = null!;
    private readonly RegistroHistorialEnMemoria _historial = new();

    public async Task InitializeAsync()
    {
        _proveedor = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(_servicioEstado)
            .AddSingleton<IRegistroHistorialSaga>(_historial)
            .AddMassTransitTestHarness(configuracion =>
            {
                configuracion.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(1));
                configuracion.AddSagaStateMachine<GestorSagaPedidos, EstadoSagaPedido>();
            })
            .BuildServiceProvider(validateScopes: true);

        _arnes = _proveedor.GetRequiredService<ITestHarness>();
        await _arnes.Start();
        _arnesSaga = _arnes.GetSagaStateMachineHarness<GestorSagaPedidos, EstadoSagaPedido>();
    }

    public async Task DisposeAsync()
    {
        await _arnes.Stop();
        await _proveedor.DisposeAsync();
    }

    [Fact]
    public async Task PedidoRegistrado_CreaLaSagaYSolicitaLaReserva()
    {
        var pedidoId = await IniciarSagaAsync();

        Assert.True(await _arnes.Sent.Any<SolicitarReservaStock>(mensaje =>
            mensaje.Context.Message.PedidoId == pedidoId && mensaje.Context.Message.CorrelationId == pedidoId));

        var instancia = _arnesSaga.Sagas.ContainsInState(pedidoId, _arnesSaga.StateMachine, _arnesSaga.StateMachine.ReservandoStock);
        Assert.NotNull(instancia);
        Assert.Equal(1_200m, instancia.Importe);
    }

    [Fact]
    public async Task EscenarioA_FlujoSatisfactorio_TerminaCompletadoYConfirmaLaReserva()
    {
        var pedidoId = await IniciarSagaAsync();

        await _arnes.Bus.Publish(new StockReservado(pedidoId, pedidoId, Guid.NewGuid()));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.ProcesandoPago));
        Assert.True(await _arnes.Sent.Any<ProcesarPago>(mensaje => mensaje.Context.Message.PedidoId == pedidoId));

        await _arnes.Bus.Publish(new PagoCompletado(pedidoId, pedidoId, Guid.NewGuid(), 1_200m, "EUR"));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.Completado));
        Assert.True(await _arnes.Sent.Any<ConfirmarReservaStock>(mensaje => mensaje.Context.Message.PedidoId == pedidoId));

        await _servicioEstado.Received(1).CompletarAsync(pedidoId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscenarioB_StockNoDisponible_CancelaSinIniciarElPago()
    {
        var pedidoId = await IniciarSagaAsync();

        await _arnes.Bus.Publish(new StockNoDisponible(pedidoId, pedidoId, "Sin existencias"));

        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.Cancelado));
        Assert.False(await _arnes.Sent.Any<ProcesarPago>(mensaje => mensaje.Context.Message.PedidoId == pedidoId));
        await _servicioEstado.Received(1).CancelarAsync(pedidoId, Arg.Is<string>(motivo => motivo.Contains("Sin existencias")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscenarioC_PagoRechazado_CompensaYSoloCancelaTrasConfirmarLaLiberacion()
    {
        var pedidoId = await LlevarAProcesandoPagoAsync();

        await _arnes.Bus.Publish(new PagoRechazado(pedidoId, pedidoId, Guid.NewGuid(), "Fondos insuficientes"));

        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.CompensandoOperaciones));
        Assert.True(await _arnes.Sent.Any<LiberarReservaStock>(mensaje => mensaje.Context.Message.PedidoId == pedidoId));
        await _servicioEstado.DidNotReceiveWithAnyArgs().CancelarAsync(default, default!, default);

        await _arnes.Bus.Publish(new ReservaLiberada(pedidoId, pedidoId, Guid.NewGuid()));

        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.Cancelado));
        await _servicioEstado.Received(1).CancelarAsync(pedidoId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscenarioD_FalloDeLaCompensacion_MantieneLaSagaCompensando()
    {
        var pedidoId = await LlevarAProcesandoPagoAsync();
        await _arnes.Bus.Publish(new PagoRechazado(pedidoId, pedidoId, Guid.NewGuid(), "Fondos insuficientes"));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.CompensandoOperaciones));

        await _arnes.Bus.Publish<Fault<LiberarReservaStock>>(new
        {
            Message = new LiberarReservaStock(pedidoId, pedidoId, "Pago rechazado"),
            Exceptions = new[] { new { Message = "Almacén no disponible", ExceptionType = "System.TimeoutException" } }
        });

        Assert.True(await _arnesSaga.Consumed.Any<Fault<LiberarReservaStock>>());
        var instancia = _arnesSaga.Sagas.ContainsInState(pedidoId, _arnesSaga.StateMachine, _arnesSaga.StateMachine.CompensandoOperaciones);
        Assert.NotNull(instancia);
        Assert.Equal(1, instancia.FallosCompensacion);
        Assert.Contains("Almacén no disponible", instancia.UltimoError);
    }

    [Fact]
    public async Task FalloTecnicoDelPago_CompensaAmbosServiciosYTerminaFallido()
    {
        var pedidoId = await LlevarAProcesandoPagoAsync();

        await _arnes.Bus.Publish<Fault<ProcesarPago>>(new
        {
            Message = new ProcesarPago(pedidoId, pedidoId, Guid.NewGuid(), 1_200m, "EUR"),
            Exceptions = new[] { new { Message = "Pasarela caída", ExceptionType = "System.Exception" } }
        });

        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.CompensandoOperaciones));
        Assert.True(await _arnes.Sent.Any<CancelarPago>(mensaje => mensaje.Context.Message.PedidoId == pedidoId));
        Assert.True(await _arnes.Sent.Any<LiberarReservaStock>(mensaje => mensaje.Context.Message.PedidoId == pedidoId));

        // Con solo una de las dos confirmaciones, la compensación no ha terminado.
        await _arnes.Bus.Publish(new ReservaLiberada(pedidoId, pedidoId, Guid.NewGuid()));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.CompensandoOperaciones));

        await _arnes.Bus.Publish(new PagoCancelado(pedidoId, pedidoId, Guid.NewGuid(), "Cancelado"));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.Fallido));
        await _servicioEstado.Received(1).MarcarFallidoAsync(pedidoId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EventoDuplicado_EnEstadoFinal_SeIgnoraSinEfectos()
    {
        var pedidoId = await LlevarAProcesandoPagoAsync();
        await _arnes.Bus.Publish(new PagoCompletado(pedidoId, pedidoId, Guid.NewGuid(), 1_200m, "EUR"));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.Completado));

        await _arnes.Bus.Publish(new PagoCompletado(pedidoId, pedidoId, Guid.NewGuid(), 1_200m, "EUR"));
        await _arnes.Bus.Publish(new StockReservado(pedidoId, pedidoId, Guid.NewGuid()));

        await _arnes.InactivityTask;
        Assert.NotNull(_arnesSaga.Sagas.ContainsInState(pedidoId, _arnesSaga.StateMachine, _arnesSaga.StateMachine.Completado));
        await _servicioEstado.Received(1).CompletarAsync(pedidoId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Historial_RegistraCadaTransicionConSuEventoYSecuencia()
    {
        var pedidoId = await LlevarAProcesandoPagoAsync();
        await _arnes.Bus.Publish(new PagoCompletado(pedidoId, pedidoId, Guid.NewGuid(), 1_200m, "EUR"));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.Completado));

        var historial = _historial.De(pedidoId);

        Assert.Equal(new[] { 1, 2, 3, 4 }, historial.Select(t => t.Secuencia));
        Assert.Equal(new string?[] { null, "PedidoPendiente", "ReservandoStock", "ProcesandoPago" }, historial.Select(t => t.EstadoAnterior));
        Assert.Equal(new[] { "PedidoPendiente", "ReservandoStock", "ProcesandoPago", "Completado" }, historial.Select(t => t.EstadoNuevo));
        Assert.Equal(new[] { "PedidoRegistrado", "PedidoRegistrado", "StockReservado", "PagoCompletado" }, historial.Select(t => t.Evento));
    }

    private async Task<Guid> IniciarSagaAsync()
    {
        var pedidoId = Guid.NewGuid();

        await _arnes.Bus.Publish(new PedidoRegistrado(
            pedidoId, pedidoId, Guid.NewGuid(), 1_200m, "EUR",
            [new LineaReserva(Guid.NewGuid(), 2)]));

        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.ReservandoStock));
        return pedidoId;
    }

    private async Task<Guid> LlevarAProcesandoPagoAsync()
    {
        var pedidoId = await IniciarSagaAsync();
        await _arnes.Bus.Publish(new StockReservado(pedidoId, pedidoId, Guid.NewGuid()));
        Assert.NotNull(await _arnesSaga.Exists(pedidoId, maquina => maquina.ProcesandoPago));
        return pedidoId;
    }
}
