namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Qué está diciendo el comportamiento de los compradores sobre una unidad.
/// </summary>
/// <remarks>
/// Es una señal, no un veredicto. El reporte sugiere dónde mirar; la decisión de bajar un
/// precio o sacar una unidad la toma quien conoce el negocio.
/// </remarks>
public enum SenalDeDemanda
{
    /// <summary>Todavía no hay tráfico suficiente para concluir nada.</summary>
    PocosDatos = 1,

    /// <summary>La proporción entre miradas y consultas es la esperable.</summary>
    Normal = 2,

    /// <summary>La miran bastante y casi nadie consulta. Suele ser precio.</summary>
    PrecioAlto = 3,

    /// <summary>Lleva tiempo publicada y ni siquiera la miran.</summary>
    SinInteres = 4,
}

/// <summary>
/// Umbrales del análisis de demanda.
/// </summary>
/// <remarks>
/// Están acá, con nombre y explicación, en vez de sueltos en la consulta. Son las
/// perillas que hay que mover cuando haya datos reales de varias automotoras, y tienen que
/// poder discutirse leyendo.
/// </remarks>
public static class UmbralesDeDemanda
{
    /// <summary>
    /// Por debajo de esto no se concluye nada. Con cinco visitas, una consulta da 20 % y
    /// ninguna da 0 %: los dos números son ruido, no señal.
    /// </summary>
    public const int VistasMinimasParaConcluir = 25;

    /// <summary>
    /// Consultas cada cien vistas por debajo de las cuales la unidad se mira pero no se
    /// pregunta. El seed sintético usa ~8 de cada 100 como comportamiento normal.
    /// </summary>
    public const double ConsultasPorCienVistasBajas = 3.0;

    /// <summary>Días publicada a partir de los cuales la falta de tráfico ya es un dato.</summary>
    public const int DiasParaEsperarInteres = 45;

    /// <summary>Vistas por debajo de las cuales, pasado ese tiempo, la unidad no se está viendo.</summary>
    public const int VistasQueIndicanFaltaDeInteres = 15;
}

/// <summary>Una unidad publicada, con lo que se sabe de su demanda.</summary>
/// <param name="Lectura">La señal explicada en una frase, para no dejar el número solo.</param>
public sealed record VehiculoEnGondolaDto(
    int VehiculoId,
    string Marca,
    string Modelo,
    string? Version,
    int Anio,
    decimal Precio,
    string Moneda,
    string Estado,
    string? FotoPortadaUrl,
    int DiasEnGondola,
    int Vistas,
    int Consultas,
    double ConsultasPorCienVistas,
    string Senal,
    string Lectura);

/// <summary>
/// Una combinación de filtros que la gente buscó y no encontró, con cuántas veces pasó.
/// </summary>
/// <remarks>
/// Es la demanda insatisfecha: lo más cerca que hay de una lista de compras hecha por los
/// propios compradores.
/// </remarks>
public sealed record DemandaInsatisfechaDto(
    string? Marca,
    string? Modelo,
    string? Carroceria,
    string? Combustible,
    string? Transmision,
    int? AnioDesde,
    decimal? PrecioHasta,
    string? Moneda,
    int Veces,
    DateTime UltimaVez,
    string Descripcion);

/// <summary>El reporte de demanda completo.</summary>
public sealed record ReporteDeDemandaDto(
    int DiasAnalizados,
    int VistasTotales,
    int ConsultasTotales,
    IReadOnlyList<VehiculoEnGondolaDto> Vehiculos,
    IReadOnlyList<DemandaInsatisfechaDto> DemandaInsatisfecha);

/// <summary>
/// Qué conviene traer, y por qué.
/// </summary>
/// <param name="Fundamento">
/// La razón en una frase. Una sugerencia sin fundamento es una corazonada con formato de
/// dato, y el producto existe justamente para reemplazar corazonadas.
/// </param>
/// <param name="UnidadesVendidasSimilares">
/// Cuántas unidades parecidas vendió esta automotora. Nulo si nunca vendió ninguna: es
/// información que falta, no un cero.
/// </param>
public sealed record SugerenciaDeCompraDto(
    string Descripcion,
    string Fundamento,
    int BusquedasSinResultado,
    DateTime UltimaBusqueda,
    string? Marca,
    string? Modelo,
    string? Carroceria,
    int? AnioDesde,
    decimal? PrecioHasta,
    string? Moneda,
    int? UnidadesVendidasSimilares,
    int? DiasPromedioParaVender);

/// <summary>
/// Umbrales de las sugerencias de compra.
/// </summary>
public static class UmbralesDeSugerencia
{
    /// <summary>
    /// Búsquedas fallidas mínimas para sugerir algo. Una sola persona buscando un modelo
    /// raro no es demanda: es una persona.
    /// </summary>
    public const int BusquedasMinimas = 3;

    /// <summary>Cuántas sugerencias devolver. Una lista larga no se lee ni se acciona.</summary>
    public const int Maximo = 10;

    /// <summary>
    /// Ventas mínimas para animarse a hablar de rotación. Con una sola venta, el promedio
    /// de días es esa venta.
    /// </summary>
    public const int VentasMinimasParaRotacion = 2;
}
