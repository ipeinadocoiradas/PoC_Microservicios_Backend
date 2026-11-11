using Shared.Aplicacion.Cqrs;
using Shared.Infraestructura.Mensajeria;
using Contratos.Integracion.Comandos;
using Contratos.Integracion.Eventos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Pagos.Application;
using Pagos.Domain;

namespace Pagos.Infrastructure.Mensajeria;

/// <summary>Consume <see cref="ProcesarPago"/> y publica <see cref="PagoCompletado"/>, <see cref="PagoRechazado"/> o <see cref="PagoCancelado"/>.</summary>
public sealed class ConsumidorProcesarPago(IDespachador despachador, ILogger<ConsumidorProcesarPago> registro)
    : IConsumer<ProcesarPago>
{
    public async Task Consume(ConsumeContext<ProcesarPago> contexto)
    {
        var mensaje = contexto.Message;
        using var ambito = registro.IniciarAmbitoMensaje(mensaje.CorrelationId, mensaje.PedidoId);

        var resultado = await despachador.EnviarAsync(
            new ProcesarPagoComando(mensaje.PedidoId, mensaje.ClienteId, mensaje.Importe, mensaje.Moneda),
            contexto.CancellationToken);

        if (resultado.EsFallido)
        {
            await contexto.Publish(new PagoRechazado(mensaje.CorrelationId, mensaje.PedidoId, PagoId: null, resultado.Error.Descripcion));
            return;
        }

        // Ante un duplicado se republica el resultado ya registrado: la respuesta es siempre la misma.
        var pago = resultado.Valor;
        switch (pago.Estado)
        {
            case EstadoPago.Completado:
                await contexto.Publish(new PagoCompletado(mensaje.CorrelationId, mensaje.PedidoId, pago.PagoId, pago.Importe, pago.Moneda));
                break;
            case EstadoPago.Rechazado:
                await contexto.Publish(new PagoRechazado(mensaje.CorrelationId, mensaje.PedidoId, pago.PagoId, pago.Motivo ?? "Pago rechazado"));
                break;
            case EstadoPago.Cancelado or EstadoPago.Reembolsado:
                // ProcesarPago tardío tras una cancelación preventiva: no se cobra y se informa de la anulación.
                await contexto.Publish(new PagoCancelado(mensaje.CorrelationId, mensaje.PedidoId, pago.PagoId, pago.Estado.ToString()));
                break;
            default:
                throw new InvalidOperationException($"El pago {pago.PagoId} quedó en un estado no final ({pago.Estado}).");
        }
    }
}

/// <summary>Consume la compensación <see cref="CancelarPago"/> y publica <see cref="PagoCancelado"/>.</summary>
public sealed class ConsumidorCancelarPago(IDespachador despachador, ILogger<ConsumidorCancelarPago> registro)
    : IConsumer<CancelarPago>
{
    public async Task Consume(ConsumeContext<CancelarPago> contexto)
    {
        var mensaje = contexto.Message;
        using var ambito = registro.IniciarAmbitoMensaje(mensaje.CorrelationId, mensaje.PedidoId);

        var resultado = await despachador.EnviarAsync(
            new CancelarPagoComando(mensaje.PedidoId, mensaje.ClienteId, mensaje.Importe, mensaje.Moneda, mensaje.Motivo),
            contexto.CancellationToken);

        if (resultado.EsFallido)
        {
            throw new InvalidOperationException($"No se pudo cancelar el pago del pedido {mensaje.PedidoId}: {resultado.Error.Descripcion}");
        }

        var pago = resultado.Valor;
        await contexto.Publish(new PagoCancelado(mensaje.CorrelationId, mensaje.PedidoId, pago.PagoId, pago.Estado.ToString()));
    }
}
