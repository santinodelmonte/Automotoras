using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AutomotoraSaaS.Core.Auth;

namespace AutomotoraSaaS.Infrastructure.Auth;

/// <summary>
/// PBKDF2-HMAC-SHA256. Viene en la BCL, no agrega dependencias y es de los algoritmos
/// aceptados por OWASP para contraseñas.
/// </summary>
/// <remarks>
/// El hash guardado incluye el algoritmo, la cantidad de iteraciones y la sal:
/// <c>pbkdf2-sha256$210000$&lt;sal-b64&gt;$&lt;hash-b64&gt;</c>. Es lo que permite subir el
/// costo más adelante sin invalidar las contraseñas ya existentes: los hashes viejos se
/// siguen verificando con las iteraciones con las que se generaron.
/// </remarks>
public sealed class PasswordHasherPbkdf2 : IPasswordHasher
{
    private const string Algoritmo = "pbkdf2-sha256";
    private const int Iteraciones = 210_000;
    private const int LargoDeSal = 16;
    private const int LargoDeHash = 32;
    private const char Separador = '$';

    /// <summary>
    /// Hash de una contraseña que nadie usa. Se calcula una sola vez por proceso y sirve
    /// para gastar el mismo tiempo cuando el email no existe: sin esto, un email
    /// inexistente responde mucho más rápido que uno real y esa diferencia alcanza para
    /// enumerar usuarios.
    /// </summary>
    private static readonly string HashSenuelo =
        Calcular("contrasena-senuelo-que-nunca-se-usa", RandomNumberGenerator.GetBytes(LargoDeSal), Iteraciones);

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return Calcular(password, RandomNumberGenerator.GetBytes(LargoDeSal), Iteraciones);
    }

    public bool Verificar(string password, string hashAlmacenado)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashAlmacenado))
        {
            return false;
        }

        var partes = hashAlmacenado.Split(Separador);

        if (partes.Length != 4 || !string.Equals(partes[0], Algoritmo, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iteraciones)
            || iteraciones <= 0)
        {
            return false;
        }

        byte[] sal;
        byte[] esperado;

        try
        {
            sal = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            // Una fila corrupta no tiene por qué tumbar el login de todos los demás.
            return false;
        }

        if (sal.Length == 0 || esperado.Length == 0)
        {
            return false;
        }

        var calculado = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);

        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    /// <inheritdoc />
    public void VerificarSenuelo(string password) => Verificar(password, HashSenuelo);

    private static string Calcular(string password, byte[] sal, int iteraciones)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), sal, iteraciones, HashAlgorithmName.SHA256, LargoDeHash);

        return string.Join(
            Separador,
            Algoritmo,
            iteraciones.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(sal),
            Convert.ToBase64String(hash));
    }
}
