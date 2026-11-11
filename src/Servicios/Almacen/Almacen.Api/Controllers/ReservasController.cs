using System.ComponentModel.DataAnnotations;
using Almacen.Application.Reservas;
using Shared.Aplicacion.Cqrs;
using DistributedTransactions.ServiceDefaults.Http;
using Microsoft.AspNetCore.Mvc;

namespace Almacen.Api.Controllers;

/// <summary>
/// Acceso HTTP a las reservas, útil para pruebas manuales e inspección. En el flujo de la Saga las reservas
/// se crean por mensajería; esta vía reutiliza exactamente el mismo caso de uso (y su idempotencia),
/// pero no publica eventos de integración.
/// </summary>
[ApiController]
[Route("api/reservas")]
[Produces("application/json")]
public sealed class ReservasController(IDespachador despachador) : ControllerBase
{
    /// <summary>Crea (o devuelve, si ya existía) la reserva de un pedido. Una reserva rechazada se devuelve con estado <c>Rechazada</c>.</summary>
    [HttpPost]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(ReservarStockPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await despachador.EnviarAsync(peticion.AComando(), cancellationToken);

        return resultado.EsCorrecto
            ? CreatedAtAction(nameof(ObtenerPorPedido), new { pedidoId = resultado.Valor.PedidoId }, resultado.Valor)
            : this.Problema(resultado.Error);
    }

    /// <summary>Reserva asociada a un pedido (relación 1:1).</summary>
    [HttpGet]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorPedido([FromQuery, Required] Guid pedidoId, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ObtenerReservaPorPedidoConsulta(pedidoId), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }
}

public sealed record ReservarStockPeticion(
    [Required] Guid? PedidoId,
    [Required, MinLength(1)] IReadOnlyList<LineaReservaPeticion> Lineas)
{
    public ReservarStockComando AComando() =>
        new(PedidoId!.Value, [.. Lineas.Select(linea => new LineaSolicitadaDto(linea.ProductoId!.Value, linea.Cantidad))]);
}

public sealed record LineaReservaPeticion([Required] Guid? ProductoId, [Range(1, 1000)] int Cantidad);
