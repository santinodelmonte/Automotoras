namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Versión de un modelo ("Trendline", "GLS", "Sportback"). Catálogo global, tabla
/// <c>versiones</c>.
/// </summary>
/// <remarks>
/// Se llama <c>VersionVehiculo</c> y no <c>Version</c> para no chocar con
/// <see cref="System.Version"/>, que entra por los <c>ImplicitUsings</c> y volvería
/// ambiguo el nombre dentro de este namespace.
/// </remarks>
public class VersionVehiculo
{
    public int Id { get; set; }

    public int ModeloId { get; set; }
    public Modelo? Modelo { get; set; }

    public required string Nombre { get; set; }

    public bool Activo { get; set; } = true;
}
