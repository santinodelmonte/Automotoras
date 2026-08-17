namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Marca de vehículo. Catálogo global compartido por todos los tenants: lo administra
/// el SuperAdmin.
/// </summary>
/// <remarks>
/// Nunca texto libre. Si el vendedor pudiera escribir "VW", "Volkswagen" y "volkswagen ",
/// cualquier agregación posterior sería basura irrecuperable, y la analítica de demanda
/// es el producto.
/// </remarks>
public class Marca
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Modelo> Modelos { get; } = new List<Modelo>();
}
