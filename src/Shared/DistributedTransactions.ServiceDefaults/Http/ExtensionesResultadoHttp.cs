using Shared.Aplicacion.Resultados;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DistributedTransactions.ServiceDefaults.Http;

/// <summary>Traducción del <see cref="Error"/> de aplicación a una respuesta HTTP ProblemDetails.</summary>
public static class ExtensionesResultadoHttp
{
    extension(ControllerBase controlador)
    {
        /// <summary>Crea una respuesta ProblemDetails (RFC 9457) con el código HTTP correspondiente al error.</summary>
        public ObjectResult Problema(Error error)
        {
            var respuesta = controlador.Problem(
                title: Titulo(error.Tipo),
                detail: error.Descripcion,
                statusCode: CodigoHttp(error.Tipo));

            if (respuesta.Value is ProblemDetails problema)
            {
                problema.Extensions["codigo"] = error.Codigo;
            }

            return respuesta;
        }
    }

    private static int CodigoHttp(TipoError tipo) => tipo switch
    {
        TipoError.Validacion => StatusCodes.Status400BadRequest,
        TipoError.NoEncontrado => StatusCodes.Status404NotFound,
        TipoError.Conflicto => StatusCodes.Status409Conflict,
        TipoError.ReglaNegocio => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string Titulo(TipoError tipo) => tipo switch
    {
        TipoError.Validacion => "Datos de entrada no válidos",
        TipoError.NoEncontrado => "Recurso no encontrado",
        TipoError.Conflicto => "Conflicto con el estado actual",
        TipoError.ReglaNegocio => "Regla de negocio incumplida",
        _ => "Error"
    };
}
