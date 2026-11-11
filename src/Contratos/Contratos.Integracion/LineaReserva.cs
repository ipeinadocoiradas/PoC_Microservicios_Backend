namespace Contratos.Integracion;

/// <summary>
/// Unidades de un producto que deben reservarse. Es un dato de integración, no una entidad:
/// cada servicio lo traduce a su propio modelo de dominio.
/// </summary>
/// <param name="ProductoId">Identificador del producto en el catálogo de Almacén.</param>
/// <param name="Cantidad">Número de unidades solicitadas.</param>
public sealed record LineaReserva(Guid ProductoId, int Cantidad);
