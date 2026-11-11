using System.Collections.Concurrent;
using Shared.Aplicacion.Resultados;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Aplicacion.Cqrs;

/// <summary>
/// Implementación propia y tipada del despachador CQRS.
/// </summary>
/// <remarks>
/// <para>
/// El llamador solo conoce <c>IComando&lt;TResultado&gt;</c>, pero el manejador registrado es
/// <c>IManejadorComando&lt;TComandoConcreto, TResultado&gt;</c>. Para cerrar ese tipo genérico sin usar
/// <c>dynamic</c> ni reflexión en cada invocación, se crea (una sola vez por tipo de petición) un
/// envoltorio genérico fuertemente tipado y se guarda en caché.
/// </para>
/// <para>
/// Se registra con ámbito <i>scoped</i>: resuelve los manejadores desde el mismo ámbito que la petición HTTP
/// o el mensaje consumido, de modo que comparten <c>DbContext</c> y transacción.
/// </para>
/// </remarks>
public sealed class Despachador(IServiceProvider proveedorServicios) : IDespachador
{
    private static readonly ConcurrentDictionary<Type, object> EnvoltoriosComandos = new();
    private static readonly ConcurrentDictionary<Type, object> EnvoltoriosConsultas = new();

    public Task<Resultado<TResultado>> EnviarAsync<TResultado>(IComando<TResultado> comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var envoltorio = (EnvoltorioPeticion<TResultado>)EnvoltoriosComandos.GetOrAdd(
            comando.GetType(),
            tipoComando => CrearEnvoltorio(typeof(EnvoltorioComando<,>), tipoComando, typeof(TResultado)));

        return envoltorio.EjecutarAsync(comando, proveedorServicios, cancellationToken);
    }

    public Task<Resultado<TResultado>> ConsultarAsync<TResultado>(IConsulta<TResultado> consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var envoltorio = (EnvoltorioPeticion<TResultado>)EnvoltoriosConsultas.GetOrAdd(
            consulta.GetType(),
            tipoConsulta => CrearEnvoltorio(typeof(EnvoltorioConsulta<,>), tipoConsulta, typeof(TResultado)));

        return envoltorio.EjecutarAsync(consulta, proveedorServicios, cancellationToken);
    }

    private static object CrearEnvoltorio(Type definicionGenerica, Type tipoPeticion, Type tipoResultado) =>
        Activator.CreateInstance(definicionGenerica.MakeGenericType(tipoPeticion, tipoResultado))
        ?? throw new InvalidOperationException($"No se pudo crear el envoltorio para {tipoPeticion.Name}.");

    private abstract class EnvoltorioPeticion<TResultado>
    {
        public abstract Task<Resultado<TResultado>> EjecutarAsync(object peticion, IServiceProvider proveedor, CancellationToken cancellationToken);
    }

    private sealed class EnvoltorioComando<TComando, TResultado> : EnvoltorioPeticion<TResultado>
        where TComando : IComando<TResultado>
    {
        public override Task<Resultado<TResultado>> EjecutarAsync(object peticion, IServiceProvider proveedor, CancellationToken cancellationToken) =>
            proveedor.GetRequiredService<IManejadorComando<TComando, TResultado>>()
                .ManejarAsync((TComando)peticion, cancellationToken);
    }

    private sealed class EnvoltorioConsulta<TConsulta, TResultado> : EnvoltorioPeticion<TResultado>
        where TConsulta : IConsulta<TResultado>
    {
        public override Task<Resultado<TResultado>> EjecutarAsync(object peticion, IServiceProvider proveedor, CancellationToken cancellationToken) =>
            proveedor.GetRequiredService<IManejadorConsulta<TConsulta, TResultado>>()
                .ManejarAsync((TConsulta)peticion, cancellationToken);
    }
}
