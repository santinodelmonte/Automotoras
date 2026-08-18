using System.Linq.Expressions;
using System.Text.Json;
using AutomotoraSaaS.Core.Analitica;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Los reportes de demanda: qué se mira, qué se consulta, y qué se busca y no está.
/// </summary>
/// <remarks>
/// Es el producto. El catálogo lo tiene cualquiera; lo que no tiene cualquiera es saber
/// que el Corolla 2018 se miró ciento ochenta veces y no lo consultó nadie, o que hubo
/// treinta búsquedas de pickups que terminaron en una pantalla vacía.
/// <para>
/// Solo el Owner, igual que el tablero: el vendedor carga stock y atiende consultas.
/// </para>
/// <para>
/// Todo se lee de eventos que se vienen guardando desde el paso 4a, sin que existiera
/// ningún reporte que los usara. Ese orden era el punto: los datos de demanda solo valen
/// acumulados, y lo que no se midió no se recupera.
/// </para>
/// </remarks>
[ApiController]
[Route("api/reportes")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ReportesController : ControllerBase
{
    /// <summary>
    /// Tope de búsquedas sin resultado que se traen para agrupar.
    /// </summary>
    /// <remarks>
    /// El agrupado no se puede hacer en SQL: la clave está adentro de una columna JSON y
    /// hay que parsearla. Traer las más recientes con un tope acotado es la forma de que
    /// el reporte no dependa de cuánto creció la tabla. Una automotora que pase este tope
    /// en la ventana pedida ve las más recientes, que es la parte que todavía se puede
    /// accionar.
    /// </remarks>
    private const int TopeDeBusquedas = 5_000;

    private static readonly JsonSerializerOptions OpcionesDeLectura = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;

    public ReportesController(AppDbContext db, TimeProvider reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    /// <summary>
    /// Demanda por unidad: días en góndola, vistas, consultas y la lectura de las tres
    /// cosas juntas.
    /// </summary>
    /// <param name="dias">Ventana en días. Por defecto treinta, tope un año.</param>
    /// <remarks>
    /// El reporte cubre lo que está publicado —disponible y reservado—. Los pausados y
    /// los vendidos no salen en el sitio, así que sus cero vistas no significan que nadie
    /// los quiera: significan que nadie los pudo ver, y mezclarlos correría todos los
    /// promedios hacia abajo.
    /// </remarks>
    [HttpGet("demanda")]
    [ProducesResponseType(typeof(ReporteDeDemandaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteDeDemandaDto>> Demanda(
        [FromQuery] int? dias,
        CancellationToken cancellationToken)
    {
        var ventana = VentanaDeReporte.Normalizar(dias);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var desde = ahora.AddDays(-ventana);

        var publicados = await _db.Vehiculos
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Fotos)
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vistas = await ContarPorVehiculoAsync(
            e => e.Tipo == TipoEvento.ViewFicha, desde, cancellationToken).ConfigureAwait(false);

        var consultas = await ContarPorVehiculoAsync(
            e => EventosDeContacto.Tipos.Contains(e.Tipo), desde, cancellationToken).ConfigureAwait(false);

        var filas = publicados
            .Select(v => Fila(v, vistas, consultas, ahora))
            .OrderByDescending(f => f.Vistas)
            .ThenBy(f => f.VehiculoId)
            .ToList();

        var sinResultado = await _db.Busquedas
            .CountAsync(b => b.ResultadosCount == 0 && b.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);

        var resumen = await ResumenAsync(filas, ventana, desde, ahora, sinResultado, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new ReporteDeDemandaDto(resumen, filas));
    }

    /// <summary>
    /// Las búsquedas que terminaron en una pantalla vacía, agrupadas por lo que se estaba
    /// buscando.
    /// </summary>
    /// <remarks>
    /// Es la señal más valiosa del producto y la única que no se puede reconstruir después:
    /// dice qué le están pidiendo a la automotora que no tiene. Un vehículo que no se vende
    /// se ve mirando el stock; una búsqueda que no encontró nada no deja rastro en ningún
    /// lado salvo acá.
    /// </remarks>
    [HttpGet("busquedas-sin-resultado")]
    [ProducesResponseType(typeof(IReadOnlyList<BusquedaSinResultadoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BusquedaSinResultadoDto>>> BusquedasSinResultado(
        [FromQuery] int? dias,
        CancellationToken cancellationToken)
    {
        var ventana = VentanaDeReporte.Normalizar(dias);
        var desde = _reloj.GetUtcNow().UtcDateTime.AddDays(-ventana);

        var grupos = await AgruparBusquedasSinResultadoAsync(desde, cancellationToken).ConfigureAwait(false);

        return Ok(grupos);
    }

    /// <summary>
    /// Agrupa las búsquedas vacías por marca, modelo y carrocería, y les pone los nombres
    /// del catálogo.
    /// </summary>
    private async Task<IReadOnlyList<BusquedaSinResultadoDto>> AgruparBusquedasSinResultadoAsync(
        DateTime desde,
        CancellationToken cancellationToken)
    {
        var crudas = await _db.Busquedas
            .Where(b => b.ResultadosCount == 0 && b.CreatedAt >= desde)
            .OrderByDescending(b => b.CreatedAt)
            .Take(TopeDeBusquedas)
            .Select(b => new { b.Filtros, b.SessionId, b.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var parseadas = crudas
            .Select(b => new { Filtros = Parsear(b.Filtros), b.SessionId, b.CreatedAt })
            .Where(b => b.Filtros is not null)
            .ToList();

        var grupos = parseadas
            .GroupBy(b => new { b.Filtros!.MarcaId, b.Filtros.ModeloId, b.Filtros.Carroceria })
            .Select(g => new
            {
                g.Key.MarcaId,
                g.Key.ModeloId,
                g.Key.Carroceria,
                Veces = g.Count(),

                // Las búsquedas sin sesión no cuentan como visita distinta, en vez de
                // contarse todas juntas como una sola: el id de sesión puede faltar —el
                // visitante bloqueó el almacenamiento local— y meterlas en un mismo balde
                // inventaría una visita que hizo veinte búsquedas.
                Sesiones = g.Select(x => x.SessionId)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                UltimaVez = g.Max(x => x.CreatedAt),
                AnioDesde = g.Min(x => x.Filtros!.AnioDesde),
                AnioHasta = g.Max(x => x.Filtros!.AnioHasta),
                PrecioDesde = g.Min(x => x.Filtros!.PrecioDesde),
                PrecioHasta = g.Max(x => x.Filtros!.PrecioHasta),

                // La moneda más pedida del grupo. Puede haber grupos con las dos, y un
                // rango de precio que cruce dólares y pesos no significa nada.
                Moneda = g.Select(x => x.Filtros!.Moneda)
                    .Where(m => !string.IsNullOrEmpty(m))
                    .GroupBy(m => m!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(m => m.Count())
                    .Select(m => m.Key)
                    .FirstOrDefault(),
            })
            .OrderByDescending(g => g.Sesiones)
            .ThenByDescending(g => g.Veces)
            .ToList();

        var nombres = await NombresDelCatalogoAsync(
                grupos.Select(g => g.MarcaId),
                grupos.Select(g => g.ModeloId),
                cancellationToken)
            .ConfigureAwait(false);

        return grupos
            .Select(g => new BusquedaSinResultadoDto(
                g.MarcaId,
                g.MarcaId is null ? null : nombres.Marcas.GetValueOrDefault(g.MarcaId.Value),
                g.ModeloId,
                g.ModeloId is null ? null : nombres.Modelos.GetValueOrDefault(g.ModeloId.Value),
                g.Carroceria,
                g.AnioDesde,
                g.AnioHasta,
                g.Moneda,
                g.PrecioDesde,
                g.PrecioHasta,
                g.Veces,
                g.Sesiones,
                g.UltimaVez))
            .ToList();
    }

    private static FiltrosDeBusquedaGuardados? Parsear(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<FiltrosDeBusquedaGuardados>(json, OpcionesDeLectura);
        }
        catch (JsonException)
        {
            // Una fila con el JSON roto no puede voltear el reporte entero. Se descarta:
            // es un registro de analítica, no un dato del que dependa una operación.
            return null;
        }
    }

    /// <summary>
    /// Los nombres de marca y modelo del catálogo, que es global y no lleva tenant.
    /// </summary>
    private async Task<(Dictionary<int, string> Marcas, Dictionary<int, string> Modelos)> NombresDelCatalogoAsync(
        IEnumerable<int?> marcas,
        IEnumerable<int?> modelos,
        CancellationToken cancellationToken)
    {
        var idsDeMarca = marcas.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var idsDeModelo = modelos.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();

        var nombresDeMarca = idsDeMarca.Count == 0
            ? []
            : await _db.Marcas
                .Where(m => idsDeMarca.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken)
                .ConfigureAwait(false);

        var nombresDeModelo = idsDeModelo.Count == 0
            ? []
            : await _db.Modelos
                .Where(m => idsDeModelo.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken)
                .ConfigureAwait(false);

        return (nombresDeMarca, nombresDeModelo);
    }

    private async Task<Dictionary<int, int>> ContarPorVehiculoAsync(
        Expression<Func<Evento, bool>> criterio,
        DateTime desde,
        CancellationToken cancellationToken)
        => await _db.Eventos
            .Where(e => e.CreatedAt >= desde && e.VehiculoId != null)
            .Where(criterio)
            .GroupBy(e => e.VehiculoId!.Value)
            .Select(g => new { VehiculoId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.VehiculoId, x => x.Cantidad, cancellationToken)
            .ConfigureAwait(false);

    private static DemandaDeVehiculoDto Fila(
        Vehiculo vehiculo,
        IReadOnlyDictionary<int, int> vistas,
        IReadOnlyDictionary<int, int> consultas,
        DateTime ahora)
    {
        var modelo = vehiculo.Modelo!;
        var vistasDelVehiculo = vistas.GetValueOrDefault(vehiculo.Id);
        var consultasDelVehiculo = consultas.GetValueOrDefault(vehiculo.Id);
        var enGondola = MapeosDeVehiculo.DiasEnGondola(vehiculo.FechaPublicacion, vehiculo.FechaVenta, ahora);

        return new DemandaDeVehiculoDto(
            vehiculo.Id,
            modelo.Marca!.Nombre,
            modelo.Nombre,
            vehiculo.Anio,
            vehiculo.Estado.ToString(),
            vehiculo.Precio,
            vehiculo.Moneda.ToString(),
            MapeosDeVehiculo.Portada(vehiculo)?.Url,
            enGondola,
            vistasDelVehiculo,
            consultasDelVehiculo,
            UmbralesDeDemanda.ConsultasPorCienVistas(vistasDelVehiculo, consultasDelVehiculo),
            UmbralesDeDemanda.Clasificar(vistasDelVehiculo, consultasDelVehiculo, enGondola).ToString());
    }

    private async Task<ResumenDeDemandaDto> ResumenAsync(
        IReadOnlyList<DemandaDeVehiculoDto> filas,
        int ventana,
        DateTime desde,
        DateTime ahora,
        int sinResultado,
        CancellationToken cancellationToken)
    {
        var vistas = filas.Sum(f => f.Vistas);
        var consultas = filas.Sum(f => f.Consultas);
        var enGondola = filas.Select(f => f.DiasEnGondola).ToList();

        // Los vendidos del período responden otra pregunta que los publicados —cuánto
        // tardé en vender, no cuánto llevo esperando— y por eso se cuentan aparte en vez
        // de mezclarse en el promedio de góndola.
        var vendidos = await _db.Vehiculos
            .Where(v => v.Estado == EstadoVehiculo.Vendido && v.FechaVenta != null && v.FechaVenta >= desde)
            .Select(v => new { v.FechaPublicacion, v.FechaVenta })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int? diasHastaLaVenta = vendidos.Count == 0
            ? null
            : (int)Math.Round(vendidos.Average(v =>
                (double)MapeosDeVehiculo.DiasEnGondola(v.FechaPublicacion, v.FechaVenta, ahora)));

        return new ResumenDeDemandaDto(
            ventana,
            filas.Count,
            vistas,
            consultas,
            UmbralesDeDemanda.ConsultasPorCienVistas(vistas, consultas),
            sinResultado,
            enGondola.Count == 0 ? 0 : (int)Math.Round(enGondola.Average()),
            Mediana(enGondola),
            vendidos.Count,
            diasHastaLaVenta);
    }

    /// <summary>
    /// La mediana de una lista. Con cantidad par se promedian las dos del medio, que es la
    /// definición y no una aproximación cómoda.
    /// </summary>
    private static int Mediana(IReadOnlyList<int> valores)
    {
        if (valores.Count == 0)
        {
            return 0;
        }

        var ordenados = valores.OrderBy(v => v).ToList();
        var medio = ordenados.Count / 2;

        return ordenados.Count % 2 == 1
            ? ordenados[medio]
            : (int)Math.Round((ordenados[medio - 1] + ordenados[medio]) / 2.0);
    }
}
