namespace AutomotoraSaaS.Api.Seguridad;

/// <summary>
/// Los headers de seguridad que salen en cada respuesta: la API, el sitio público y el
/// panel son la misma aplicación, así que se ponen una sola vez y acá.
/// </summary>
/// <remarks>
/// La política de contenido se puede permitir ser estricta porque el frontend no carga
/// nada de afuera: la fuente va empaquetada, no hay scripts de terceros ni iframes, y la
/// API está en el mismo origen. Lo único externo son las imágenes —las fotos viven en R2 y
/// el logo de una automotora puede ser una URL cualquiera—, por eso <c>img-src</c> acepta
/// cualquier <c>https:</c>.
/// <para>
/// <c>style-src</c> lleva <c>'unsafe-inline'</c> porque los atributos <c>style</c> del
/// HTML los necesita el propio React para los anchos de las barras y los retrasos de las
/// animaciones. Con los scripts no se cede: una inyección de HTML no ejecuta nada.
/// </para>
/// <para>
/// Swagger queda afuera de la política: su página usa scripts inline y solo existe en
/// Development.
/// </para>
/// </remarks>
public static class HeadersDeSeguridad
{
    public const string SeccionDeConfiguracion = "Seguridad:OrigenesExtraDeConexion";

    public static IApplicationBuilder UseHeadersDeSeguridad(this IApplicationBuilder app, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        // Si alguna vez el frontend se sirve aparte y llama a la API en otro dominio, ese
        // origen se agrega por configuración en vez de abrir connect-src a cualquiera.
        var extra = configuracion.GetSection(SeccionDeConfiguracion).Get<string[]>() ?? [];
        var politica = Politica(extra);

        return app.Use((contexto, siguiente) =>
        {
            var headers = contexto.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (!contexto.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = politica;
            }

            return siguiente(contexto);
        });
    }

    public static string Politica(IEnumerable<string> origenesExtraDeConexion)
    {
        var conexion = string.Join(' ', new[] { "'self'" }.Concat(origenesExtraDeConexion));

        return string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob: https:",
            "font-src 'self' data:",
            $"connect-src {conexion}",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");
    }
}
