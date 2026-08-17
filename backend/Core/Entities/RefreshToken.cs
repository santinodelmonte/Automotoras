using AutomotoraSaaS.Core.Common;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Refresh token emitido a un usuario. Se guarda hasheado, nunca en claro: si se filtra
/// la tabla, los tokens no son utilizables.
/// </summary>
public class RefreshToken : ICreatedAt
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Hash del token. El valor en claro solo lo conoce el cliente.</summary>
    public required string TokenHash { get; set; }

    /// <summary>UTC.</summary>
    public DateTime ExpiraEn { get; set; }

    /// <summary>UTC. Nulo mientras el token siga vigente.</summary>
    public DateTime? RevocadoEn { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
