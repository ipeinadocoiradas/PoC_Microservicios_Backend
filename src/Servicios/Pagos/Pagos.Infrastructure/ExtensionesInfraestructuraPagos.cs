using Shared.Infraestructura.Mensajeria;
using Shared.Infraestructura.Persistencia;
using Contratos.Integracion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pagos.Application.Abstracciones;
using Pagos.Domain;
using Pagos.Infrastructure.Mensajeria;
using Pagos.Infrastructure.Persistencia;
using Pagos.Infrastructure.Simulacion;

namespace Pagos.Infrastructure;

/// <summary>Composición de la infraestructura de Pagos.</summary>
public static class ExtensionesInfraestructuraPagos
{
    extension(IServiceCollection servicios)
    {
        public IServiceCollection AgregarInfraestructuraPagos(IConfiguration configuracion)
        {
            var cadenaSql = configuracion.ObtenerCadenaConexionObligatoria(PagosDbContext.NombreCadenaConexion);
            var cadenaRabbit = configuracion.ObtenerCadenaConexionObligatoria(NombresConexion.RabbitMq);

            servicios.AddDbContext<PagosDbContext>(opciones => opciones.UseSqlServer(cadenaSql));
            servicios.AddScoped<IUnidadTrabajo>(proveedor => proveedor.GetRequiredService<PagosDbContext>());
            servicios.AddScoped<IPagoRepositorio, RepositorioPagos>();

            servicios.AddOptions<OpcionesSimulacionPagos>().Bind(configuracion.GetSection(OpcionesSimulacionPagos.Seccion));
            servicios.AddScoped<IAutorizadorPagos, SimuladorAutorizacionPagos>();

            servicios.AddHealthChecks().AddDbContextCheck<PagosDbContext>("pagos-db", tags: ["ready"]);
            servicios.AddHostedService<InicializadorBaseDatos<PagosDbContext>>();

            servicios.AgregarOpcionesMensajeria(configuracion);
            AgregarMassTransit(servicios, cadenaRabbit);

            return servicios;
        }
    }

    private static void AgregarMassTransit(IServiceCollection servicios, string cadenaRabbit) =>
        servicios.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<PagosDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            bus.AddConsumer<ConsumidorProcesarPago>();
            bus.AddConsumer<ConsumidorCancelarPago>();

            bus.UsingRabbitMq((contexto, rabbit) =>
            {
                rabbit.ConfigurarHost(cadenaRabbit);

                var reintentos = contexto.GetRequiredService<IOptions<OpcionesMensajeria>>().Value.Reintentos;

                // Agotados los reintentos, el mensaje pasa a "pagos-comandos_error" (Dead Letter Queue).
                rabbit.ReceiveEndpoint(ColasIntegracion.ComandosPagos, endpoint =>
                {
                    endpoint.UseMessageRetry(politica => politica.AplicarPoliticaReintentos<ExcepcionDominio>(reintentos));
                    endpoint.UseEntityFrameworkOutbox<PagosDbContext>(contexto);

                    endpoint.ConfigureConsumer<ConsumidorProcesarPago>(contexto);
                    endpoint.ConfigureConsumer<ConsumidorCancelarPago>(contexto);
                });
            });
        });
}
