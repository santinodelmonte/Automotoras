using System.Collections.Concurrent;
using AutomotoraSaaS.Core.Tenants;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Un DNS de mentira, con las respuestas que cada test necesita.
/// </summary>
/// <remarks>
/// Los tests no salen a la red. Un test que consulte el DNS de verdad falla el día que se
/// corre sin internet, y falla también el día que alguien deja de renovar el dominio que
/// usaba de ejemplo: en los dos casos la falla no dice nada sobre el código.
/// <para>
/// Un dominio del que nadie declaró respuesta devuelve la lista vacía, que es exactamente
/// lo que devuelve el resolvedor real cuando el dominio no existe.
/// </para>
/// </remarks>
public sealed class DnsDePrueba : IResolvedorDeDns
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _respuestas =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Declara a qué direcciones resuelve un dominio.</summary>
    public void Responder(string dominio, IReadOnlyList<string> direcciones)
        => _respuestas[dominio] = direcciones;

    public Task<IReadOnlyList<string>> DireccionesDeAsync(
        string dominio,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_respuestas.GetValueOrDefault(dominio, []));
}
