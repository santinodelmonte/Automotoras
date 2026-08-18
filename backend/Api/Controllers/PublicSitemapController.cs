using System.Globalization;
using System.Text;
using System.Xml;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Sitemap del sitio público de la automotora.
/// </summary>
/// <remarks>
/// Uno por tenant, con las URLs de su propio dominio. Sale de la API porque es la que sabe
/// qué está publicado; en producción el servidor web reescribe <c>/sitemap.xml</c> hacia
/// acá, que es donde los buscadores lo van a buscar.
/// <para>
/// Solo se listan los vehículos disponibles. Dejar en el sitemap una unidad ya vendida
/// manda al comprador —y al buscador— a una ficha que responde 404.
/// </para>
/// </remarks>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicSitemapController : ControllerBase
{
    /// <summary>Tope de URLs. El formato admite 50.000; ninguna automotora se acerca.</summary>
    private const int MaximoDeUrls = 5_000;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PublicSitemapController(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("sitemap.xml")]
    [Produces("application/xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is not { } tenantId)
        {
            return NotFound();
        }

        var tenant = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Slug, t.DominioCustom })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return NotFound();
        }

        var vehiculos = await _db.Vehiculos
            .Where(v => v.Estado == EstadoVehiculo.Disponible)
            .OrderByDescending(v => v.UpdatedAt)
            .Take(MaximoDeUrls)
            .Select(v => new { v.Id, v.UpdatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Si la automotora ya tiene su dominio, las URLs son las de su dominio. Si todavía
        // no, se usa por dónde entró el request: publicar URLs de un dominio que no existe
        // deja el sitemap entero apuntando a la nada.
        var baseUrl = tenant.DominioCustom is { Length: > 0 } dominio
            ? $"https://{dominio}"
            : $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

        var xml = new StringBuilder();

        var ajustes = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
        };

        using (var escritor = XmlWriter.Create(xml, ajustes))
        {
            escritor.WriteStartDocument();
            escritor.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            EscribirUrl(escritor, baseUrl, null, "daily", "1.0");

            foreach (var vehiculo in vehiculos)
            {
                EscribirUrl(
                    escritor,
                    $"{baseUrl}/vehiculos/{vehiculo.Id.ToString(CultureInfo.InvariantCulture)}",
                    vehiculo.UpdatedAt,
                    "weekly",
                    "0.8");
            }

            escritor.WriteEndElement();
            escritor.WriteEndDocument();
        }

        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    private static void EscribirUrl(
        XmlWriter escritor,
        string url,
        DateTime? modificado,
        string frecuencia,
        string prioridad)
    {
        escritor.WriteStartElement("url");
        escritor.WriteElementString("loc", url);

        if (modificado is { } fecha)
        {
            escritor.WriteElementString("lastmod", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        escritor.WriteElementString("changefreq", frecuencia);
        escritor.WriteElementString("priority", prioridad);
        escritor.WriteEndElement();
    }
}
