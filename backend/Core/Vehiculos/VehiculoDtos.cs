namespace AutomotoraSaaS.Core.Vehiculos;

/// <summary>Foto de la galería.</summary>
public sealed record VehiculoFotoDto(int Id, string Url, string? UrlThumb, int Orden, bool EsPortada);

/// <summary>
/// Un vehículo visto desde el panel.
/// </summary>
/// <remarks>
/// <paramref name="PrecioCosto"/> viaja en <c>null</c> cuando quien pregunta es un Seller.
/// No es que el dato no exista: es que no sale de la base hacia un rol que no tiene por
/// qué verlo. El recorte se hace al proyectar, no escondiéndolo en la UI.
/// </remarks>
public sealed record VehiculoDto(
    int Id,
    int MarcaId,
    string Marca,
    int ModeloId,
    string Modelo,
    int? VersionId,
    string? Version,
    string Carroceria,
    int Anio,
    int Kilometraje,
    string Combustible,
    string Transmision,
    string? Color,
    int? Puertas,
    string? Motor,
    decimal Precio,
    string Moneda,
    string Estado,
    string? Descripcion,
    bool Destacado,
    decimal? PrecioCosto,
    DateTime FechaPublicacion,
    DateTime? FechaVenta,
    decimal? PrecioVenta,
    int DiasEnGondola,
    IReadOnlyList<VehiculoFotoDto> Fotos,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Un vehículo en el listado del panel. Lo mínimo para pintar una fila.</summary>
public sealed record VehiculoResumenDto(
    int Id,
    string Marca,
    string Modelo,
    string? Version,
    int Anio,
    int Kilometraje,
    decimal Precio,
    string Moneda,
    string Estado,
    bool Destacado,
    string? FotoPortadaUrl,
    int DiasEnGondola,
    DateTime FechaPublicacion);

/// <summary>
/// Alta y edición de un vehículo. Sirve para las dos: son el mismo conjunto de campos y
/// tenerlos en dos records idénticos solo garantiza que algún día se desfasen.
/// </summary>
/// <remarks>
/// El estado no está acá a propósito. Se cambia por su propio endpoint, porque marcar
/// vendido no es editar un campo: arrastra fecha y precio de venta, y saca la unidad del
/// sitio público.
/// </remarks>
public sealed record GuardarVehiculoRequest(
    int ModeloId,
    int? VersionId,
    int Anio,
    int Kilometraje,
    string Combustible,
    string Transmision,
    string? Color,
    int? Puertas,
    string? Motor,
    decimal Precio,
    string Moneda,
    string? Descripcion,
    bool Destacado,
    decimal? PrecioCosto,
    DateTime? FechaPublicacion);

/// <summary>
/// Cambio de estado. Al pasar a <c>Vendido</c> son obligatorias la fecha y el precio de
/// venta: sin eso no hay forma de calcular días en góndola ni margen, que es la mitad de
/// para qué existe el producto.
/// </summary>
public sealed record CambiarEstadoRequest(string Estado, DateTime? FechaVenta, decimal? PrecioVenta);

/// <summary>Nuevo orden de la galería, de la portada a la última.</summary>
public sealed record ReordenarFotosRequest(IReadOnlyList<int> FotoIds);
