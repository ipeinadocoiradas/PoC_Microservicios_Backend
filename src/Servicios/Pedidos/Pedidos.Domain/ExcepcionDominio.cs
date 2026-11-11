namespace Pedidos.Domain;

/// <summary>
/// Se lanza cuando una operación incumpliría una invariante del dominio de Pedidos.
/// Indica un error permanente: reintentar la misma operación nunca tendrá éxito.
/// </summary>
public sealed class ExcepcionDominio(string mensaje) : Exception(mensaje);
