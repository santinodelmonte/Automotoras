using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Usuario del panel privado.
/// </summary>
/// <remarks>
/// No implementa <c>ITenantEntity</c> porque <see cref="TenantId"/> es anulable: el
/// SuperAdmin no pertenece a ninguna automotora. Aun así el <c>DbContext</c> le aplica
/// un filtro global equivalente, configurado explícitamente.
/// </remarks>
public class User : ICreatedAt
{
    public int Id { get; set; }

    /// <summary>Nulo solo para <see cref="RolUsuario.SuperAdmin"/>.</summary>
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Único en todo el sistema, no por tenant.</summary>
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string Nombre { get; set; }

    public RolUsuario Rol { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();
}
