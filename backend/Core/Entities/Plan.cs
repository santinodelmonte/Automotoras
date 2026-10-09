using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Un plan del catálogo comercial: qué cuesta y qué habilita.
/// </summary>
/// <remarks>
/// Vive en la base y no en una constante del código: los precios cambian, los topes
/// cambian, y una promoción no puede exigir un deploy. Es global, como el catálogo de
/// marcas: el plan Demanda es el mismo para todas las automotoras.
/// </remarks>
public class Plan : ICreatedAt
{
    public int Id { get; set; }

    /// <summary>Identificador estable: <c>vidriera</c>, <c>demanda</c> o <c>full</c>. Único.</summary>
    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public decimal PrecioMensual { get; set; }

    public Moneda Moneda { get; set; }

    /// <summary>Tope de vehículos publicados. Nulo es sin límite.</summary>
    public int? MaxVehiculos { get; set; }

    /// <summary>Tope de usuarios. Nulo es sin límite.</summary>
    public int? MaxUsuarios { get; set; }

    public bool IncluyeReportes { get; set; }
    public bool IncluyeBenchmark { get; set; }
    public bool IncluyeDominioPropio { get; set; }

    /// <summary>Horas de ajustes incluidas por mes.</summary>
    public int HorasSoporteMes { get; set; }

    /// <summary>
    /// Si se puede asignar a una automotora nueva. Un plan que se deja de vender no se
    /// borra: las suscripciones viejas lo siguen nombrando.
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
