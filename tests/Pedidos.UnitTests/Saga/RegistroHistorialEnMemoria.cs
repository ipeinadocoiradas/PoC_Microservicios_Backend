using System.Collections.Concurrent;
using Pedidos.Application.Abstracciones;
using Pedidos.Application.Saga;

namespace Pedidos.UnitTests.Saga;

/// <summary>Doble de prueba que guarda en memoria las transiciones registradas por la Saga.</summary>
internal sealed class RegistroHistorialEnMemoria : IRegistroHistorialSaga
{
    private readonly ConcurrentQueue<TransicionSagaPedido> _transiciones = new();

    public void Registrar(TransicionSagaPedido transicion) => _transiciones.Enqueue(transicion);

    public IReadOnlyList<TransicionSagaPedido> De(Guid correlationId) =>
        [.. _transiciones.Where(t => t.CorrelationId == correlationId).OrderBy(t => t.Secuencia)];
}