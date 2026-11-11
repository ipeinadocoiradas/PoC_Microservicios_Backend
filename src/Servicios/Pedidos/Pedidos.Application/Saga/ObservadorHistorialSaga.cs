using MassTransit;
using Pedidos.Application.Abstracciones;

namespace Pedidos.Application.Saga;

/// <summary>
/// Registra en el historial cada cambio de estado de <see cref="GestorSagaPedidos"/>.
/// </summary>
/// <remarks>
/// MassTransit invoca <see cref="StateChanged"/> durante la actividad <c>TransitionTo</c>, antes de que el
/// repositorio de la Saga guarde la instancia. <see cref="IRegistroHistorialSaga"/> se resuelve desde el ámbito
/// del mensaje en curso, de modo que la transición se confirma (o se descarta) junto con el estado de la Saga.
/// </remarks>
public sealed class ObservadorHistorialSaga(TimeProvider reloj) : IStateObserver<EstadoSagaPedido>
{
    /// <summary>Nombre del estado inicial implícito de MassTransit.</summary>
    private const string EstadoInicialMassTransit = "Initial";

    public Task StateChanged(BehaviorContext<EstadoSagaPedido> context, State currentState, State previousState)
    {
        var estadoAnterior = previousState is null || previousState.Name == EstadoInicialMassTransit
            ? null
            : previousState.Name;

        var transicion = TransicionSagaPedido.Crear(
            context.Saga.CorrelationId,
            context.Saga.SiguienteSecuencia(),
            estadoAnterior,
            currentState.Name,
            context.Event.Name,
            reloj.GetUtcNow());

        context.GetServiceOrCreateInstance<IRegistroHistorialSaga>().Registrar(transicion);
        return Task.CompletedTask;
    }
}