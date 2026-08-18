using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Resuelve el tenant del sitio público a partir de lo único que trae un visitante
/// anónimo: el dominio por el que entró, o el slug de la ruta en desarrollo.
/// </summary>
/// <remarks>
/// Siempre contra la tabla <c>tenants</c>. Un dominio o un slug que no matchea no
/// resuelve nada, y el sitio responde 404: no existe el caso "tenant por defecto".
/// <para>
/// Solo resuelve tenants activos. Dar de baja una automotora tiene que apagarle el sitio,
/// no dejarlo publicado.
/// </para>
/// </remarks>
public sealed class ResolvedorDeTenantPublico
{
    private readonly AppDbContext _db;

    public ResolvedorDeTenantPublico(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Id del tenant dueño del dominio, o <c>null</c>.</summary>
    public async Task<int?> PorDominioAsync(string? host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var dominio = NormalizarDominio(host);

        return await _db.Tenants
            .Where(t => t.Activo && t.DominioCustom == dominio)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Id del tenant con ese slug, o <c>null</c>.</summary>
    public async Task<int?> PorSlugAsync(string? slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalizado = NormalizarSlug(slug);

        return await _db.Tenants
            .Where(t => t.Activo && t.Slug == normalizado)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// El <c>Host</c> llega como lo mandó el navegador. Se compara en minúsculas y sin el
    /// <c>www.</c>: los dominios no distinguen mayúsculas y nadie quiere cargar dos filas
    /// para el mismo sitio.
    /// </summary>
    public static string NormalizarDominio(string host)
    {
        var dominio = host.Trim().ToLowerInvariant();

        return dominio.StartsWith("www.", StringComparison.Ordinal) ? dominio[4..] : dominio;
    }

    public static string NormalizarSlug(string slug) => slug.Trim().ToLowerInvariant();
}
