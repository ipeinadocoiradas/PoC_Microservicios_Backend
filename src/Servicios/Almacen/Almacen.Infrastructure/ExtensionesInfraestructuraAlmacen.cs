using Almacen.Application.Abstracciones;
using Almacen.Domain;
using Almacen.Infrastructure.Mensajeria;
using Almacen.Infrastructure.Persistencia;
using Almacen.Infrastructure.Simulacion;
using Shared.Infraestructura.Mensajeria;
using Shared.Infraestructura.Persistencia;
using Contratos.Integracion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Almacen.Infrastructure;

/// <summary>Composición de la infraestructura de Almacén.</summary>
public static class ExtensionesInfraestructuraAlmacen
{
    extension(IServiceCollection servicios)
    {
        public IServiceCollection AgregarInfraestructuraAlmacen(IConfiguration configuracion)
        {
            var cadenaSql = configuracion.ObtenerCadenaConexionObligatoria(AlmacenDbContext.NombreCadenaConexion);
            var cadenaRabbit = configuracion.ObtenerCadenaConexionObligatoria(NombresConexion.RabbitMq);

            servicios.AddDbContext<AlmacenDbContext>(opciones => opciones
                .UseSqlServer(cadenaSql)
                .UseSeeding((contexto, _) => SembradorCatalogo.Sembrar(contexto))
                .UseAsyncSeeding((contexto, _, cancelacion) => SembradorCatalogo.SembrarAsync(contexto, cancelacion)));

            servicios.AddScoped<IUnidadTrabajo>(proveedor => proveedor.GetRequiredService<AlmacenDbContext>());
            servicios.AddScoped<IProductoRepositorio, RepositorioProductos>();
            servicios.AddScoped<IReservaStockRepositorio, RepositorioReservasStock>();

            servicios.AddOptions<OpcionesSimulacionFallos>().Bind(configuracion.GetSection(OpcionesSimulacionFallos.Seccion));
            servicios.AddSingleton<SimuladorFallosTransitorios>();

            servicios.AddHealthChecks().AddDbContextCheck<AlmacenDbContext>("almacen-db", tags: ["ready"]);
            servicios.AddHostedService<InicializadorBaseDatos<AlmacenDbContext>>();

            servicios.AgregarOpcionesMensajeria(configuracion);
            AgregarMassTransit(servicios, cadenaRabbit);

            return servicios;
        }
    }

    private static void AgregarMassTransit(IServiceCollection servicios, string cadenaRabbit) =>
        servicios.AddMassTransit(bus =>
        {
            // Outbox de consumidor + Inbox: deduplicación por MessageId y publicación atómica de eventos.
            bus.AddEntityFrameworkOutbox<AlmacenDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            bus.AddConsumer<ConsumidorSolicitarReservaStock>();
            bus.AddConsumer<ConsumidorLiberarReservaStock>();
            bus.AddConsumer<ConsumidorConfirmarReservaStock>();

            bus.UsingRabbitMq((contexto, rabbit) =>
            {
                rabbit.ConfigurarHost(cadenaRabbit);

                var reintentos = contexto.GetRequiredService<IOptions<OpcionesMensajeria>>().Value.Reintentos;

                // Cola única y explícita para todos los comandos de Almacén. Agotados los reintentos,
                // MassTransit mueve el mensaje a "almacen-comandos_error" (Dead Letter Queue).
                rabbit.ReceiveEndpoint(ColasIntegracion.ComandosAlmacen, endpoint =>
                {
                    endpoint.UseMessageRetry(politica => politica.AplicarPoliticaReintentos<ExcepcionDominio>(reintentos));
                    endpoint.UseEntityFrameworkOutbox<AlmacenDbContext>(contexto);

                    endpoint.ConfigureConsumer<ConsumidorSolicitarReservaStock>(contexto);
                    endpoint.ConfigureConsumer<ConsumidorLiberarReservaStock>(contexto);
                    endpoint.ConfigureConsumer<ConsumidorConfirmarReservaStock>(contexto);
                });
            });
        });
}
