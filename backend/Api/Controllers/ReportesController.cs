using System.Linq.Expressions;
using System.Text.Json;
using AutomotoraSaaS.Core.Analitica;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using AutomotoraSaaS.Api.Planes;
using AutomotoraSaaS.Core.Planes;

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
[RequiereDelPlan(FuncionDelPlan.Reportes)]
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

    /// <summary>
    /// Cuántos días para atrás se acepta una cotización cuando falta la del día del
    /// snapshot. Un mes: el dólar no se mueve tanto en ese plazo como para invalidar una
    /// comparación que igual se expresa en porcentaje redondeado.
    /// </summary>
    private const int DiasDeToleranciaDeCotizacion = 30;

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

        var mercado = await PreciosDeMercadoAsync(publicados, cancellationToken).ConfigureAwait(false);

        var cotizaciones = await CotizacionesAsync(mercado.Values, cancellationToken).ConfigureAwait(false);

        var filas = publicados
            .Select(v => Fila(v, vistas, consultas, mercado, cotizaciones, ahora))
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
    /// Qué conviene comprar, según lo que se buscó y no estaba.
    /// </summary>
    /// <param name="dias">Ventana en días. Por defecto noventa.</param>
    /// <remarks>
    /// Es el reporte anterior cruzado contra el patio, y ese cruce es todo el valor: una
    /// búsqueda vacía de pickups significa una cosa si no hay ninguna publicada y otra
    /// bien distinta si hay tres. En el primer caso hay que comprar; en el segundo, mirar
    /// el precio, el año o las fotos de lo que ya está.
    /// <para>
    /// Las búsquedas que no nombran ni marca, ni modelo, ni carrocería —solo un precio o
    /// un kilometraje— quedan afuera. Aparecen en el reporte de búsquedas sin resultado,
    /// donde se pueden leer, pero no se traducen a "comprá esto": nadie sale a comprar un
    /// vehículo de hasta quince mil dólares sin saber de qué.
    /// </para>
    /// </remarks>
    [HttpGet("sugerencias")]
    [ProducesResponseType(typeof(IReadOnlyList<SugerenciaDeCompraDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SugerenciaDeCompraDto>>> Sugerencias(
        [FromQuery] int? dias,
        CancellationToken cancellationToken)
    {
        var ventana = VentanaDeReporte.Normalizar(dias ?? UmbralesDeSugerencia.DiasPorDefecto);
        var desde = _reloj.GetUtcNow().UtcDateTime.AddDays(-ventana);

        var grupos = await AgruparBusquedasSinResultadoAsync(desde, cancellationToken).ConfigureAwait(false);

        var candidatos = grupos
            .Where(g => g.Sesiones >= UmbralesDeSugerencia.VisitasMinimas)
            .Where(g => g.ModeloId is not null || g.MarcaId is not null || g.Carroceria is not null)
            .ToList();

        if (candidatos.Count == 0)
        {
            return Ok(Array.Empty<SugerenciaDeCompraDto>());
        }

        var stock = await StockPublicadoAsync(cancellationToken).ConfigureAwait(false);

        var sugerencias = candidatos
            .Select(g =>
            {
                var unidades = UnidadesQueEncajan(stock, g);

                return new SugerenciaDeCompraDto(
                    (unidades == 0 ? TipoDeSugerencia.Comprar : TipoDeSugerencia.RevisarLoQueTenes).ToString(),
                    g.MarcaId,
                    g.Marca,
                    g.ModeloId,
                    g.Modelo,
                    g.Carroceria,
                    g.AnioDesde,
                    g.AnioHasta,
                    g.Moneda,
                    g.PresupuestoTipico,
                    g.Sesiones,
                    g.Veces,
                    g.UltimaVez,
                    unidades);
            })
            .OrderByDescending(s => s.Visitas)
            .ThenByDescending(s => s.Busquedas)
            .Take(UmbralesDeSugerencia.Maximo)
            .ToList();

        return Ok(sugerencias);
    }

    /// <summary>
    /// El stock publicado, reducido a lo que hace falta para el cruce. Se trae una vez y
    /// se cuenta en memoria: son decenas de filas, y una consulta por sugerencia serían
    /// veinte viajes a la base para contestar una sola pantalla.
    /// </summary>
    private async Task<IReadOnlyList<UnidadPublicada>> StockPublicadoAsync(CancellationToken cancellationToken)
        => await _db.Vehiculos
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .Select(v => new UnidadPublicada(v.ModeloId, v.Modelo!.MarcaId, v.Modelo.Carroceria))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Cuántas unidades publicadas caen dentro de lo que pedía el grupo, de lo más
    /// específico a lo más general: si la búsqueda nombró un modelo, lo que importa es ese
    /// modelo y no cuántos autos de la marca hay.
    /// </summary>
    private static int UnidadesQueEncajan(IReadOnlyList<UnidadPublicada> stock, BusquedaSinResultadoDto grupo)
    {
        if (grupo.ModeloId is not null)
        {
            return stock.Count(u => u.ModeloId == grupo.ModeloId);
        }

        if (grupo.MarcaId is not null)
        {
            return stock.Count(u => u.MarcaId == grupo.MarcaId);
        }

        var carroceria = Enumeraciones.ParsearOpcional<Carroceria>(grupo.Carroceria);

        return carroceria is null ? 0 : stock.Count(u => u.Carroceria == carroceria);
    }

    /// <summary>Una unidad publicada, con lo justo para saber si encaja en una búsqueda.</summary>
    private sealed record UnidadPublicada(int ModeloId, int MarcaId, Carroceria Carroceria);

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
            .GroupBy(b => new
            {
                b.Filtros!.MarcaId,
                b.Filtros.ModeloId,
                b.Filtros.Carroceria,

                // El texto solo separa grupos cuando no se reconoció nada del catálogo: "hilux"
                // y "toyota hilux" son la misma demanda, "tesla" y "lada" no.
                Texto = b.Filtros.MarcaId is null && b.Filtros.ModeloId is null && b.Filtros.Carroceria is null
                    ? TextoLibre(b.Filtros.Texto)
                    : null,
            })
            .Select(g => new
            {
                g.Key.MarcaId,
                g.Key.ModeloId,
                g.Key.Carroceria,
                g.Key.Texto,
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
                Topes = g.Select(x => x.Filtros!).ToList(),
            })
            .Select(g => new
            {
                g.MarcaId,
                g.ModeloId,
                g.Carroceria,
                g.Texto,
                g.Veces,
                g.Sesiones,
                g.UltimaVez,
                g.AnioDesde,
                g.AnioHasta,
                g.PrecioDesde,
                g.PrecioHasta,
                g.Moneda,

                // Solo los topes de la moneda dominante: mezclar dólares con pesos daría
                // una mediana que no es plata de ninguna de las dos.
                PresupuestoTipico = Mediana(g.Topes
                    .Where(f => f.PrecioHasta is not null
                                && string.Equals(f.Moneda, g.Moneda, StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.PrecioHasta!.Value)
                    .ToList()),
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
            .Select(g =>
            {
                var modelo = g.ModeloId is null ? null : nombres.Modelos.GetValueOrDefault(g.ModeloId.Value);

                // Si la búsqueda nombró el modelo, la marca sale de él aunque el filtro no
                // la traiga: son el mismo dato dicho con menos precisión.
                var marcaId = g.MarcaId ?? modelo?.MarcaId;

                var marca = marcaId is null
                    ? null
                    : nombres.Marcas.GetValueOrDefault(marcaId.Value) ?? modelo?.Marca;

                return new BusquedaSinResultadoDto(
                marcaId,
                marca,
                g.ModeloId,
                modelo?.Nombre,
                g.Carroceria,
                g.AnioDesde,
                g.AnioHasta,
                g.Moneda,
                g.PrecioDesde,
                g.PrecioHasta,
                g.PresupuestoTipico,
                g.Veces,
                g.Sesiones,
                g.UltimaVez,
                g.Texto);
            })
            .ToList();
    }

    /// <summary>
    /// La mediana de una lista de importes, o <c>null</c> si no hay ninguno.
    /// </summary>
    private static decimal? Mediana(IReadOnlyList<decimal> valores)
    {
        if (valores.Count == 0)
        {
            return null;
        }

        var ordenados = valores.OrderBy(v => v).ToList();
        var medio = ordenados.Count / 2;

        return ordenados.Count % 2 == 1
            ? ordenados[medio]
            : Math.Round((ordenados[medio - 1] + ordenados[medio]) / 2m, 2);
    }

    private static string? TextoLibre(string? texto)
        => InterpreteDeBusqueda.Normalizar(texto) is { Length: > 0 } normalizado ? normalizado : null;

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
    private async Task<(Dictionary<int, string> Marcas, Dictionary<int, ModeloDelCatalogo> Modelos)>
        NombresDelCatalogoAsync(
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

        // El modelo se trae con su marca. El sitio público manda el modelo sin la marca
        // cuando el visitante eligió directamente el modelo, y un reporte que dice
        // "Corolla" a secas obliga a quien lo lee a saberse el catálogo de memoria.
        var nombresDeModelo = idsDeModelo.Count == 0
            ? []
            : await _db.Modelos
                .Where(m => idsDeModelo.Contains(m.Id))
                .Select(m => new ModeloDelCatalogo(m.Id, m.Nombre, m.MarcaId, m.Marca!.Nombre))
                .ToDictionaryAsync(m => m.Id, m => m, cancellationToken)
                .ConfigureAwait(false);

        return (nombresDeMarca, nombresDeModelo);
    }

    /// <summary>Un modelo del catálogo con su marca al lado.</summary>
    private sealed record ModeloDelCatalogo(int Id, string Nombre, int MarcaId, string Marca);

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

    /// <summary>
    /// El último precio de referencia de cada modelo y año presentes en el stock.
    /// </summary>
    /// <remarks>
    /// Se traen los snapshots de esos modelos y se elige el más reciente en memoria. Los
    /// modelos publicados son unas decenas y los snapshots uno por día, así que el volumen
    /// es chico; hacerlo con una consulta correlacionada por vehículo, en cambio, sería un
    /// viaje a la base por fila de la tabla.
    /// </remarks>
    private async Task<Dictionary<(int ModeloId, int Anio), PrecioDeMercado>> PreciosDeMercadoAsync(
        IReadOnlyList<Vehiculo> publicados,
        CancellationToken cancellationToken)
    {
        if (publicados.Count == 0)
        {
            return [];
        }

        var modelos = publicados.Select(v => v.ModeloId).Distinct().ToList();
        var anios = publicados.Select(v => v.Anio).Distinct().ToList();

        var snapshots = await _db.PreciosDeMercado
            .Where(p => modelos.Contains(p.ModeloId) && anios.Contains(p.Anio))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return snapshots
            .GroupBy(p => (p.ModeloId, p.Anio))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Fecha).First());
    }

    /// <summary>
    /// Las cotizaciones que hacen falta para comparar un aviso en pesos contra una
    /// referencia en dólares.
    /// </summary>
    /// <remarks>
    /// Se traen las del mes previo al snapshot más viejo y no solo las del día exacto: el
    /// job de cotizaciones puede no haber corrido un feriado, y perder la comparación de
    /// media flota por un día sin cotización sería tirar el dato por una razón que no tiene
    /// que ver con el dato.
    /// </remarks>
    private async Task<IReadOnlyList<Cotizacion>> CotizacionesAsync(
        IReadOnlyCollection<PrecioDeMercado> snapshots,
        CancellationToken cancellationToken)
    {
        if (snapshots.Count == 0)
        {
            return [];
        }

        var desde = snapshots.Min(p => p.Fecha).AddDays(-DiasDeToleranciaDeCotizacion);
        var hasta = snapshots.Max(p => p.Fecha);

        return await _db.Cotizaciones
            .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Pasa el precio de referencia a la moneda del aviso, con la cotización del día del
    /// snapshot.
    /// </summary>
    /// <remarks>
    /// La del día del snapshot y no la de hoy: los dos números tienen que quedar parados en
    /// la misma fecha. Convertir un precio de mercado de la semana pasada con la cotización
    /// de hoy mezcla la diferencia de precio con la del tipo de cambio, y el porcentaje que
    /// sale no es ninguna de las dos.
    /// <para>
    /// Sin cotización aplicable devuelve <c>null</c> y no hay comparación. Es preferible a
    /// convertir con un valor inventado: el número que falta se nota, el que está mal se
    /// cree.
    /// </para>
    /// </remarks>
    private static decimal? EnLaMonedaDelAviso(
        PrecioDeMercado referencia,
        Moneda monedaDelAviso,
        IReadOnlyList<Cotizacion> cotizaciones)
    {
        if (referencia.Moneda == monedaDelAviso)
        {
            return referencia.PrecioMediano;
        }

        var cotizacion = cotizaciones.FirstOrDefault(c => c.Fecha <= referencia.Fecha);

        if (cotizacion is null || cotizacion.UsdUyu <= 0m)
        {
            return null;
        }

        return monedaDelAviso == Moneda.Uyu
            ? Math.Round(referencia.PrecioMediano * cotizacion.UsdUyu, 2)
            : Math.Round(referencia.PrecioMediano / cotizacion.UsdUyu, 2);
    }

    private static DemandaDeVehiculoDto Fila(
        Vehiculo vehiculo,
        IReadOnlyDictionary<int, int> vistas,
        IReadOnlyDictionary<int, int> consultas,
        IReadOnlyDictionary<(int ModeloId, int Anio), PrecioDeMercado> mercado,
        IReadOnlyList<Cotizacion> cotizaciones,
        DateTime ahora)
    {
        var modelo = vehiculo.Modelo!;
        var vistasDelVehiculo = vistas.GetValueOrDefault(vehiculo.Id);
        var consultasDelVehiculo = consultas.GetValueOrDefault(vehiculo.Id);
        var enGondola = MapeosDeVehiculo.DiasEnGondola(vehiculo.FechaPublicacion, vehiculo.FechaVenta, ahora);

        // La referencia se expresa en la moneda del aviso: en Uruguay se publica en las dos
        // y una comparación que cruce monedas no significa nada. La conversión usa la
        // cotización del día del snapshot, no la de hoy.
        var referencia = mercado.GetValueOrDefault((vehiculo.ModeloId, vehiculo.Anio));

        var precioDeMercado = referencia is null
            ? null
            : EnLaMonedaDelAviso(referencia, vehiculo.Moneda, cotizaciones);

        return new DemandaDeVehiculoDto(
            vehiculo.Id,
            modelo.Marca!.Nombre,
            modelo.Nombre,
            vehiculo.Anio,
            vehiculo.Estado.ToString(),
            vehiculo.Precio,
            vehiculo.Moneda.ToString(),
            MapeosDeVehiculo.PortadaParaGrilla(vehiculo),
            enGondola,
            vistasDelVehiculo,
            consultasDelVehiculo,
            UmbralesDeDemanda.ConsultasPorCienVistas(vistasDelVehiculo, consultasDelVehiculo),
            UmbralesDeDemanda.Clasificar(vistasDelVehiculo, consultasDelVehiculo, enGondola).ToString(),
            precioDeMercado,
            UmbralesDeDemanda.DiferenciaContraElMercado(vehiculo.Precio, precioDeMercado),
            precioDeMercado is null ? null : referencia!.Fecha);
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
