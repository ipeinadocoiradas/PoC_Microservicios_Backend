using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shared.Aplicacion.Cqrs;

/// <summary>Registro del despachador y de los manejadores de un ensamblado de aplicación.</summary>
public static class ExtensionesRegistroCqrs
{
    private static readonly Type[] InterfacesManejador = [typeof(IManejadorComando<,>), typeof(IManejadorConsulta<,>)];

    extension(IServiceCollection servicios)
    {
        /// <summary>
        /// Registra <see cref="IDespachador"/> y todos los manejadores concretos encontrados en <paramref name="ensamblado"/>.
        /// </summary>
        public IServiceCollection AgregarCqrs(Assembly ensamblado)
        {
            servicios.TryAddScoped<IDespachador, Despachador>();

            var registros =
                from tipo in ensamblado.GetTypes()
                where tipo is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                from interfaz in tipo.GetInterfaces()
                where interfaz.IsGenericType && InterfacesManejador.Contains(interfaz.GetGenericTypeDefinition())
                select (Interfaz: interfaz, Implementacion: tipo);

            foreach (var (interfaz, implementacion) in registros)
            {
                servicios.TryAddScoped(interfaz, implementacion);
            }

            return servicios;
        }
    }
}
