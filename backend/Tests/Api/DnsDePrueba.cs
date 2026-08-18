using System.Collections.Concurrent;
using AutomotoraSaaS.Core.Dominios;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// DNS en memoria.
/// </summary>
/// <remarks>
/// Los tests no salen a internet. Sin esto la verificación de dominios necesitaría un
/// dominio real con un TXT real, y la suite pasaría a depender de que nadie toque una zona
/// DNS que no está en este repositorio.
/// </remarks>
public sealed class DnsDePrueba : IConsultaDns
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _txt =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _caidos = new(StringComparer.OrdinalIgnoreCase);

    public void Publicar(string nombre, params string[] valores) => _txt[nombre] = valores;

    public void Borrar(string nombre) => _txt.TryRemove(nombre, out _);

    /// <summary>Hace que consultar ese nombre falle, como un resolver que no contesta.</summary>
    public void HacerFallar(string nombre) => _caidos.Add(nombre);

    public Task<IReadOnlyList<string>> TxtAsync(string nombre, CancellationToken cancellationToken = default)
    {
        if (_caidos.Contains(nombre))
        {
            throw new ConsultaDnsFallidaException($"El DNS de prueba está configurado para fallar en {nombre}.");
        }

        return Task.FromResult(_txt.TryGetValue(nombre, out var valores) ? valores : []);
    }
}
