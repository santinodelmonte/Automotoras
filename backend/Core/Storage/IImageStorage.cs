namespace AutomotoraSaaS.Core.Storage;

/// <summary>
/// Una imagen ya guardada en el storage.
/// </summary>
/// <param name="Clave">
/// Ruta interna dentro del bucket. Es lo que hay que guardar para poder borrarla después:
/// la URL pública puede cambiar si cambia el dominio del CDN, la clave no.
/// </param>
/// <param name="Url">URL pública desde la que se sirve.</param>
public sealed record ImagenSubida(string Clave, string Url);

/// <summary>
/// Guarda los binarios del producto. Detrás hay object storage S3-compatible en
/// producción y el disco local en desarrollo.
/// </summary>
/// <remarks>
/// La abstracción no es por gusto arquitectónico: el deploy inicial es shared hosting
/// Windows/IIS, donde escribir en el disco del servidor está prohibido —el app pool
/// recicla, el disco no es persistente y no escala a más de una instancia—. Todo binario
/// va a object storage, y la implementación local existe únicamente para no obligar a
/// tener credenciales de Cloudflare para levantar el proyecto.
/// </remarks>
public interface IImageStorage
{
    /// <summary>
    /// Guarda la imagen y devuelve dónde quedó.
    /// </summary>
    /// <param name="contenido">Stream de la imagen. No se cierra: lo cierra quien lo abrió.</param>
    /// <param name="carpeta">Prefijo lógico, por ejemplo <c>tenants/3/vehiculos/12</c>.</param>
    /// <param name="extension">Extensión con punto, ya validada contra el contenido real.</param>
    Task<ImagenSubida> GuardarAsync(
        Stream contenido,
        string carpeta,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra la imagen. Es idempotente: borrar algo que ya no está no es un error.
    /// </summary>
    Task BorrarAsync(string clave, CancellationToken cancellationToken = default);
}
