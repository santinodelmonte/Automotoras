using AutomotoraSaaS.Infrastructure.Auth;
using AutomotoraSaaS.Infrastructure.Storage;

namespace AutomotoraSaaS.Api.Configuracion;

/// <summary>
/// Lo que se revisa de la configuración al arrancar en Production, todo junto y antes de
/// atender el primer request.
/// </summary>
/// <remarks>
/// Cada pieza ya se valida sola cuando se usa por primera vez: el JWT en el primer login,
/// el storage en la primera foto, la base en el primer request. En producción eso es tarde.
/// <c>appsettings.json</c> trae <c>Storage:Provider=Local</c> y una URL de localhost para
/// que el desarrollo arranque sin nada; si en el servidor falta <c>Storage__Provider=R2</c>,
/// la API levanta, el sitio se ve bien, y la primera foto se guarda en el disco del hosting
/// con una URL que no abre nadie.
/// <para>
/// Dos niveles. Un <b>error</b> impide arrancar: sin eso el sistema hace algo peor que no
/// andar. Un <b>aviso</b> se loguea y se sigue: el correo, los dominios propios y Sentry
/// tienen su propia forma de fallar cerrado y el resto del sistema anda sin ellos.
/// </para>
/// </remarks>
public static class VerificacionDeProduccion
{
    public sealed record Resultado(IReadOnlyList<string> Errores, IReadOnlyList<string> Avisos);

    public static Resultado Revisar(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var errores = new List<string>();
        var avisos = new List<string>();

        if (string.IsNullOrWhiteSpace(configuracion.GetConnectionString("Default")))
        {
            errores.Add("Falta ConnectionStrings:Default.");
        }

        var jwt = configuracion.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();
        Juntar(jwt.Validar, errores);

        var storage = configuracion.GetSection(StorageOptions.Seccion).Get<StorageOptions>() ?? new StorageOptions();

        if (storage.EsLocal)
        {
            errores.Add("Storage:Provider es \"Local\": en producción las fotos van a R2, nunca al disco del servidor.");
        }
        else
        {
            Juntar(storage.Validar, errores);
        }

        if (!string.IsNullOrWhiteSpace(storage.PublicBaseUrl) && !EsHttpsPublica(storage.PublicBaseUrl))
        {
            errores.Add($"Storage:PublicBaseUrl (\"{storage.PublicBaseUrl}\") tiene que ser una URL https pública.");
        }

        if (string.IsNullOrWhiteSpace(configuracion["Jobs:Secret"]))
        {
            errores.Add("Falta Jobs:Secret: sin él no corre ningún job (cotizaciones, avisos, limpieza).");
        }

        if (string.IsNullOrWhiteSpace(configuracion["Correo:Host"]) || string.IsNullOrWhiteSpace(configuracion["Correo:Remitente"]))
        {
            avisos.Add("Correo:Host o Correo:Remitente vacío: los avisos de vencimiento no van a salir.");
        }

        var ips = configuracion.GetSection("Deploy:IpsPublicas").Get<string[]>() ?? [];

        if (ips.Length == 0 || ips.Any(ip => ip.Contains("CAMBIAR", StringComparison.OrdinalIgnoreCase)))
        {
            avisos.Add("Deploy:IpsPublicas sin completar: ningún dominio propio se va a poder verificar.");
        }

        if (string.IsNullOrWhiteSpace(configuracion["Sentry:Dsn"]))
        {
            avisos.Add("Sentry:Dsn vacío: los errores de producción no se reportan a ningún lado.");
        }

        return new Resultado(errores, avisos);
    }

    /// <summary>
    /// Loguea los avisos y, si hay errores, no deja arrancar. En IIS el mensaje queda en el
    /// log de stdout del módulo de ASP.NET Core, y el sitio responde 500.30.
    /// </summary>
    public static void Aplicar(IConfiguration configuracion, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var resultado = Revisar(configuracion);

        foreach (var aviso in resultado.Avisos)
        {
            logger.LogWarning("Configuración de producción: {Aviso}", aviso);
        }

        if (resultado.Errores.Count > 0)
        {
            throw new InvalidOperationException(
                "La configuración de producción está incompleta:" + Environment.NewLine +
                string.Join(Environment.NewLine, resultado.Errores.Select(e => "  - " + e)) + Environment.NewLine +
                "La forma esperada está en appsettings.Example.json; en el servidor va por variables de entorno.");
        }
    }

    private static void Juntar(Action validar, List<string> errores)
    {
        try
        {
            validar();
        }
        catch (InvalidOperationException ex)
        {
            errores.Add(ex.Message);
        }
    }

    private static bool EsHttpsPublica(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && !uri.IsLoopback;
}
