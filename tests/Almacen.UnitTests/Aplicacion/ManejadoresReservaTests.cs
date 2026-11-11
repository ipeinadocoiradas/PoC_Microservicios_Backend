using Almacen.Application.Abstracciones;
using Almacen.Application.Reservas;
using Almacen.Domain;
using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging.Abstractions;

namespace Almacen.UnitTests.Aplicacion;

public sealed class ManejadoresReservaTests
{
    private readonly IReservaStockRepositorio _reservas = Substitute.For<IReservaStockRepositorio>();
    private readonly IProductoRepositorio _productos = Substitute.For<IProductoRepositorio>();
    private readonly IUnidadTrabajo _unidadTrabajo = Substitute.For<IUnidadTrabajo>();
    private readonly Producto _monitor = Producto.Crear(new ProductoId(Guid.NewGuid()), "Monitor", 5);

    public ManejadoresReservaTests() =>
        _productos.ObtenerPorIdsAsync(Arg.Any<IEnumerable<ProductoId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<ProductoId, Producto> { [_monitor.Id] = _monitor });

    [Fact]
    public async Task Reservar_PorPrimeraVez_PersisteLaReserva()
    {
        var pedidoId = Guid.NewGuid();

        var resultado = await CrearManejadorReservar().ManejarAsync(Comando(pedidoId, 2), CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal(EstadoReserva.Reservada, resultado.Valor.Estado);
        _reservas.Received(1).Agregar(Arg.Is<ReservaStock>(reserva => reserva.PedidoId == pedidoId));
        await _unidadTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        Assert.Equal(2, _monitor.StockReservado);
    }

    [Fact]
    public async Task Reservar_MensajeDuplicado_DevuelveLaReservaExistenteSinReservarDeNuevo()
    {
        var pedidoId = Guid.NewGuid();
        var existente = ServicioReservaStock.Reservar(pedidoId, [LineaReservaStock.Crear(_monitor.Id, 2)],
            new Dictionary<ProductoId, Producto> { [_monitor.Id] = _monitor }, DateTimeOffset.UtcNow);
        _reservas.ObtenerPorPedidoAsync(pedidoId, Arg.Any<CancellationToken>()).Returns(existente);

        var resultado = await CrearManejadorReservar().ManejarAsync(Comando(pedidoId, 2), CancellationToken.None);

        Assert.Equal(existente.Id.Valor, resultado.Valor.ReservaId);
        Assert.Equal(2, _monitor.StockReservado);
        _reservas.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _unidadTrabajo.DidNotReceiveWithAnyArgs().GuardarCambiosAsync(default);
    }

    [Fact]
    public async Task Reservar_SinLineas_DevuelveErrorDeValidacion()
    {
        var resultado = await CrearManejadorReservar().ManejarAsync(new ReservarStockComando(Guid.NewGuid(), []), CancellationToken.None);

        Assert.Equal(TipoError.Validacion, resultado.Error?.Tipo);
    }

    [Fact]
    public async Task Liberar_LiberacionRepetida_SoloGuardaLaPrimeraVez()
    {
        var pedidoId = Guid.NewGuid();
        var reserva = ServicioReservaStock.Reservar(pedidoId, [LineaReservaStock.Crear(_monitor.Id, 3)],
            new Dictionary<ProductoId, Producto> { [_monitor.Id] = _monitor }, DateTimeOffset.UtcNow);
        _reservas.ObtenerPorPedidoAsync(pedidoId, Arg.Any<CancellationToken>()).Returns(reserva);
        var manejador = new ManejadorLiberarReserva(_reservas, _productos, _unidadTrabajo, TimeProvider.System, NullLogger<ManejadorLiberarReserva>.Instance);

        await manejador.ManejarAsync(new LiberarReservaComando(pedidoId), CancellationToken.None);
        var segundo = await manejador.ManejarAsync(new LiberarReservaComando(pedidoId), CancellationToken.None);

        Assert.Equal(EstadoReserva.Liberada, segundo.Valor.Estado);
        Assert.Equal(0, _monitor.StockReservado);
        await _unidadTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Liberar_SinReserva_DevuelveNoEncontrado()
    {
        var manejador = new ManejadorLiberarReserva(_reservas, _productos, _unidadTrabajo, TimeProvider.System, NullLogger<ManejadorLiberarReserva>.Instance);

        var resultado = await manejador.ManejarAsync(new LiberarReservaComando(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TipoError.NoEncontrado, resultado.Error?.Tipo);
    }

    private ManejadorReservarStock CrearManejadorReservar() =>
        new(_reservas, _productos, _unidadTrabajo, TimeProvider.System, NullLogger<ManejadorReservarStock>.Instance);

    private ReservarStockComando Comando(Guid pedidoId, int cantidad) =>
        new(pedidoId, [new LineaSolicitadaDto(_monitor.Id.Valor, cantidad)]);
}
