using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Infrastructure;

/// <summary>
/// Punto único de registro de los servicios de infraestructura (DbContext, repositorios,
/// storage de imágenes, servicios externos). Por ahora vacío: el esqueleto no tiene
/// dependencias de infraestructura todavía.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services;
    }
}
