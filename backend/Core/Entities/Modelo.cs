using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Modelo de una marca. Catálogo global administrado por el SuperAdmin: el vendedor
/// que necesita uno que falta abre una <see cref="SolicitudModelo"/>, no lo crea.
/// </summary>
public class Modelo
{
    public int Id { get; set; }

    public int MarcaId { get; set; }
    public Marca? Marca { get; set; }

    public required string Nombre { get; set; }

    public Carroceria Carroceria { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<VersionVehiculo> Versiones { get; } = new List<VersionVehiculo>();
}
