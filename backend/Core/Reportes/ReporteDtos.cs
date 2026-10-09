namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Lo que el reporte dice sobre una unidad, en una palabra.
/// </summary>
/// <remarks>
/// Es la traducción de dos números a una decisión. Un dueño no necesita que le muestren
/// que un auto tiene 180 vistas y 2 consultas: necesita que le digan que el precio está
/// alto. Los umbrales están acá, en un solo lugar, para que la regla se pueda discutir
/// leyendo en vez de deducirla de una consulta.
/// </remarks>
public enum SenalDeDemanda
{
    /// <summary>Todavía no hay tráfico suficiente para decir nada.</summary>
    SinDatos = 1,

    /// <summary>Se mira y se consulta en la proporción esperable.</summary>
    Saludable = 2,

    /// <summary>Mucha vista y poca consulta: el aviso interesa y el precio frena.</summary>
    PrecioAlto = 3,

    /// <summary>Casi no se mira. El problema no es el precio, es que no llega nadie.</summary>
    SinVisibilidad = 4,

    /// <summary>Lleva demasiado en góndola sin una sola consulta.</summary>
    Estancado = 5,
}

/// <summary>
/// Umbrales de la lectura de demanda. Están declarados, no escondidos en un <c>if</c>.
/// </summary>
public static class UmbralesDeDemanda
{
    /// <summary>Debajo de esto no hay muestra: cualquier ratio sería ruido.</summary>
    public const int VistasMinimasParaLeerElRatio = 20;

    /// <summary>
    /// Consultas cada cien vistas por debajo de las cuales el precio es el sospechoso.
    /// </summary>
    public const decimal RatioBajo = 3m;

    /// <summary>Vistas por debajo de las cuales el problema es de visibilidad.</summary>
    public const int VistasQueSonPocas = 5;

    /// <summary>Días en góndola sin una consulta a partir de los cuales la unidad está estancada.</summary>
    public const int DiasParaEstarEstancado = 60;

    /// <summary>
    /// Clasifica una unidad. El orden de las preguntas es el orden en que importan: sin
    /// tráfico no se puede hablar de precio, y una unidad que lleva meses sin que nadie
    /// pregunte es un problema aunque el ratio dé bien.
    /// </summary>
    public static SenalDeDemanda Clasificar(int vistas, int consultas, int diasEnGondola)
    {
        if (diasEnGondola >= DiasParaEstarEstancado && consultas == 0)
        {
            return SenalDeDemanda.Estancado;
        }

        if (vistas <= VistasQueSonPocas)
        {
            return vistas == 0 ? SenalDeDemanda.SinDatos : SenalDeDemanda.SinVisibilidad;
        }

        if (vistas < VistasMinimasParaLeerElRatio)
        {
            return SenalDeDemanda.SinDatos;
        }

        return ConsultasPorCienVistas(vistas, consultas) < RatioBajo
            ? SenalDeDemanda.PrecioAlto
            : SenalDeDemanda.Saludable;
    }

    /// <summary>
    /// Cuánto se aparta el precio publicado del de mercado, en porcentaje.
    /// </summary>
    /// <remarks>
    /// Positivo es más caro que el mercado. Se redondea a un decimal porque la precisión
    /// de la referencia —una mediana de unas pocas decenas de avisos— no da para más, y
    /// mostrar dos decimales sería fingir una exactitud que el dato no tiene.
    /// </remarks>
    public static decimal? DiferenciaContraElMercado(decimal precio, decimal? precioDeMercado)
        => precioDeMercado is null or 0m
            ? null
            : Math.Round((precio - precioDeMercado.Value) * 100m / precioDeMercado.Value, 1);

    /// <summary>
    /// Consultas cada cien vistas, con un decimal. Se expresa por cien y no como
    /// porcentaje entre cero y uno porque los valores reales viven entre el 1 % y el 10 %,
    /// y redondeados a entero serían todos iguales.
    /// </summary>
    public static decimal ConsultasPorCienVistas(int vistas, int consultas)
        => vistas == 0 ? 0m : Math.Round(consultas * 100m / vistas, 1);
}

/// <summary>La demanda de una unidad concreta en la ventana pedida.</summary>
/// <param name="Vistas">Vistas de ficha. El listado no cuenta: mirar una grilla no es mirar un auto.</param>
/// <param name="Consultas">Clics en WhatsApp y en teléfono, que es lo más cerca de una intención de compra que se puede medir.</param>
/// <param name="Senal">La lectura de esas dos cifras junto con el tiempo en góndola.</param>
/// <param name="PrecioDeMercado">
/// La mediana de lo que se pide por ese modelo y año, del último snapshot disponible.
/// <c>null</c> cuando todavía no hay referencia, que es lo normal hasta que el job de
/// precios lleve unos días corriendo.
/// </param>
/// <param name="DiferenciaConElMercado">
/// Cuánto porcentaje está por encima o por debajo del mercado. Va calculada y no como dos
/// números para que el panel la lea: la resta es trivial, pero hacerla en cada cliente es
/// la forma segura de que dos pantallas la redondeen distinto.
/// </param>
/// <param name="PrecioDeMercadoAl">
/// Día del snapshot. Viaja siempre: un precio de referencia sin fecha invita a comparar
/// contra un número de hace medio año como si fuera de hoy.
/// </param>
public sealed record DemandaDeVehiculoDto(
    int VehiculoId,
    string Marca,
    string Modelo,
    int Anio,
    string Estado,
    decimal Precio,
    string Moneda,
    string? FotoPortadaUrl,
    int DiasEnGondola,
    int Vistas,
    int Consultas,
    decimal ConsultasPorCienVistas,
    string Senal,
    decimal? PrecioDeMercado,
    decimal? DiferenciaConElMercado,
    DateOnly? PrecioDeMercadoAl);

/// <summary>
/// El encabezado del reporte: el estado general de la demanda en la ventana pedida.
/// </summary>
/// <param name="DiasEnGondolaMediana">
/// La mediana además del promedio, porque una sola unidad olvidada hace dos años corre el
/// promedio lo suficiente como para que deje de describir al stock.
/// </param>
public sealed record ResumenDeDemandaDto(
    int Dias,
    int VehiculosPublicados,
    int Vistas,
    int Consultas,
    decimal ConsultasPorCienVistas,
    int BusquedasSinResultado,
    int DiasEnGondolaPromedio,
    int DiasEnGondolaMediana,
    int VendidosEnElPeriodo,
    int? DiasHastaLaVentaPromedio);

/// <summary>El reporte completo: el resumen y la unidad por unidad.</summary>
public sealed record ReporteDeDemandaDto(
    ResumenDeDemandaDto Resumen,
    IReadOnlyList<DemandaDeVehiculoDto> Vehiculos);

/// <summary>
/// Un grupo de búsquedas que no encontraron nada, ya traducido a lo que se puede comprar.
/// </summary>
/// <remarks>
/// Se agrupa por marca, modelo y carrocería, que es la forma en que se sale a buscar una
/// unidad. Agrupar por el JSON completo daría grupos de a uno —cada visitante mueve el
/// rango de precio un poco— y el reporte no diría nada.
/// </remarks>
/// <param name="Veces">Cuántas búsquedas cayeron en este grupo.</param>
/// <param name="Sesiones">
/// Cuántas visitas distintas. Es la cifra que importa: veinte búsquedas de una sola
/// persona indecisa no son demanda, veinte de veinte personas sí.
/// </param>
/// <param name="PresupuestoTipico">
/// La mediana de los topes de precio pedidos, en la moneda más pedida del grupo. La
/// mediana y no el promedio: un solo visitante con un tope de cien mil correría el
/// promedio hasta un presupuesto que no tiene nadie.
/// </param>
public sealed record BusquedaSinResultadoDto(
    int? MarcaId,
    string? Marca,
    int? ModeloId,
    string? Modelo,
    string? Carroceria,
    int? AnioDesde,
    int? AnioHasta,
    string? Moneda,
    decimal? PrecioDesde,
    decimal? PrecioHasta,
    decimal? PresupuestoTipico,
    int Veces,
    int Sesiones,
    DateTime UltimaVez,
    string? Texto = null);

/// <summary>
/// Los filtros de una búsqueda, tal como se guardaron en la columna JSON.
/// </summary>
/// <remarks>
/// Se deserializa con las opciones web —camelCase e insensible a mayúsculas—, así que lee
/// igual lo que escribe el sitio público hoy y lo que dejó el seed.
/// </remarks>
public sealed class FiltrosDeBusquedaGuardados
{
    public int? MarcaId { get; set; }
    public int? ModeloId { get; set; }
    public int? AnioDesde { get; set; }
    public int? AnioHasta { get; set; }
    public string? Moneda { get; set; }
    public decimal? PrecioDesde { get; set; }
    public decimal? PrecioHasta { get; set; }
    public int? KmDesde { get; set; }
    public int? KmHasta { get; set; }
    public string? Combustible { get; set; }
    public string? Transmision { get; set; }
    public string? Carroceria { get; set; }

    /// <summary>Lo escrito en el buscador de la portada, cuando la búsqueda vino de ahí.</summary>
    public string? Texto { get; set; }
}

/// <summary>
/// Ventana de tiempo de los reportes, en días.
/// </summary>
/// <remarks>
/// Acotada por arriba porque la tabla de eventos es la que más crece: un pedido de dos
/// años de historia con un <c>GROUP BY</c> encima es la consulta que deja sin base a
/// todos los tenants a la vez.
/// </remarks>
public static class VentanaDeReporte
{
    public const int DiasPorDefecto = 30;
    public const int DiasMinimo = 7;
    public const int DiasMaximo = 365;

    public static int Normalizar(int? dias) => dias switch
    {
        null => DiasPorDefecto,
        < DiasMinimo => DiasMinimo,
        > DiasMaximo => DiasMaximo,
        _ => dias.Value,
    };
}
