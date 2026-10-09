using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
/// <para>
/// Y por dominio propio, solo si está verificado: cargar un dominio es declarar una
/// intención, servirlo es otra cosa.
/// </para>
/// </remarks>
public sealed class ResolvedorDeTenantPublico
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;
    private readonly OpcionesDeCobranza _cobranza;

    public ResolvedorDeTenantPublico(AppDbContext db, TimeProvider reloj, IOptions<OpcionesDeCobranza> cobranza)
    {
        ArgumentNullException.ThrowIfNull(cobranza);

        _db = db;
        _reloj = reloj;
        _cobranza = cobranza.Value;
    }

    /// <summary>
    /// Si el sitio de la automotora está suspendido por falta de pago, su nombre; si no,
    /// <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Se calcula en cada request a partir de <c>paga_hasta</c>, no se lee de un campo que
    /// algún job actualiza: registrar un pago reactiva el sitio en el acto. Una automotora
    /// sin suscripción vigente —por ejemplo, después de una baja— también queda suspendida.
    /// </remarks>
    public async Task<string?> SuspendidoAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var pagaHasta = await _db.Suscripciones
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.Fin == null)
            .Select(s => (DateOnly?)s.PagaHasta)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var estado = CicloDeCobro.Evaluar(pagaHasta, PoliticaDePlanEnBase.Hoy(_reloj), _cobranza);

        if (CicloDeCobro.SitioPublicado(estado))
        {
            return null;
        }

        return await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Nombre)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Id del tenant dueño del dominio, o <c>null</c>.</summary>
    public async Task<int?> PorDominioAsync(string? host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var dominio = NormalizarDominio(host);

        // Verificado, no solamente cargado. Un dominio que alguien escribió en su
        // configuración pero que nunca se comprobó que apunte acá no sirve el sitio de
        // nadie: si no, cualquier automotora podría reservarse el dominio de otra empresa
        // y quedarse con su tráfico el día que ese dominio apunte para acá.
        return await _db.Tenants
            .Where(t => t.Activo && t.DominioCustom == dominio && t.DominioVerificadoEn != null)
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
