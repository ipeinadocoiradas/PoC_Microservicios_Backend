using Shared.Aplicacion.Cqrs;
using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.Logging;
using Pagos.Application.Abstracciones;
using Pagos.Domain;

namespace Pagos.Application;

/// <summary>
/// Autoriza y registra el pago de un pedido.
/// </summary>
/// <remarks>
/// <para><b>Idempotencia:</b> si ya existe un pago para el pedido se devuelve sin volver a llamar a la pasarela,
/// de modo que un <c>ProcesarPago</c> duplicado nunca genera un segundo cargo.</para>
/// <para><b>Fallos técnicos:</b> si la pasarela lanza una excepción, no se persiste nada y el error se propaga;
/// MassTransit reintenta con backoff y, agotados los intentos, la Saga recibe <c>Fault&lt;ProcesarPago&gt;</c>.</para>
/// </remarks>
public sealed class ManejadorProcesarPago(
    IPagoRepositorio pagos,
    IAutorizadorPagos autorizador,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj,
    ILogger<ManejadorProcesarPago> registro) : IManejadorComando<ProcesarPagoComando, PagoDto>
{
    public async Task<Resultado<PagoDto>> ManejarAsync(ProcesarPagoComando comando, CancellationToken cancellationToken)
    {
        if (ErroresPagos.Validar(comando.PedidoId, comando.ClienteId, comando.Importe, comando.Moneda) is { } error)
        {
            return error;
        }

        var existente = await pagos.ObtenerPorPedidoAsync(comando.PedidoId, cancellationToken);
        if (existente is not null)
        {
            registro.LogInformation("Solicitud duplicada: el pedido {PedidoId} ya tiene un pago {Estado}", comando.PedidoId, existente.Estado);
            return PagoDto.Desde(existente);
        }

        var pago = Pago.Iniciar(comando.PedidoId, comando.ClienteId, Dinero.Crear(comando.Importe, comando.Moneda), reloj.GetUtcNow());

        var autorizacion = await autorizador.AutorizarAsync(
            new SolicitudAutorizacion(pago.Id.Valor, pago.ClienteId, pago.Importe.Cantidad, pago.Importe.Moneda),
            cancellationToken);

        if (autorizacion.Aprobada)
        {
            pago.Completar(autorizacion.CodigoAutorizacion!, reloj.GetUtcNow());
        }
        else
        {
            pago.Rechazar(autorizacion.MotivoRechazo ?? "Pago denegado por la pasarela", reloj.GetUtcNow());
        }

        pagos.Agregar(pago);
        await unidadTrabajo.GuardarCambiosAsync(cancellationToken);

        registro.LogInformation("Pago {PagoId} del pedido {PedidoId} registrado como {Estado} ({Importe})",
            pago.Id.Valor, pago.PedidoId, pago.Estado, pago.Importe);

        return PagoDto.Desde(pago);
    }
}

/// <summary>
/// Compensación: anula el pago del pedido. Si el pago no existe (el cobro nunca llegó a registrarse),
/// deja constancia de una cancelación preventiva para bloquear un posible <c>ProcesarPago</c> tardío.
/// </summary>
public sealed class ManejadorCancelarPago(
    IPagoRepositorio pagos,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj,
    ILogger<ManejadorCancelarPago> registro) : IManejadorComando<CancelarPagoComando, PagoDto>
{
    public async Task<Resultado<PagoDto>> ManejarAsync(CancelarPagoComando comando, CancellationToken cancellationToken)
    {
        if (ErroresPagos.Validar(comando.PedidoId, comando.ClienteId, comando.Importe, comando.Moneda) is { } error)
        {
            return error;
        }

        var ahora = reloj.GetUtcNow();
        var pago = await pagos.ObtenerPorPedidoAsync(comando.PedidoId, cancellationToken);

        if (pago is null)
        {
            pago = Pago.RegistrarCancelacionPreventiva(comando.PedidoId, comando.ClienteId, Dinero.Crear(comando.Importe, comando.Moneda), comando.Motivo, ahora);
            pagos.Agregar(pago);
            await unidadTrabajo.GuardarCambiosAsync(cancellationToken);
            registro.LogInformation("Cancelación preventiva registrada para el pedido {PedidoId}", comando.PedidoId);
        }
        else if (pago.Cancelar(comando.Motivo, ahora))
        {
            await unidadTrabajo.GuardarCambiosAsync(cancellationToken);
            registro.LogInformation("Pago {PagoId} del pedido {PedidoId} anulado: {Estado}", pago.Id.Valor, comando.PedidoId, pago.Estado);
        }

        return PagoDto.Desde(pago);
    }
}

public sealed class ManejadorObtenerPago(IPagoRepositorio pagos) : IManejadorConsulta<ObtenerPagoConsulta, PagoDto>
{
    public async Task<Resultado<PagoDto>> ManejarAsync(ObtenerPagoConsulta consulta, CancellationToken cancellationToken)
    {
        var pago = await pagos.ObtenerPorIdAsync(new PagoId(consulta.PagoId), cancellationToken);
        return pago is null ? ErroresPagos.NoEncontrado(consulta.PagoId) : PagoDto.Desde(pago);
    }
}

public sealed class ManejadorObtenerPagoPorPedido(IPagoRepositorio pagos) : IManejadorConsulta<ObtenerPagoPorPedidoConsulta, PagoDto>
{
    public async Task<Resultado<PagoDto>> ManejarAsync(ObtenerPagoPorPedidoConsulta consulta, CancellationToken cancellationToken)
    {
        var pago = await pagos.ObtenerPorPedidoAsync(consulta.PedidoId, cancellationToken);
        return pago is null ? ErroresPagos.NoEncontradoParaPedido(consulta.PedidoId) : PagoDto.Desde(pago);
    }
}
