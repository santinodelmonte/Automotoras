using AutomotoraSaaS.Core.Storage;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Infrastructure.Storage;

/// <summary>
/// Guarda las imágenes en una carpeta del disco. <b>Solo para desarrollo.</b>
/// </summary>
/// <remarks>
/// En producción no se usa nunca: el deploy es shared hosting Windows/IIS, donde el app
/// pool recicla, el disco no es persistente y no hay una sola instancia garantizada.
/// Existe para que levantar el proyecto no requiera credenciales de Cloudflare.
/// <para>
/// La carpeta va fuera del repositorio, y la API la sirve como archivos estáticos solo
/// cuando el proveedor es este.
/// </para>
/// </remarks>
public sealed class LocalImageStorage : IImageStorage
{
    private readonly StorageOptions _opciones;

    public LocalImageStorage(IOptions<StorageOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _opciones.Validar();
    }

    /// <summary>Carpeta raíz de las subidas. La API la sirve como estáticos en desarrollo.</summary>
    public string Raiz => _opciones.LocalRootPath!;

    public async Task<ImagenSubida> GuardarAsync(
        Stream contenido,
        string carpeta,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var clave = $"{carpeta.Trim('/')}/{Guid.NewGuid():N}{extension}";
        var destino = RutaFisica(clave);

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        await using (var archivo = File.Create(destino))
        {
            await contenido.CopyToAsync(archivo, cancellationToken).ConfigureAwait(false);
        }

        return new ImagenSubida(clave, $"{_opciones.BaseNormalizada()}/{clave}");
    }

    public Task BorrarAsync(string clave, CancellationToken cancellationToken = default)
    {
        if (!GeneradorDeClaves.EsSegura(clave))
        {
            return Task.CompletedTask;
        }

        var destino = RutaFisica(clave);

        // Idempotente: borrar algo que ya no está no es un error para quien llama.
        if (File.Exists(destino))
        {
            File.Delete(destino);
        }

        return Task.CompletedTask;
    }

    private string RutaFisica(string clave)
    {
        var raiz = Path.GetFullPath(Raiz);
        var destino = Path.GetFullPath(Path.Combine(raiz, clave.Replace('/', Path.DirectorySeparatorChar)));

        // Cinturón y tirantes: la clave la genera este código, pero un destino que se
        // escape de la raíz no puede llegar a tocar el disco por ningún camino.
        if (!destino.StartsWith(raiz, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"La clave '{clave}' apunta fuera de la carpeta de subidas.");
        }

        return destino;
    }
}
