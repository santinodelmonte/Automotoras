using System.Globalization;

namespace AutomotoraSaaS.Infrastructure.Storage;

/// <summary>
/// Arma la ruta con la que se guarda cada imagen.
/// </summary>
/// <remarks>
/// El nombre original del archivo se descarta entero. Viene del cliente, así que puede
/// traer <c>../</c>, caracteres que el sistema de archivos no acepta, o el nombre de un
/// archivo que ya existe. Un GUID no tiene ninguno de esos problemas y además evita que
/// dos vendedores que suben "foto.jpg" se pisen.
/// </remarks>
public static class GeneradorDeClaves
{
    /// <summary>
    /// Carpeta lógica de las fotos de un vehículo. El nombre del archivo lo pone el
    /// storage, con un GUID: dos vendedores subiendo "foto.jpg" no se pisan, y el nombre
    /// original —que viene del cliente y puede traer <c>../</c>— se descarta entero.
    /// </summary>
    public static string CarpetaDeVehiculo(int tenantId, int vehiculoId)
        => string.Create(CultureInfo.InvariantCulture, $"tenants/{tenantId}/vehiculos/{vehiculoId}");

    /// <summary>
    /// <c>true</c> si la clave es una de las que genera este código. Se chequea antes de
    /// borrar: la clave sale de la base, pero un borrado que acepte cualquier ruta es un
    /// borrado arbitrario esperando a que alguien encuentre cómo escribir en esa columna.
    /// </summary>
    public static bool EsSegura(string? clave)
        => !string.IsNullOrWhiteSpace(clave)
           && !clave.Contains("..", StringComparison.Ordinal)
           && !clave.StartsWith('/')
           && !clave.Contains('\\', StringComparison.Ordinal)
           && clave.StartsWith("tenants/", StringComparison.Ordinal);
}
