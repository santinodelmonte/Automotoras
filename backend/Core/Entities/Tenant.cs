using AutomotoraSaaS.Core.Common;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Una automotora. Su identidad visual y sus datos de contacto son configuración,
/// no un deploy separado: una sola aplicación atiende a todos los tenants.
/// </summary>
public class Tenant : ICreatedAt
{
    public int Id { get; set; }

    /// <summary>Identificador en la URL de desarrollo: <c>/t/{slug}</c>. Único.</summary>
    public required string Slug { get; set; }

    public required string Nombre { get; set; }

    /// <summary>Dominio propio de la automotora. Único cuando no es nulo.</summary>
    public string? DominioCustom { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>Color en formato <c>#RRGGBB</c>.</summary>
    public string? ColorPrimario { get; set; }

    /// <summary>Color en formato <c>#RRGGBB</c>.</summary>
    public string? ColorSecundario { get; set; }

    public string? Whatsapp { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public ICollection<User> Users { get; } = new List<User>();
    public ICollection<Vehiculo> Vehiculos { get; } = new List<Vehiculo>();
}
