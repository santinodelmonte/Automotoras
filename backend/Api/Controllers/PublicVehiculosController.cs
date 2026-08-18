using System.Text.Json;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Catálogo público de la automotora: home, listado con filtros y ficha.
/// </summary>
/// <remarks>
/// Sin autenticación y sin ningún identificador de automotora en la ruta: el tenant ya
/// viene resuelto por el middleware, desde el dominio propio o desde el slug, y validado
/// contra la tabla. Un endpoint público que aceptara el tenant como parámetro sería un
/// catálogo abierto de todos los clientes del SaaS.
/// <para>
/// Solo salen los vehículos <c>Disponible</c>. Los vendidos y los pausados no aparecen, y
/// los reservados tampoco: el DTO público no tiene dónde decir "reservado", y mostrar
/// como disponible algo que no lo está hace perder el viaje al comprador.
/// </para>
/// </remarks>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicVehiculosController : ControllerBase
{
    private const int DestacadosEnLaHome = 6;
    private const int RecientesEnLaHome = 8;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PublicVehiculosController(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// La home en un solo request: destacados, recientes y el total publicado.
    /// </summary>
    /// <remarks>
    /// Un viaje en vez de tres. La mayoría del tráfico es de celular en 4G, donde la
    /// latencia de cada ida y vuelta pesa más que el tamaño de la respuesta.
    /// </remarks>
    [HttpGet("home")]
    [ProducesResponseType(typeof(HomePublicaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomePublicaDto>> Home(CancellationToken cancellationToken)
    {
        var disponibles = Publicables();

        var destacados = await disponibles
            .Where(v => v.Destacado)
            .OrderByDescending(v => v.FechaPublicacion)
            .Take(DestacadosEnLaHome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var yaMostrados = destacados.Select(v => v.Id).ToList();

        var recientes = await disponibles
            .Where(v => !yaMostrados.Contains(v.Id))
            .OrderByDescending(v => v.FechaPublicacion)
            .Take(RecientesEnLaHome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = await Publicables().CountAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new HomePublicaDto(
            destacados.Select(v => v.AResumenPublico()).ToList(),
            recientes.Select(v => v.AResumenPublico()).ToList(),
            total));
    }

    /// <summary>
    /// Las opciones de filtrado que tienen sentido en este sitio: solo lo que hay
    /// publicado.
    /// </summary>
    [HttpGet("filtros")]
    [ProducesResponseType(typeof(FiltrosDisponiblesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FiltrosDisponiblesDto>> Filtros(CancellationToken cancellationToken)
    {
        var stock = await _db.Vehiculos
            .Where(v => v.Estado == EstadoVehiculo.Disponible)
            .Select(v => new
            {
                MarcaId = v.Modelo!.MarcaId,
                Marca = v.Modelo!.Marca!.Nombre,
                v.ModeloId,
                Modelo = v.Modelo!.Nombre,
                v.Modelo!.Carroceria,
                v.Combustible,
                v.Transmision,
                v.Moneda,
                v.Anio,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stock.Count == 0)
        {
            return Ok(new FiltrosDisponiblesDto([], [], [], [], [], null, null));
        }

        var marcas = stock
            .GroupBy(v => new { v.MarcaId, v.Marca })
            .OrderBy(g => g.Key.Marca, StringComparer.CurrentCulture)
            .Select(g => new MarcaConStockDto(
                g.Key.MarcaId,
                g.Key.Marca,
                g.GroupBy(v => new { v.ModeloId, v.Modelo })
                    .OrderBy(m => m.Key.Modelo, StringComparer.CurrentCulture)
                    .Select(m => new ModeloConStockDto(m.Key.ModeloId, m.Key.Modelo))
                    .ToList()))
            .ToList();

        return Ok(new FiltrosDisponiblesDto(
            marcas,
            Distintos(stock.Select(v => v.Carroceria.ToString())),
            Distintos(stock.Select(v => v.Combustible.ToString())),
            Distintos(stock.Select(v => v.Transmision.ToString())),
            Distintos(stock.Select(v => v.Moneda.ToString())),
            stock.Min(v => v.Anio),
            stock.Max(v => v.Anio)));
    }

    private static IReadOnlyList<string> Distintos(IEnumerable<string> valores)
        => valores.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();

    [HttpGet("vehiculos")]
    [ProducesResponseType(typeof(PaginaDe<VehiculoPublicoResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDe<VehiculoPublicoResumenDto>>> Listar(
        [FromQuery] FiltrosPublicosDeVehiculos filtros,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtros);

        var pagina = Paginacion.NormalizarPagina(filtros.Pagina);
        var porPagina = Paginacion.NormalizarPorPagina(filtros.PorPagina);

        var consulta = Aplicar(Publicables(), filtros);

        var total = await consulta.CountAsync(cancellationToken).ConfigureAwait(false);

        var vehiculos = await Ordenar(consulta, filtros.Orden)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await RegistrarBusquedaAsync(filtros, total, cancellationToken).ConfigureAwait(false);

        return Ok(new PaginaDe<VehiculoPublicoResumenDto>(
            vehiculos.Select(v => v.AResumenPublico()).ToList(), total, pagina, porPagina));
    }

    [HttpGet("vehiculos/{id:int}")]
    [ProducesResponseType(typeof(VehiculoPublicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehiculoPublicoDto>> Obtener(int id, CancellationToken cancellationToken)
    {
        var vehiculo = await Publicables()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NotFound();
        }

        var nombre = await _db.Tenants
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => t.Nombre)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(vehiculo.ADtoPublico(nombre ?? string.Empty));
    }

    private IQueryable<Vehiculo> Publicables()
        => _db.Vehiculos
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Version)
            .Include(v => v.Fotos)
            .Where(v => v.Estado == EstadoVehiculo.Disponible);

    private static IQueryable<Vehiculo> Aplicar(IQueryable<Vehiculo> consulta, FiltrosPublicosDeVehiculos filtros)
    {
        if (filtros.MarcaId is { } marcaId)
        {
            consulta = consulta.Where(v => v.Modelo!.MarcaId == marcaId);
        }

        if (filtros.ModeloId is { } modeloId)
        {
            consulta = consulta.Where(v => v.ModeloId == modeloId);
        }

        if (filtros.AnioDesde is { } anioDesde)
        {
            consulta = consulta.Where(v => v.Anio >= anioDesde);
        }

        if (filtros.AnioHasta is { } anioHasta)
        {
            consulta = consulta.Where(v => v.Anio <= anioHasta);
        }

        if (filtros.KmDesde is { } kmDesde)
        {
            consulta = consulta.Where(v => v.Kilometraje >= kmDesde);
        }

        if (filtros.KmHasta is { } kmHasta)
        {
            consulta = consulta.Where(v => v.Kilometraje <= kmHasta);
        }

        // El rango de precio siempre va atado a una moneda. El validador la exige: un
        // rango que cruce dólares con pesos no significa nada.
        if (Enumeraciones.ParsearOpcional<Moneda>(filtros.Moneda) is { } moneda)
        {
            consulta = consulta.Where(v => v.Moneda == moneda);

            if (filtros.PrecioDesde is { } precioDesde)
            {
                consulta = consulta.Where(v => v.Precio >= precioDesde);
            }

            if (filtros.PrecioHasta is { } precioHasta)
            {
                consulta = consulta.Where(v => v.Precio <= precioHasta);
            }
        }

        if (Enumeraciones.ParsearOpcional<Combustible>(filtros.Combustible) is { } combustible)
        {
            consulta = consulta.Where(v => v.Combustible == combustible);
        }

        if (Enumeraciones.ParsearOpcional<Transmision>(filtros.Transmision) is { } transmision)
        {
            consulta = consulta.Where(v => v.Transmision == transmision);
        }

        if (Enumeraciones.ParsearOpcional<Carroceria>(filtros.Carroceria) is { } carroceria)
        {
            consulta = consulta.Where(v => v.Modelo!.Carroceria == carroceria);
        }

        return consulta;
    }

    private static IQueryable<Vehiculo> Ordenar(IQueryable<Vehiculo> consulta, string? orden) => orden switch
    {
        "precio_asc" => consulta.OrderBy(v => v.Precio).ThenByDescending(v => v.Id),
        "precio_desc" => consulta.OrderByDescending(v => v.Precio).ThenByDescending(v => v.Id),
        "km_asc" => consulta.OrderBy(v => v.Kilometraje).ThenByDescending(v => v.Id),
        "anio_desc" => consulta.OrderByDescending(v => v.Anio).ThenByDescending(v => v.Id),

        // Por defecto, destacados primero: es la vidriera que eligió la automotora.
        _ => consulta
            .OrderByDescending(v => v.Destacado)
            .ThenByDescending(v => v.FechaPublicacion)
            .ThenByDescending(v => v.Id),
    };

    /// <summary>
    /// Guarda la búsqueda con sus filtros y cuántos resultados devolvió.
    /// </summary>
    /// <remarks>
    /// Solo cuando el visitante aplicó algún filtro: entrar al listado sin filtrar no es
    /// una búsqueda, y registrarlo llenaría la tabla de ruido que después hay que
    /// descartar en cada reporte.
    /// <para>
    /// Las que devuelven cero son la señal más valiosa del producto —dicen qué le están
    /// pidiendo a la automotora que no tiene en stock—, así que además dejan su propio
    /// evento.
    /// </para>
    /// </remarks>
    private async Task RegistrarBusquedaAsync(
        FiltrosPublicosDeVehiculos filtros,
        int resultados,
        CancellationToken cancellationToken)
    {
        if (!filtros.HayFiltros)
        {
            return;
        }

        // Se serializan los filtros uno por uno y no el objeto entero: así el JSON no
        // arrastra la paginación, la propiedad calculada ni el id de sesión, que ya va en
        // su columna. Lo que se guarda es lo que después se agrupa en los reportes.
        var json = JsonSerializer.Serialize(
            new
            {
                filtros.MarcaId,
                filtros.ModeloId,
                filtros.AnioDesde,
                filtros.AnioHasta,
                filtros.Moneda,
                filtros.PrecioDesde,
                filtros.PrecioHasta,
                filtros.KmDesde,
                filtros.KmHasta,
                filtros.Combustible,
                filtros.Transmision,
                filtros.Carroceria,
            },
            OpcionesDeSerializacion);

        _db.Busquedas.Add(new Busqueda
        {
            Filtros = json,
            ResultadosCount = resultados,
            SessionId = Recortar(filtros.SessionId, 64),
        });

        if (resultados == 0)
        {
            _db.Eventos.Add(new Evento
            {
                Tipo = TipoEvento.BusquedaSinResultado,
                SessionId = Recortar(filtros.SessionId, 64),
                Metadata = json,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? Recortar(string? valor, int largo)
        => string.IsNullOrWhiteSpace(valor) ? null : valor[..Math.Min(valor.Length, largo)];

    private static readonly JsonSerializerOptions OpcionesDeSerializacion = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
