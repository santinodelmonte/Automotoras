using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AutomotoraSaaS.Infrastructure.Auth;

/// <summary>
/// Emite el par de tokens de una sesión.
/// </summary>
/// <remarks>
/// El access token lleva el tenant como claim firmado. Ese claim es la única fuente del
/// tenant en el panel privado: como está dentro de la firma, un usuario del tenant A no
/// puede convertirse en uno del tenant B sin la clave.
/// </remarks>
public sealed class GeneradorDeTokens
{
    private readonly JwtOptions _opciones;
    private readonly TimeProvider _reloj;
    private readonly SigningCredentials _credenciales;

    public GeneradorDeTokens(IOptions<JwtOptions> opciones, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _opciones.Validar();
        _reloj = reloj;

        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Secret));
        _credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTimeOffset ExpiraEn) CrearAccessToken(User usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var ahora = _reloj.GetUtcNow();
        var expiraEn = ahora.AddMinutes(_opciones.AccessTokenMinutes);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(CultureInfo.InvariantCulture),
            [JwtRegisteredClaimNames.Email] = usuario.Email,
            [ClaimsDeLaApp.Nombre] = usuario.Nombre,
            [ClaimsDeLaApp.Rol] = usuario.Rol.ToString(),
        };

        // El SuperAdmin no pertenece a ninguna automotora: su token no lleva tenant, y sin
        // tenant en el token no hay nada que resolver ni datos de tenant que leer. Opera
        // por /api/admin/*, de forma explícita.
        if (usuario.TenantId is { } tenantId)
        {
            claims[ClaimsDeLaApp.TenantId] = tenantId.ToString(CultureInfo.InvariantCulture);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Issuer,
            Audience = _opciones.Audience,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expiraEn.UtcDateTime,
            SigningCredentials = _credenciales,
            Claims = claims,
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiraEn);
    }

    /// <summary>
    /// Refresh token opaco: 32 bytes de aleatoriedad criptográfica. No lleva información
    /// adentro, así que no hay nada que firmar ni que se pueda leer sin la base.
    /// </summary>
    public static string CrearRefreshToken()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Hash del refresh token, que es lo único que se guarda. Un SHA-256 desnudo alcanza
    /// y es lo correcto: el token ya tiene 256 bits de entropía, así que no hay diccionario
    /// que atacar y no hace falta el costo de un PBKDF2 en cada renovación.
    /// </summary>
    public static string HashDe(string refreshToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
