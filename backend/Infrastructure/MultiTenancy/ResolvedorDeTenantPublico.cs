using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Tenants;
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
    /// <remarks>
    /// Solo resuelven los dominios verificados. Uno pendiente todavía no probó ser de quien
    /// lo dio de alta, y servirle tráfico sería dejar que cualquiera reclame un dominio
    /// ajeno escribiéndolo en un formulario.
    /// <para>
    /// Sin filtro de tenant: el request es anónimo y justamente lo que se está resolviendo
    /// es a qué tenant pertenece, así que todavía no hay ninguno en contexto.
    /// </para>
    /// </remarks>
    public async Task<int?> PorDominioAsync(string? host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var dominio = NombresDeDominio.Normalizar(host);

        return await _db.Dominios
            .IgnoreQueryFilters()
            .Where(d => d.Dominio == dominio
                        && d.Estado == EstadoDeDominio.Verificado
                        && d.Tenant!.Activo)
            .Select(d => (int?)d.TenantId)
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

    public static string NormalizarSlug(string slug) => slug.Trim().ToLowerInvariant();
}
