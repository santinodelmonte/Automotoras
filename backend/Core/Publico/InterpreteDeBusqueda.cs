using System.Globalization;
using System.Text;

namespace AutomotoraSaaS.Core.Publico;

/// <summary>Una marca o modelo del catálogo global, con lo justo para reconocerlo en un texto.</summary>
public sealed record EntradaDelCatalogo(int MarcaId, string Marca, int? ModeloId, string? Modelo);

/// <summary>Lo que se entendió de un texto: marca, modelo, las dos o ninguna.</summary>
public sealed record BusquedaInterpretada(int? MarcaId, int? ModeloId);

/// <summary>
/// Traduce lo que alguien escribió en el buscador a una marca y un modelo del catálogo.
/// </summary>
/// <remarks>
/// Es lo que permite que "toyota corola" o "hilux" terminen en el mismo grupo del reporte
/// que una búsqueda hecha con los filtros. Mira el catálogo <b>global</b> y no el stock de
/// la automotora: lo que más importa registrar es justamente lo que la automotora no tiene.
/// <para>
/// Es conservador a propósito. Ante la duda —un modelo que existe en dos marcas, un texto
/// que no se parece a nada— no adivina: devuelve menos precisión o nada. Una búsqueda mal
/// atribuida ensucia un reporte que se usa para decidir qué comprar.
/// </para>
/// </remarks>
public static class InterpreteDeBusqueda
{
    /// <summary>Largo mínimo para aceptar un prefijo: "toy" es Toyota, "to" no es nada.</summary>
    public const int LargoMinimoDePrefijo = 3;

    public static BusquedaInterpretada Interpretar(string? texto, IReadOnlyCollection<EntradaDelCatalogo> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        var normalizado = Normalizar(texto);

        if (normalizado.Length == 0)
        {
            return new BusquedaInterpretada(null, null);
        }

        var palabras = normalizado.Split(' ');
        var marcas = catalogo
            .GroupBy(e => e.MarcaId)
            .Select(g => (Id: g.Key, Nombre: Normalizar(g.First().Marca)))
            .ToList();

        var marca = BuscarMarca(normalizado, palabras, marcas);

        var modelos = catalogo
            .Where(e => e.ModeloId is not null && (marca is null || e.MarcaId == marca))
            .Select(e => (e.MarcaId, Id: e.ModeloId!.Value, Nombre: Normalizar(e.Modelo)))
            .ToList();

        // El modelo nombrado entero, y de los que entran, el más largo: en "corolla cross"
        // entran Corolla y Corolla Cross, y el que buscaba es el segundo.
        var completos = modelos
            .Where(m => ContieneFrase(normalizado, m.Nombre))
            .GroupBy(m => m.Nombre.Length)
            .OrderByDescending(g => g.Key)
            .FirstOrDefault()
            ?.ToList();

        if (completos is { Count: 1 })
        {
            return new BusquedaInterpretada(completos[0].MarcaId, completos[0].Id);
        }

        // Con la marca ya clara, la última palabra puede ser el modelo a medio escribir.
        if (marca is not null && completos is null && palabras[^1].Length >= LargoMinimoDePrefijo)
        {
            var prefijo = palabras[^1];
            var candidatos = modelos.Where(m => m.Nombre.StartsWith(prefijo, StringComparison.Ordinal)).ToList();

            if (candidatos.Count == 1 && !marcas.Any(ma => ma.Nombre.StartsWith(prefijo, StringComparison.Ordinal)))
            {
                return new BusquedaInterpretada(marca, candidatos[0].Id);
            }
        }

        // Varios modelos con el mismo nombre en marcas distintas ("2008"): sin la marca no
        // se elige ninguno.
        return new BusquedaInterpretada(marca, null);
    }

    private static int? BuscarMarca(string texto, string[] palabras, List<(int Id, string Nombre)> marcas)
    {
        var completa = marcas
            .Where(m => ContieneFrase(texto, m.Nombre))
            .OrderByDescending(m => m.Nombre.Length)
            .ToList();

        if (completa.Count > 0)
        {
            return completa[0].Id;
        }

        // "toyo", "volks": una palabra que es el comienzo de una sola marca.
        foreach (var palabra in palabras.Where(p => p.Length >= LargoMinimoDePrefijo))
        {
            var candidatas = marcas.Where(m => m.Nombre.StartsWith(palabra, StringComparison.Ordinal)).ToList();

            if (candidatas.Count == 1)
            {
                return candidatas[0].Id;
            }
        }

        return null;
    }

    /// <summary>La frase aparece entera, como palabras sueltas: "ka" está en "ford ka", no en "kangoo".</summary>
    private static bool ContieneFrase(string texto, string frase)
        => frase.Length > 0 && $" {texto} ".Contains($" {frase} ", StringComparison.Ordinal);

    /// <summary>
    /// Minúsculas, sin tildes y con todo lo que no es letra o número convertido en un solo
    /// espacio. "Citroën C-Elysée" y "citroen c elysee" quedan iguales.
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        var espacioPendiente = false;

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                if (espacioPendiente && resultado.Length > 0)
                {
                    resultado.Append(' ');
                }

                espacioPendiente = false;
                resultado.Append(char.ToLowerInvariant(c));
            }
            else
            {
                espacioPendiente = true;
            }
        }

        return resultado.ToString();
    }
}
