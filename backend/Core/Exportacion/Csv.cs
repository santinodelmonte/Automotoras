using System.Globalization;
using System.Text;

namespace AutomotoraSaaS.Core.Exportacion;

/// <summary>
/// Un CSV armado en memoria, pensado para abrirse en Excel o en Google Sheets.
/// </summary>
/// <remarks>
/// Coma como separador y números con punto decimal (cultura invariante): es lo que leen
/// bien las dos planillas y cualquier script. Las fechas van en ISO 8601.
/// <para>
/// Los textos que escribió alguien —la descripción de un auto, los filtros de una
/// búsqueda— se neutralizan si empiezan con un carácter de fórmula. Un CSV que se abre en
/// Excel ejecuta <c>=HYPERLINK(...)</c>, y la descripción la puede haber escrito
/// cualquiera del equipo.
/// </para>
/// </remarks>
public sealed class Csv
{
    private readonly StringBuilder _texto = new();

    public Csv(params string[] encabezados)
    {
        Fila(encabezados.Cast<object?>().ToArray());
    }

    public Csv Fila(params object?[] valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        _texto.AppendJoin(',', valores.Select(Celda));
        _texto.Append("\r\n");
        return this;
    }

    /// <summary>UTF-8 con BOM: sin el BOM, Excel abre los acentos rotos.</summary>
    public byte[] ABytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_texto.ToString())).ToArray();

    public override string ToString() => _texto.ToString();

    private static string Celda(object? valor)
    {
        var texto = valor switch
        {
            null => string.Empty,
            string s => Neutralizar(s),
            DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly dia => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool b => b ? "si" : "no",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Neutralizar(valor.ToString() ?? string.Empty),
        };

        return texto.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{texto.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : texto;
    }

    private static string Neutralizar(string texto)
        => texto.Length > 0 && texto[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + texto : texto;
}
