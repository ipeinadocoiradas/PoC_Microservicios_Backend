using Pedidos.Domain;

namespace Pedidos.UnitTests.Dominio;

public sealed class PedidoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Cliente = Guid.NewGuid();

    [Fact]
    public void Crear_ConDatosValidos_QuedaPendienteYCalculaElTotal()
    {
        var pedido = Pedido.Crear(Cliente, "eur",
        [
            LineaPedido.Crear(Guid.NewGuid(), 2, 49.90m),
            LineaPedido.Crear(Guid.NewGuid(), 1, 10.00m)
        ], Ahora);

        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
        Assert.Equal(109.80m, pedido.Total);
        Assert.Equal("EUR", pedido.Moneda);
        Assert.Equal(2, pedido.Lineas.Count);
        Assert.NotEqual(Guid.Empty, pedido.Id.Valor);
        Assert.False(pedido.EstaFinalizado);
    }

    [Fact]
    public void Crear_SinLineas_LanzaExcepcionDominio()
    {
        var excepcion = Assert.Throws<ExcepcionDominio>(() => Pedido.Crear(Cliente, "EUR", [], Ahora));
        Assert.Contains("al menos una línea", excepcion.Message);
    }

    [Fact]
    public void Crear_ConProductoRepetido_LanzaExcepcionDominio()
    {
        var producto = Guid.NewGuid();

        Assert.Throws<ExcepcionDominio>(() => Pedido.Crear(Cliente, "EUR",
        [
            LineaPedido.Crear(producto, 1, 5m),
            LineaPedido.Crear(producto, 2, 5m)
        ], Ahora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    public void Crear_ConMonedaNoValida_LanzaExcepcionDominio(string moneda)
    {
        Assert.Throws<ExcepcionDominio>(() => Pedido.Crear(Cliente, moneda, [LineaPedido.Crear(Guid.NewGuid(), 1, 5m)], Ahora));
    }

    [Fact]
    public void Crear_SinCliente_LanzaExcepcionDominio()
    {
        Assert.Throws<ExcepcionDominio>(() => Pedido.Crear(Guid.Empty, "EUR", [LineaPedido.Crear(Guid.NewGuid(), 1, 5m)], Ahora));
    }

    [Theory]
    [InlineData(0, 10.0)]
    [InlineData(-1, 10.0)]
    [InlineData(1001, 10.0)]
    [InlineData(1, 0.0)]
    [InlineData(1, -5.0)]
    public void CrearLinea_ConCantidadOPrecioNoValidos_LanzaExcepcionDominio(int cantidad, double precio)
    {
        Assert.Throws<ExcepcionDominio>(() => LineaPedido.Crear(Guid.NewGuid(), cantidad, (decimal)precio));
    }

    [Fact]
    public void CrearLinea_ConMasDeDosDecimales_LanzaExcepcionDominio()
    {
        Assert.Throws<ExcepcionDominio>(() => LineaPedido.Crear(Guid.NewGuid(), 1, 9.999m));
    }

    [Fact]
    public void Completar_DesdePendiente_FinalizaElPedido()
    {
        var pedido = CrearPedido();

        pedido.Completar(Ahora.AddMinutes(1));

        Assert.Equal(EstadoPedido.Completado, pedido.Estado);
        Assert.Equal(Ahora.AddMinutes(1), pedido.FechaFinalizacionUtc);
        Assert.Null(pedido.MotivoFinalizacion);
    }

    [Fact]
    public void Cancelar_RegistraElMotivo()
    {
        var pedido = CrearPedido();

        pedido.Cancelar("  Stock no disponible  ", Ahora);

        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
        Assert.Equal("Stock no disponible", pedido.MotivoFinalizacion);
    }

    [Fact]
    public void Cancelar_SinMotivo_LanzaExcepcionDominio()
    {
        Assert.Throws<ExcepcionDominio>(() => CrearPedido().Cancelar(" ", Ahora));
    }

    [Fact]
    public void MarcarFallido_DesdePendiente_FinalizaComoFallido()
    {
        var pedido = CrearPedido();

        pedido.MarcarFallido("Pasarela no disponible", Ahora);

        Assert.Equal(EstadoPedido.Fallido, pedido.Estado);
    }

    [Fact]
    public void UnPedidoFinalizado_NoAdmiteNuevasTransiciones()
    {
        var pedido = CrearPedido();
        pedido.Cancelar("Pago rechazado", Ahora);

        Assert.Throws<ExcepcionDominio>(() => pedido.Completar(Ahora));
        Assert.Throws<ExcepcionDominio>(() => pedido.MarcarFallido("x", Ahora));
        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
    }

    private static Pedido CrearPedido() =>
        Pedido.Crear(Cliente, "EUR", [LineaPedido.Crear(Guid.NewGuid(), 1, 20m)], Ahora);
}
