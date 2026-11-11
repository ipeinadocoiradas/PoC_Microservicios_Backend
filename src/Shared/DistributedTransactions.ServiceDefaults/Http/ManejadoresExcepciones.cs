using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedTransactions.ServiceDefaults.Http;

/// <summary>
/// Traduce las excepciones de dominio de un servicio (invariantes incumplidas) a HTTP 422 con ProblemDetails.
/// </summary>
/// <typeparam name="TExcepcionDominio">Excepción de dominio propia del servicio.</typeparam>
public sealed class ManejadorExcepcionesDominio<TExcepcionDominio>(
    IProblemDetailsService servicioProblemas,
    ILogger<ManejadorExcepcionesDominio<TExcepcionDominio>> registro) : IExceptionHandler
    where TExcepcionDominio : Exception
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken cancellationToken)
    {
        if (excepcion is not TExcepcionDominio)
        {
            return false;
        }

        registro.LogWarning("Regla de dominio incumplida: {Mensaje}", excepcion.Message);
        contexto.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        return await servicioProblemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Regla de negocio incumplida",
                Detail = excepcion.Message
            }
        });
    }
}

/// <summary>
/// Último recurso: cualquier excepción no controlada se registra y se responde con HTTP 500 sin
/// filtrar detalles internos al cliente.
/// </summary>
public sealed class ManejadorGlobalExcepciones(
    IProblemDetailsService servicioProblemas,
    ILogger<ManejadorGlobalExcepciones> registro) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken cancellationToken)
    {
        registro.LogError(excepcion, "Error no controlado procesando {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);
        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await servicioProblemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = "Se ha producido un error inesperado. Consulte el traceId en los logs."
            }
        });
    }
}

/// <summary>Registro ordenado de los manejadores de excepciones.</summary>
public static class ExtensionesManejoExcepciones
{
    extension(IServiceCollection servicios)
    {
        /// <summary>
        /// Registra primero el manejador de la excepción de dominio y después el global:
        /// ASP.NET Core los invoca en orden de registro hasta que uno la gestiona.
        /// </summary>
        public IServiceCollection AgregarGestionExcepciones<TExcepcionDominio>()
            where TExcepcionDominio : Exception
        {
            servicios.AddExceptionHandler<ManejadorExcepcionesDominio<TExcepcionDominio>>();
            servicios.AddExceptionHandler<ManejadorGlobalExcepciones>();
            return servicios;
        }
    }
}
