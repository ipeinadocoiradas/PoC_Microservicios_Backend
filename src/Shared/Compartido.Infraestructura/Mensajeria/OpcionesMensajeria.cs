using System.ComponentModel.DataAnnotations;

namespace Shared.Infraestructura.Mensajeria;

/// <summary>Configuración de mensajería (sección <c>Mensajeria</c> de appsettings).</summary>
public sealed class OpcionesMensajeria
{
    public const string Seccion = "Mensajeria";

    [Required]
    public OpcionesReintentos Reintentos { get; init; } = new();
}

/// <summary>
/// Política de reintentos con backoff exponencial para errores transitorios.
/// </summary>
/// <remarks>
/// El intervalo entre intentos crece con <see cref="Incremento"/> partiendo de <see cref="IntervaloMinimo"/>
/// y nunca supera <see cref="IntervaloMaximo"/>. Agotados los intentos, MassTransit mueve el mensaje a la
/// cola <c>&lt;cola&gt;_error</c> (Dead Letter Queue) y publica un <c>Fault&lt;T&gt;</c>.
/// </remarks>
public sealed class OpcionesReintentos
{
    [Range(1, 20)]
    public int NumeroMaximo { get; init; } = 5;

    public TimeSpan IntervaloMinimo { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan IntervaloMaximo { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan Incremento { get; init; } = TimeSpan.FromSeconds(2);
}
