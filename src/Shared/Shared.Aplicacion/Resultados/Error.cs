namespace Shared.Aplicacion.Resultados;

/// <summary>Categoría del error; la capa Api la traduce a un código HTTP.</summary>
public enum TipoError
{
    /// <summary>Los datos de entrada no son válidos (HTTP 400).</summary>
    Validacion,

    /// <summary>El recurso solicitado no existe (HTTP 404).</summary>
    NoEncontrado,

    /// <summary>La operación entra en conflicto con el estado actual (HTTP 409).</summary>
    Conflicto,

    /// <summary>Se incumple una regla de negocio (HTTP 422).</summary>
    ReglaNegocio
}

/// <summary>
/// Error esperado de la aplicación. Se devuelve como valor dentro de <see cref="Resultado{TValor}"/>
/// en lugar de lanzarse como excepción: las excepciones quedan reservadas para situaciones inesperadas.
/// </summary>
/// <param name="Codigo">Código estable y legible por máquinas (p. ej. <c>Pedido.NoEncontrado</c>).</param>
/// <param name="Descripcion">Mensaje legible por personas.</param>
/// <param name="Tipo">Categoría del error.</param>
public sealed record Error(string Codigo, string Descripcion, TipoError Tipo)
{
    public static Error Validacion(string codigo, string descripcion) => new(codigo, descripcion, TipoError.Validacion);

    public static Error NoEncontrado(string codigo, string descripcion) => new(codigo, descripcion, TipoError.NoEncontrado);

    public static Error Conflicto(string codigo, string descripcion) => new(codigo, descripcion, TipoError.Conflicto);

    public static Error ReglaNegocio(string codigo, string descripcion) => new(codigo, descripcion, TipoError.ReglaNegocio);
}
