using Shared.Infraestructura.Mensajeria;
using Shared.Infraestructura.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pedidos.Application.Abstracciones;
using Pedidos.Application.Saga;
using Pedidos.Domain;
using Pedidos.Infrastructure.Persistencia;

namespace Pedidos.Infrastructure;

/// <summary>Composición de la infraestructura de Pedidos (persistencia, Outbox, Saga y RabbitMQ).</summary>
public static class ExtensionesInfraestructuraPedidos
{
    /// <summary>Cola de la Saga: recibe los eventos de Almacén y Pagos.</summary>
    public const string ColaSaga = "pedidos-saga";

    extension(IServiceCollection servicios)
    {
        public IServiceCollection AgregarInfraestructuraPedidos(IConfiguration configuracion)
        {
            var cadenaSql = configuracion.ObtenerCadenaConexionObligatoria(PedidosDbContext.NombreCadenaConexion);
            var cadenaRabbit = configuracion.ObtenerCadenaConexionObligatoria(NombresConexion.RabbitMq);

            servicios.AddDbContext<PedidosDbContext>(opciones => opciones.UseSqlServer(cadenaSql));
            servicios.AddScoped<IUnidadTrabajo>(proveedor => proveedor.GetRequiredService<PedidosDbContext>());
            servicios.AddScoped<IPedidoRepositorio, RepositorioPedidos>();
            servicios.AddScoped<ILectorEstadoSaga, LectorEstadoSaga>();
            servicios.AddScoped<IRegistroHistorialSaga, RegistroHistorialSaga>();
            servicios.AddScoped<ILectorHistorialSaga, LectorHistorialSaga>();
            servicios.AddScoped<IIniciadorProcesoPedido, IniciadorProcesoPedido>();

            servicios.AddHealthChecks().AddDbContextCheck<PedidosDbContext>("pedidos-db", tags: ["ready"]);

            // El inicializador se registra antes que MassTransit: los hosted services arrancan en orden.
            servicios.AddHostedService<InicializadorBaseDatos<PedidosDbContext>>();

            servicios.AgregarOpcionesMensajeria(configuracion);
            AgregarMassTransit(servicios, cadenaRabbit);

            return servicios;
        }
    }

    private static void AgregarMassTransit(IServiceCollection servicios, string cadenaRabbit) =>
        servicios.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            // Transactional Outbox + Inbox sobre PedidosDb.
            //  - UseBusOutbox: los mensajes publicados desde la API (fuera de un consumidor) también pasan por el Outbox.
            //  - Inbox: deduplica por MessageId los mensajes recibidos (primera línea de idempotencia).
            bus.AddEntityFrameworkOutbox<PedidosDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            // Se aplica a todos los endpoints configurados automáticamente (aquí, la cola de la Saga).
            // Orden: primero reintentos y después Outbox, para que cada reintento use una transacción nueva.
            bus.AddConfigureEndpointsCallback((contexto, _, endpoint) =>
            {
                var reintentos = contexto.GetRequiredService<IOptions<OpcionesMensajeria>>().Value.Reintentos;
                endpoint.UseMessageRetry(politica => politica.AplicarPoliticaReintentos<ExcepcionDominio>(reintentos));
                endpoint.UseEntityFrameworkOutbox<PedidosDbContext>(contexto);
            });

            bus.AddSagaStateMachine<GestorSagaPedidos, EstadoSagaPedido>()
                .EntityFrameworkRepository(repositorio =>
                {
                    // Concurrencia optimista (rowversion): si dos eventos del mismo pedido llegan a la vez,
                    // uno falla con conflicto y se reintenta sobre el estado ya actualizado.
                    repositorio.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    repositorio.ExistingDbContext<PedidosDbContext>();
                    repositorio.UseSqlServer();
                })
                .Endpoint(endpoint => endpoint.Name = ColaSaga);

            bus.UsingRabbitMq((contexto, rabbit) =>
            {
                rabbit.ConfigurarHost(cadenaRabbit);
                rabbit.ConfigureEndpoints(contexto);
            });
        });
}
