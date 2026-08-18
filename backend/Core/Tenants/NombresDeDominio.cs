using System.Text.RegularExpressions;

namespace AutomotoraSaaS.Core.Tenants;

/// <summary>
/// Cómo se escribe un dominio en este sistema.
/// </summary>
/// <remarks>
/// Está en un solo lugar porque el dominio se normaliza en tres momentos distintos —cuando
/// el dueño lo da de alta, cuando el cron lo reverifica y cuando llega un visitante con un
/// <c>Host</c>— y si esos tres no coinciden carácter por carácter, el sitio no resuelve.
/// </remarks>
public static partial class NombresDeDominio
{
    /// <summary>Nombre del TXT donde se publica el token, delante del dominio.</summary>
    public const string PrefijoDeVerificacion = "_automotora";

    /// <summary>
    /// Minúsculas y sin <c>www.</c>: los dominios no distinguen mayúsculas, y nadie quiere
    /// dos filas para el mismo sitio.
    /// </summary>
    public static string Normalizar(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var dominio = host.Trim().TrimEnd('.').ToLowerInvariant();

        // El Host de un request puede traer el puerto. Sin sacarlo, localhost:5173 nunca
        // matchearía contra un dominio guardado.
        var puerto = dominio.IndexOf(':', StringComparison.Ordinal);

        if (puerto >= 0)
        {
            dominio = dominio[..puerto];
        }

        return dominio.StartsWith("www.", StringComparison.Ordinal) ? dominio[4..] : dominio;
    }

    public static bool EsValido(string? dominio)
        => !string.IsNullOrWhiteSpace(dominio)
           && dominio.Length <= 255
           && Formato().IsMatch(dominio);

    /// <summary>Dónde tiene que estar el TXT de verificación de este dominio.</summary>
    public static string NombreDelTxt(string dominio) => $"{PrefijoDeVerificacion}.{dominio}";

    [GeneratedRegex(FormatosDeTenant.Dominio, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Formato();
}
