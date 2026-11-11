using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Infraestructura.Mensajeria;

/// <summary>Nombres de las cadenas de conexión compartidas por Aspire, Docker Compose y las pruebas.</summary>
public static class NombresConexion
{
    /// <summary>Clave <c>ConnectionStrings:rabbitmq</c>, con formato <c>amqp://usuario:clave@host:puerto</c>.</summary>
    public const string RabbitMq = "rabbitmq";
}

/// <summary>Configuración de MassTransit común a los tres microservicios.</summary>
public static class ExtensionesMensajeria
{
    extension(IServiceCollection servicios)
    {
        /// <summary>
        /// Registra las opciones de mensajería y hace que el host espere a que el bus esté arrancado,
        /// de modo que colas y enlaces existan antes de aceptar peticiones.
        /// </summary>
        public IServiceCollection AgregarOpcionesMensajeria(IConfiguration configuracion)
        {
            servicios.AddOptions<OpcionesMensajeria>()
                .Bind(configuracion.GetSection(OpcionesMensajeria.Seccion))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            servicios.AddOptions<MassTransitHostOptions>().Configure(opciones =>
            {
                opciones.WaitUntilStarted = true;
                opciones.StartTimeout = TimeSpan.FromSeconds(60);
                opciones.StopTimeout = TimeSpan.FromSeconds(30);
            });

            return servicios;
        }
    }

    extension(IRabbitMqBusFactoryConfigurator configurador)
    {
        /// <summary>Configura el host de RabbitMQ a partir de una URI AMQP con credenciales.</summary>
        public void ConfigurarHost(string cadenaConexion)
        {
            var uri = new Uri(cadenaConexion);
            var credenciales = uri.UserInfo.Split(':', 2);

            configurador.Host(uri, host =>
            {
                if (credenciales.Length == 2)
                {
                    host.Username(Uri.UnescapeDataString(credenciales[0]));
                    host.Password(Uri.UnescapeDataString(credenciales[1]));
                }
            });
        }
    }

    extension(IRetryConfigurator reintentos)
    {
        /// <summary>
        /// Aplica el backoff exponencial configurado. Las excepciones de <typeparamref name="TExcepcionPermanente"/>
        /// (reglas de negocio incumplidas) no se reintentan: repetirlas nunca tendría éxito.
        /// </summary>
        public void AplicarPoliticaReintentos<TExcepcionPermanente>(OpcionesReintentos opciones)
            where TExcepcionPermanente : Exception
        {
            reintentos.Exponential(opciones.NumeroMaximo, opciones.IntervaloMinimo, opciones.IntervaloMaximo, opciones.Incremento);
            reintentos.Ignore<TExcepcionPermanente>();
        }
    }

    extension(IConfiguration configuracion)
    {
        /// <summary>Obtiene una cadena de conexión o falla al arrancar con un mensaje claro.</summary>
        public string ObtenerCadenaConexionObligatoria(string nombre) =>
            configuracion.GetConnectionString(nombre) is { Length: > 0 } cadena
                ? cadena
                : throw new InvalidOperationException($"Falta la cadena de conexión 'ConnectionStrings:{nombre}'.");
    }

    extension(ModelBuilder modelo)
    {
        /// <summary>
        /// Añade al modelo de EF Core las tablas del Transactional Outbox e Inbox de MassTransit.
        /// Se renombran en español; sus columnas pertenecen a la librería y conservan su nombre original.
        /// </summary>
        public ModelBuilder AgregarTablasMensajeria()
        {
            modelo.AddInboxStateEntity(entidad => entidad.ToTable("BandejaEntradaEstado"));
            modelo.AddOutboxMessageEntity(entidad => entidad.ToTable("BandejaSalidaMensajes"));
            modelo.AddOutboxStateEntity(entidad => entidad.ToTable("BandejaSalidaEstado"));
            return modelo;
        }
    }

    extension(ILogger registro)
    {
        /// <summary>Abre un ámbito de log con los identificadores de correlación del mensaje.</summary>
        public IDisposable? IniciarAmbitoMensaje(Guid correlationId, Guid pedidoId) =>
            registro.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["PedidoId"] = pedidoId
            });
    }
}
