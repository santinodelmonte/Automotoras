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

    /// <summary>
    /// Identificador al azar de la visita, que vive lo que la pestaña abierta. Permite
    /// contar personas distintas sin poder seguir a nadie de un día para otro.
    /// </summary>
    /// <remarks>
    /// Es lo único de la visita que se guarda. La IP, el navegador y la página de origen
    /// se guardaban y ningún reporte los usaba: eran datos personales sin ninguna
    /// finalidad, que es justo lo que la Ley 18.331 pide no tener.
    /// </remarks>
    public string? SessionId { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Payload JSON con el detalle propio de cada tipo de evento.</summary>
    public string? Metadata { get; set; }
}
