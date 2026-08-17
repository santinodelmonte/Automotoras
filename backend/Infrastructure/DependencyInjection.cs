using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Infrastructure;

/// <summary>
/// Punto único de registro de los servicios de infraestructura.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Versión de MySQL contra la que se generan las consultas.
    /// </summary>
    /// <remarks>
    /// Declarada, no autodetectada: <c>ServerVersion.AutoDetect</c> abre una conexión
    /// durante el arranque, y en IIS eso convierte una base momentáneamente caída en una
    /// aplicación que no levanta.
    /// </remarks>
    public static readonly MySqlServerVersion VersionMySql = new(new Version(8, 0, 36));

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Uno por request. Es el que alimenta los filtros globales del DbContext.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Default");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Falta la connection string 'ConnectionStrings:Default'. Definila por " +
                    "variable de entorno (ConnectionStrings__Default) o en " +
                    "appsettings.Development.json. La forma esperada está en " +
                    "appsettings.Example.json.");
            }

            options
                .UseMySql(connectionString, VersionMySql)
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
