using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AutomotoraSaaS.Core.Storage;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Infrastructure.Storage;

/// <summary>
/// Guarda las imágenes en Cloudflare R2, que habla el protocolo de S3.
/// </summary>
/// <remarks>
/// Es el proveedor de producción. R2 no cobra egreso, que para un catálogo de fotos que
/// se sirve a compradores es la diferencia entre una factura previsible y una que crece
/// con el tráfico.
/// <para>
/// El cliente se arma una sola vez y se comparte: <c>AmazonS3Client</c> mantiene su pool
/// de conexiones adentro, y crear uno por request es la forma clásica de agotar los
/// sockets del servidor.
/// </para>
/// </remarks>
public sealed class R2ImageStorage : IImageStorage, IDisposable
{
    private readonly StorageOptions _opciones;
    private readonly AmazonS3Client _cliente;

    public R2ImageStorage(IOptions<StorageOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _opciones.Validar();

        _cliente = new AmazonS3Client(
            new BasicAWSCredentials(_opciones.AccessKeyId, _opciones.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = _opciones.Endpoint,

                // R2 no soporta buckets como subdominio del endpoint.
                ForcePathStyle = true,

                // R2 ignora la región pero el SDK exige una firmada.
                AuthenticationRegion = "auto",
            });
    }

    public async Task<ImagenSubida> GuardarAsync(
        Stream contenido,
        string carpeta,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var clave = $"{carpeta.Trim('/')}/{Guid.NewGuid():N}{extension}";

        await _cliente.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _opciones.Bucket,
                Key = clave,
                InputStream = contenido,
                ContentType = contentType,

                // Un año, inmutable: la clave lleva un GUID, así que el contenido de una
                // URL no cambia nunca. Es lo que hace que la segunda visita al sitio no
                // vuelva a bajar ni una foto.
                Headers = { CacheControl = "public, max-age=31536000, immutable" },
            },
            cancellationToken).ConfigureAwait(false);

        return new ImagenSubida(clave, $"{_opciones.BaseNormalizada()}/{clave}");
    }

    public async Task BorrarAsync(string clave, CancellationToken cancellationToken = default)
    {
        if (!GeneradorDeClaves.EsSegura(clave))
        {
            return;
        }

        // S3 responde igual borre o no borre: la operación ya es idempotente.
        await _cliente.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = _opciones.Bucket, Key = clave },
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _cliente.Dispose();
}
