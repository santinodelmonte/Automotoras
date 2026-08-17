using AutomotoraSaaS.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Construye el <see cref="AppDbContext"/> para las herramientas de línea de comandos de
/// EF (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>).
/// </summary>
/// <remarks>
/// Generar una migración no necesita una base viva: solo el modelo. Por eso la versión de
/// MySQL va declarada y la connection string tiene un valor por defecto inofensivo, que se
/// puede sobrescribir con <c>ConnectionStrings__Default</c> cuando sí haya que aplicarla
/// contra un servidor real.
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConnectionStringPorDefecto =
        "Server=localhost;Port=3306;Database=automotora_saas;User Id=root;Password=;";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? ConnectionStringPorDefecto;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, DependencyInjection.VersionMySql)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new TenantContextDeDiseno(), TimeProvider.System);
    }

    /// <summary>Sin tenant: en tiempo de diseño no hay request ni datos que filtrar.</summary>
    private sealed class TenantContextDeDiseno : ITenantContext
    {
        public int? TenantId => null;
    }
}
