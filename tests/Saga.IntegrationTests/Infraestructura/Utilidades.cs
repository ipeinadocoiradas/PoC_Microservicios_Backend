using System.Collections.Concurrent;
using System.Diagnostics;
using MassTransit;

namespace Saga.IntegrationTests.Infraestructura;

/// <summary>
/// Espera activa sobre el estado <b>persistido</b>. En un sistema asíncrono no hay un "return" que esperar:
/// la única verdad es lo que ha quedado escrito en las bases de datos de cada servicio.
/// </summary>
public static class Sondeo
{
    private static readonly TimeSpan LimitePorDefecto = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(250);

    public static async Task EsperarAsync(Func<Task<bool>> condicion, string descripcion, TimeSpan? limite = null)
    {
        var limiteEfectivo = limite ?? LimitePorDefecto;
        var cronometro = Stopwatch.StartNew();

        while (cronometro.Elapsed < limiteEfectivo)
        {
            if (await condicion())
            {
                return;
            }

            await Task.Delay(Intervalo);
        }

        throw new TimeoutException($"No se alcanzó la condición «{descripcion}» en {limiteEfectivo.TotalSeconds:0} s.");
    }
}

/// <summary>Cuenta los mensajes consumidos con éxito por un bus para saber cuándo se han procesado los duplicados.</summary>
public sealed class ObservadorConsumo : IConsumeObserver
{
    private readonly ConcurrentDictionary<Type, int> _consumidos = new();

    public int Consumidos<TMensaje>() => _consumidos.GetValueOrDefault(typeof(TMensaje));

    public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        _consumidos.AddOrUpdate(typeof(T), 1, (_, anterior) => anterior + 1);
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Task.CompletedTask;
}
