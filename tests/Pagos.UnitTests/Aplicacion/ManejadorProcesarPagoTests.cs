using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging.Abstractions;
using Pagos.Application;
using Pagos.Application.Abstracciones;
using Pagos.Domain;

namespace Pagos.UnitTests.Aplicacion;

public sealed class ManejadorProcesarPagoTests
{
    private readonly IPagoRepositorio _pagos = Substitute.For<IPagoRepositorio>();
    private readonly IAutorizadorPagos _autorizador = Substitute.For<IAutorizadorPagos>();
    private readonly IUnidadTrabajo _unidadTrabajo = Substitute.For<IUnidadTrabajo>();
    private readonly ManejadorProcesarPago _manejador;

    public ManejadorProcesarPagoTests() =>
        _manejador = new ManejadorProcesarPago(_pagos, _autorizador, _unidadTrabajo, TimeProvider.System, NullLogger<ManejadorProcesarPago>.Instance);

    [Fact]
    public async Task AutorizacionAprobada_RegistraUnPagoCompletado()
    {
        _autorizador.AutorizarAsync(Arg.Any<SolicitudAutorizacion>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoAutorizacion.Aprobar("AUT-OK"));

        var resultado = await _manejador.ManejarAsync(Comando(), CancellationToken.None);

        Assert.Equal(EstadoPago.Completado, resultado.Valor.Estado);
        Assert.Equal("AUT-OK", resultado.Valor.CodigoAutorizacion);
        _pagos.Received(1).Agregar(Arg.Any<Pago>());
        await _unidadTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutorizacionDenegada_RegistraUnPagoRechazado()
    {
        _autorizador.AutorizarAsync(Arg.Any<SolicitudAutorizacion>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoAutorizacion.Denegar("Límite superado"));

        var resultado = await _manejador.ManejarAsync(Comando(), CancellationToken.None);

        Assert.Equal(EstadoPago.Rechazado, resultado.Valor.Estado);
        Assert.Equal("Límite superado", resultado.Valor.Motivo);
    }

    [Fact]
    public async Task MensajeDuplicado_DevuelveElPagoExistenteSinVolverACobrar()
    {
        var comando = Comando();
        var existente = Pago.Iniciar(comando.PedidoId, comando.ClienteId, Dinero.Crear(comando.Importe, comando.Moneda), DateTimeOffset.UtcNow);
        existente.Completar("AUT-PREVIO", DateTimeOffset.UtcNow);
        _pagos.ObtenerPorPedidoAsync(comando.PedidoId, Arg.Any<CancellationToken>()).Returns(existente);

        var resultado = await _manejador.ManejarAsync(comando, CancellationToken.None);

        Assert.Equal(existente.Id.Valor, resultado.Valor.PagoId);
        await _autorizador.DidNotReceiveWithAnyArgs().AutorizarAsync(default!, default);
        _pagos.DidNotReceiveWithAnyArgs().Agregar(default!);
    }

    [Fact]
    public async Task FalloTecnicoDeLaPasarela_SePropagaSinPersistirNada()
    {
        _autorizador.AutorizarAsync(Arg.Any<SolicitudAutorizacion>(), Arg.Any<CancellationToken>())
            .Returns<ResultadoAutorizacion>(_ => throw new TimeoutException("Pasarela caída"));

        await Assert.ThrowsAsync<TimeoutException>(() => _manejador.ManejarAsync(Comando(), CancellationToken.None));

        _pagos.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _unidadTrabajo.DidNotReceiveWithAnyArgs().GuardarCambiosAsync(default);
    }

    [Fact]
    public async Task ImporteNoPositivo_DevuelveErrorDeValidacion()
    {
        var resultado = await _manejador.ManejarAsync(Comando() with { Importe = 0 }, CancellationToken.None);

        Assert.Equal(TipoError.Validacion, resultado.Error?.Tipo);
    }

    private static ProcesarPagoComando Comando() => new(Guid.NewGuid(), Guid.NewGuid(), 250m, "EUR");
}
