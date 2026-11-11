using System.Diagnostics.CodeAnalysis;

namespace Shared.Aplicacion.Resultados;

/// <summary>
/// Result Pattern: representa el resultado de una operación que puede terminar correctamente
/// con un <typeparamref name="TValor"/> o fallar con un <see cref="Resultados.Error"/>.
/// </summary>
/// <remarks>
/// Hace explícitos en la firma los fallos esperados (validación, no encontrado, conflicto),
/// obligando al llamador a tratarlos y evitando el uso de excepciones como control de flujo.
/// Las conversiones implícitas permiten escribir <c>return dto;</c> o <c>return error;</c> en los manejadores.
/// </remarks>
public sealed class Resultado<TValor>
{
    private readonly TValor? _valor;

    private Resultado(TValor valor) => _valor = valor;

    private Resultado(Error error) => Error = error;

    /// <summary>Error producido; solo tiene valor cuando <see cref="EsCorrecto"/> es <c>false</c>.</summary>
    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool EsCorrecto => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool EsFallido => Error is not null;

    /// <summary>Valor obtenido. Acceder a él en un resultado fallido es un error de programación.</summary>
    public TValor Valor => EsCorrecto
        ? _valor!
        : throw new InvalidOperationException($"No se puede leer el valor de un resultado fallido ({Error.Codigo}).");

    public static Resultado<TValor> Correcto(TValor valor) => new(valor);

    public static Resultado<TValor> Fallo(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>Proyecta el resultado en un único valor según haya tenido éxito o no.</summary>
    public TSalida Coincidir<TSalida>(Func<TValor, TSalida> siCorrecto, Func<Error, TSalida> siFallido) =>
        EsCorrecto ? siCorrecto(Valor) : siFallido(Error);

    public static implicit operator Resultado<TValor>(TValor valor) => Correcto(valor);

    public static implicit operator Resultado<TValor>(Error error) => Fallo(error);
}
