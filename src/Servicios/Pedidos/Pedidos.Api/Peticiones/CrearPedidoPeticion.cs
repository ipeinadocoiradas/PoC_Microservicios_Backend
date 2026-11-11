using System.ComponentModel.DataAnnotations;
using Pedidos.Application.CrearPedido;

namespace Pedidos.Api.Peticiones;

/// <summary>
/// DTO de entrada de <c>POST /api/pedidos</c>. Las anotaciones validan el formato (HTTP 400 automático
/// por <c>[ApiController]</c>); las reglas de negocio se validan en Application y en el dominio.
/// </summary>
public sealed record CrearPedidoPeticion(
    [Required] Guid? ClienteId,
    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "La moneda debe ser un código ISO 4217 de tres letras.")] string Moneda,
    [Required, MinLength(1)] IReadOnlyList<LineaPedidoPeticion> Lineas)
{
    public CrearPedidoComando AComando() => new(
        ClienteId!.Value,
        Moneda,
        [.. Lineas.Select(linea => new LineaPedidoSolicitada(linea.ProductoId!.Value, linea.Cantidad, linea.PrecioUnitario))]);
}

public sealed record LineaPedidoPeticion(
    [Required] Guid? ProductoId,
    [Range(1, 1000)] int Cantidad,
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal PrecioUnitario);
