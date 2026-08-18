using AutomotoraSaaS.Core.Vehiculos;

namespace AutomotoraSaaS.Core.Publico;

/// <summary>
/// Un vehículo en el listado público.
/// </summary>
/// <remarks>
/// No lleva precio de costo, ni fecha de venta, ni estado, ni nada del histórico
/// comercial. Lo que no está en este record no sale del servidor hacia un visitante
/// anónimo, y eso se decide acá y no en la pantalla que lo consume.
/// </remarks>
public sealed record VehiculoPublicoResumenDto(
    int Id,
    string Marca,
    string Modelo,
    string? Version,
    string Carroceria,
    int Anio,
    int Kilometraje,
    decimal Precio,
    string Moneda,
    string Combustible,
    string Transmision,
    string? FotoPortadaUrl,
    bool Destacado);

/// <summary>Ficha completa de un vehículo en el sitio público.</summary>
/// <param name="Titulo">Listo para el <c>&lt;title&gt;</c> y el Open Graph.</param>
/// <param name="MensajeDeWhatsapp">
/// Mensaje prearmado para el botón de WhatsApp. Lo arma el servidor y no el cliente, para
/// que diga lo mismo desde cualquier pantalla y desde cualquier automotora.
/// </param>
public sealed record VehiculoPublicoDto(
    int Id,
    string Marca,
    string Modelo,
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
    string? Descripcion,
    bool Destacado,
    IReadOnlyList<VehiculoFotoDto> Fotos,
    string Titulo,
    string MensajeDeWhatsapp);

/// <summary>
/// La home del sitio público en un solo request.
/// </summary>
/// <remarks>
/// Un solo viaje en vez de tres. La mayoría del tráfico es de celular en 4G, y ahí la
/// latencia de cada request de ida y vuelta pesa más que el tamaño de la respuesta.
/// </remarks>
public sealed record HomePublicaDto(
    IReadOnlyList<VehiculoPublicoResumenDto> Destacados,
    IReadOnlyList<VehiculoPublicoResumenDto> Recientes,
    int TotalDisponibles);
