namespace AutomotoraSaaS.Core.Auth;

/// <summary>
/// Hashing de contraseñas. La implementación decide el algoritmo y el costo; el resto de
/// la aplicación solo conoce esta interfaz.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Devuelve el hash a guardar. Incluye el algoritmo, el costo y la sal, para que
    /// subir el costo más adelante no invalide los hashes ya existentes.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Compara en tiempo constante. Devuelve <c>false</c> ante cualquier hash con formato
    /// inválido en vez de lanzar: una fila corrupta no debe tumbar el login.
    /// </summary>
    bool Verificar(string password, string hashAlmacenado);

    /// <summary>
    /// Gasta el mismo trabajo que una verificación real, sin comparar contra nadie. Lo
    /// usa el login cuando el email no existe: sin esto, un email inexistente responde
    /// mucho más rápido que uno real, y esa diferencia de tiempo alcanza para enumerar
    /// los usuarios del sistema.
    /// </summary>
    void VerificarSenuelo(string password);
}
