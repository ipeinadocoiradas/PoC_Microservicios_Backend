using Shared.Aplicacion.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pedidos.Application.Saga;

namespace Pedidos.Application;

/// <summary>Registro de los servicios de la capa de aplicación de Pedidos.</summary>
public static class ExtensionesAplicacionPedidos
{
    extension(IServiceCollection servicios)
    {
        public IServiceCollection AgregarAplicacionPedidos()
        {
            servicios.TryAddSingleton(TimeProvider.System);
            servicios.AgregarCqrs(typeof(ExtensionesAplicacionPedidos).Assembly);
            servicios.AddScoped<IServicioEstadoPedido, ServicioEstadoPedido>();
            return servicios;
        }
    }
}
