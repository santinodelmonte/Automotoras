using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Infrastructure.MultiTenancy;

namespace AutomotoraSaaS.Api.MultiTenancy;

/// <summary>
/// Resuelve el tenant del request. Corre después de la autenticación y antes de la
/// autorización y del routing hacia los controllers.
/// </summary>
/// <remarks>
/// Hay exactamente dos caminos, y no se cruzan:
/// <list type="number">
///   <item><b>Panel privado.</b> El tenant sale del claim <c>tenant_id</c> del JWT, que
///   está adentro de la firma. Si el request además trae un slug en la ruta, se ignora:
///   permitir que el cliente proponga un tenant sería exactamente el agujero que este
///   middleware existe para cerrar.</item>
///   <item><b>Sitio público.</b> Sin token, el tenant sale del <c>Host</c> (dominio
///   propio de la automotora) o del slug de <c>/t/{slug}</c> en desarrollo, siempre
///   validado contra la tabla <c>tenants</c>. Si no matchea, 404: no existe un tenant por
///   defecto.</item>
/// </list>
/// Fuera de esos dos casos el request queda sin tenant, y sin tenant no se lee ni se
/// escribe nada de ningún tenant.
/// </remarks>
public sealed class ResolucionDeTenantMiddleware
{
    private const string PrefijoDeSlug = "/t/";
    private const string PrefijoPublico = "/api/public";

    private readonly RequestDelegate _next;
    private readonly ILogger<ResolucionDeTenantMiddleware> _logger;

    public ResolucionDeTenantMiddleware(RequestDelegate next, ILogger<ResolucionDeTenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        ResolvedorDeTenantPublico resolvedor)
    {
        ArgumentNullException.ThrowIfNull(context);

        var slug = SepararSlugDeLaRuta(context.Request);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!ResolverDesdeElToken(context, tenantContext))
            {
                await Responder(context, StatusCodes.Status401Unauthorized,
                    "El token no identifica ningún tenant.").ConfigureAwait(false);
                return;
            }

            await _next(context).ConfigureAwait(false);
            return;
        }

        if (EsRutaPublica(context.Request.Path))
        {
            var tenantId = slug is not null
                ? await resolvedor.PorSlugAsync(slug, context.RequestAborted).ConfigureAwait(false)
                : await resolvedor.PorDominioAsync(context.Request.Host.Host, context.RequestAborted).ConfigureAwait(false);

            if (tenantId is null)
            {
                _logger.LogInformation(
                    "Sitio público sin tenant: host {Host}, slug {Slug}.",
                    context.Request.Host.Host,
                    slug ?? "(ninguno)");

                await Responder(context, StatusCodes.Status404NotFound,
                    "No hay ninguna automotora publicada en esta dirección.").ConfigureAwait(false);
                return;
            }

            tenantContext.Resolver(tenantId.Value);
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Toma el tenant del claim firmado. El SuperAdmin no tiene tenant y no es un error:
    /// opera cross-tenant por <c>/api/admin/*</c>, con el filtro global apagado de forma
    /// consciente, nunca por un flag colado en un endpoint normal.
    /// </summary>
    private static bool ResolverDesdeElToken(HttpContext context, TenantContext tenantContext)
    {
        if (context.User.EsSuperAdmin())
        {
            return true;
        }

        if (context.User.TenantIdDelToken() is not { } tenantId)
        {
            return false;
        }

        tenantContext.Resolver(tenantId);
        return true;
    }

    /// <summary>
    /// Saca el prefijo <c>/t/{slug}</c> de la ruta y lo pasa a <c>PathBase</c>, de modo
    /// que los controllers declaren sus rutas una sola vez y funcionen igual detrás de un
    /// dominio propio que detrás del slug de desarrollo.
    /// </summary>
    private static string? SepararSlugDeLaRuta(HttpRequest request)
    {
        var ruta = request.Path.Value;

        if (ruta is null || !ruta.StartsWith(PrefijoDeSlug, StringComparison.Ordinal))
        {
            return null;
        }

        var resto = ruta[PrefijoDeSlug.Length..];
        var corte = resto.IndexOf('/');

        var slug = corte < 0 ? resto : resto[..corte];

        if (slug.Length == 0)
        {
            return null;
        }

        request.PathBase = request.PathBase.Add(PrefijoDeSlug + slug);
        request.Path = corte < 0 ? "/" : resto[corte..];

        return slug;
    }

    private static bool EsRutaPublica(PathString ruta)
        => ruta.StartsWithSegments(PrefijoPublico, StringComparison.OrdinalIgnoreCase);

    private static Task Responder(HttpContext context, int status, string detalle)
        => Results.Problem(detail: detalle, statusCode: status).ExecuteAsync(context);
}

public static class ResolucionDeTenantMiddlewareExtensions
{
    /// <summary>
    /// Va después de <c>UseAuthentication</c> —necesita el token ya validado— y antes de
    /// <c>UseAuthorization</c> y de los controllers, que ya trabajan con el tenant puesto.
    /// </summary>
    public static IApplicationBuilder UseResolucionDeTenant(this IApplicationBuilder app)
        => app.UseMiddleware<ResolucionDeTenantMiddleware>();
}
