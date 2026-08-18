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
    IReadOnlyList<VehiculoMasVistoDto> MasVistos);
