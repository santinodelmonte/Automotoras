using System.Globalization;
using System.Security.Claims;
using AutomotoraSaaS.Core.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AutomotoraSaaS.Api.Auth;

/// <summary>
/// Lectura de los claims del access token.
/// </summary>
/// <remarks>
/// Todo lo que se lee acá viene de adentro de la firma del token. Nada llega por header,
/// query param ni body: eso lo controla el cliente, y el tenant no es negociable con el
/// cliente.
/// </remarks>
public static class ClaimsDelRequest
{
    public static int? IdDeUsuario(this ClaimsPrincipal principal)
        => LeerEntero(principal, JwtRegisteredClaimNames.Sub);

    /// <summary>Tenant del usuario. Nulo en el SuperAdmin, que no pertenece a ninguno.</summary>
    public static int? TenantIdDelToken(this ClaimsPrincipal principal)
        => LeerEntero(principal, ClaimsDeLaApp.TenantId);

    public static string? RolDelToken(this ClaimsPrincipal principal)
        => principal?.FindFirstValue(ClaimsDeLaApp.Rol);

    public static string? EmailDelToken(this ClaimsPrincipal principal)
        => principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public static string? NombreDelToken(this ClaimsPrincipal principal)
        => principal?.FindFirstValue(ClaimsDeLaApp.Nombre);

    public static bool EsSuperAdmin(this ClaimsPrincipal principal)
        => string.Equals(principal.RolDelToken(), Roles.SuperAdmin, StringComparison.Ordinal);

    /// <summary>
    /// El precio de costo y el margen los ve el dueño, no el vendedor. Con esto se decide
    /// qué se proyecta al DTO: el dato no sale del servidor, no se esconde en la pantalla.
    /// </summary>
    public static bool PuedeVerCostos(this ClaimsPrincipal principal)
    {
        var rol = principal.RolDelToken();

        return string.Equals(rol, Roles.Owner, StringComparison.Ordinal)
               || string.Equals(rol, Roles.SuperAdmin, StringComparison.Ordinal);
    }

    private static int? LeerEntero(ClaimsPrincipal? principal, string claim)
    {
        var valor = principal?.FindFirstValue(claim);

        return int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : null;
    }
}
