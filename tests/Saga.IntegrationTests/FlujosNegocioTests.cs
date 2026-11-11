using Almacen.Domain;
using Almacen.Infrastructure.Persistencia;
using Pagos.Domain;
using Pedidos.Domain;
using Saga.IntegrationTests.Infraestructura;

namespace Saga.IntegrationTests;

/// <summary>
/// Escenarios de negocio A, B y C de extremo a extremo. Todas las aserciones se hacen sobre el estado
/// persistido en PedidosDb, AlmacenDb y PagosDb.
/// </summary>
[Collection(ColeccionIntegracion.Nombre)]
public sealed class FlujosNegocioTests(EntornoIntegracion entorno) : PruebaIntegracionBase(entorno)
{
    private static readonly Guid Teclado = ProductosDemostracion.TecladoMecanico;
    private static readonly Guid Monitor = ProductosDemostracion.Monitor4K;
    private static readonly Guid Agotado = ProductosDemostracion.EdicionLimitadaAgotada;

    [Fact]
    public async Task EscenarioA_FlujoSatisfactorio_CompletaElPedidoCobraYConsolidaElStock()
    {
        var (pedidos, _, _) = await IniciarTodosAsync();
        var stockInicial = await LeerProductoAsync(Teclado);

        var pedidoId = await CrearPedidoAsync(pedidos, Teclado, cantidad: 2, precioUnitario: 49.90m);

        await EsperarEstadoSagaAsync(pedidoId, EstadoCompletado);
        await Sondeo.EsperarAsync(async () => (await LeerSagaAsync(pedidoId))!.ReservaConfirmadaEnAlmacen, "reserva confirmada en Almacén");

        var pedido = await LeerPedidoAsync(pedidoId);
        Assert.Equal(EstadoPedido.Completado, pedido!.Estado);

        var pago = Assert.Single(await LeerPagosAsync(pedidoId));
        Assert.Equal(EstadoPago.Completado, pago.Estado);
        Assert.Equal(99.80m, pago.Importe.Cantidad);

        var reserva = await LeerReservaAsync(pedidoId);
        Assert.Equal(EstadoReserva.Confirmada, reserva!.Estado);

        var stockFinal = await LeerProductoAsync(Teclado);
        Assert.Equal(stockInicial.StockFisico - 2, stockFinal.StockFisico);
        Assert.Equal(stockInicial.StockReservado, stockFinal.StockReservado);
    }

    [Fact]
    public async Task EscenarioB_StockInsuficiente_CancelaElPedidoSinIniciarElPago()
    {
        var (pedidos, _, _) = await IniciarTodosAsync();

        var pedidoId = await CrearPedidoAsync(pedidos, Agotado, cantidad: 1, precioUnitario: 199m);

        await EsperarEstadoSagaAsync(pedidoId, EstadoCancelado);

        var pedido = await LeerPedidoAsync(pedidoId);
        Assert.Equal(EstadoPedido.Cancelado, pedido!.Estado);
        Assert.Contains("Stock no disponible", pedido.MotivoFinalizacion);

        var reserva = await LeerReservaAsync(pedidoId);
        Assert.Equal(EstadoReserva.Rechazada, reserva!.Estado);

        // El pago nunca debe iniciarse.
        Assert.Empty(await LeerPagosAsync(pedidoId));
        Assert.Null((await LeerSagaAsync(pedidoId))!.PagoId);
    }

    [Fact]
    public async Task EscenarioC_PagoRechazado_CompensaLaReservaYCancelaElPedido()
    {
        var (pedidos, _, _) = await IniciarTodosAsync();

        // 2 x 600 € = 1.200 € supera el límite de autorización simulado (1.000 €).
        var pedidoId = await CrearPedidoAsync(pedidos, Monitor, cantidad: 2, precioUnitario: 600m);

        await EsperarEstadoSagaAsync(pedidoId, EstadoCancelado);

        var saga = await LeerSagaAsync(pedidoId);
        Assert.True(saga!.LiberacionReservaConfirmada);
        Assert.False(saga.RequiereCancelacionPago);

        var pedido = await LeerPedidoAsync(pedidoId);
        Assert.Equal(EstadoPedido.Cancelado, pedido!.Estado);
        Assert.Contains("Pago rechazado", pedido.MotivoFinalizacion);

        var pago = Assert.Single(await LeerPagosAsync(pedidoId));
        Assert.Equal(EstadoPago.Rechazado, pago.Estado);

        var reserva = await LeerReservaAsync(pedidoId);
        Assert.Equal(EstadoReserva.Liberada, reserva!.Estado);

        var historial = await LeerHistorialAsync(pedidoId);
        Assert.Equal(
            new[] { "PedidoPendiente", "ReservandoStock", "ProcesandoPago", "CompensandoOperaciones", "Cancelado" },
            historial.Select(transicion => transicion.EstadoNuevo));
        Assert.Equal(Enumerable.Range(1, historial.Count), historial.Select(transicion => transicion.Secuencia));
        Assert.Equal("ReservaLiberada", historial[^1].Evento);
    }

    [Fact]
    public async Task LiberacionDeStock_DevuelveExactamenteLasUnidadesReservadas()
    {
        var (pedidos, _, _) = await IniciarTodosAsync();
        var stockInicial = await LeerProductoAsync(Monitor);

        var pedidoId = await CrearPedidoAsync(pedidos, Monitor, cantidad: 3, precioUnitario: 400m);

        await EsperarEstadoSagaAsync(pedidoId, EstadoCancelado);

        var stockFinal = await LeerProductoAsync(Monitor);
        Assert.Equal(stockInicial.StockFisico, stockFinal.StockFisico);
        Assert.Equal(stockInicial.StockReservado, stockFinal.StockReservado);
        Assert.Equal(stockInicial.StockDisponible, stockFinal.StockDisponible);
    }
}
