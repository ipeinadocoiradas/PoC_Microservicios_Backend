using Shared.Aplicacion.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Pagos.Application;

public static class ExtensionesAplicacionPagos
{
    extension(IServiceCollection servicios)
    {
        public IServiceCollection AgregarAplicacionPagos()
        {
            servicios.TryAddSingleton(TimeProvider.System);
            servicios.AgregarCqrs(typeof(ExtensionesAplicacionPagos).Assembly);
            return servicios;
        }
    }
}
