using System.Collections.Concurrent;
using AutomotoraSaaS.Core.Storage;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Storage de imágenes en memoria.
/// </summary>
/// <remarks>
/// Los tests no escriben en el disco ni salen a la red. Además deja ver qué se guardó y
/// qué se borró, que es justamente lo que hay que verificar cuando se reemplaza el logo o
/// se borra un vehículo con fotos.
/// </remarks>
public sealed class AlmacenamientoDePrueba : IImageStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _guardadas = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Claves => _guardadas.Keys.ToList();

    public List<string> Borradas { get; } = [];

    public async Task<ImagenSubida> GuardarAsync(
        Stream contenido,
        string carpeta,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, cancellationToken);

        var clave = $"{carpeta.Trim('/')}/{Guid.NewGuid():N}{extension}";
        _guardadas[clave] = memoria.ToArray();

        return new ImagenSubida(clave, $"https://cdn.de-prueba.uy/{clave}");
    }

    public Task BorrarAsync(string clave, CancellationToken cancellationToken = default)
    {
        Borradas.Add(clave);
        _guardadas.TryRemove(clave, out _);

        return Task.CompletedTask;
    }
}
