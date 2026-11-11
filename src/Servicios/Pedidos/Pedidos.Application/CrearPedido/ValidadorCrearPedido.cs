using Shared.Aplicacion.Resultados;
using Pedidos.Domain;

namespace Pedidos.Application.CrearPedido;

/// <summary>
/// Validación de entrada del caso de uso. Devuelve errores amigables (HTTP 400) antes de llegar al dominio,
/// que conserva sus propias comprobaciones como última línea de defensa de sus invariantes.
/// </summary>
internal static class ValidadorCrearPedido
{
    public static Error? Validar(CrearPedidoComando comando)
    {
        if (comando.ClienteId == Guid.Empty)
        {
            return ErroresPedidos.ClienteObligatorio;
        }

        if (string.IsNullOrWhiteSpace(comando.Moneda) || comando.Moneda.Trim().Length != 3)
        {
            return ErroresPedidos.MonedaNoValida;
        }

        if (comando.Lineas is null || comando.Lineas.Count == 0)
        {
            return ErroresPedidos.SinLineas;
        }

        if (comando.Lineas.Any(EsLineaNoValida))
        {
            return ErroresPedidos.LineaNoValida;
        }

        return comando.Lineas.DistinctBy(linea => linea.ProductoId).Count() == comando.Lineas.Count
            ? null
            : ErroresPedidos.ProductoRepetido;
    }

    private static bool EsLineaNoValida(LineaPedidoSolicitada linea) =>
        linea.ProductoId == Guid.Empty
        || linea.Cantidad is <= 0 or > LineaPedido.CantidadMaximaPorLinea
        || linea.PrecioUnitario <= 0;
}
