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

/// <summary>Un modelo con stock disponible en esta automotora.</summary>
public sealed record ModeloConStockDto(int Id, string Nombre);

/// <summary>Una marca con stock, y los modelos suyos que hay publicados.</summary>
public sealed record MarcaConStockDto(int Id, string Nombre, IReadOnlyList<ModeloConStockDto> Modelos);

/// <summary>
/// Lo que se puede filtrar en este sitio, ahora mismo.
/// </summary>
/// <remarks>
/// No es el catálogo global: son las marcas, modelos y características que esta automotora
/// tiene efectivamente publicadas. Ofrecerle al comprador un filtro que siempre devuelve
/// cero es hacerle perder el tiempo, y además expondría el catálogo entero del SaaS a
/// cualquiera que mire el sitio de un cliente.
/// <para>
/// El rango de años sirve para los <c>placeholder</c> de los campos: sugerir "desde 1990"
/// donde el más viejo es de 2015 no ayuda a nadie.
/// </para>
/// </remarks>
public sealed record FiltrosDisponiblesDto(
    IReadOnlyList<MarcaConStockDto> Marcas,
    IReadOnlyList<string> Carrocerias,
    IReadOnlyList<string> Combustibles,
    IReadOnlyList<string> Transmisiones,
    IReadOnlyList<string> Monedas,
    int? AnioMinimo,
    int? AnioMaximo);
