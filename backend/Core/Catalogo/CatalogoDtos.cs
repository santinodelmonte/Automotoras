namespace AutomotoraSaaS.Core.Catalogo;

/// <summary>
/// Marca del catálogo global. Alimenta el primer select del formulario de carga.
/// </summary>
public sealed record MarcaDto(int Id, string Nombre, bool Activo);

/// <summary>
/// Modelo de una marca.
/// </summary>
/// <param name="Carroceria">Nombre de la carrocería: <c>Sedan</c>, <c>Suv</c>, …</param>
public sealed record ModeloDto(int Id, int MarcaId, string Nombre, string Carroceria, bool Activo);

/// <summary>Versión de un modelo. Opcional al cargar un vehículo.</summary>
public sealed record VersionDto(int Id, int ModeloId, string Nombre, bool Activo);

/// <summary>
/// Las opciones fijas del formulario, servidas desde el servidor.
/// </summary>
/// <remarks>
/// Duplicar los enums en el frontend garantiza que algún día queden desfasados y que el
/// select ofrezca un valor que la API rechaza. Servirlos evita esa clase entera de bug.
/// </remarks>
public sealed record OpcionesDeCatalogoDto(
    IReadOnlyList<string> Carrocerias,
    IReadOnlyList<string> Combustibles,
    IReadOnlyList<string> Transmisiones,
    IReadOnlyList<string> Monedas,
    IReadOnlyList<string> EstadosDeVehiculo);

/// <summary>
/// Pedido de alta de un modelo que falta. El vendedor lo solicita, el SuperAdmin resuelve.
/// </summary>
public sealed record SolicitudModeloDto(
    int Id,
    int MarcaId,
    string Marca,
    string NombreModelo,
    string Carroceria,
    string Estado,
    string SolicitadaPor,
    DateTime CreatedAt,
    DateTime? ResueltaEn,
    string? NotaResolucion,
    int? ModeloCreadoId);

/// <summary>Cuerpo de <c>POST /api/catalogo/solicitudes-modelo</c>.</summary>
public sealed record CrearSolicitudModeloRequest(int MarcaId, string NombreModelo, string Carroceria);

/// <summary>Cuerpo con el que el SuperAdmin resuelve una solicitud.</summary>
public sealed record ResolverSolicitudRequest(bool Aprobar, string? Nota);
