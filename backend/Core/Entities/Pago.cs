using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Un cobro registrado a mano. El sistema no cobra: sabe qué se cobró y qué cubre.
/// </summary>
/// <remarks>
/// Los meses bonificados también son un pago, de monto cero y con su nota. Así el
/// descuento queda documentado y no como un hueco inexplicable en el historial.
/// </remarks>
public class Pago : ITenantEntity, ICreatedAt
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int SuscripcionId { get; set; }
    public Suscripcion? Suscripcion { get; set; }

    /// <summary>Cuándo se cobró.</summary>
    public DateOnly Fecha { get; set; }

    public decimal Monto { get; set; }

    public Moneda Moneda { get; set; }

    /// <summary>Primer día que cubre, inclusive.</summary>
    public DateOnly PeriodoDesde { get; set; }

    /// <summary>Último día que cubre, inclusive.</summary>
    public DateOnly PeriodoHasta { get; set; }

    /// <summary>Transferencia, efectivo, bonificación… Texto libre: el cobro vive afuera.</summary>
    public required string Medio { get; set; }

    /// <summary>Número de transferencia, de factura o lo que permita encontrarlo después.</summary>
    public string? Comprobante { get; set; }

    public string? Nota { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
