using Almacen.Domain;
using Microsoft.EntityFrameworkCore;

namespace Almacen.Infrastructure.Persistencia;

/// <summary>
/// Catálogo con identificadores fijos para reproducir los escenarios de la demostración
/// (ejemplos HTTP del README y pruebas de integración).
/// </summary>
public static class ProductosDemostracion
{
    /// <summary>Teclado mecánico, 100 unidades: escenario satisfactorio.</summary>
    public static readonly Guid TecladoMecanico = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Ratón inalámbrico, 50 unidades.</summary>
    public static readonly Guid RatonInalambrico = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Monitor 4K, 10 unidades: escenarios de pago rechazado y compensación.</summary>
    public static readonly Guid Monitor4K = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>Edición limitada sin existencias: escenario de stock insuficiente.</summary>
    public static readonly Guid EdicionLimitadaAgotada = Guid.Parse("44444444-4444-4444-4444-444444444444");

    internal static IEnumerable<Producto> CrearCatalogo() =>
    [
        Producto.Crear(new ProductoId(TecladoMecanico), "Teclado mecánico", 100),
        Producto.Crear(new ProductoId(RatonInalambrico), "Ratón inalámbrico", 50),
        Producto.Crear(new ProductoId(Monitor4K), "Monitor 4K 27\"", 10),
        Producto.Crear(new ProductoId(EdicionLimitadaAgotada), "Edición limitada", 0)
    ];
}

/// <summary>Siembra el catálogo una sola vez (se invoca desde <c>UseSeeding</c>/<c>UseAsyncSeeding</c>).</summary>
internal static class SembradorCatalogo
{
    public static void Sembrar(DbContext contexto)
    {
        var productos = contexto.Set<Producto>();
        if (productos.Any())
        {
            return;
        }

        productos.AddRange(ProductosDemostracion.CrearCatalogo());
        contexto.SaveChanges();
    }

    public static async Task SembrarAsync(DbContext contexto, CancellationToken cancellationToken)
    {
        var productos = contexto.Set<Producto>();
        if (await productos.AnyAsync(cancellationToken))
        {
            return;
        }

        productos.AddRange(ProductosDemostracion.CrearCatalogo());
        await contexto.SaveChangesAsync(cancellationToken);
    }
}
