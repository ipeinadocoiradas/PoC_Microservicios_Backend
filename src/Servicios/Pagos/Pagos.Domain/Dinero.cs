namespace Pagos.Domain;

/// <summary>
/// Objeto de valor inmutable: una cantidad positiva en una moneda concreta. Se persiste como
/// <i>complex type</i> de EF Core (columnas <c>Importe</c> y <c>Moneda</c> en la tabla del pago).
/// </summary>
public sealed record Dinero
{
    private Dinero(decimal cantidad, string moneda)
    {
        Cantidad = cantidad;
        Moneda = moneda;
    }

    public decimal Cantidad { get; }

    /// <summary>Código ISO 4217 en mayúsculas.</summary>
    public string Moneda { get; }

    public static Dinero Crear(decimal cantidad, string moneda)
    {
        if (cantidad <= 0)
        {
            throw new ExcepcionDominio("El importe debe ser mayor que cero.");
        }

        if (decimal.Round(cantidad, 2) != cantidad)
        {
            throw new ExcepcionDominio("El importe admite como máximo dos decimales.");
        }

        var monedaNormalizada = moneda?.Trim().ToUpperInvariant() ?? string.Empty;
        if (monedaNormalizada.Length != 3 || !monedaNormalizada.All(char.IsAsciiLetterUpper))
        {
            throw new ExcepcionDominio("La moneda debe ser un código ISO 4217 de tres letras.");
        }

        return new Dinero(cantidad, monedaNormalizada);
    }

    public override string ToString() => $"{Cantidad:0.00} {Moneda}";
}
