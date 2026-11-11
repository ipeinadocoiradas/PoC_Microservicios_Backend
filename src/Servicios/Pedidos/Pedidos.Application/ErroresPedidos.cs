using Shared.Aplicacion.Resultados;

namespace Pedidos.Application;

/// <summary>Catálogo de errores esperados del servicio de Pedidos.</summary>
public static class ErroresPedidos
{
    public static readonly Error ClienteObligatorio =
        Error.Validacion("Pedido.ClienteObligatorio", "Debe indicarse el cliente del pedido.");

    public static readonly Error MonedaNoValida =
        Error.Validacion("Pedido.MonedaNoValida", "La moneda debe ser un código ISO 4217 de tres letras.");

    public static readonly Error SinLineas =
        Error.Validacion("Pedido.SinLineas", "El pedido debe contener al menos una línea.");

    public static readonly Error LineaNoValida =
        Error.Validacion("Pedido.LineaNoValida", "Cada línea necesita producto, una cantidad positiva y un precio mayor que cero.");

    public static readonly Error ProductoRepetido =
        Error.Validacion("Pedido.ProductoRepetido", "Un producto no puede aparecer en varias líneas del mismo pedido.");

    public static Error NoEncontrado(Guid pedidoId) =>
        Error.NoEncontrado("Pedido.NoEncontrado", $"No existe el pedido {pedidoId}.");
}
