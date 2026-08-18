using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace AutomotoraSaaS.Infrastructure.Analitica;

/// <summary>
/// Convierte una IP en un identificador estable que no permite recuperarla.
/// </summary>
/// <remarks>
/// La IP en claro no se guarda nunca. Sirve para dos cosas —descontar repeticiones del
/// mismo visitante y detectar abuso— y las dos funcionan igual de bien con un hash.
/// <para>
/// El hash lleva sal secreta y el id del tenant. Sin sal, el espacio de IPv4 son cuatro
/// mil millones de valores: cualquiera con la tabla puede precalcularlo entero y deshacer
/// el hash en minutos. Con el tenant adentro, además, la misma IP da hashes distintos en
/// cada automotora, y ninguna puede cruzar visitantes con otra.
/// </para>
/// </remarks>
public sealed class HasheadorDeIp
{
    private readonly byte[] _sal;

    public HasheadorDeIp(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        // Si no hay sal propia se usa la clave de firma, que siempre está: sin ella la API
        // no arranca. Es una sal secreta y estable, que es todo lo que hace falta acá.
        var sal = configuracion["Analytics:IpHashSalt"];

        if (string.IsNullOrWhiteSpace(sal))
        {
            sal = configuracion["Jwt:Secret"];
        }

        if (string.IsNullOrWhiteSpace(sal))
        {
            throw new InvalidOperationException(
                "Falta una sal para hashear las IPs. Definí Analytics:IpHashSalt o Jwt:Secret.");
        }

        _sal = Encoding.UTF8.GetBytes(sal);
    }

    /// <summary>Hash hexadecimal de 64 caracteres, o <c>null</c> si no hay IP.</summary>
    public string? Hashear(IPAddress? ip, int tenantId)
    {
        if (ip is null)
        {
            return null;
        }

        var material = Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{ip}|{tenantId}|"));

        var buffer = new byte[material.Length + _sal.Length];
        material.CopyTo(buffer, 0);
        _sal.CopyTo(buffer, material.Length);

        return Convert.ToHexString(SHA256.HashData(buffer));
    }
}
