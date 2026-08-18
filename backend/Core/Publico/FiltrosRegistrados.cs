namespace AutomotoraSaaS.Core.Publico;

/// <summary>
/// Los filtros de una búsqueda, tal como quedan guardados en la columna JSON de
/// <c>busquedas</c>.
/// </summary>
/// <remarks>
/// Existe como tipo y no como objeto anónimo para que el lado que escribe y el que lee
/// sean el mismo contrato. Con un anónimo al guardar y otro al leer, renombrar un campo
/// compila, corre, y devuelve todo en <c>null</c> en los reportes — el peor tipo de bug,
/// porque no falla: miente.
/// </remarks>
public sealed record FiltrosRegistrados(
    int? MarcaId,
    int? ModeloId,
    int? AnioDesde,
    int? AnioHasta,
    string? Moneda,
    decimal? PrecioDesde,
    decimal? PrecioHasta,
    int? KmDesde,
    int? KmHasta,
    string? Combustible,
    string? Transmision,
    string? Carroceria);
