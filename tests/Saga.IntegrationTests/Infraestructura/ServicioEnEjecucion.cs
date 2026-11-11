using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Saga.IntegrationTests.Infraestructura;

/// <summary>
/// Un microservicio arrancado en un <see cref="IHost"/> propio (su bus, sus consumidores, su DbContext).
/// Detenerlo equivale a una caída del servicio: sus colas duraderas siguen acumulando mensajes en RabbitMQ.
/// </summary>
public sealed class ServicioEnEjecucion(string nombre, IHost host) : IAsyncDisposable
{
    private bool _detenido;

    public string Nombre { get; } = nombre;

    public IServiceProvider Servicios => host.Services;

    public IBus Bus => host.Services.GetRequiredService<IBus>();

    public async Task DetenerAsync()
    {
        if (_detenido)
        {
            return;
        }

        _detenido = true;
        await host.StopAsync(TimeSpan.FromSeconds(30));
        host.Dispose();
    }

    public async ValueTask DisposeAsync() => await DetenerAsync();
}
