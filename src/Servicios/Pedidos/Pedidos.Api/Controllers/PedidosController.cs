using Shared.Aplicacion.Cqrs;
using DistributedTransactions.ServiceDefaults.Http;
using Microsoft.AspNetCore.Mvc;
using Pedidos.Api.Peticiones;
using Pedidos.Application.ConsultarPedido;
using Pedidos.Application.CrearPedido;

namespace Pedidos.Api.Controllers;

/// <summary>
/// API de pedidos. La creación devuelve <b>202 Accepted</b>: el pedido queda registrado pero su resultado
/// (completado o cancelado) se decide de forma asíncrona; el cliente consulta <c>/estado</c> para seguirlo.
/// </summary>
[ApiController]
[Route("api/pedidos")]
[Produces("application/json")]
public sealed class PedidosController(IDespachador despachador) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PedidoCreadoDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Crear(CrearPedidoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await despachador.EnviarAsync(peticion.AComando(), cancellationToken);

        return resultado.EsCorrecto
            ? AcceptedAtAction(nameof(ObtenerEstado), new { id = resultado.Valor.PedidoId }, resultado.Valor)
            : this.Problema(resultado.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ObtenerPedidoConsulta(id), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }

    [HttpGet("{id:guid}/estado")]
    [ProducesResponseType<EstadoPedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerEstado(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ObtenerEstadoPedidoConsulta(id), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }

    /// <summary>Transiciones de la Saga del pedido, en el orden en que ocurrieron.</summary>
    [HttpGet("{id:guid}/historial")]
    [ProducesResponseType<IReadOnlyList<TransicionSagaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerHistorial(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ObtenerHistorialSagaConsulta(id), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }
}
