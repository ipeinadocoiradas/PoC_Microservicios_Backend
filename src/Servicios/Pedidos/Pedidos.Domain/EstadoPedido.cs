namespace Pedidos.Domain;

/// <summary>
/// Estado de negocio del pedido, el que ve el cliente.
/// </summary>
/// <remarks>
/// No debe confundirse con el estado interno de la Saga (ReservandoStock, ProcesandoPago, CompensandoOperaciones…):
/// mientras la Saga atraviesa varios estados técnicos, el pedido permanece <see cref="Pendiente"/>
/// hasta que el proceso termina en uno de sus estados finales.
/// </remarks>
public enum EstadoPedido
{
    /// <summary>Registrado y en proceso: aún no se ha decidido si se completa o se cancela.</summary>
    Pendiente = 1,

    /// <summary>Stock reservado y pago cobrado. Estado final.</summary>
    Completado = 2,

    /// <summary>No pudo completarse por una causa de negocio (sin stock, pago rechazado). Estado final.</summary>
    Cancelado = 3,

    /// <summary>No pudo completarse por un fallo técnico; las compensaciones se ejecutaron. Estado final.</summary>
    Fallido = 4
}
