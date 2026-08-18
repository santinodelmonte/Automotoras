namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Reglas de publicación del benchmark. Son de privacidad, no de presentación.
/// </summary>
/// <remarks>
/// El producto entero se apoya en que los datos de todas las automotoras vivan en la misma
/// base. Eso solo es defendible si ninguna puede deducir nada de otra, y un agregado sobre
/// una muestra chica no es un agregado: con dos competidores en el promedio, cada uno
/// despeja al otro con una resta.
/// </remarks>
public static class ReglasDelBenchmark
{
    /// <summary>
    /// Automotoras —además de la que pregunta— que tienen que aportar datos para publicar
    /// una comparación.
    /// </summary>
    /// <remarks>
    /// Cinco, no dos. Con menos, quien pregunta puede acotar el dato de un competidor
    /// concreto, y en un mercado chico como el uruguayo sabe perfectamente quiénes son los
    /// otros. Por debajo del mínimo no se devuelve un número aproximado ni redondeado: no
    /// se devuelve nada.
    /// </remarks>
    public const int AutomotorasMinimas = 5;

    /// <summary>
    /// Unidades publicadas que necesita una automotora para entrar en la muestra.
    /// </summary>
    /// <remarks>
    /// Una automotora con un solo auto no describe una operación, y además es la más fácil
    /// de identificar: su mediana es su único vehículo.
    /// </remarks>
    public const int VehiculosMinimosParaAportar = 3;

    /// <summary>Ventana por defecto del benchmark.</summary>
    public const int DiasPorDefecto = 90;
}

/// <summary>
/// Una métrica propia al lado de la del resto del mercado.
/// </summary>
/// <param name="Mercado">
/// La mediana entre automotoras, nunca el mínimo ni el máximo. Un extremo es un dato de
/// una automotora puntual con otro nombre.
/// </param>
/// <param name="MejorCuandoBaja">
/// Si menos es mejor. Va en el dato y no en el panel: quien define la métrica es quien
/// sabe para qué lado se lee, y sin esto la pantalla tiene que adivinarlo por el nombre.
/// </param>
public sealed record MetricaComparadaDto(
    decimal? Propio,
    decimal? Mercado,
    bool MejorCuandoBaja);

/// <summary>
/// Cómo le va a esta automotora comparada con el resto, sin decir nada de nadie.
/// </summary>
/// <param name="Disponible">
/// <c>false</c> mientras no haya muestra suficiente. Con el motivo al lado, para que el
/// panel pueda explicar por qué está vacío en vez de parecer roto.
/// </param>
/// <param name="AutomotorasEnLaMuestra">
/// Cuántas aportaron, sin incluir a la que pregunta. Es lo único que se dice del resto.
/// </param>
public sealed record BenchmarkDto(
    int Dias,
    bool Disponible,
    string? Motivo,
    int AutomotorasEnLaMuestra,
    MetricaComparadaDto? DiasEnGondola,
    MetricaComparadaDto? ConsultasPorCienVistas,
    MetricaComparadaDto? DiasHastaLaVenta);
