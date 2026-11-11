using Almacen.Domain;
using Almacen.Infrastructure.Persistencia;
using Almacen.Infrastructure.Simulacion;
using Contratos.Integracion;
using Contratos.Integracion.Comandos;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Pagos.Domain;
using Pedidos.Domain;
using Saga.IntegrationTests.Infraestructura;

namespace Saga.IntegrationTests;

/// <summary>
/// Idempotencia, reintentos, persistencia de la Saga, caídas de servicios y el escenario D
/// (fallo durante una compensación y su recuperación).
/// </summary>
[Collection(ColeccionIntegracion.Nombre)]
public sealed class ResilienciaTests(EntornoIntegracion entorno) : PruebaIntegracionBase(entorno)
{
    private static readonly Guid Teclado = ProductosDemostracion.TecladoMecanico;
    private static readonly Guid Monitor = ProductosDemostracion.Monitor4K;

    /// <summary>Importe que la pasarela simulada rechaza (supera 1.000 €).</summary>
    private const decimal PrecioMonitorRechazado = 600m;

    [Fact]
    public async Task MensajesDuplicados_NoDuplicanReservasPagosNiLiberaciones()
    {
        var (pedidos, almacen, pagos) = await IniciarTodosAsync();

        var pedidoCompletado = await CrearPedidoAsync(pedidos, Teclado, cantidad: 1, precioUnitario: 25m);
        var pedidoCancelado = await CrearPedidoAsync(pedidos, Monitor, cantidad: 2, PrecioMonitorRechazado);
        await EsperarEstadoSagaAsync(pedidoCompletado, EstadoCompletado);
        await EsperarEstadoSagaAsync(pedidoCancelado, EstadoCancelado);
        await Sondeo.EsperarAsync(async () => (await LeerReservaAsync(pedidoCompletado))!.Estado == EstadoReserva.Confirmada, "reserva confirmada");

        var teclado = await LeerProductoAsync(Teclado);
        var monitor = await LeerProductoAsync(Monitor);

        var observadorAlmacen = new ObservadorConsumo();
        var observadorPagos = new ObservadorConsumo();
        almacen.Bus.ConnectConsumeObserver(observadorAlmacen);
        pagos.Bus.ConnectConsumeObserver(observadorPagos);

        // Se reenvían comandos ya procesados con MessageId nuevo: el Inbox no los reconoce,
        // así que es la idempotencia de negocio (y las restricciones únicas) la que los neutraliza.
        var colaAlmacen = await pedidos.Bus.GetSendEndpoint(ColasIntegracion.DireccionComandosAlmacen);
        var colaPagos = await pedidos.Bus.GetSendEndpoint(ColasIntegracion.DireccionComandosPagos);

        await colaAlmacen.Send(new SolicitarReservaStock(pedidoCompletado, pedidoCompletado, [new LineaReserva(Teclado, 1)]));
        await colaPagos.Send(new ProcesarPago(pedidoCompletado, pedidoCompletado, Guid.NewGuid(), 25m, "EUR"));
        await colaAlmacen.Send(new LiberarReservaStock(pedidoCancelado, pedidoCancelado, "Duplicado"));
        await colaAlmacen.Send(new LiberarReservaStock(pedidoCancelado, pedidoCancelado, "Duplicado"));

        await Sondeo.EsperarAsync(() => Task.FromResult(
                observadorAlmacen.Consumidos<SolicitarReservaStock>() >= 1
                && observadorAlmacen.Consumidos<LiberarReservaStock>() >= 2
                && observadorPagos.Consumidos<ProcesarPago>() >= 1),
            "duplicados consumidos");

        Assert.Single(await LeerReservasAsync(pedidoCompletado));
        Assert.Single(await LeerReservasAsync(pedidoCancelado));
        Assert.Equal(EstadoPago.Completado, Assert.Single(await LeerPagosAsync(pedidoCompletado)).Estado);

        var tecladoFinal = await LeerProductoAsync(Teclado);
        var monitorFinal = await LeerProductoAsync(Monitor);
        Assert.Equal(teclado.StockFisico, tecladoFinal.StockFisico);
        Assert.Equal(teclado.StockReservado, tecladoFinal.StockReservado);
        Assert.Equal(monitor.StockReservado, monitorFinal.StockReservado);

        Assert.Equal(EstadoCompletado, (await LeerSagaAsync(pedidoCompletado))!.EstadoActual);
        Assert.Equal(EstadoCancelado, (await LeerSagaAsync(pedidoCancelado))!.EstadoActual);
    }

    [Fact]
    public async Task Reintentos_UnFalloTransitorioEnLaCompensacionSeSuperaConBackoff()
    {
        var pedidos = await IniciarPedidosAsync();
        var almacen = await IniciarAlmacenAsync(fallosAntesDeLiberar: 2);
        await IniciarPagosAsync();

        var pedidoId = await CrearPedidoAsync(pedidos, Monitor, cantidad: 2, PrecioMonitorRechazado);

        await EsperarEstadoSagaAsync(pedidoId, EstadoCancelado);

        // Dos intentos fallidos + uno correcto, dentro del mismo ciclo de reintentos (sin pasar por la DLQ).
        var simulador = almacen.Servicios.GetRequiredService<SimuladorFallosTransitorios>();
        Assert.Equal(3, simulador.ObtenerIntentos(pedidoId));
        Assert.Equal(0, (await LeerSagaAsync(pedidoId))!.FallosCompensacion);
        Assert.Equal(EstadoReserva.Liberada, (await LeerReservaAsync(pedidoId))!.Estado);
    }

    [Fact]
    public async Task PersistenciaDeLaSaga_SobreviveAlReinicioDePedidos()
    {
        var pedidos = await IniciarPedidosAsync();
        await IniciarAlmacenAsync();

        // Pagos aún no está disponible: la Saga se detiene en ProcesandoPago.
        var pedidoId = await CrearPedidoAsync(pedidos, Teclado, cantidad: 1, precioUnitario: 30m);
        await EsperarEstadoSagaAsync(pedidoId, EstadoProcesandoPago);

        // El orquestador cae. Su estado sigue en PedidosDb.
        await pedidos.DetenerAsync();
        Assert.Equal(EstadoProcesandoPago, (await LeerSagaAsync(pedidoId))!.EstadoActual);

        // Pagos procesa el comando pendiente y publica PagoCompletado mientras Pedidos está caído:
        // el evento espera en la cola duradera de la Saga.
        await IniciarPagosAsync();
        await Sondeo.EsperarAsync(async () => (await LeerPagosAsync(pedidoId)).Any(pago => pago.Estado == EstadoPago.Completado), "pago completado");
        Assert.Equal(EstadoProcesandoPago, (await LeerSagaAsync(pedidoId))!.EstadoActual);

        // Una nueva instancia de Pedidos retoma la Saga exactamente donde se quedó.
        await IniciarPedidosAsync();
        await EsperarEstadoSagaAsync(pedidoId, EstadoCompletado);
        Assert.Equal(EstadoPedido.Completado, (await LeerPedidoAsync(pedidoId))!.Estado);
    }

    [Fact]
    public async Task RecuperacionTrasReinicio_ElComandoRetenidoSeProcesaCuandoAlmacenVuelve()
    {
        var pedidos = await IniciarPedidosAsync();

        // Almacén está caído al crear el pedido: SolicitarReservaStock queda retenido en su cola.
        var pedidoId = await CrearPedidoAsync(pedidos, Teclado, cantidad: 1, precioUnitario: 15m);
        await EsperarEstadoSagaAsync(pedidoId, EstadoReservandoStock);
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Empty(await LeerReservasAsync(pedidoId));

        await IniciarAlmacenAsync();
        await IniciarPagosAsync();

        await EsperarEstadoSagaAsync(pedidoId, EstadoCompletado);
        Assert.Equal(EstadoPedido.Completado, (await LeerPedidoAsync(pedidoId))!.Estado);
    }

    [Fact]
    public async Task EscenarioD_FalloDuranteLaCompensacion_LaSagaEsperaYSeRecuperaAlReinyectarDesdeLaDlq()
    {
        var pedidos = await IniciarPedidosAsync();
        var almacenAveriado = await IniciarAlmacenAsync(fallosAntesDeLiberar: 100);
        await IniciarPagosAsync();
        var stockInicial = await LeerProductoAsync(Monitor);

        var pedidoId = await CrearPedidoAsync(pedidos, Monitor, cantidad: 2, PrecioMonitorRechazado);

        // Se agotan los reintentos: el mensaje pasa a almacen-comandos_error y la Saga recibe el Fault.
        await Sondeo.EsperarAsync(async () => (await LeerSagaAsync(pedidoId))?.FallosCompensacion > 0, "fallo de compensación registrado");

        var saga = await LeerSagaAsync(pedidoId);
        Assert.Equal(EstadoCompensando, saga!.EstadoActual);
        Assert.Contains("LiberarReservaStock", saga.UltimoError);
        Assert.Equal(EstadoPedido.Pendiente, (await LeerPedidoAsync(pedidoId))!.Estado);
        Assert.Equal(EstadoReserva.Reservada, (await LeerReservaAsync(pedidoId))!.Estado);

        // Se corrige la avería y un operador reinyecta el mensaje (equivale a moverlo desde la cola _error).
        await almacenAveriado.DetenerAsync();
        await IniciarAlmacenAsync();

        var colaAlmacen = await pedidos.Bus.GetSendEndpoint(ColasIntegracion.DireccionComandosAlmacen);
        await colaAlmacen.Send(new LiberarReservaStock(pedidoId, pedidoId, "Reinyectado desde la cola de errores"));

        await EsperarEstadoSagaAsync(pedidoId, EstadoCancelado);
        Assert.Equal(EstadoPedido.Cancelado, (await LeerPedidoAsync(pedidoId))!.Estado);
        Assert.Equal(stockInicial.StockReservado, (await LeerProductoAsync(Monitor)).StockReservado);
    }

    [Fact]
    public async Task EscenarioD_CompensacionPendiente_SeCompletaTrasReiniciarAlmacenSinLiberarDosVeces()
    {
        var pedidos = await IniciarPedidosAsync();
        var almacen = await IniciarAlmacenAsync();
        var stockInicial = await LeerProductoAsync(Monitor);

        var pedidoId = await CrearPedidoAsync(pedidos, Monitor, cantidad: 2, PrecioMonitorRechazado);
        await EsperarEstadoSagaAsync(pedidoId, EstadoProcesandoPago);

        // Almacén cae justo antes de que llegue la compensación.
        await almacen.DetenerAsync();
        await IniciarPagosAsync();

        await EsperarEstadoSagaAsync(pedidoId, EstadoCompensando);
        await Task.Delay(TimeSpan.FromSeconds(3));

        // La compensación no se pierde ni se da por buena: la Saga sigue esperando y el stock sigue reservado.
        Assert.Equal(EstadoCompensando, (await LeerSagaAsync(pedidoId))!.EstadoActual);
        Assert.Equal(stockInicial.StockReservado + 2, (await LeerProductoAsync(Monitor)).StockReservado);

        // Almacén vuelve: consume LiberarReservaStock de su cola duradera y la Saga finaliza.
        var almacenRecuperado = await IniciarAlmacenAsync();
        await EsperarEstadoSagaAsync(pedidoId, EstadoCancelado);
        Assert.Equal(stockInicial.StockReservado, (await LeerProductoAsync(Monitor)).StockReservado);

        // Una reentrega posterior de la compensación no devuelve las unidades por segunda vez.
        var observador = new ObservadorConsumo();
        almacenRecuperado.Bus.ConnectConsumeObserver(observador);
        var colaAlmacen = await pedidos.Bus.GetSendEndpoint(ColasIntegracion.DireccionComandosAlmacen);
        await colaAlmacen.Send(new LiberarReservaStock(pedidoId, pedidoId, "Reentrega"));
        await Sondeo.EsperarAsync(() => Task.FromResult(observador.Consumidos<LiberarReservaStock>() >= 1), "reentrega consumida");

        Assert.Equal(stockInicial.StockReservado, (await LeerProductoAsync(Monitor)).StockReservado);
        Assert.Equal(EstadoReserva.Liberada, (await LeerReservaAsync(pedidoId))!.Estado);
    }
}
