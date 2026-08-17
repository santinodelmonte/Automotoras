using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Un vehículo publicado por una automotora.
/// </summary>
/// <remarks>
/// Los tipos no son negociables: el kilometraje y el año son <c>int</c>, el precio es
/// <c>decimal</c> con la moneda en columna aparte. Nunca texto. En Uruguay se publica
/// tanto en dólares como en pesos: sin moneda explícita y sin cotizaciones históricas
/// no se puede comparar nada a lo largo del tiempo.
/// </remarks>
public class Vehiculo : ITenantEntity, ICreatedAt, IUpdatedAt
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ModeloId { get; set; }
    public Modelo? Modelo { get; set; }

    public int? VersionId { get; set; }
    public VersionVehiculo? Version { get; set; }

    public int Anio { get; set; }

    public int Kilometraje { get; set; }

    public Combustible Combustible { get; set; }
    public Transmision Transmision { get; set; }

    public string? Color { get; set; }
    public int? Puertas { get; set; }
    public string? Motor { get; set; }

    public decimal Precio { get; set; }
    public Moneda Moneda { get; set; }

    public EstadoVehiculo Estado { get; set; } = EstadoVehiculo.Disponible;

    public string? Descripcion { get; set; }

    public bool Destacado { get; set; }

    /// <summary>
    /// Precio de costo. Solo visible para Owner y SuperAdmin: nunca debe salir en un DTO
    /// del sitio público ni en uno consumido por un Seller.
    /// </summary>
    public decimal? PrecioCosto { get; set; }

    /// <summary>UTC. Base del cálculo de días en góndola.</summary>
    public DateTime FechaPublicacion { get; set; }

    /// <summary>UTC. Se completa al marcar el vehículo como vendido.</summary>
    public DateTime? FechaVenta { get; set; }

    /// <summary>Se completa al marcar el vehículo como vendido.</summary>
    public decimal? PrecioVenta { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    public ICollection<VehiculoFoto> Fotos { get; } = new List<VehiculoFoto>();
}
