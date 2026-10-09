using System.Globalization;
using System.Text;
using AutomotoraSaaS.Core.Exportacion;
using FluentValidation;

namespace AutomotoraSaaS.Core.Vehiculos;

/// <summary>Un problema en una fila del CSV. <c>Fila</c> es la línea del archivo, contando el encabezado.</summary>
public sealed record ErrorDeImportacionDto(int Fila, string? Columna, string Mensaje);

/// <summary>
/// Resultado de procesar un CSV de stock.
/// </summary>
/// <param name="Filas">Filas de datos leídas, sin el encabezado.</param>
/// <param name="Validas">Filas sin ningún error.</param>
/// <param name="Importados">Vehículos efectivamente creados. Cero si fue solo una validación o si hubo errores.</param>
public sealed record ResultadoDeImportacionDto(
    int Filas,
    int Validas,
    IReadOnlyList<ErrorDeImportacionDto> Errores,
    int Importados);

/// <summary>Lo que el importador necesita saber del catálogo, por nombre normalizado.</summary>
public sealed record ModeloDelCatalogo(int Id, IReadOnlyDictionary<string, int> Versiones);

/// <summary>
/// Carga masiva de stock por CSV: lectura, validación fila por fila y armado de los altas.
/// </summary>
/// <remarks>
/// La carga inicial es parte de la implementación que se cobra, y hacerla a mano no escala
/// ni a tres clientes. La regla es todo o nada: si una sola fila tiene un error no se
/// escribe ninguna, y el informe dice cuál y por qué. Un stock cargado a medias es peor
/// que uno sin cargar, porque nadie sabe qué falta.
/// <para>
/// Cada fila pasa por el mismo validador que el formulario del panel: un auto cargado por
/// CSV no puede tener reglas distintas que uno cargado a mano.
/// </para>
/// </remarks>
public static class ImportacionDeStock
{
    public const int MaximoDeFilas = 1000;
    public const long TamanioMaximo = 2 * 1024 * 1024;

    public static readonly string[] Columnas =
    [
        "marca", "modelo", "version", "anio", "kilometraje", "combustible", "transmision",
        "color", "puertas", "motor", "precio", "moneda", "descripcion", "destacado", "precio_costo",
    ];

    private static readonly string[] Obligatorias =
        ["marca", "modelo", "anio", "kilometraje", "combustible", "transmision", "precio", "moneda"];

    /// <summary>La plantilla para descargar, con los encabezados y un ejemplo.</summary>
    public static Csv Plantilla()
        => new Csv(Columnas).Fila(
            "Toyota", "Corolla", "", 2019, 85000, "Nafta", "Automatica", "Gris", 4, "1.8",
            18500, "Usd", "Único dueño, service oficial.", "no", "");

    /// <summary>
    /// Lee el archivo y valida cada fila. Devuelve los altas listos si no hubo ningún error.
    /// </summary>
    /// <param name="catalogo">Marca normalizada → modelo normalizado → modelo.</param>
    /// <param name="lugaresLibres">Cuántos vehículos más se pueden publicar según el plan. Nulo es sin límite.</param>
    public static (ResultadoDeImportacionDto Resultado, IReadOnlyList<GuardarVehiculoRequest> Altas) Analizar(
        string contenido,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModeloDelCatalogo>> catalogo,
        IValidator<GuardarVehiculoRequest> validador,
        int? lugaresLibres)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(validador);

        var errores = new List<ErrorDeImportacionDto>();
        var altas = new List<GuardarVehiculoRequest>();

        var filas = LeerCsv(contenido);

        if (filas.Count == 0)
        {
            return (Vacio("El archivo está vacío."), altas);
        }

        var encabezado = filas[0].Select(Normalizar).ToArray();
        var faltantes = Obligatorias.Where(c => !encabezado.Contains(c)).ToList();

        if (faltantes.Count > 0)
        {
            return (Vacio($"Faltan columnas obligatorias: {string.Join(", ", faltantes)}. Usá la plantilla."), altas);
        }

        var datos = filas.Skip(1).Where(f => f.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();

        if (datos.Count > MaximoDeFilas)
        {
            return (Vacio($"El archivo tiene {datos.Count} filas y el máximo por carga es {MaximoDeFilas}."), altas);
        }

        var validas = 0;

        for (var i = 0; i < datos.Count; i++)
        {
            // La línea del archivo, como la ve quien lo abre en una planilla: el encabezado es la 1.
            var linea = i + 2;
            var celdas = datos[i];
            string Celda(string columna)
            {
                var indice = Array.IndexOf(encabezado, columna);
                return indice >= 0 && indice < celdas.Length ? celdas[indice].Trim() : string.Empty;
            }

            var erroresDeLaFila = new List<ErrorDeImportacionDto>();
            void Error(string? columna, string mensaje) => erroresDeLaFila.Add(new(linea, columna, mensaje));

            var modeloId = 0;
            int? versionId = null;

            if (!catalogo.TryGetValue(Normalizar(Celda("marca")), out var modelos))
            {
                Error("marca", $"La marca '{Celda("marca")}' no está en el catálogo.");
            }
            else if (!modelos.TryGetValue(Normalizar(Celda("modelo")), out var modelo))
            {
                Error("modelo", $"El modelo '{Celda("modelo")}' no está en el catálogo de {Celda("marca")}.");
            }
            else
            {
                modeloId = modelo.Id;

                if (Celda("version") is { Length: > 0 } version)
                {
                    if (modelo.Versiones.TryGetValue(Normalizar(version), out var id))
                    {
                        versionId = id;
                    }
                    else
                    {
                        Error("version", $"La versión '{version}' no está en el catálogo de {Celda("modelo")}.");
                    }
                }
            }

            var anio = Entero(Celda("anio"), "anio", obligatorio: true, Error);
            var kilometraje = Entero(Celda("kilometraje"), "kilometraje", obligatorio: true, Error);
            var puertas = Entero(Celda("puertas"), "puertas", obligatorio: false, Error);
            var precio = Decimal(Celda("precio"), "precio", obligatorio: true, Error);
            var precioCosto = Decimal(Celda("precio_costo"), "precio_costo", obligatorio: false, Error);
            var destacado = Booleano(Celda("destacado"), Error);

            var request = new GuardarVehiculoRequest(
                modeloId,
                versionId,
                anio ?? 0,
                kilometraje ?? 0,
                SinAcentos(Celda("combustible")),
                SinAcentos(Celda("transmision")),
                Opcional(Celda("color")),
                puertas,
                Opcional(Celda("motor")),
                precio ?? 0m,
                SinAcentos(Celda("moneda")),
                Opcional(Celda("descripcion")),
                destacado,
                precioCosto,
                FechaPublicacion: null);

            // El validador del formulario, salvo lo que ya quedó reportado acá con un
            // mensaje más preciso (marca y modelo inexistentes, números ilegibles).
            if (erroresDeLaFila.Count == 0)
            {
                foreach (var falla in validador.Validate(request).Errors)
                {
                    Error(Columna(falla.PropertyName), falla.ErrorMessage);
                }
            }

            if (erroresDeLaFila.Count == 0)
            {
                validas++;
                altas.Add(request);
            }

            errores.AddRange(erroresDeLaFila);
        }

        if (lugaresLibres is { } libres && validas > libres)
        {
            errores.Add(new ErrorDeImportacionDto(
                0,
                null,
                $"El plan admite {Math.Max(libres, 0)} vehículos publicados más y el archivo trae {validas}. " +
                "Cargá menos unidades o pasá a un plan más grande."));
        }

        return (new ResultadoDeImportacionDto(datos.Count, validas, errores, Importados: 0), altas);
    }

    /// <summary>
    /// Lee un CSV con comillas a la RFC 4180. Acepta coma o punto y coma: Excel en
    /// castellano guarda con punto y coma, y pedirle a un vendedor que cambie la
    /// configuración regional para cargar autos no es razonable.
    /// </summary>
    public static List<string[]> LeerCsv(string contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var texto = contenido.TrimStart('﻿');
        var primeraLinea = texto.Split('\n', 2)[0];
        var separador = primeraLinea.Count(c => c == ';') > primeraLinea.Count(c => c == ',') ? ';' : ',';

        var filas = new List<string[]>();
        var fila = new List<string>();
        var celda = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];

            if (entreComillas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    celda.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    entreComillas = false;
                }
                else
                {
                    celda.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                entreComillas = true;
            }
            else if (c == separador)
            {
                fila.Add(celda.ToString());
                celda.Clear();
            }
            else if (c == '\n')
            {
                fila.Add(celda.ToString());
                celda.Clear();
                filas.Add(fila.ToArray());
                fila.Clear();
            }
            else if (c != '\r')
            {
                celda.Append(c);
            }
        }

        if (celda.Length > 0 || fila.Count > 0)
        {
            fila.Add(celda.ToString());
            filas.Add(fila.ToArray());
        }

        return filas;
    }

    /// <summary>Para comparar nombres: minúsculas, sin acentos y sin espacios de más.</summary>
    public static string Normalizar(string texto)
        => string.Join(' ', SinAcentos(texto).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string SinAcentos(string texto)
    {
        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(c);
            }
        }

        return limpio.ToString().Normalize(NormalizationForm.FormC);
    }

    private static ResultadoDeImportacionDto Vacio(string mensaje)
        => new(0, 0, [new ErrorDeImportacionDto(1, null, mensaje)], 0);

    private static string? Opcional(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    private static int? Entero(string valor, string columna, bool obligatorio, Action<string?, string> error)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            if (obligatorio)
            {
                error(columna, $"Falta {columna}.");
            }

            return null;
        }

        // Los miles con punto ("85.000") son lo más común en una planilla uruguaya.
        var limpio = valor.Replace(".", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);

        if (int.TryParse(limpio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
        {
            return numero;
        }

        error(columna, $"'{valor}' no es un número entero.");
        return null;
    }

    private static decimal? Decimal(string valor, string columna, bool obligatorio, Action<string?, string> error)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            if (obligatorio)
            {
                error(columna, $"Falta {columna}.");
            }

            return null;
        }

        // Acepta "18500", "18.500", "18500.50" y "18.500,50": lo que esté después de la
        // última coma son decimales; los puntos antes son miles. Un solo punto con dos
        // dígitos detrás también son decimales.
        var limpio = valor.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (limpio.Contains(',', StringComparison.Ordinal))
        {
            limpio = limpio.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else if (limpio.Count(c => c == '.') > 1 || (limpio.LastIndexOf('.') is var punto and >= 0 && limpio.Length - punto - 1 == 3))
        {
            limpio = limpio.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        if (decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero))
        {
            return numero;
        }

        error(columna, $"'{valor}' no es un importe válido.");
        return null;
    }

    private static bool Booleano(string valor, Action<string?, string> error)
    {
        switch (Normalizar(valor))
        {
            case "":
            case "no":
            case "n":
            case "0":
            case "false":
                return false;
            case "si":
            case "s":
            case "1":
            case "true":
            case "x":
                return true;
            default:
                error("destacado", $"'{valor}' no es sí o no.");
                return false;
        }
    }

    private static string? Columna(string propiedad) => propiedad switch
    {
        nameof(GuardarVehiculoRequest.Anio) => "anio",
        nameof(GuardarVehiculoRequest.Kilometraje) => "kilometraje",
        nameof(GuardarVehiculoRequest.Combustible) => "combustible",
        nameof(GuardarVehiculoRequest.Transmision) => "transmision",
        nameof(GuardarVehiculoRequest.Color) => "color",
        nameof(GuardarVehiculoRequest.Puertas) => "puertas",
        nameof(GuardarVehiculoRequest.Motor) => "motor",
        nameof(GuardarVehiculoRequest.Precio) => "precio",
        nameof(GuardarVehiculoRequest.Moneda) => "moneda",
        nameof(GuardarVehiculoRequest.Descripcion) => "descripcion",
        nameof(GuardarVehiculoRequest.PrecioCosto) => "precio_costo",
        _ => null,
    };
}
