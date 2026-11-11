using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Almacen.Infrastructure.Simulacion;

/// <summary>Configuración de la inyección de fallos (sección <c>SimulacionFallos</c>).</summary>
public sealed class OpcionesSimulacionFallos
{
    public const string Seccion = "SimulacionFallos";

    /// <summary>
    /// Número de intentos de <c>LiberarReservaStock</c> que fallarán por pedido antes de procesarse.
    /// 0 desactiva la simulación. Si supera el número de reintentos, el mensaje acabará en la cola de errores.
    /// </summary>
    public int FallosAntesDeLiberarReserva { get; init; }
}

/// <summary>Error transitorio simulado (equivalente a un timeout de base de datos o de red).</summary>
public sealed class ExcepcionTransitoriaSimulada(string mensaje) : Exception(mensaje);

/// <summary>
/// Inyecta fallos transitorios controlados en la compensación para demostrar reintentos con backoff,
/// envío a la Dead Letter Queue y recuperación. Solo existe con fines didácticos.
/// </summary>
public sealed class SimuladorFallosTransitorios(IOptions<OpcionesSimulacionFallos> opciones)
{
    private readonly ConcurrentDictionary<Guid, int> _intentosPorPedido = new();

    /// <summary>Registra un intento de liberación y lanza un error transitorio mientras proceda.</summary>
    public void RegistrarIntentoLiberacion(Guid pedidoId)
    {
        var intento = _intentosPorPedido.AddOrUpdate(pedidoId, 1, (_, anterior) => anterior + 1);
        var fallosConfigurados = opciones.Value.FallosAntesDeLiberarReserva;

        if (intento <= fallosConfigurados)
        {
            throw new ExcepcionTransitoriaSimulada(
                $"Fallo transitorio simulado liberando la reserva del pedido {pedidoId} (intento {intento} de {fallosConfigurados} fallidos).");
        }
    }

    public int ObtenerIntentos(Guid pedidoId) => _intentosPorPedido.GetValueOrDefault(pedidoId);
}
