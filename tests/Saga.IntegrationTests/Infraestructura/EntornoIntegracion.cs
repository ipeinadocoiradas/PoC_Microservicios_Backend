using Almacen.Application;
using Almacen.Infrastructure;
using Almacen.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pagos.Application;
using Pagos.Infrastructure;
using Pagos.Infrastructure.Persistencia;
using Pedidos.Application;
using Pedidos.Infrastructure;
using Pedidos.Infrastructure.Persistencia;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Saga.IntegrationTests.Infraestructura;

/// <summary>
/// Fixture compartida por todas las pruebas de integración: un SQL Server y un RabbitMQ reales.
/// Sabe arrancar cada microservicio con la configuración apuntando a los contenedores.
/// </summary>
public sealed class EntornoIntegracion : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.1-management")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlServer.StartAsync(), _rabbitMq.StartAsync());

        // Calentamiento: cada servicio crea su base de datos y declara colas, exchanges y enlaces.
        // Así ningún escenario depende de qué servicio arrancó primero.
        await (await IniciarPedidosAsync()).DisposeAsync();
        await (await IniciarAlmacenAsync()).DisposeAsync();
        await (await IniciarPagosAsync()).DisposeAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlServer.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }

    public string CadenaSql(string baseDatos) =>
        new SqlConnectionStringBuilder(_sqlServer.GetConnectionString()) { InitialCatalog = baseDatos }.ConnectionString;

    public Task<ServicioEnEjecucion> IniciarPedidosAsync() =>
        IniciarAsync("pedidos", ConfiguracionBase(PedidosDbContext.NombreCadenaConexion),
            (servicios, configuracion) => servicios.AgregarAplicacionPedidos().AgregarInfraestructuraPedidos(configuracion));

    /// <param name="fallosAntesDeLiberar">Intentos de <c>LiberarReservaStock</c> que fallarán por pedido.</param>
    public Task<ServicioEnEjecucion> IniciarAlmacenAsync(int fallosAntesDeLiberar = 0)
    {
        var configuracion = ConfiguracionBase(AlmacenDbContext.NombreCadenaConexion);
        configuracion["SimulacionFallos:FallosAntesDeLiberarReserva"] = fallosAntesDeLiberar.ToString();

        return IniciarAsync("almacen", configuracion,
            (servicios, valores) => servicios.AgregarAplicacionAlmacen().AgregarInfraestructuraAlmacen(valores));
    }

    public Task<ServicioEnEjecucion> IniciarPagosAsync()
    {
        var configuracion = ConfiguracionBase(PagosDbContext.NombreCadenaConexion);
        configuracion["SimulacionPagos:ImporteMaximoAutorizado"] = "1000";
        configuracion["SimulacionPagos:ImporteFalloTecnico"] = "666.66";
        configuracion["SimulacionPagos:LatenciaSimulada"] = "00:00:00";

        return IniciarAsync("pagos", configuracion,
            (servicios, valores) => servicios.AgregarAplicacionPagos().AgregarInfraestructuraPagos(valores));
    }

    private Dictionary<string, string?> ConfiguracionBase(string nombreBaseDatos) => new()
    {
        [$"ConnectionStrings:{nombreBaseDatos}"] = CadenaSql(nombreBaseDatos),
        ["ConnectionStrings:rabbitmq"] = _rabbitMq.GetConnectionString(),

        // Reintentos rápidos para que las pruebas no esperen el backoff de producción.
        ["Mensajeria:Reintentos:NumeroMaximo"] = "3",
        ["Mensajeria:Reintentos:IntervaloMinimo"] = "00:00:00.200",
        ["Mensajeria:Reintentos:IntervaloMaximo"] = "00:00:01",
        ["Mensajeria:Reintentos:Incremento"] = "00:00:00.200"
    };

    private static async Task<ServicioEnEjecucion> IniciarAsync(
        string nombre,
        Dictionary<string, string?> configuracion,
        Action<IServiceCollection, IConfiguration> registrar)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configuracion);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        registrar(builder.Services, builder.Configuration);

        var host = builder.Build();
        await host.StartAsync();
        return new ServicioEnEjecucion(nombre, host);
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionIntegracion : ICollectionFixture<EntornoIntegracion>
{
    public const string Nombre = "Saga distribuida";
}
