using AutomotoraSaaS.Core.Dominios;
using DnsClient;

namespace AutomotoraSaaS.Infrastructure.Dominios;

/// <summary>
/// Consulta TXT contra los resolvers del sistema.
/// </summary>
/// <remarks>
/// El <c>LookupClient</c> es singleton porque mantiene su propio caché y sus sockets;
/// crear uno por request tiraría el caché a la basura en cada verificación.
/// <para>
/// Timeout corto y sin reintentos internos: esto corre dentro de un request HTTP, y una
/// consulta colgada se lleva un hilo del app pool que atiende a todos los tenants. Si el
/// DNS no contesta rápido, se prefiere devolver "no pudimos consultar" y que la automotora
/// reintente, antes que dejar el request esperando.
/// </para>
/// </remarks>
public sealed class ConsultaDnsConDnsClient : IConsultaDns
{
    private readonly ILookupClient _cliente;

    public ConsultaDnsConDnsClient(ILookupClient cliente)
    {
        _cliente = cliente;
    }

    public static LookupClientOptions Opciones()
    {
        var opciones = new LookupClientOptions
        {
            Timeout = TimeSpan.FromSeconds(3),
            Retries = 1,
            UseCache = true,
            ThrowDnsErrors = false,
        };

        return opciones;
    }

    public async Task<IReadOnlyList<string>> TxtAsync(
        string nombre,
        CancellationToken cancellationToken = default)
    {
        IDnsQueryResponse respuesta;

        try
        {
            respuesta = await _cliente
                .QueryAsync(nombre, QueryType.TXT, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DnsResponseException ex)
        {
            throw new ConsultaDnsFallidaException($"No se pudo consultar el TXT de {nombre}.", ex);
        }

        // NXDOMAIN no es un fallo de consulta: es una respuesta, y significa que el TXT no
        // está. Los demás códigos de error sí son "no sabemos".
        if (respuesta.HasError && respuesta.Header.ResponseCode != DnsHeaderResponseCode.NotExistentDomain)
        {
            throw new ConsultaDnsFallidaException(
                $"El DNS respondió con error al consultar {nombre}: {respuesta.ErrorMessage}");
        }

        return respuesta.Answers.TxtRecords()
            // Un TXT largo viene partido en trozos de 255 caracteres y hay que volver a
            // pegarlo: un token de 32 no se parte, pero el registro puede tener otras cosas.
            .Select(registro => string.Concat(registro.Text))
            .ToList();
    }
}
