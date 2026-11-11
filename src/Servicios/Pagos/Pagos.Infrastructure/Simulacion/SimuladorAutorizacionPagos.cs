using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pagos.Application.Abstracciones;

namespace Pagos.Infrastructure.Simulacion;

/// <summary>Reglas de la pasarela simulada (sección <c>SimulacionPagos</c>).</summary>
public sealed class OpcionesSimulacionPagos
{
    public const string Seccion = "SimulacionPagos";

    /// <summary>Importes superiores se rechazan (rechazo de negocio → <c>PagoRechazado</c>).</summary>
    public decimal ImporteMaximoAutorizado { get; init; } = 1_000m;

    /// <summary>
    /// Un pago por exactamente este importe provoca un fallo técnico permanente (pasarela caída). Tras agotar
    /// los reintentos, la Saga recibe <c>Fault&lt;ProcesarPago&gt;</c> y el pedido termina como Fallido.
    /// Nulo desactiva la simulación.
    /// </summary>
    public decimal? ImporteFalloTecnico { get; init; } = 666.66m;

    /// <summary>Latencia artificial para que las trazas muestren el tiempo de la "pasarela".</summary>
    public TimeSpan LatenciaSimulada { get; init; } = TimeSpan.FromMilliseconds(150);
}

/// <summary>La pasarela no responde (error técnico transitorio desde el punto de vista del consumidor).</summary>
public sealed class ExcepcionPasarelaNoDisponible(string mensaje) : Exception(mensaje);

/// <summary>Pasarela de pagos simulada. No existe integración bancaria real.</summary>
public sealed class SimuladorAutorizacionPagos(
    IOptions<OpcionesSimulacionPagos> opciones,
    ILogger<SimuladorAutorizacionPagos> registro) : IAutorizadorPagos
{
    public async Task<ResultadoAutorizacion> AutorizarAsync(SolicitudAutorizacion solicitud, CancellationToken cancellationToken)
    {
        var reglas = opciones.Value;

        if (reglas.LatenciaSimulada > TimeSpan.Zero)
        {
            await Task.Delay(reglas.LatenciaSimulada, cancellationToken);
        }

        if (reglas.ImporteFalloTecnico is { } importeFallo && solicitud.Importe == importeFallo)
        {
            registro.LogWarning("Pasarela simulada no disponible para el pago {PagoId}", solicitud.PagoId);
            throw new ExcepcionPasarelaNoDisponible("La pasarela de pagos simulada no responde.");
        }

        if (solicitud.Importe > reglas.ImporteMaximoAutorizado)
        {
            return ResultadoAutorizacion.Denegar(
                $"El importe {solicitud.Importe:0.00} {solicitud.Moneda} supera el límite autorizado de {reglas.ImporteMaximoAutorizado:0.00}.");
        }

        return ResultadoAutorizacion.Aprobar($"AUT-{Guid.NewGuid():N}"[..16].ToUpperInvariant());
    }
}
