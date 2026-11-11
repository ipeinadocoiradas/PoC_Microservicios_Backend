namespace Almacen.Domain;

/// <summary>Invariante del dominio de Almacén incumplida. Error permanente: no se reintenta.</summary>
public sealed class ExcepcionDominio(string mensaje) : Exception(mensaje);
