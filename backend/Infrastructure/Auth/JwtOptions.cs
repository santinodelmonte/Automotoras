namespace AutomotoraSaaS.Infrastructure.Auth;

/// <summary>
/// Configuración de los tokens. Se lee de la sección <c>Jwt</c> de <c>appsettings</c> o
/// de variables de entorno (<c>Jwt__Secret</c>, …). Nada hardcodeado.
/// </summary>
public sealed class JwtOptions
{
    public const string Seccion = "Jwt";

    /// <summary>
    /// Largo mínimo del secreto. HMAC-SHA256 usa una clave de 256 bits: con menos, la
    /// firma es más débil que el algoritmo que la produce.
    /// </summary>
    public const int LargoMinimoDelSecreto = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave de firma. Nunca se versiona.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Vida del access token. Corta a propósito: es sin estado y no se puede revocar, así
    /// que lo que lo acota es que venza rápido.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Vida del refresh token, que sí es revocable.</summary>
    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>
    /// Falla temprano y con un mensaje que dice qué falta. Un arranque roto con el motivo
    /// escrito es mucho mejor que una API que levanta y firma tokens con una clave vacía.
    /// </summary>
    public void Validar()
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(Issuer)) faltantes.Add("Jwt:Issuer");
        if (string.IsNullOrWhiteSpace(Audience)) faltantes.Add("Jwt:Audience");

        if (string.IsNullOrWhiteSpace(Secret))
        {
            faltantes.Add("Jwt:Secret");
        }
        else if (Secret.Length < LargoMinimoDelSecreto)
        {
            faltantes.Add($"Jwt:Secret (tiene {Secret.Length} caracteres, necesita al menos {LargoMinimoDelSecreto})");
        }

        if (AccessTokenMinutes <= 0) faltantes.Add("Jwt:AccessTokenMinutes (tiene que ser mayor que cero)");
        if (RefreshTokenDays <= 0) faltantes.Add("Jwt:RefreshTokenDays (tiene que ser mayor que cero)");

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuración de JWT incompleta: {string.Join(", ", faltantes)}. " +
                "Definila por variables de entorno (Jwt__Secret, …) o en " +
                "appsettings.Development.json. La forma esperada está en appsettings.Example.json.");
        }
    }
}
