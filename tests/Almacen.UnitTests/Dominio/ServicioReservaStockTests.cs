using Almacen.Domain;

namespace Almacen.UnitTests.Dominio;

public sealed class ServicioReservaStockTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    private readonly Producto _teclado = Producto.Crear(new ProductoId(Guid.NewGuid()), "Teclado", 10);
    private readonly Producto _raton = Producto.Crear(new ProductoId(Guid.NewGuid()), "Ratón", 2);

    [Fact]
    public void Reservar_ConStockSuficiente_ReservaTodasLasLineas()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_teclado, 3), Linea(_raton, 2)], Catalogo(), Ahora);

        Assert.Equal(EstadoReserva.Reservada, reserva.Estado);
        Assert.Equal(3, _teclado.StockReservado);
        Assert.Equal(2, _raton.StockReservado);
    }

    [Fact]
    public void Reservar_ConUnaLineaSinStock_RechazaSinReservarNada()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_teclado, 3), Linea(_raton, 5)], Catalogo(), Ahora);

        Assert.Equal(EstadoReserva.Rechazada, reserva.Estado);
        Assert.Contains("Stock insuficiente", reserva.MotivoRechazo);
        Assert.Equal(0, _teclado.StockReservado);
        Assert.Equal(0, _raton.StockReservado);
    }

    [Fact]
    public void Reservar_ConProductoInexistente_Rechaza()
    {
        var desconocido = LineaReservaStock.Crear(new ProductoId(Guid.NewGuid()), 1);

        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [desconocido], Catalogo(), Ahora);

        Assert.Equal(EstadoReserva.Rechazada, reserva.Estado);
        Assert.Contains("no existe", reserva.MotivoRechazo);
    }

    [Fact]
    public void Liberar_DevuelveElStockYEsIdempotente()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_teclado, 4)], Catalogo(), Ahora);

        var primeraLiberacion = ServicioReservaStock.Liberar(reserva, Catalogo(), Ahora);
        var segundaLiberacion = ServicioReservaStock.Liberar(reserva, Catalogo(), Ahora);

        Assert.True(primeraLiberacion);
        Assert.False(segundaLiberacion);
        Assert.Equal(EstadoReserva.Liberada, reserva.Estado);
        Assert.Equal(0, _teclado.StockReservado);
        Assert.Equal(10, _teclado.StockDisponible);
    }

    [Fact]
    public void Liberar_UnaReservaRechazada_NoModificaElStock()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_raton, 99)], Catalogo(), Ahora);

        Assert.False(ServicioReservaStock.Liberar(reserva, Catalogo(), Ahora));
        Assert.Equal(EstadoReserva.Rechazada, reserva.Estado);
        Assert.Equal(0, _raton.StockReservado);
    }

    [Fact]
    public void Confirmar_ConsolidaLaSalidaYEsIdempotente()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_teclado, 4)], Catalogo(), Ahora);

        Assert.True(ServicioReservaStock.Confirmar(reserva, Catalogo(), Ahora));
        Assert.False(ServicioReservaStock.Confirmar(reserva, Catalogo(), Ahora));

        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Equal(6, _teclado.StockFisico);
        Assert.Equal(0, _teclado.StockReservado);
    }

    [Fact]
    public void Liberar_UnaReservaConfirmada_LanzaExcepcionDominio()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_teclado, 1)], Catalogo(), Ahora);
        ServicioReservaStock.Confirmar(reserva, Catalogo(), Ahora);

        Assert.Throws<ExcepcionDominio>(() => ServicioReservaStock.Liberar(reserva, Catalogo(), Ahora));
    }

    [Fact]
    public void Confirmar_UnaReservaLiberada_LanzaExcepcionDominio()
    {
        var reserva = ServicioReservaStock.Reservar(Guid.NewGuid(), [Linea(_teclado, 1)], Catalogo(), Ahora);
        ServicioReservaStock.Liberar(reserva, Catalogo(), Ahora);

        Assert.Throws<ExcepcionDominio>(() => ServicioReservaStock.Confirmar(reserva, Catalogo(), Ahora));
    }

    private Dictionary<ProductoId, Producto> Catalogo() => new()
    {
        [_teclado.Id] = _teclado,
        [_raton.Id] = _raton
    };

    private static LineaReservaStock Linea(Producto producto, int cantidad) => LineaReservaStock.Crear(producto.Id, cantidad);
}
