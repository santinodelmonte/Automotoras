namespace AutomotoraSaaS.Core.Auth;

/// <summary>
/// Nombres de los roles tal como viajan en el claim <c>role</c> del JWT.
/// </summary>
/// <remarks>
/// Coinciden exactamente con los nombres de <c>RolUsuario</c>. Que sean constantes y no
/// literales sueltos es lo que permite que un typo en un <c>[Authorize]</c> no compile
/// en vez de dejar un endpoint abierto.
/// </remarks>
public static class Roles
{
    public const string SuperAdmin = nameof(SuperAdmin);
    public const string Owner = nameof(Owner);
    public const string Seller = nameof(Seller);
}

/// <summary>
/// Políticas de autorización registradas en <c>Program.cs</c>.
/// </summary>
public static class Politicas
{
    /// <summary>Cross-tenant. Solo para los endpoints bajo <c>/api/admin/*</c>.</summary>
    public const string SoloSuperAdmin = nameof(SoloSuperAdmin);

    /// <summary>Dueño de la automotora: reportes, analítica y gestión de vendedores.</summary>
    public const string SoloOwner = nameof(SoloOwner);

    /// <summary>Cualquiera que trabaje dentro de un tenant: Owner o Seller.</summary>
    public const string PanelDeTenant = nameof(PanelDeTenant);
}

/// <summary>
/// Claims propios de la aplicación dentro del access token.
/// </summary>
public static class ClaimsDeLaApp
{
    /// <summary>
    /// Tenant del usuario. Ausente en el SuperAdmin, que no pertenece a ninguna automotora.
    /// </summary>
    /// <remarks>
    /// Es la <b>única</b> fuente del tenant en el panel privado. Nunca un header, un query
    /// param ni un campo del body: todo eso lo controla el cliente.
    /// </remarks>
    public const string TenantId = "tenant_id";

    /// <summary>Rol del usuario. Se mapea a <c>RoleClaimType</c> en la validación del token.</summary>
    public const string Rol = "role";

    /// <summary>Nombre para mostrar.</summary>
    public const string Nombre = "name";
}
