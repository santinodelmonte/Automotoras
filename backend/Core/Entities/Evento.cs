using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Evento de comportamiento en el sitio público. Es la tabla que alimenta todos los
/// reportes de demanda.
/// </summary>
/// <remarks>
/// Se instrumenta desde el primer día, antes de que exista un solo reporte: los datos de
/// demanda solo valen acumulados en el tiempo y lo que no se mide hoy no se recupera
/// nunca. Crece rápido, por eso el índice compuesto
/// <c>(tenant_id, vehiculo_id, tipo, created_at)</c>.
/// </remarks>
public class Evento : ITenantEntity, ICreatedAt
{
    public long Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Nulo en eventos que no son sobre un vehículo puntual.</summary>
    public int? VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }

    public TipoEvento Tipo { get; set; }

    /// <summary>Cookie de primera parte. Permite agrupar la actividad de una visita.</summary>
    public string? SessionId { get; set; }

    /// <summary>Hash de la IP. Nunca se guarda la IP en claro.</summary>
    public string? IpHash { get; set; }

    public string? UserAgent { get; set; }
    public string? Referer { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Payload JSON con el detalle propio de cada tipo de evento.</summary>
    public string? Metadata { get; set; }
}
