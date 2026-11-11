namespace Pagos.Domain;

/// <summary>Invariante del dominio de Pagos incumplida. Error permanente: no se reintenta.</summary>
public sealed class ExcepcionDominio(string mensaje) : Exception(mensaje);
