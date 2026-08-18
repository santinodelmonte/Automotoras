namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Reglas de anonimato de los benchmarks comparativos.
/// </summary>
/// <remarks>
/// Son la condición para que el cruce cross-tenant exista. Ninguna automotora puede ver
/// datos identificables de otra, así que un agregado solo se publica cuando hay suficientes
/// automotoras detrás como para que ninguna sea deducible: con dos, quien pregunta conoce
/// la suya y despeja la otra restando.
/// <para>
/// Los mínimos se cuentan <b>sin</b> contar a quien pregunta. Incluirlo inflaría el número
/// sin agregar anonimato.
/// </para>
/// </remarks>
public static class UmbralesDeBenchmark
{
    /// <summary>Automotoras distintas, además de la que pregunta, para publicar un agregado.</summary>
    public const int AutomotorasMinimas = 3;

    /// <summary>Registros mínimos detrás del agregado. Tres ventas no son un promedio de mercado.</summary>
    public const int RegistrosMinimos = 10;

    /// <summary>Vistas mínimas para que el ratio de una automotora entre al promedio.</summary>
    public const int VistasMinimasPorAutomotora = 25;
}

/// <summary>
/// Un indicador propio contra el mismo indicador del resto del mercado.
/// </summary>
/// <param name="Propio">
/// El valor de esta automotora, o <c>null</c> si todavía no tiene datos para calcularlo.
/// </param>
/// <param name="Mercado">Promedio anonimizado del resto.</param>
/// <param name="AutomotorasAportantes">
/// Cuántas automotoras hay detrás del número del mercado, sin contar la propia. Se expone
/// para que quien lee sepa cuánto pesa la comparación.
/// </param>
public sealed record ComparativoDto(
    string Dimension,
    double? Propio,
    double Mercado,
    int AutomotorasAportantes,
    int RegistrosAportantes,
    string Lectura);

/// <summary>
/// Cómo le va a esta automotora comparada con el resto, sin que ninguna sea identificable.
/// </summary>
public sealed record BenchmarkDto(
    int DiasAnalizados,
    IReadOnlyList<ComparativoDto> DiasParaVenderPorCarroceria,
    ComparativoDto? ConsultasPorCienVistas,
    string NotaDePrivacidad);
