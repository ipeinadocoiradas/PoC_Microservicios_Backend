using System.Diagnostics;
using DistributedTransactions.ServiceDefaults.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DistributedTransactions.ServiceDefaults;

/// <summary>
/// Configuración transversal compartida por las tres APIs.
/// </summary>
/// <remarks>
/// La trazabilidad distribuida funciona así: ASP.NET Core abre una <see cref="Activity"/> por petición;
/// MassTransit (fuente <c>MassTransit</c>) copia el contexto W3C (<c>traceparent</c>) en las cabeceras de
/// cada mensaje y lo restaura al consumirlo. El resultado es una única traza que recorre
/// Pedidos → RabbitMQ → Almacén → Pagos, visible en el dashboard de Aspire.
/// </remarks>
public static class ExtensionesServiceDefaults
{
    /// <summary>Nombre del <see cref="ActivitySource"/> y del medidor que publica MassTransit.</summary>
    private const string FuenteMassTransit = "MassTransit";

    private const string EtiquetaVivo = "live";

    extension(IHostApplicationBuilder builder)
    {
        /// <summary>Añade OpenTelemetry, health checks y ProblemDetails.</summary>
        public IHostApplicationBuilder AgregarServiceDefaults()
        {
            ConfigurarOpenTelemetry(builder);

            builder.Services.AddHealthChecks()
                .AddCheck("proceso", () => HealthCheckResult.Healthy(), [EtiquetaVivo]);

            builder.Services.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
            {
                contexto.ProblemDetails.Instance ??= contexto.HttpContext.Request.Path;
                contexto.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? contexto.HttpContext.TraceIdentifier;
            });

            return builder;
        }
    }

    private static void ConfigurarOpenTelemetry(IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(registro =>
        {
            registro.IncludeFormattedMessage = true;
            registro.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metricas => metricas
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(FuenteMassTransit))
            .WithTracing(trazas => trazas
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(FuenteMassTransit)
                .AddAspNetCoreInstrumentation(opciones =>
                    opciones.Filter = contexto => !EsPeticionDeSalud(contexto.Request.Path))
                .AddHttpClientInstrumentation());

        // Aspire y Docker Compose definen OTEL_EXPORTER_OTLP_ENDPOINT; sin él no se exporta nada.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }

    extension(WebApplication aplicacion)
    {
        /// <summary>Añade el pipeline común: excepciones, ID de correlación y endpoints de salud.</summary>
        public WebApplication UsarServiceDefaults()
        {
            aplicacion.UseExceptionHandler();
            aplicacion.UseStatusCodePages();
            aplicacion.UseMiddleware<MiddlewareIdCorrelacion>();

            // /health: todas las comprobaciones (BD, bus). /alive: solo que el proceso responde.
            aplicacion.MapHealthChecks("/health");
            aplicacion.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = registro => registro.Tags.Contains(EtiquetaVivo)
            });

            return aplicacion;
        }
    }

    private static bool EsPeticionDeSalud(PathString ruta) =>
        ruta.StartsWithSegments("/health") || ruta.StartsWithSegments("/alive");
}
