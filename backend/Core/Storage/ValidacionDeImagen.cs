namespace AutomotoraSaaS.Core.Storage;

/// <summary>Resultado de mirar un archivo antes de aceptarlo.</summary>
public sealed record ResultadoDeValidacion(bool EsValida, string? Error, string Extension, string ContentType)
{
    public static ResultadoDeValidacion Rechazada(string error) => new(false, error, string.Empty, string.Empty);

    public static ResultadoDeValidacion Aceptada(string extension, string contentType)
        => new(true, null, extension, contentType);
}

/// <summary>
/// Qué se acepta como foto de un vehículo.
/// </summary>
/// <remarks>
/// El <c>Content-Type</c> lo manda el cliente, así que no prueba nada: se mira además la
/// firma de los primeros bytes. Sin eso, el endpoint de fotos es una forma de dejar
/// cualquier archivo en el bucket público de la automotora.
/// <para>
/// El tope de tamaño es chico a propósito. Las fotos se achican en el navegador antes de
/// subirlas: mandar el original de 12 MP de un celular por 4G es lo que hace que cargar
/// diez fotos termine en timeout, y es exactamente el criterio de aceptación que hay que
/// cumplir.
/// </para>
/// </remarks>
public static class ValidacionDeImagen
{
    public const int TamanoMaximoEnBytes = 5 * 1024 * 1024;
    public const int FotosMaximasPorVehiculo = 20;

    private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] FirmaRiff = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] FirmaWebp = [0x57, 0x45, 0x42, 0x50]; // "WEBP", en el byte 8

    /// <summary>
    /// Mira la firma real del archivo. <paramref name="encabezado"/> tiene que traer al
    /// menos los primeros 12 bytes.
    /// </summary>
    public static ResultadoDeValidacion Validar(ReadOnlySpan<byte> encabezado, long tamanoEnBytes)
    {
        if (tamanoEnBytes <= 0)
        {
            return ResultadoDeValidacion.Rechazada("El archivo está vacío.");
        }

        if (tamanoEnBytes > TamanoMaximoEnBytes)
        {
            return ResultadoDeValidacion.Rechazada(
                $"La foto pesa más de {TamanoMaximoEnBytes / (1024 * 1024)} MB. Achicala antes de subirla.");
        }

        if (Empieza(encabezado, FirmaJpeg))
        {
            return ResultadoDeValidacion.Aceptada(".jpg", "image/jpeg");
        }

        if (Empieza(encabezado, FirmaPng))
        {
            return ResultadoDeValidacion.Aceptada(".png", "image/png");
        }

        if (Empieza(encabezado, FirmaRiff)
            && encabezado.Length >= 12
            && encabezado[8..12].SequenceEqual(FirmaWebp))
        {
            return ResultadoDeValidacion.Aceptada(".webp", "image/webp");
        }

        return ResultadoDeValidacion.Rechazada("Solo se aceptan imágenes JPEG, PNG o WebP.");
    }

    private static bool Empieza(ReadOnlySpan<byte> contenido, ReadOnlySpan<byte> firma)
        => contenido.Length >= firma.Length && contenido[..firma.Length].SequenceEqual(firma);
}
