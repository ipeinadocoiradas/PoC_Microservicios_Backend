namespace Contratos.Integracion;

/// <summary>
/// Direcciones de las colas de comandos. Forman parte del contrato de integración:
/// un comando tiene un único destinatario lógico, por lo que se envía (<c>Send</c>) a una cola concreta,
/// mientras que los eventos se publican (<c>Publish</c>) y cualquier interesado se suscribe.
/// </summary>
/// <remarks>
/// Enviar a una cola explícita garantiza que el comando no se pierda aunque el servicio destinatario
/// esté caído: la cola es duradera y retiene el mensaje hasta que el consumidor vuelva a estar disponible.
/// </remarks>
public static class ColasIntegracion
{
    /// <summary>Cola donde Almacen.Api recibe sus comandos.</summary>
    public const string ComandosAlmacen = "almacen-comandos";

    /// <summary>Cola donde Pagos.Api recibe sus comandos.</summary>
    public const string ComandosPagos = "pagos-comandos";

    /// <summary>Dirección MassTransit de la cola de comandos de Almacén.</summary>
    public static Uri DireccionComandosAlmacen { get; } = new($"queue:{ComandosAlmacen}");

    /// <summary>Dirección MassTransit de la cola de comandos de Pagos.</summary>
    public static Uri DireccionComandosPagos { get; } = new($"queue:{ComandosPagos}");
}
