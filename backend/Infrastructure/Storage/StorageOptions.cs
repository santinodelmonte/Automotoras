namespace AutomotoraSaaS.Infrastructure.Storage;

/// <summary>
/// Configuración del storage de imágenes. Sección <c>Storage</c> de <c>appsettings</c>.
/// </summary>
public sealed class StorageOptions
{
    public const string Seccion = "Storage";

    public const string ProveedorLocal = "Local";
    public const string ProveedorR2 = "R2";

    /// <summary><c>Local</c> en desarrollo, <c>R2</c> en producción.</summary>
    public string Provider { get; set; } = ProveedorLocal;

    /// <summary>Solo con <c>Provider=Local</c>: carpeta de subidas, fuera del repo.</summary>
    public string? LocalRootPath { get; set; }

    /// <summary>URL pública desde la que se sirven las imágenes, sin barra final.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string? Bucket { get; set; }
    public string? Endpoint { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }

    public bool EsLocal => string.Equals(Provider, ProveedorLocal, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Falla temprano y diciendo qué falta. Descubrir que el bucket no estaba configurado
    /// cuando un vendedor sube la primera foto es tarde.
    /// </summary>
    public void Validar()
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(PublicBaseUrl))
        {
            faltantes.Add("Storage:PublicBaseUrl");
        }

        if (EsLocal)
        {
            if (string.IsNullOrWhiteSpace(LocalRootPath))
            {
                faltantes.Add("Storage:LocalRootPath");
            }
        }
        else if (string.Equals(Provider, ProveedorR2, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Bucket)) faltantes.Add("Storage:Bucket");
            if (string.IsNullOrWhiteSpace(Endpoint)) faltantes.Add("Storage:Endpoint");
            if (string.IsNullOrWhiteSpace(AccessKeyId)) faltantes.Add("Storage:AccessKeyId");
            if (string.IsNullOrWhiteSpace(SecretAccessKey)) faltantes.Add("Storage:SecretAccessKey");
        }
        else
        {
            faltantes.Add($"Storage:Provider (\"{Provider}\" no existe; usá \"{ProveedorLocal}\" o \"{ProveedorR2}\")");
        }

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuración de storage incompleta: {string.Join(", ", faltantes)}. " +
                "La forma esperada está en appsettings.Example.json.");
        }
    }

    /// <summary>La base pública sin barra final, para poder concatenar sin duplicarla.</summary>
    public string BaseNormalizada() => PublicBaseUrl.TrimEnd('/');
}
