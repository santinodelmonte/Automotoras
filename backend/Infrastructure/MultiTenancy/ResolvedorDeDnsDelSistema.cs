using System.Net;
using System.Net.Sockets;
using AutomotoraSaaS.Core.Tenants;

namespace AutomotoraSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Consulta el DNS del sistema operativo.
/// </summary>
/// <remarks>
/// Con un tope de tiempo propio. <c>Dns.GetHostAddressesAsync</c> no tiene timeout: contra
/// un servidor de DNS que no contesta se queda esperando lo que el sistema decida, y en
/// shared hosting IIS eso es un hilo del app pool que atienden todos los tenants tomado por
/// la verificación de un dominio de uno solo.
/// <para>
/// La verificación la dispara una persona apretando un botón, así que unos pocos segundos
/// alcanzan: si el DNS no contestó en ese plazo, lo correcto es decir que no se pudo y que
/// vuelva a intentar, no sostener el request.
/// </para>
/// </remarks>
public sealed class ResolvedorDeDnsDelSistema : IResolvedorDeDns
{
    private static readonly TimeSpan Tope = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<string>> DireccionesDeAsync(
        string dominio,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dominio))
        {
            return [];
        }

        using var reloj = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        reloj.CancelAfter(Tope);

        try
        {
            var direcciones = await Dns
                .GetHostAddressesAsync(dominio.Trim(), reloj.Token)
                .ConfigureAwait(false);

            return direcciones.Select(d => d.ToString()).ToList();
        }
        catch (SocketException)
        {
            // El dominio no existe o no resuelve. Es la respuesta más común mientras el
            // dueño todavía no tocó su DNS, no una falla: se devuelve vacío y quien llama
            // decide qué decirle.
            return [];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Se agotó el tope propio, no lo canceló el cliente.
            return [];
        }
    }
}
