using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging;
using Pedidos.Application.Abstracciones;
using Pedidos.Domain;

namespace Pedidos.Application.CrearPedido;

/// <summary>
/// Crea el pedido y arranca la Saga de forma atómica.
/// </summary>
/// <remarks>
/// El orden importa: primero se añade el pedido al contexto, después se escribe el mensaje de inicio en el
/// Outbox y, por último, se confirma todo en <b>una sola transacción local</b>. Si la confirmación falla,
/// ni el pedido ni el mensaje existen; si tiene éxito, el mensaje se entregará aunque RabbitMQ esté caído
/// en ese momento (el servicio de entrega del Outbox lo reintentará).
/// </remarks>
public sealed class ManejadorCrearPedido(
    IPedidoRepositorio repositorio,
    IIniciadorProcesoPedido iniciadorProceso,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj,
    ILogger<ManejadorCrearPedido> registro) : IManejadorComando<CrearPedidoComando, PedidoCreadoDto>
{
    public async Task<Resultado<PedidoCreadoDto>> ManejarAsync(CrearPedidoComando comando, CancellationToken cancellationToken)
    {
        if (ValidadorCrearPedido.Validar(comando) is { } error)
        {
            return error;
        }

        var lineas = comando.Lineas
            .Select(linea => LineaPedido.Crear(linea.ProductoId, linea.Cantidad, linea.PrecioUnitario))
            .ToList();

        var pedido = Pedido.Crear(comando.ClienteId, comando.Moneda, lineas, reloj.GetUtcNow());

        repositorio.Agregar(pedido);
        await iniciadorProceso.IniciarAsync(pedido, cancellationToken);
        await unidadTrabajo.GuardarCambiosAsync(cancellationToken);

        registro.LogInformation("Pedido {PedidoId} registrado por un total de {Total} {Moneda}; Saga iniciada",
            pedido.Id.Valor, pedido.Total, pedido.Moneda);

        return new PedidoCreadoDto(pedido.Id.Valor, pedido.Estado.ToString(), pedido.Total, pedido.Moneda);
    }
}
