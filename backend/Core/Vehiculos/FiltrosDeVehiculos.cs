using AutomotoraSaaS.Core.Common;

namespace AutomotoraSaaS.Core.Vehiculos;

/// <summary>
/// Filtros del listado del panel. Se bindean desde el query string, así que son
/// propiedades con <c>set</c> y no un record posicional.
/// </summary>
public sealed class FiltrosDeVehiculos
{
    /// <summary>Nombre del estado. Sin valor, trae todos —el panel sí ve los vendidos.</summary>
    public string? Estado { get; set; }

    public int? MarcaId { get; set; }
    public int? ModeloId { get; set; }

    /// <summary>Busca en marca, modelo, versión y color.</summary>
    public string? Texto { get; set; }

    public bool? Destacado { get; set; }

    public int Pagina { get; set; } = Paginacion.PrimeraPagina;
    public int PorPagina { get; set; } = Paginacion.PorPaginaPorDefecto;
}

/// <summary>
/// Filtros del listado público. Son los del brief, ni uno más.
/// </summary>
/// <remarks>
/// El estado no está: el sitio público muestra lo que está a la venta y nada más. Que un
/// query param pudiera pedir los vendidos sería exponer el histórico comercial de la
/// automotora a cualquiera.
/// </remarks>
public sealed class FiltrosPublicosDeVehiculos
{
    public int? MarcaId { get; set; }
    public int? ModeloId { get; set; }

    public int? AnioDesde { get; set; }
    public int? AnioHasta { get; set; }

    /// <summary>
    /// Obligatoria si se filtra por precio: en Uruguay se publica en dólares y en pesos, y
    /// un rango que cruce las dos monedas no significa nada.
    /// </summary>
    public string? Moneda { get; set; }

    public decimal? PrecioDesde { get; set; }
    public decimal? PrecioHasta { get; set; }

    public int? KmDesde { get; set; }
    public int? KmHasta { get; set; }

    public string? Combustible { get; set; }
    public string? Transmision { get; set; }
    public string? Carroceria { get; set; }

    /// <summary><c>reciente</c>, <c>precio_asc</c>, <c>precio_desc</c>, <c>km_asc</c>, <c>anio_desc</c>.</summary>
    public string? Orden { get; set; }

    public int Pagina { get; set; } = Paginacion.PrimeraPagina;
    public int PorPagina { get; set; } = Paginacion.PorPaginaPorDefecto;

    /// <summary>
    /// Identifica la visita para poder agrupar su actividad. Lo pone el cliente desde la
    /// cookie de primera parte; si no viene, la búsqueda se registra igual, sin sesión.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary><c>true</c> si el visitante aplicó al menos un filtro.</summary>
    public bool HayFiltros =>
        MarcaId is not null || ModeloId is not null
        || AnioDesde is not null || AnioHasta is not null
        || PrecioDesde is not null || PrecioHasta is not null
        || KmDesde is not null || KmHasta is not null
        || Combustible is not null || Transmision is not null || Carroceria is not null;
}
