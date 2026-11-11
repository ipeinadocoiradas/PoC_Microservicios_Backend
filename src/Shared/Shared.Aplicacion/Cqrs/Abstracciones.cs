using Shared.Aplicacion.Resultados;

namespace Shared.Aplicacion.Cqrs;

/// <summary>Petición que modifica el estado del sistema y devuelve un <typeparamref name="TResultado"/>.</summary>
public interface IComando<TResultado>;

/// <summary>Petición de solo lectura que devuelve un <typeparamref name="TResultado"/>.</summary>
public interface IConsulta<TResultado>;

/// <summary>Manejador de un comando concreto. Existe exactamente uno por tipo de comando.</summary>
public interface IManejadorComando<in TComando, TResultado>
    where TComando : IComando<TResultado>
{
    Task<Resultado<TResultado>> ManejarAsync(TComando comando, CancellationToken cancellationToken);
}

/// <summary>Manejador de una consulta concreta. Existe exactamente uno por tipo de consulta.</summary>
public interface IManejadorConsulta<in TConsulta, TResultado>
    where TConsulta : IConsulta<TResultado>
{
    Task<Resultado<TResultado>> ManejarAsync(TConsulta consulta, CancellationToken cancellationToken);
}

/// <summary>
/// Punto único de entrada a la capa de aplicación. Desacopla a los llamadores (controladores,
/// consumidores de mensajes) de los manejadores concretos y de su construcción.
/// </summary>
public interface IDespachador
{
    Task<Resultado<TResultado>> EnviarAsync<TResultado>(IComando<TResultado> comando, CancellationToken cancellationToken = default);

    Task<Resultado<TResultado>> ConsultarAsync<TResultado>(IConsulta<TResultado> consulta, CancellationToken cancellationToken = default);
}
