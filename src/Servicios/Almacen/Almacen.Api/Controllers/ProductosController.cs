using Almacen.Application.Productos;
using Shared.Aplicacion.Cqrs;
using DistributedTransactions.ServiceDefaults.Http;
using Microsoft.AspNetCore.Mvc;

namespace Almacen.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Produces("application/json")]
public sealed class ProductosController(IDespachador despachador) : ControllerBase
{
    /// <summary>Stock físico, reservado y disponible de un producto.</summary>
    [HttpGet("{id:guid}/disponibilidad")]
    [ProducesResponseType<DisponibilidadProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerDisponibilidad(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await despachador.ConsultarAsync(new ConsultarDisponibilidadConsulta(id), cancellationToken);
        return resultado.EsCorrecto ? Ok(resultado.Valor) : this.Problema(resultado.Error);
    }
}
