namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Foto de un vehículo. Las URLs apuntan a object storage: nunca se escribe un binario
/// en el disco del servidor.
/// </summary>
/// <remarks>
/// No lleva <c>tenant_id</c> propio: pertenece al tenant de su vehículo. El filtro
/// global la alcanza a través de la navegación a <see cref="Vehiculo"/>.
/// </remarks>
public class VehiculoFoto
{
    public int Id { get; set; }

    public int VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }

    public required string Url { get; set; }

    public string? UrlThumb { get; set; }

    /// <summary>Posición en la galería, empezando en 0.</summary>
    public int Orden { get; set; }

    public bool EsPortada { get; set; }
}
