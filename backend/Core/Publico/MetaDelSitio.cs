using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Publico;

/// <summary>
/// Las etiquetas del <c>&lt;head&gt;</c> de una página del sitio público.
/// </summary>
/// <param name="Titulo">El <c>&lt;title&gt;</c> y el <c>og:title</c>.</param>
/// <param name="Descripcion">La descripción para buscadores y para la vista previa.</param>
/// <param name="Imagen">URL absoluta de la imagen de la vista previa, si hay.</param>
/// <param name="Url">URL canónica de la página.</param>
/// <param name="SitioNombre">El nombre de la automotora, para <c>og:site_name</c>.</param>
/// <param name="Color">Color de la marca, para la barra del navegador en el celular.</param>
/// <param name="Icono">El logo de la automotora como ícono de la pestaña.</param>
/// <param name="Indexable">
/// <c>false</c> para lo que no tiene que aparecer en Google: el panel, una automotora que no
/// existe, un vehículo que ya no está publicado.
/// </param>
public sealed record MetaDelSitio(
    string Titulo,
    string? Descripcion = null,
    string? Imagen = null,
    string? Url = null,
    string? SitioNombre = null,
    string? Color = null,
    string? Icono = null,
    bool Indexable = true,
    string Tipo = "website");

/// <summary>
/// Arma las etiquetas de cada página y las mete en el <c>index.html</c> del frontend.
/// </summary>
/// <remarks>
/// El sitio es una SPA: lo que ve el navegador lo pinta JavaScript, pero WhatsApp, Facebook
/// y buena parte de los buscadores leen solo el HTML que llega del servidor. Sin esto,
/// cada vehículo compartido por WhatsApp —que es por donde se venden los autos usados en
/// Uruguay— salía como "Automotora SaaS", sin foto, para todas las automotoras.
/// </remarks>
public static partial class PaginaDelSitio
{
    private static readonly CultureInfo Uruguay = CultureInfo.GetCultureInfo("es-UY");

    public static MetaDelSitio DeVehiculo(
        string automotora,
        string marca,
        string modelo,
        string? version,
        int anio,
        int kilometraje,
        Combustible combustible,
        Transmision transmision,
        decimal precio,
        Moneda moneda,
        string? imagen,
        string url)
    {
        var titulo = $"{marca} {modelo} {anio}";
        var nombre = version is { Length: > 0 } ? $"{marca} {modelo} {version}" : $"{marca} {modelo}";

        var descripcion =
            $"{nombre} {anio}, {kilometraje.ToString("N0", Uruguay)} km, {Etiqueta(combustible)}, " +
            $"{Etiqueta(transmision)}. {Precio(precio, moneda)}. Consultalo en {automotora}.";

        return new MetaDelSitio(
            $"{titulo} — {automotora}",
            descripcion,
            imagen,
            url,
            automotora,
            Tipo: "product");
    }

    public static MetaDelSitio DeAutomotora(string automotora, string? direccion, int publicados, string? imagen, string url)
    {
        var donde = direccion is { Length: > 0 } ? $" en {direccion}" : string.Empty;
        var cuantos = publicados switch
        {
            0 => "Mirá el stock",
            1 => "Mirá el vehículo disponible",
            _ => $"Mirá los {publicados.ToString("N0", Uruguay)} vehículos disponibles",
        };

        return new MetaDelSitio(
            $"{automotora} — Autos en venta",
            $"{cuantos} de {automotora}{donde}. Filtrá por marca, año y precio, y consultá por WhatsApp.",
            imagen,
            url,
            automotora);
    }

    public static string Precio(decimal precio, Moneda moneda)
        => $"{(moneda == Moneda.Usd ? "US$" : "$")} {precio.ToString("N0", Uruguay)}";

    public static string Etiqueta(Combustible combustible) => combustible switch
    {
        Combustible.Diesel => "diésel",
        Combustible.Hibrido => "híbrido",
        Combustible.Electrico => "eléctrico",
        Combustible.Gnc => "GNC",
        _ => "nafta",
    };

    public static string Etiqueta(Transmision transmision)
        => transmision == Transmision.Automatica ? "automático" : "manual";

    /// <summary>
    /// Reemplaza el <c>&lt;title&gt;</c> de la plantilla por las etiquetas de la página y,
    /// si la automotora tiene logo, el ícono.
    /// </summary>
    /// <remarks>
    /// Todo valor pasa por <see cref="WebUtility.HtmlEncode(string)"/>: el nombre de la
    /// automotora y la descripción del vehículo los escribe el cliente, y sin escapar, un
    /// <c>"&gt;&lt;script&gt;</c> en el nombre se ejecutaría en el sitio de cada visitante.
    /// </remarks>
    public static string Renderizar(string plantilla, MetaDelSitio meta)
    {
        ArgumentNullException.ThrowIfNull(plantilla);
        ArgumentNullException.ThrowIfNull(meta);

        var html = new StringBuilder();
        html.Append("<title>").Append(Cod(meta.Titulo)).Append("</title>");

        Etiqueta(html, "name", "robots", meta.Indexable ? "index, follow" : "noindex");
        Etiqueta(html, "name", "description", meta.Descripcion);
        Etiqueta(html, "name", "theme-color", meta.Color);
        Etiqueta(html, "property", "og:type", meta.Tipo);
        Etiqueta(html, "property", "og:title", meta.Titulo);
        Etiqueta(html, "property", "og:description", meta.Descripcion);
        Etiqueta(html, "property", "og:image", meta.Imagen);
        Etiqueta(html, "property", "og:url", meta.Url);
        Etiqueta(html, "property", "og:site_name", meta.SitioNombre);
        Etiqueta(html, "property", "og:locale", "es_UY");
        Etiqueta(html, "name", "twitter:card", meta.Imagen is null ? "summary" : "summary_large_image");

        if (meta.Url is not null && meta.Indexable)
        {
            html.Append("\n    <link rel=\"canonical\" href=\"").Append(Cod(meta.Url)).Append("\" />");
        }

        var resultado = TituloDeLaPlantilla().Replace(plantilla, _ => html.ToString(), 1);

        if (meta.Icono is { Length: > 0 } icono)
        {
            resultado = IconoDeLaPlantilla().Replace(
                resultado, _ => $"<link rel=\"icon\" href=\"{Cod(icono)}\" />", 1);
        }

        return resultado;
    }

    private static void Etiqueta(StringBuilder html, string atributo, string nombre, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return;
        }

        html.Append("\n    <meta ").Append(atributo).Append("=\"").Append(nombre)
            .Append("\" content=\"").Append(Cod(valor)).Append("\" />");
    }

    private static string Cod(string valor) => WebUtility.HtmlEncode(valor);

    [GeneratedRegex("<title>.*?</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TituloDeLaPlantilla();

    [GeneratedRegex("<link rel=\"icon\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex IconoDeLaPlantilla();
}
