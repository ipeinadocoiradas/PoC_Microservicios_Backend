using Pagos.Domain;

namespace Pagos.UnitTests.Dominio;

public sealed class PagoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Iniciar_CreaUnPagoPendiente()
    {
        var pago = CrearPago();

        Assert.Equal(EstadoPago.Pendiente, pago.Estado);
        Assert.Equal(99.95m, pago.Importe.Cantidad);
        Assert.Equal("EUR", pago.Importe.Moneda);
    }

    [Fact]
    public void Completar_RegistraElCodigoDeAutorizacion()
    {
        var pago = CrearPago();

        pago.Completar("AUT-123", Ahora);

        Assert.Equal(EstadoPago.Completado, pago.Estado);
        Assert.Equal("AUT-123", pago.CodigoAutorizacion);
    }

    [Fact]
    public void Rechazar_RegistraElMotivo()
    {
        var pago = CrearPago();

        pago.Rechazar("Fondos insuficientes", Ahora);

        Assert.Equal(EstadoPago.Rechazado, pago.Estado);
        Assert.Equal("Fondos insuficientes", pago.Motivo);
    }

    [Fact]
    public void UnPagoRechazado_NoPuedeCompletarse()
    {
        var pago = CrearPago();
        pago.Rechazar("Denegado", Ahora);

        Assert.Throws<ExcepcionDominio>(() => pago.Completar("AUT-1", Ahora));
    }

    [Fact]
    public void Cancelar_UnPagoCompletado_LoReembolsa()
    {
        var pago = CrearPago();
        pago.Completar("AUT-1", Ahora);

        Assert.True(pago.Cancelar("Compensación", Ahora));
        Assert.Equal(EstadoPago.Reembolsado, pago.Estado);
    }

    [Fact]
    public void Cancelar_EsIdempotente()
    {
        var pago = CrearPago();

        Assert.True(pago.Cancelar("Compensación", Ahora));
        Assert.False(pago.Cancelar("Compensación", Ahora));
        Assert.Equal(EstadoPago.Cancelado, pago.Estado);
    }

    [Fact]
    public void CancelacionPreventiva_DejaElPagoCanceladoSinCargo()
    {
        var pago = Pago.RegistrarCancelacionPreventiva(Guid.NewGuid(), Guid.NewGuid(), Dinero.Crear(10m, "EUR"), "Fallo técnico", Ahora);

        Assert.Equal(EstadoPago.Cancelado, pago.Estado);
        Assert.Null(pago.CodigoAutorizacion);
    }

    [Theory]
    [InlineData(0.0, "EUR")]
    [InlineData(-1.0, "EUR")]
    [InlineData(10.0, "EURO")]
    [InlineData(10.0, "")]
    public void Dinero_ConDatosNoValidos_LanzaExcepcionDominio(double cantidad, string moneda)
    {
        Assert.Throws<ExcepcionDominio>(() => Dinero.Crear((decimal)cantidad, moneda));
    }

    private static Pago CrearPago() =>
        Pago.Iniciar(Guid.NewGuid(), Guid.NewGuid(), Dinero.Crear(99.95m, "eur"), Ahora);
}
