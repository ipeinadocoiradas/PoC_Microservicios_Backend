using System.ComponentModel.DataAnnotations;
using Shared.Aplicacion.Cqrs;
using DistributedTransactions.ServiceDefaults.Http;
using Microsoft.AspNetCore.Mvc;
using Pagos.Application;

namespace Pagos.Api.Controllers;

/// <summary>
/// Acceso HTTP a los pagos simulados. Reutiliza el mismo caso de uso (e idempotencia) que el consumidor
/// de <c>ProcesarPago</c>, pero no publica eventos de integración.
/// </summary>
[ApiController]
[Route("api/pagos")]
[Produces("application/json")]
public sealed class PagosController(IDespachador despachador) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PagoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ObtenerPagoConsulta(id), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }

    /// <summary>Pago asociado a un pedido (relación 1:1).</summary>
    [HttpGet]
    [ProducesResponseType<PagoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorPedido([FromQuery, Required] Guid pedidoId, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ObtenerPagoPorPedidoConsulta(pedidoId), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }

    /// <summary>Procesa (o devuelve, si ya existía) el pago de un pedido. Un rechazo se devuelve con estado <c>Rechazado</c>.</summary>
    [HttpPost]
    [ProducesResponseType<PagoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Procesar(ProcesarPagoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await despachador.EnviarAsync(peticion.AComando(), cancellationToken);

        return resultado.EsCorrecto
            ? CreatedAtAction(nameof(Obtener), new { id = resultado.Valor.PagoId }, resultado.Valor)
            : this.Problema(resultado.Error);
    }
}

public sealed record ProcesarPagoPeticion(
    [Required] Guid? PedidoId,
    [Required] Guid? ClienteId,
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Importe,
    [Required, RegularExpression("^[A-Za-z]{3}$")] string Moneda)
{
    public ProcesarPagoComando AComando() => new(PedidoId!.Value, ClienteId!.Value, Importe, Moneda);
}
