using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Sitio;

/// <summary>
/// El <c>index.html</c> compilado del frontend, leído una vez y guardado en memoria.
/// </summary>
/// <remarks>
/// En producción el frontend se publica dentro de <c>wwwroot</c> y lo sirve la misma
/// aplicación: un solo sitio en IIS, un solo binding por dominio y sin CORS. En desarrollo
/// no existe —lo sirve Vite— y el respaldo responde 404.
/// </remarks>
public sealed class PlantillaDelSitio
{
    private readonly string _ruta;
    private string? _contenido;

    public PlantillaDelSitio(IConfiguration configuracion, IWebHostEnvironment entorno)
    {
        _ruta = configuracion["Sitio:Index"] is { Length: > 0 } configurada
            ? configuracion["Sitio:Index"]!
            : Path.Combine(entorno.WebRootPath ?? Path.Combine(entorno.ContentRootPath, "wwwroot"), "index.html");
    }

    public string? Leer()
    {
        if (_contenido is not null)
        {
            return _contenido;
        }

        if (!File.Exists(_ruta))
        {
            return null;
        }

        // Si dos requests lo leen a la vez, los dos leen lo mismo: no hace falta lock.
        _contenido = File.ReadAllText(_ruta, Encoding.UTF8);
        return _contenido;
    }
}

/// <summary>
/// Sirve las páginas del frontend con las etiquetas de cada automotora y cada vehículo ya
/// puestas en el HTML.
/// </summary>
/// <remarks>
/// Corre después de los controllers, para todo lo que no es un archivo ni una ruta de la
/// API. Lee vehículos con <c>IgnoreQueryFilters()</c> y el tenant siempre explícito en el
/// <c>WHERE</c>: acá el request todavía no tiene tenant resuelto —el middleware solo lo
/// resuelve para <c>/api/public</c>—, y lo único que sale es lo mismo que ya muestra el
/// sitio público: unidades disponibles de la automotora de esta dirección.
/// </remarks>
public static partial class SitioDelFrontend
{
    public static async Task<IResult> ServirAsync(
        HttpContext contexto,
        PlantillaDelSitio plantilla,
        ResolvedorDeTenantPublico resolvedor,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(plantilla);
        ArgumentNullException.ThrowIfNull(resolvedor);

        var ruta = contexto.Request.Path;

        // Una ruta de la API que no existe es un 404 de la API, no la home del sitio.
        if (ruta.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || ruta.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase)
            || plantilla.Leer() is not { } html)
        {
            return Results.NotFound();
        }

        contexto.Response.Headers.CacheControl = "no-cache";

        if (ruta.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return Html(html, new MetaDelSitio("Panel de administración", Indexable: false));
        }

        var slug = SlugDe(contexto.Request.PathBase);
        var tenantId = slug is not null
            ? await resolvedor.PorSlugAsync(slug, cancellationToken).ConfigureAwait(false)
            : await resolvedor.PorDominioAsync(contexto.Request.Host.Host, cancellationToken).ConfigureAwait(false);

        if (tenantId is not { } id)
        {
            return Html(html, new MetaDelSitio("Automotora no encontrada", Indexable: false), StatusCodes.Status404NotFound);
        }

        if (await resolvedor.SuspendidoAsync(id, cancellationToken).ConfigureAwait(false) is { } suspendida)
        {
            contexto.Response.Headers.RetryAfter = "86400";
            return Html(html, new MetaDelSitio($"{suspendida} — Sitio en mantenimiento", Indexable: false),
                StatusCodes.Status503ServiceUnavailable);
        }

        var tenant = await db.Tenants
            .Where(t => t.Id == id)
            .Select(t => new { t.Nombre, t.Direccion, t.LogoUrl, t.ColorPrimario, t.DominioCustom, t.DominioVerificadoEn })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var baseUrl = tenant.DominioCustom is { Length: > 0 } dominio && tenant.DominioVerificadoEn is not null
            ? $"https://{dominio}"
            : $"{contexto.Request.Scheme}://{contexto.Request.Host}{contexto.Request.PathBase}";

        var url = baseUrl + (ruta.Value == "/" ? string.Empty : ruta.Value);

        var disponibles = db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == id && v.Estado == EstadoVehiculo.Disponible);

        MetaDelSitio meta;
        var estado = StatusCodes.Status200OK;

        if (RutaDeFicha().Match(ruta.Value ?? string.Empty) is { Success: true } ficha)
        {
            var vehiculoId = int.Parse(ficha.Groups[1].Value, CultureInfo.InvariantCulture);

            var vehiculo = await disponibles
                .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
                .Include(v => v.Version)
                .Include(v => v.Fotos)
                .FirstOrDefaultAsync(v => v.Id == vehiculoId, cancellationToken)
                .ConfigureAwait(false);

            if (vehiculo is null)
            {
                // La página igual se sirve —dice que ya no está y ofrece el resto—, pero
                // con 404, para que el buscador la saque del índice.
                meta = new MetaDelSitio($"Vehículo no disponible — {tenant.Nombre}", Indexable: false);
                estado = StatusCodes.Status404NotFound;
            }
            else
            {
                meta = PaginaDelSitio.DeVehiculo(
                    tenant.Nombre,
                    vehiculo.Modelo!.Marca!.Nombre,
                    vehiculo.Modelo.Nombre,
                    vehiculo.Version?.Nombre,
                    vehiculo.Anio,
                    vehiculo.Kilometraje,
                    vehiculo.Combustible,
                    vehiculo.Transmision,
                    vehiculo.Precio,
                    vehiculo.Moneda,
                    MapeosDeVehiculo.Portada(vehiculo)?.Url,
                    url);
            }
        }
        else if (!RutaConocida().IsMatch(ruta.Value ?? "/"))
        {
            // Una dirección que el sitio no tiene. Antes salía la portada con 200, y un link
            // roto quedaba indexado como una copia de la home.
            meta = new MetaDelSitio($"Página no encontrada — {tenant.Nombre}", Indexable: false);
            estado = StatusCodes.Status404NotFound;
        }
        else if (ruta.StartsWithSegments("/privacidad", StringComparison.OrdinalIgnoreCase))
        {
            meta = new MetaDelSitio($"Privacidad — {tenant.Nombre}", Url: url, SitioNombre: tenant.Nombre, Indexable: false);
        }
        else
        {
            var publicados = await disponibles.CountAsync(cancellationToken).ConfigureAwait(false);

            // Para la vista previa de la home, un auto dice más que un logo.
            var portada = await disponibles
                .OrderByDescending(v => v.Destacado)
                .ThenByDescending(v => v.FechaPublicacion)
                .SelectMany(v => v.Fotos.OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden).Take(1))
                .Select(f => f.Url)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            meta = PaginaDelSitio.DeAutomotora(tenant.Nombre, tenant.Direccion, publicados, portada ?? tenant.LogoUrl, url);

            if (ruta.StartsWithSegments("/vehiculos", StringComparison.OrdinalIgnoreCase))
            {
                meta = meta with { Titulo = $"Vehículos en venta — {tenant.Nombre}" };
            }
        }

        return Html(html, meta with { Color = tenant.ColorPrimario, Icono = tenant.LogoUrl }, estado);
    }

    /// <summary>
    /// <c>robots.txt</c> por dirección: el de una automotora apunta a su sitemap; el del
    /// dominio del SaaS, que no es el sitio de nadie, no deja indexar nada.
    /// </summary>
    public static async Task<IResult> RobotsAsync(
        HttpContext contexto,
        ResolvedorDeTenantPublico resolvedor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(resolvedor);

        var slug = SlugDe(contexto.Request.PathBase);
        var tenantId = slug is not null
            ? await resolvedor.PorSlugAsync(slug, cancellationToken).ConfigureAwait(false)
            : await resolvedor.PorDominioAsync(contexto.Request.Host.Host, cancellationToken).ConfigureAwait(false);

        var texto = tenantId is null
            ? "User-agent: *\nDisallow: /\n"
            : "User-agent: *\nAllow: /\nDisallow: /admin\n" +
              $"Sitemap: {contexto.Request.Scheme}://{contexto.Request.Host}{contexto.Request.PathBase}/api/public/sitemap.xml\n";

        return Results.Text(texto, "text/plain", Encoding.UTF8);
    }

    private static string? SlugDe(PathString pathBase)
        => pathBase.Value is { } valor && valor.StartsWith("/t/", StringComparison.Ordinal) ? valor[3..] : null;

    private static IResult Html(string plantilla, MetaDelSitio meta, int estado = StatusCodes.Status200OK)
        => Results.Content(PaginaDelSitio.Renderizar(plantilla, meta), "text/html; charset=utf-8", Encoding.UTF8, estado);

    [GeneratedRegex(@"^/vehiculos/(\d{1,9})/?$")]
    private static partial Regex RutaDeFicha();

    /// <summary>Las páginas que tiene el sitio público, además de la ficha. Tiene que coincidir con las rutas de <c>App.tsx</c>.</summary>
    [GeneratedRegex(@"^/(vehiculos/?|privacidad/?)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RutaConocida();
}
