using Shared.Aplicacion.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Almacen.Application;

public static class ExtensionesAplicacionAlmacen
{
    extension(IServiceCollection servicios)
    {
        public IServiceCollection AgregarAplicacionAlmacen()
        {
            servicios.TryAddSingleton(TimeProvider.System);
            servicios.AgregarCqrs(typeof(ExtensionesAplicacionAlmacen).Assembly);
            return servicios;
        }
    }
}
