namespace Pedidos.Domain;

/// <summary>
/// Identificador fuertemente tipado del pedido. Impide confundir, por ejemplo, un ClienteId con un PedidoId
/// en firmas de métodos, sin coste en tiempo de ejecución (es un <c>struct</c>).
/// </summary>
public readonly record struct PedidoId(Guid Valor)
{
    /// <summary>Genera un identificador nuevo basado en tiempo (UUID v7).</summary>
    public static PedidoId Nuevo() => new(Guid.CreateVersion7());

    public override string ToString() => Valor.ToString();
}
