using AutomotoraSaaS.Core.Common;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Qué plan tuvo una automotora y durante cuándo.
/// </summary>
/// <remarks>
/// Es una tabla de historial y no una columna en <c>tenants</c>: hace falta saber en qué
/// plan estaba una automotora en marzo, y un campo que se pisa no lo dice. Un cambio de
/// plan cierra la suscripción vigente y abre otra.
/// <para>
/// Un tenant tiene como mucho una suscripción con <see cref="Fin"/> nulo, que es la
/// vigente. Lo garantiza un índice único en la base, no la disciplina de quien escribe.
/// </para>
/// </remarks>
public class Suscripcion : ITenantEntity, ICreatedAt
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int PlanId { get; set; }
    public Plan? Plan { get; set; }

    public DateOnly Inicio { get; set; }

    /// <summary>Último día de la suscripción. Nulo mientras está vigente.</summary>
    public DateOnly? Fin { get; set; }

    /// <summary>
    /// Último día cubierto por lo cobrado, inclusive. Es el corazón del vencimiento.
    /// </summary>
    /// <remarks>
    /// Lo mueve el registro de un pago y nada más: así cada día cubierto tiene un pago
    /// que lo explica, aunque sea uno de monto cero por una bonificación.
    /// </remarks>
    public DateOnly PagaHasta { get; set; }

    public string? MotivoDeBaja { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public ICollection<Pago> Pagos { get; } = new List<Pago>();
}
