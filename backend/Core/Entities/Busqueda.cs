using AutomotoraSaaS.Core.Common;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Una búsqueda ejecutada en el sitio público, con los filtros aplicados y cuántos
/// resultados devolvió.
/// </summary>
/// <remarks>
/// Las búsquedas con <see cref="ResultadosCount"/> cero son la señal más valiosa del
/// producto: dicen qué le están pidiendo a la automotora que no tiene en stock.
/// </remarks>
public class Busqueda : ITenantEntity, ICreatedAt
{
    public long Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Filtros aplicados, serializados como JSON.</summary>
    public required string Filtros { get; set; }

    public int ResultadosCount { get; set; }

    public string? SessionId { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
