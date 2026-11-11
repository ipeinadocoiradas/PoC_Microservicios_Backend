using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Infraestructura.Persistencia;

/// <summary>
/// Crea el esquema de la base de datos del servicio al arrancar, reintentando mientras SQL Server
/// no esté disponible (habitual en el primer arranque de los contenedores).
/// </summary>
/// <remarks>
/// Para una PoC se usa <c>EnsureCreated</c>, que genera el esquema a partir del modelo y ejecuta el
/// sembrado configurado con <c>UseSeeding</c>/<c>UseAsyncSeeding</c>. En producción se sustituiría por
/// migraciones de EF Core aplicadas en el pipeline de despliegue.
/// Se registra antes que MassTransit para que las tablas de Outbox existan cuando el bus arranque.
/// </remarks>
public sealed class InicializadorBaseDatos<TContexto>(
    IServiceScopeFactory factoryAmbitos,
    ILogger<InicializadorBaseDatos<TContexto>> registro) : IHostedService
    where TContexto : DbContext
{
    private const int IntentosMaximos = 12;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                await using var ambito = factoryAmbitos.CreateAsyncScope();
                var contexto = ambito.ServiceProvider.GetRequiredService<TContexto>();
                var creada = await contexto.Database.EnsureCreatedAsync(cancellationToken);

                registro.LogInformation("Base de datos de {Contexto} preparada (esquema creado: {Creada})", typeof(TContexto).Name, creada);
                return;
            }
            catch (Exception excepcion) when (intento < IntentosMaximos && excepcion is not OperationCanceledException)
            {
                var espera = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, intento)));
                registro.LogWarning(excepcion, "SQL Server no disponible para {Contexto} (intento {Intento}/{Maximo}); nuevo intento en {Espera}",
                    typeof(TContexto).Name, intento, IntentosMaximos, espera);
                await Task.Delay(espera, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
