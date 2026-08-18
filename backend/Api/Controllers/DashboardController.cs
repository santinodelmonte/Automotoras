using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Dashboard;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Tablero del panel: estado del stock y demanda de los últimos treinta días.
/// </summary>
/// <remarks>
/// Solo el Owner. El vendedor carga vehículos y atiende consultas; los reportes y la
/// analítica son del dueño.
/// <para>
/// Es la primera lectura de la tabla de eventos, que se viene instrumentando desde el
/// paso 4a sin que existiera ningún reporte. Ese orden es a propósito: los datos de
/// demanda solo valen acumulados, y lo que no se midió no se recupera.
/// </para>
/// </remarks>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DashboardController : ControllerBase
{
    private const int DiasDeVentana = 30;
    private const int VehiculosEnElTop = 5;

    private static readonly TipoEvento[] EventosDeConsulta =
        [TipoEvento.ClickWhatsapp, TipoEvento.ClickTelefono];

    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;

    public DashboardController(AppDbContext db, TimeProvider reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Obtener(CancellationToken cancellationToken)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var desde = ahora.AddDays(-DiasDeVentana);

        var porEstado = await _db.Vehiculos
            .GroupBy(v => v.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vistas = await _db.Eventos
            .CountAsync(e => e.Tipo == TipoEvento.ViewFicha && e.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);

        var consultas = await _db.Eventos
            .CountAsync(e => EventosDeConsulta.Contains(e.Tipo) && e.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);

        var sinResultado = await _db.Busquedas
            .CountAsync(b => b.ResultadosCount == 0 && b.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);

        var masVistos = await MasVistosAsync(desde, cancellationToken).ConfigureAwait(false);

        // El promedio se calcula solo sobre lo que está publicado: meter los vendidos
        // mezclaría "cuánto tardo en vender" con "cuánto lleva esperando lo que tengo", y
        // la pregunta del tablero es la segunda.
        var publicaciones = await _db.Vehiculos
            .Where(v => v.Estado == EstadoVehiculo.Disponible)
            .Select(v => v.FechaPublicacion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var promedio = publicaciones.Count == 0
            ? 0
            : (int)Math.Round(publicaciones.Average(f => MapeosDeVehiculo.DiasEnGondola(f, null, ahora)));

        return Ok(new DashboardDto(
            porEstado
                .Select(g => new ConteoPorEstadoDto(g.Estado.ToString(), g.Cantidad))
                .OrderBy(c => c.Estado, StringComparer.Ordinal)
                .ToList(),
            porEstado.Sum(g => g.Cantidad),
            vistas,
            consultas,
            sinResultado,
            promedio,
            masVistos));
    }

    /// <summary>
    /// Los cinco más vistos, con sus consultas al lado. Las dos cifras juntas son las que
    /// dicen algo: muchas vistas y pocas consultas es la señal de que el precio está alto.
    /// </summary>
    private async Task<IReadOnlyList<VehiculoMasVistoDto>> MasVistosAsync(
        DateTime desde,
        CancellationToken cancellationToken)
    {
        var vistasPorVehiculo = await _db.Eventos
            .Where(e => e.Tipo == TipoEvento.ViewFicha && e.CreatedAt >= desde && e.VehiculoId != null)
            .GroupBy(e => e.VehiculoId!.Value)
            .Select(g => new { VehiculoId = g.Key, Vistas = g.Count() })
            .OrderByDescending(x => x.Vistas)
            .Take(VehiculosEnElTop)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (vistasPorVehiculo.Count == 0)
        {
            return [];
        }

        var ids = vistasPorVehiculo.Select(x => x.VehiculoId).ToList();

        var consultasPorVehiculo = await _db.Eventos
            .Where(e => EventosDeConsulta.Contains(e.Tipo)
                        && e.CreatedAt >= desde
                        && e.VehiculoId != null
                        && ids.Contains(e.VehiculoId.Value))
            .GroupBy(e => e.VehiculoId!.Value)
            .Select(g => new { VehiculoId = g.Key, Consultas = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vehiculos = await _db.Vehiculos
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Fotos)
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var consultas = consultasPorVehiculo.ToDictionary(x => x.VehiculoId, x => x.Consultas);
        var porId = vehiculos.ToDictionary(v => v.Id);

        return vistasPorVehiculo
            // Un vehículo borrado deja sus eventos con vehiculo_id en null, pero uno que
            // ya no está visible para este tenant sencillamente no vuelve de la consulta.
            .Where(x => porId.ContainsKey(x.VehiculoId))
            .Select(x =>
            {
                var vehiculo = porId[x.VehiculoId];
                var modelo = vehiculo.Modelo!;

                return new VehiculoMasVistoDto(
                    vehiculo.Id,
                    modelo.Marca!.Nombre,
                    modelo.Nombre,
                    vehiculo.Anio,
                    MapeosDeVehiculo.Portada(vehiculo)?.Url,
                    x.Vistas,
                    consultas.GetValueOrDefault(x.VehiculoId));
            })
            .ToList();
    }
}
