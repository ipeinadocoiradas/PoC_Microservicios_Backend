using Almacen.Domain;

namespace Almacen.UnitTests.Dominio;

public sealed class ProductoTests
{
    [Fact]
    public void Reservar_ReduceElDisponibleSinTocarElFisico()
    {
        var producto = CrearProducto(stock: 10);

        producto.Reservar(4);

        Assert.Equal(10, producto.StockFisico);
        Assert.Equal(4, producto.StockReservado);
        Assert.Equal(6, producto.StockDisponible);
    }

    [Fact]
    public void Reservar_MasDelDisponible_LanzaExcepcionDominio()
    {
        var producto = CrearProducto(stock: 3);
        producto.Reservar(2);

        Assert.Throws<ExcepcionDominio>(() => producto.Reservar(2));
        Assert.Equal(2, producto.StockReservado);
    }

    [Fact]
    public void LiberarReserva_DevuelveLasUnidadesAlDisponible()
    {
        var producto = CrearProducto(stock: 10);
        producto.Reservar(4);

        producto.LiberarReserva(4);

        Assert.Equal(0, producto.StockReservado);
        Assert.Equal(10, producto.StockDisponible);
    }

    [Fact]
    public void LiberarReserva_MasDeLoReservado_LanzaExcepcionDominio()
    {
        var producto = CrearProducto(stock: 10);
        producto.Reservar(1);

        Assert.Throws<ExcepcionDominio>(() => producto.LiberarReserva(2));
    }

    [Fact]
    public void ConfirmarSalida_ReduceFisicoYReservado()
    {
        var producto = CrearProducto(stock: 10);
        producto.Reservar(3);

        producto.ConfirmarSalida(3);

        Assert.Equal(7, producto.StockFisico);
        Assert.Equal(0, producto.StockReservado);
        Assert.Equal(7, producto.StockDisponible);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Operaciones_ConCantidadNoPositiva_LanzanExcepcionDominio(int cantidad)
    {
        var producto = CrearProducto(stock: 10);

        Assert.Throws<ExcepcionDominio>(() => producto.Reservar(cantidad));
        Assert.Throws<ExcepcionDominio>(() => producto.LiberarReserva(cantidad));
        Assert.Throws<ExcepcionDominio>(() => producto.ConfirmarSalida(cantidad));
    }

    private static Producto CrearProducto(int stock) => Producto.Crear(new ProductoId(Guid.NewGuid()), "Producto de prueba", stock);
}
