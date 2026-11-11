using Almacen.Application.Reservas;
using Almacen.Domain;
using Almacen.Infrastructure.Simulacion;
using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Shared.Infraestructura.Mensajeria;
using Contratos.Integracion.Comandos;
using Contratos.Integracion.Eventos;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Almacen.Infrastructure.Mensajeria;

// Los consumidores son adaptadores: traducen el mensaje a un comando de aplicación y el resultado a un evento.
// Publican mediante ConsumeContext, por lo que los eventos se escriben en el Outbox y solo salen a RabbitMQ
// cuando la transacción local (reserva + stock + Inbox) se ha confirmado.

/// <summary>Consume <see cref="SolicitarReservaStock"/> y publica <see cref="StockReservado"/> o <see cref="StockNoDisponible"/>.</summary>
public sealed class ConsumidorSolicitarReservaStock(IDespachador despachador, ILogger<ConsumidorSolicitarReservaStock> registro)
    : IConsumer<SolicitarReservaStock>
{
    public async Task Consume(ConsumeContext<SolicitarReservaStock> contexto)
    {
        var mensaje = contexto.Message;
        using var ambito = registro.IniciarAmbitoMensaje(mensaje.CorrelationId, mensaje.PedidoId);

        var comando = new ReservarStockComando(
            mensaje.PedidoId,
            [.. mensaje.Lineas.Select(linea => new LineaSolicitadaDto(linea.ProductoId, linea.Cantidad))]);

        var resultado = await despachador.EnviarAsync(comando, contexto.CancellationToken);

        if (resultado.EsFallido)
        {
            // Solicitud inválida: es una respuesta de negocio definitiva, no un error técnico que reintentar.
            await contexto.Publish(new StockNoDisponible(mensaje.CorrelationId, mensaje.PedidoId, resultado.Error.Descripcion));
            return;
        }

        var reserva = resultado.Valor;
        switch (reserva.Estado)
        {
            case EstadoReserva.Reservada or EstadoReserva.Confirmada:
                await contexto.Publish(new StockReservado(mensaje.CorrelationId, mensaje.PedidoId, reserva.ReservaId));
                break;
            case EstadoReserva.Rechazada:
                await contexto.Publish(new StockNoDisponible(mensaje.CorrelationId, mensaje.PedidoId, reserva.MotivoRechazo ?? "Stock insuficiente"));
                break;
            default:
                registro.LogWarning("Solicitud de reserva tardía para el pedido {PedidoId}: la reserva ya está {Estado}", mensaje.PedidoId, reserva.Estado);
                break;
        }
    }
}

/// <summary>Consume la compensación <see cref="LiberarReservaStock"/> y publica <see cref="ReservaLiberada"/>.</summary>
public sealed class ConsumidorLiberarReservaStock(
    IDespachador despachador,
    SimuladorFallosTransitorios simuladorFallos,
    ILogger<ConsumidorLiberarReservaStock> registro) : IConsumer<LiberarReservaStock>
{
    public async Task Consume(ConsumeContext<LiberarReservaStock> contexto)
    {
        var mensaje = contexto.Message;
        using var ambito = registro.IniciarAmbitoMensaje(mensaje.CorrelationId, mensaje.PedidoId);

        // Punto de inyección de fallos para el escenario D (reintentos, DLQ y recuperación).
        simuladorFallos.RegistrarIntentoLiberacion(mensaje.PedidoId);

        var resultado = await despachador.EnviarAsync(new LiberarReservaComando(mensaje.PedidoId), contexto.CancellationToken);

        if (resultado.EsFallido && resultado.Error.Tipo is not TipoError.NoEncontrado)
        {
            throw new InvalidOperationException($"No se pudo liberar la reserva del pedido {mensaje.PedidoId}: {resultado.Error.Descripcion}");
        }

        // Si no existía reserva, el objetivo de la compensación (no retener stock) ya se cumple.
        var reservaId = resultado.EsCorrecto ? resultado.Valor.ReservaId : (Guid?)null;
        await contexto.Publish(new ReservaLiberada(mensaje.CorrelationId, mensaje.PedidoId, reservaId));
    }
}

/// <summary>Consume <see cref="ConfirmarReservaStock"/> y publica <see cref="ReservaConfirmada"/>.</summary>
public sealed class ConsumidorConfirmarReservaStock(IDespachador despachador, ILogger<ConsumidorConfirmarReservaStock> registro)
    : IConsumer<ConfirmarReservaStock>
{
    public async Task Consume(ConsumeContext<ConfirmarReservaStock> contexto)
    {
        var mensaje = contexto.Message;
        using var ambito = registro.IniciarAmbitoMensaje(mensaje.CorrelationId, mensaje.PedidoId);

        var resultado = await despachador.EnviarAsync(new ConfirmarReservaComando(mensaje.PedidoId), contexto.CancellationToken);

        if (resultado.EsFallido)
        {
            throw new InvalidOperationException($"No se pudo confirmar la reserva del pedido {mensaje.PedidoId}: {resultado.Error.Descripcion}");
        }

        await contexto.Publish(new ReservaConfirmada(mensaje.CorrelationId, mensaje.PedidoId, resultado.Valor.ReservaId));
    }
}
