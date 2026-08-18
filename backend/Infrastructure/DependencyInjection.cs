using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Infrastructure.Auth;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using AutomotoraSaaS.Core.Storage;
using AutomotoraSaaS.Infrastructure.Analitica;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Storage;
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
    /// Versión de servidor contra la que se generan las consultas cuando no se declara
    /// otra: MySQL 8, que es lo que corre en producción.
    /// </summary>
    /// <remarks>
    /// Declarada, no autodetectada: <c>ServerVersion.AutoDetect</c> abre una conexión
    /// durante el arranque, y en IIS eso convierte una base momentáneamente caída en una
    /// aplicación que no levanta.
    /// </remarks>
    public static readonly ServerVersion VersionPorDefecto = new MySqlServerVersion(new Version(8, 0, 36));

    /// <summary>Clave de configuración que permite declarar otra versión de servidor.</summary>
    public const string ClaveDeVersion = "Database:ServerVersion";

    /// <summary>
    /// Traduce el valor configurado a la versión con la que Pomelo genera el SQL.
    /// </summary>
    /// <remarks>
    /// Existe porque MySQL y MariaDB no son la misma base. Pomelo emite SQL distinto para
    /// cada una, y en desarrollo es habitual tener MariaDB —es lo que trae XAMPP— mientras
    /// producción corre MySQL. Con la versión clavada en el código, una de las dos puntas
    /// trabaja siempre contra un SQL que no es el suyo.
    /// <para>
    /// El formato es el de <c>ServerVersion.Parse</c>: <c>8.0.36-mysql</c>,
    /// <c>10.4.32-mariadb</c>. Un valor que no se entiende hace fallar el arranque en vez
    /// de caer al default silenciosamente: una base que responde con el dialecto equivocado
    /// falla mucho más tarde y mucho peor.
    /// </para>
    /// </remarks>
    public static ServerVersion ResolverVersion(string? declarada)
        => string.IsNullOrWhiteSpace(declarada)
            ? VersionPorDefecto
            : ServerVersion.Parse(declarada);

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Uno por request. Es el que alimenta los filtros globales del DbContext.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // Quién resuelve el tenant del sitio público a partir del dominio o del slug.
        services.AddScoped<ResolvedorDeTenantPublico>();

        // Autenticación. El hasher no tiene estado y el generador de tokens solo guarda la
        // clave de firma ya materializada, así que los dos son singleton: derivar la clave
        // en cada request sería trabajo repetido para nada.
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Seccion));
        services.AddSingleton<IPasswordHasher, PasswordHasherPbkdf2>();
        services.AddSingleton<GeneradorDeTokens>();
        services.AddScoped<IServicioDeAutenticacion, ServicioDeAutenticacion>();

        // Hashea las IPs de los eventos. Sin estado y con la sal ya materializada.
        services.AddSingleton<HasheadorDeIp>();

        // Storage de imágenes. El proveedor se elige por configuración y no por #if de
        // compilación: el mismo binario tiene que poder correr local y en producción.
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Seccion));

        var storage = configuration.GetSection(StorageOptions.Seccion).Get<StorageOptions>()
                      ?? new StorageOptions();

        if (storage.EsLocal)
        {
            services.AddSingleton<IImageStorage, LocalImageStorage>();
        }
        else
        {
            services.AddSingleton<IImageStorage, R2ImageStorage>();
        }

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
                .UseMySql(connectionString, ResolverVersion(configuration[ClaveDeVersion]))
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
