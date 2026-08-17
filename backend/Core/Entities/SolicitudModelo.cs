using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Pedido de alta de un modelo que falta en el catálogo. El vendedor lo solicita, el
/// SuperAdmin lo aprueba o lo rechaza; nadie crea modelos por su cuenta.
/// </summary>
/// <remarks>
/// Esta tabla no está en la lista del brief, pero la aprobación de solicitudes de alta
/// de modelos sí es una feature de fase 1 y necesita dónde vivir. Es la pieza que hace
/// cumplible la regla de normalización sin bloquear al vendedor.
/// </remarks>
public class SolicitudModelo : ITenantEntity, ICreatedAt
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Quién la pidió.</summary>
    public int SolicitadaPorUserId { get; set; }
    public User? SolicitadaPor { get; set; }

    /// <summary>Marca existente sobre la que se pide el modelo.</summary>
    public int MarcaId { get; set; }
    public Marca? Marca { get; set; }

    public required string NombreModelo { get; set; }

    public Carroceria Carroceria { get; set; }

    public EstadoSolicitudModelo Estado { get; set; } = EstadoSolicitudModelo.Pendiente;

    /// <summary>Modelo creado al aprobar. Nulo mientras no se aprobó.</summary>
    public int? ModeloCreadoId { get; set; }
    public Modelo? ModeloCreado { get; set; }

    /// <summary>Motivo del rechazo, o nota del SuperAdmin al resolverla.</summary>
    public string? NotaResolucion { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC. Nulo mientras siga pendiente.</summary>
    public DateTime? ResueltaEn { get; set; }
}
