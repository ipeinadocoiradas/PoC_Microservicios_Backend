namespace Almacen.Domain;

/// <summary>Identificador fuertemente tipado de un producto del catálogo.</summary>
public readonly record struct ProductoId(Guid Valor)
{
    public override string ToString() => Valor.ToString();
}

/// <summary>Identificador fuertemente tipado de una reserva de stock.</summary>
public readonly record struct ReservaId(Guid Valor)
{
    public static ReservaId Nueva() => new(Guid.CreateVersion7());

    public override string ToString() => Valor.ToString();
}
