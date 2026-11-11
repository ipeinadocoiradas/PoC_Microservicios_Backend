using Almacen.Domain;
using Almacen.Infrastructure.Persistencia;
using Shared.Aplicacion.Cqrs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pagos.Domain;
using Pagos.Infrastructure.Persistencia;
using Pedidos.Application.CrearPedido;
using Pedidos.Application.Saga;
using Pedidos.Domain;
using Pedidos.Infrastructure.Persistencia;

namespace Saga.IntegrationTests.Infraestructura;

/// <summary>
/// Base de las pruebas: arranca servicios bajo demanda, garantiza que todos se detienen al terminar
/// (aunque la prueba falle) y ofrece lecturas directas del estado persistido de cada base de datos.
/// </summary>
/// <remarks>
/// Las lecturas abren su propio <c>DbContext</c> sin pasar por los hosts, para poder comprobar el estado
/// incluso cuando el servicio propietario está detenido. Es un privilegio exclusivo de las pruebas:
/// en producción ningún servicio lee la base de datos de otro.
/// Las clases derivadas deben declarar <c>[Collection(ColeccionIntegracion.Nombre)]</c> para compartir los contenedores.
/// </remarks>
public abstract class PruebaIntegracionBase(EntornoIntegracion entorno) : IAsyncLifetime
{
    private readonly List<ServicioEnEjecucion> _servicios = [];

    protected const string EstadoProcesandoPago = nameof(GestorSagaPedidos.ProcesandoPago);
    protected const string EstadoReservandoStock = nameof(GestorSagaPedidos.ReservandoStock);
    protected const string EstadoCompensando = nameof(GestorSagaPedidos.CompensandoOperaciones);
    protected const string EstadoCompletado = nameof(GestorSagaPedidos.Completado);
    protected const string EstadoCancelado = nameof(GestorSagaPedidos.Cancelado);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var servicio in Enumerable.Reverse(_servicios))
        {
            await servicio.DetenerAsync();
        }
    }

    // ── Ciclo de vida de los servicios ─────────────────────────────────────────

    protected Task<ServicioEnEjecucion> IniciarPedidosAsync() => Registrar(entorno.IniciarPedidosAsync());

    protected Task<ServicioEnEjecucion> IniciarAlmacenAsync(int fallosAntesDeLiberar = 0) => Registrar(entorno.IniciarAlmacenAsync(fallosAntesDeLiberar));

    protected Task<ServicioEnEjecucion> IniciarPagosAsync() => Registrar(entorno.IniciarPagosAsync());

    protected async Task<(ServicioEnEjecucion Pedidos, ServicioEnEjecucion Almacen, ServicioEnEjecucion Pagos)> IniciarTodosAsync() =>
        (await IniciarPedidosAsync(), await IniciarAlmacenAsync(), await IniciarPagosAsync());

    private async Task<ServicioEnEjecucion> Registrar(Task<ServicioEnEjecucion> arranque)
    {
        var servicio = await arranque;
        _servicios.Add(servicio);
        return servicio;
    }

    // ── Acciones ───────────────────────────────────────────────────────────────

    /// <summary>Crea un pedido a través del mismo caso de uso que invoca <c>POST /api/pedidos</c>.</summary>
    protected static async Task<Guid> CrearPedidoAsync(ServicioEnEjecucion pedidos, Guid productoId, int cantidad, decimal precioUnitario)
    {
        await using var ambito = pedidos.Servicios.CreateAsyncScope();
        var despachador = ambito.ServiceProvider.GetRequiredService<IDespachador>();

        var resultado = await despachador.EnviarAsync(new CrearPedidoComando(
            Guid.NewGuid(),
            "EUR",
            [new LineaPedidoSolicitada(productoId, cantidad, precioUnitario)]));

        Assert.True(resultado.EsCorrecto, resultado.Error?.Descripcion);
        return resultado.Valor.PedidoId;
    }

    protected Task EsperarEstadoSagaAsync(Guid pedidoId, string estadoEsperado, TimeSpan? limite = null) =>
        Sondeo.EsperarAsync(async () => (await LeerSagaAsync(pedidoId))?.EstadoActual == estadoEsperado,
            $"Saga {pedidoId} en {estadoEsperado}", limite);

    // ── Lecturas del estado persistido ─────────────────────────────────────────

    protected Task<EstadoSagaPedido?> LeerSagaAsync(Guid pedidoId) =>
        LeerPedidosAsync(contexto => contexto.SagasPedido.AsNoTracking().FirstOrDefaultAsync(saga => saga.CorrelationId == pedidoId));

    protected Task<List<TransicionSagaPedido>> LeerHistorialAsync(Guid pedidoId) =>
    LeerPedidosAsync(contexto => contexto.HistorialSaga.AsNoTracking()
        .Where(transicion => transicion.CorrelationId == pedidoId)
        .OrderBy(transicion => transicion.Secuencia)
        .ToListAsync());

    protected Task<Pedido?> LeerPedidoAsync(Guid pedidoId)
    {
        var id = new PedidoId(pedidoId);
        return LeerPedidosAsync(contexto => contexto.Pedidos.AsNoTracking().FirstOrDefaultAsync(pedido => pedido.Id == id));
    }

    protected Task<List<ReservaStock>> LeerReservasAsync(Guid pedidoId) =>
        LeerAlmacenAsync(contexto => contexto.ReservasStock.AsNoTracking().Where(reserva => reserva.PedidoId == pedidoId).ToListAsync());

    protected async Task<ReservaStock?> LeerReservaAsync(Guid pedidoId) => (await LeerReservasAsync(pedidoId)).SingleOrDefault();

    protected Task<Producto> LeerProductoAsync(Guid productoId)
    {
        var id = new ProductoId(productoId);
        return LeerAlmacenAsync(contexto => contexto.Productos.AsNoTracking().SingleAsync(producto => producto.Id == id));
    }

    protected Task<List<Pago>> LeerPagosAsync(Guid pedidoId) =>
        LeerPagosDbAsync(contexto => contexto.Pagos.AsNoTracking().Where(pago => pago.PedidoId == pedidoId).ToListAsync());

    private async Task<T> LeerPedidosAsync<T>(Func<PedidosDbContext, Task<T>> consulta)
    {
        await using var contexto = new PedidosDbContext(Opciones<PedidosDbContext>(PedidosDbContext.NombreCadenaConexion));
        return await consulta(contexto);
    }

    private async Task<T> LeerAlmacenAsync<T>(Func<AlmacenDbContext, Task<T>> consulta)
    {
        await using var contexto = new AlmacenDbContext(Opciones<AlmacenDbContext>(AlmacenDbContext.NombreCadenaConexion));
        return await consulta(contexto);
    }

    private async Task<T> LeerPagosDbAsync<T>(Func<PagosDbContext, Task<T>> consulta)
    {
        await using var contexto = new PagosDbContext(Opciones<PagosDbContext>(PagosDbContext.NombreCadenaConexion));
        return await consulta(contexto);
    }

    private DbContextOptions<TContexto> Opciones<TContexto>(string baseDatos) where TContexto : DbContext =>
        new DbContextOptionsBuilder<TContexto>().UseSqlServer(entorno.CadenaSql(baseDatos)).Options;
}
