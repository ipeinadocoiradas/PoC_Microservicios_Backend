using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging.Abstractions;
using Pedidos.Application;
using Pedidos.Application.Abstracciones;
using Pedidos.Application.CrearPedido;
using Pedidos.Domain;

namespace Pedidos.UnitTests.Aplicacion;

public sealed class ManejadorCrearPedidoTests
{
    private readonly IPedidoRepositorio _repositorio = Substitute.For<IPedidoRepositorio>();
    private readonly IIniciadorProcesoPedido _iniciador = Substitute.For<IIniciadorProcesoPedido>();
    private readonly IUnidadTrabajo _unidadTrabajo = Substitute.For<IUnidadTrabajo>();
    private readonly ManejadorCrearPedido _manejador;

    public ManejadorCrearPedidoTests() =>
        _manejador = new ManejadorCrearPedido(_repositorio, _iniciador, _unidadTrabajo, TimeProvider.System, NullLogger<ManejadorCrearPedido>.Instance);

    [Fact]
    public async Task ConDatosValidos_GuardaElPedidoEIniciaLaSagaEnLaMismaTransaccion()
    {
        var comando = new CrearPedidoComando(Guid.NewGuid(), "EUR", [new LineaPedidoSolicitada(Guid.NewGuid(), 3, 10m)]);

        var resultado = await _manejador.ManejarAsync(comando, CancellationToken.None);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal(30m, resultado.Valor.Total);
        Assert.Equal(nameof(EstadoPedido.Pendiente), resultado.Valor.Estado);

        // El mensaje de inicio se escribe en el Outbox ANTES de confirmar: ambos viajan en la misma transacción.
        Received.InOrder(() =>
        {
            _repositorio.Agregar(Arg.Is<Pedido>(pedido => pedido.Id.Valor == resultado.Valor.PedidoId));
            _iniciador.IniciarAsync(Arg.Any<Pedido>(), Arg.Any<CancellationToken>());
            _unidadTrabajo.GuardarCambiosAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SinLineas_DevuelveErrorDeValidacionYNoPersisteNada()
    {
        var comando = new CrearPedidoComando(Guid.NewGuid(), "EUR", []);

        var resultado = await _manejador.ManejarAsync(comando, CancellationToken.None);

        Assert.True(resultado.EsFallido);
        Assert.Equal(TipoError.Validacion, resultado.Error.Tipo);
        Assert.Equal(ErroresPedidos.SinLineas, resultado.Error);
        _repositorio.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _unidadTrabajo.DidNotReceiveWithAnyArgs().GuardarCambiosAsync(default);
    }

    [Fact]
    public async Task ConProductoRepetido_DevuelveErrorDeValidacion()
    {
        var producto = Guid.NewGuid();
        var comando = new CrearPedidoComando(Guid.NewGuid(), "EUR",
        [
            new LineaPedidoSolicitada(producto, 1, 10m),
            new LineaPedidoSolicitada(producto, 2, 10m)
        ]);

        var resultado = await _manejador.ManejarAsync(comando, CancellationToken.None);

        Assert.Equal(ErroresPedidos.ProductoRepetido, resultado.Error);
        await _iniciador.DidNotReceiveWithAnyArgs().IniciarAsync(default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task ConCantidadNoPositiva_DevuelveErrorDeValidacion(int cantidad)
    {
        var comando = new CrearPedidoComando(Guid.NewGuid(), "EUR", [new LineaPedidoSolicitada(Guid.NewGuid(), cantidad, 10m)]);

        var resultado = await _manejador.ManejarAsync(comando, CancellationToken.None);

        Assert.Equal(ErroresPedidos.LineaNoValida, resultado.Error);
    }
}
