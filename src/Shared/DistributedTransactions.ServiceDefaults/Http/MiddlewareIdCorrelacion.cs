using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DistributedTransactions.ServiceDefaults.Http;

/// <summary>
/// Propaga la cabecera <c>X-Correlation-ID</c>: la toma de la petición o, si no viene, usa el TraceId actual.
/// La devuelve en la respuesta, la etiqueta en la traza y la añade al ámbito de log.
/// </summary>
/// <remarks>
/// Dentro de la plataforma, la correlación entre mensajes se hace con el <c>CorrelationId</c> de los contratos
/// (el identificador del pedido) y con el contexto W3C de la traza; esta cabecera cubre el tramo cliente → API.
/// </remarks>
public sealed class MiddlewareIdCorrelacion(RequestDelegate siguiente, ILogger<MiddlewareIdCorrelacion> registro)
{
    public const string Cabecera = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext contexto)
    {
        var idCorrelacion = contexto.Request.Headers.TryGetValue(Cabecera, out var valor) && !string.IsNullOrWhiteSpace(valor)
            ? valor.ToString()
            : Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;

        contexto.Response.Headers[Cabecera] = idCorrelacion;
        Activity.Current?.SetTag("correlation.id", idCorrelacion);

        using (registro.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = idCorrelacion }))
        {
            await siguiente(contexto);
        }
    }
}
