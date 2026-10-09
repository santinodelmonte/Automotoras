using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Planes;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Registro de un aviso de vencimiento enviado al dueño de una automotora.
/// </summary>
/// <remarks>
/// Uno por etapa y por período: el aviso de "por vencer" de un vencimiento se manda una
/// vez, aunque el job corra todos los días. Cuando se registra un pago, el vencimiento es
/// otro y el ciclo de avisos vuelve a empezar. Lo garantiza un índice único, así que dos
/// corridas simultáneas del cron tampoco duplican el correo.
/// </remarks>
public class AvisoDeCobro : ITenantEntity, ICreatedAt
{
    public long Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int SuscripcionId { get; set; }
    public Suscripcion? Suscripcion { get; set; }

    /// <summary>La etapa avisada.</summary>
    public EstadoDeCobro Estado { get; set; }

    /// <summary>El vencimiento al que se refiere el aviso.</summary>
    public DateOnly PagaHasta { get; set; }

    /// <summary>A quiénes se mandó, separados por coma.</summary>
    public required string Destinatarios { get; set; }

    /// <summary>UTC. Cuándo se mandó.</summary>
    public DateTime CreatedAt { get; set; }
}
