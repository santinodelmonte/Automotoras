using System.Globalization;
using System.Text;
using System.Text.Json;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Reportes de demanda: qué se mira, qué se consulta y qué se busca sin encontrar.
/// </summary>
/// <remarks>
/// Este es el producto. El catálogo lo tiene cualquiera; lo que no tiene nadie es la
/// respuesta a "qué conviene comprar", y esa sale de cruzar lo que la gente miró con lo
/// que preguntó y con lo que buscó y no estaba.
/// <para>
/// Todo lo que hay acá se apoya en eventos que se vienen registrando desde antes de que
/// existiera un solo reporte. Ese orden no fue casualidad: los datos de demanda solo valen
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
    private const int DiasPorDefecto = 30;
    private const int DiasMaximos = 365;
    private const int CombinacionesDeDemanda = 15;

    private static readonly TipoEvento[] EventosDeConsulta =
        [TipoEvento.ClickWhatsapp, TipoEvento.ClickTelefono];

    private static readonly JsonSerializerOptions LecturaDeFiltros = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;

    public ReportesController(AppDbContext db, TimeProvider reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    /// <summary>
    /// El reporte de demanda del período pedido.
    /// </summary>
    /// <param name="dias">Ventana de análisis. Por defecto 30, tope un año.</param>
    [HttpGet("demanda")]
    [ProducesResponseType(typeof(ReporteDeDemandaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteDeDemandaDto>> Demanda(
        [FromQuery] int dias,
        CancellationToken cancellationToken)
    {
        var ventana = Math.Clamp(dias <= 0 ? DiasPorDefecto : dias, 1, DiasMaximos);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var desde = ahora.AddDays(-ventana);

        var vehiculos = await AnalizarGondolaAsync(desde, ahora, cancellationToken).ConfigureAwait(false);
        var insatisfecha = await AnalizarDemandaInsatisfechaAsync(desde, cancellationToken).ConfigureAwait(false);

        return Ok(new ReporteDeDemandaDto(
            ventana,
            vehiculos.Sum(v => v.Vistas),
            vehiculos.Sum(v => v.Consultas),
            vehiculos,
            insatisfecha));
    }

    /// <summary>
    /// Cada unidad publicada con sus vistas, sus consultas y la señal que sale de cruzarlas.
    /// </summary>
    /// <remarks>
    /// Solo lo que está a la venta. Un vehículo vendido ya no tiene decisión pendiente, y
    /// mezclarlo correría el promedio de todos hacia abajo.
    /// </remarks>
    private async Task<IReadOnlyList<VehiculoEnGondolaDto>> AnalizarGondolaAsync(
        DateTime desde,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var publicados = await _db.Vehiculos
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Version)
            .Include(v => v.Fotos)
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (publicados.Count == 0)
        {
            return [];
        }

        var conteos = await _db.Eventos
            .Where(e => e.CreatedAt >= desde && e.VehiculoId != null)
            .GroupBy(e => new { VehiculoId = e.VehiculoId!.Value, e.Tipo })
            .Select(g => new { g.Key.VehiculoId, g.Key.Tipo, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vistas = conteos
            .Where(c => c.Tipo == TipoEvento.ViewFicha)
            .ToDictionary(c => c.VehiculoId, c => c.Cantidad);

        var consultas = conteos
            .Where(c => EventosDeConsulta.Contains(c.Tipo))
            .GroupBy(c => c.VehiculoId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Cantidad));

        return publicados
            .Select(vehiculo =>
            {
                var modelo = vehiculo.Modelo!;
                var mirado = vistas.GetValueOrDefault(vehiculo.Id);
                var preguntado = consultas.GetValueOrDefault(vehiculo.Id);
                var enGondola = MapeosDeVehiculo.DiasEnGondola(vehiculo.FechaPublicacion, null, ahora);
                var ratio = mirado == 0 ? 0 : Math.Round(preguntado * 100d / mirado, 1);
                var senal = Clasificar(mirado, ratio, enGondola);

                return new VehiculoEnGondolaDto(
                    vehiculo.Id,
                    modelo.Marca!.Nombre,
                    modelo.Nombre,
                    vehiculo.Version?.Nombre,
                    vehiculo.Anio,
                    vehiculo.Precio,
                    vehiculo.Moneda.ToString(),
                    vehiculo.Estado.ToString(),
                    MapeosDeVehiculo.Portada(vehiculo)?.Url,
                    enGondola,
                    mirado,
                    preguntado,
                    ratio,
                    senal.ToString(),
                    Explicar(senal, mirado, preguntado, enGondola));
            })
            // Lo que necesita decisión primero: las señales, y dentro de ellas lo que lleva
            // más tiempo parado.
            .OrderBy(v => Prioridad(v.Senal))
            .ThenByDescending(v => v.DiasEnGondola)
            .ToList();
    }

    /// <summary>
    /// Agrupa las búsquedas que no encontraron nada por la combinación de filtros pedida.
    /// </summary>
    /// <remarks>
    /// Los filtros se guardaron como JSON y se agrupan en memoria. Es a propósito: la
    /// alternativa es que cada base los sepa consultar por adentro, y eso ata la analítica
    /// —que es el producto— al motor. Con el volumen de una automotora, la diferencia no
    /// se nota.
    /// </remarks>
    private async Task<IReadOnlyList<DemandaInsatisfechaDto>> AnalizarDemandaInsatisfechaAsync(
        DateTime desde,
        CancellationToken cancellationToken)
    {
        var sinResultado = await _db.Busquedas
            .Where(b => b.ResultadosCount == 0 && b.CreatedAt >= desde)
            .Select(b => new { b.Filtros, b.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sinResultado.Count == 0)
        {
            return [];
        }

        var pedidos = sinResultado
            .Select(b => new { Filtros = Leer(b.Filtros), b.CreatedAt })
            .Where(b => b.Filtros is not null)
            .ToList();

        var marcas = await _db.Marcas
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken)
            .ConfigureAwait(false);

        var modelos = await _db.Modelos
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken)
            .ConfigureAwait(false);

        return pedidos
            .GroupBy(p => new
            {
                p.Filtros!.MarcaId,
                p.Filtros.ModeloId,
                p.Filtros.Carroceria,
                p.Filtros.Combustible,
                p.Filtros.Transmision,
                p.Filtros.AnioDesde,
                p.Filtros.PrecioHasta,
                p.Filtros.Moneda,
            })
            .Select(g =>
            {
                var marca = g.Key.MarcaId is { } id ? marcas.GetValueOrDefault(id) : null;
                var modelo = g.Key.ModeloId is { } modeloId ? modelos.GetValueOrDefault(modeloId) : null;

                return new DemandaInsatisfechaDto(
                    marca,
                    modelo,
                    g.Key.Carroceria,
                    g.Key.Combustible,
                    g.Key.Transmision,
                    g.Key.AnioDesde,
                    g.Key.PrecioHasta,
                    g.Key.Moneda,
                    g.Count(),
                    g.Max(p => p.CreatedAt),
                    Describir(marca, modelo, g.Key.Carroceria, g.Key.Combustible, g.Key.Transmision, g.Key.AnioDesde, g.Key.PrecioHasta, g.Key.Moneda));
            })
            .OrderByDescending(d => d.Veces)
            .ThenByDescending(d => d.UltimaVez)
            .Take(CombinacionesDeDemanda)
            .ToList();
    }

    private static FiltrosRegistrados? Leer(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<FiltrosRegistrados>(json, LecturaDeFiltros);
        }
        catch (JsonException)
        {
            // Una fila con JSON viejo o corrupto se saltea: un reporte incompleto es mucho
            // mejor que un reporte que no se puede abrir.
            return null;
        }
    }

    private static SenalDeDemanda Clasificar(int vistas, double consultasPorCienVistas, int diasEnGondola)
    {
        if (diasEnGondola >= UmbralesDeDemanda.DiasParaEsperarInteres
            && vistas < UmbralesDeDemanda.VistasQueIndicanFaltaDeInteres)
        {
            return SenalDeDemanda.SinInteres;
        }

        if (vistas < UmbralesDeDemanda.VistasMinimasParaConcluir)
        {
            return SenalDeDemanda.PocosDatos;
        }

        return consultasPorCienVistas < UmbralesDeDemanda.ConsultasPorCienVistasBajas
            ? SenalDeDemanda.PrecioAlto
            : SenalDeDemanda.Normal;
    }

    private static string Explicar(SenalDeDemanda senal, int vistas, int consultas, int dias) => senal switch
    {
        SenalDeDemanda.SinInteres =>
            $"Lleva {dias} días publicada y solo {vistas} visitas. El problema es que no la están viendo: "
            + "revisá las fotos, el título y si el modelo tiene demanda.",

        SenalDeDemanda.PrecioAlto =>
            $"{vistas} personas la miraron y solo {consultas} consultaron. Cuando miran y no preguntan, "
            + "casi siempre es el precio.",

        SenalDeDemanda.Normal =>
            $"{consultas} consultas sobre {vistas} visitas. La proporción es la esperable.",

        _ => $"Todavía son {vistas} visitas: hacen falta al menos "
             + $"{UmbralesDeDemanda.VistasMinimasParaConcluir} para poder decir algo.",
    };

    private static int Prioridad(string senal) => senal switch
    {
        nameof(SenalDeDemanda.PrecioAlto) => 0,
        nameof(SenalDeDemanda.SinInteres) => 1,
        nameof(SenalDeDemanda.Normal) => 2,
        _ => 3,
    };

    /// <summary>Arma la frase de lo que buscaron, para no obligar a leer una tabla de filtros.</summary>
    private static string Describir(
        string? marca,
        string? modelo,
        string? carroceria,
        string? combustible,
        string? transmision,
        int? anioDesde,
        decimal? precioHasta,
        string? moneda)
    {
        var frase = new StringBuilder();

        frase.Append(marca is null && modelo is null && carroceria is null ? "Cualquier vehículo" : string.Empty);

        if (marca is not null) frase.Append(marca);
        if (modelo is not null) frase.Append(frase.Length > 0 ? " " : string.Empty).Append(modelo);
        if (marca is null && modelo is null && carroceria is not null) frase.Append(carroceria);
        else if (carroceria is not null) frase.Append(" (").Append(carroceria).Append(')');

        if (combustible is not null) frase.Append(", ").Append(combustible.ToLowerInvariant());
        if (transmision is not null) frase.Append(", ").Append(transmision.ToLowerInvariant());
        if (anioDesde is { } anio) frase.Append(CultureInfo.InvariantCulture, $", del {anio} en adelante");

        if (precioHasta is { } tope)
        {
            var simbolo = string.Equals(moneda, nameof(Moneda.Usd), StringComparison.OrdinalIgnoreCase)
                ? "US$"
                : "$";

            frase.Append(CultureInfo.InvariantCulture, $", hasta {simbolo} {tope:N0}");
        }

        return frase.ToString();
    }
}
