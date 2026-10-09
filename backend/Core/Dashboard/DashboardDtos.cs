namespace AutomotoraSaaS.Core.Dashboard;

/// <summary>Cuántos vehículos hay en cada estado.</summary>
public sealed record ConteoPorEstadoDto(string Estado, int Cantidad);

/// <summary>
/// Un vehículo del top de vistas, con sus consultas al lado.
/// </summary>
/// <remarks>
/// Las dos cifras juntas y no cada una por su lado: muchas vistas con pocas consultas es
/// la señal de que el precio está alto, y esa lectura solo aparece cuando se las compara.
/// Es el germen de los reportes de demanda de fase 2.
/// </remarks>
public sealed record VehiculoMasVistoDto(
    int VehiculoId,
    string Marca,
    string Modelo,
    int Anio,
    string? FotoPortadaUrl,
    int Vistas,
    int Consultas);

/// <summary>
/// El tablero del panel: estado del stock y demanda de los últimos treinta días.
/// </summary>
public sealed record DashboardDto(
    IReadOnlyList<ConteoPorEstadoDto> VehiculosPorEstado,
    int TotalDeVehiculos,
    int VistasUltimos30Dias,
    int ConsultasUltimos30Dias,
    int BusquedasSinResultadoUltimos30Dias,
    int DiasEnGondolaPromedio,
    IReadOnlyList<VehiculoMasVistoDto> MasVistos,
    PrimerosPasosDto PrimerosPasos);

/// <summary>
/// Lo que le falta a una automotora recién dada de alta para que su sitio salga bien.
/// </summary>
/// <remarks>
/// Se calcula de los datos y no se marca a mano: un paso está hecho cuando el sitio lo
/// muestra, no cuando alguien tocó "listo". Cuando están todos, el panel deja de mostrarlo.
/// </remarks>
/// <param name="Slug">Para armar el link al sitio en el último paso.</param>
/// <param name="DominioCustom">El dominio verificado, si tiene; si no, se usa el slug.</param>
public sealed record PrimerosPasosDto(
    bool TieneLogo,
    bool TieneColor,
    bool TieneWhatsapp,
    int VehiculosPublicados,
    int PublicadosSinFotos,
    int Vendedores,
    string Slug,
    string? DominioCustom);
