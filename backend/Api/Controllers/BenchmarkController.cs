using AutomotoraSaaS.Core.Analitica;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
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
/// Cómo le va a esta automotora comparada con el resto del mercado del SaaS.
/// </summary>
/// <remarks>
/// <b>Este es el único endpoint de tenant que lee datos de otros tenants.</b> Está en su
/// propio controller y no mezclado con los demás reportes justamente por eso: la frontera
/// de privacidad tiene que poder auditarse abriendo un archivo, no leyendo todo el
/// proyecto para ver dónde más aparece un <c>IgnoreQueryFilters</c>.
/// <para>
/// Lo que lo hace defendible son cuatro cosas, y las cuatro tienen que seguir siendo
/// ciertas: sale un solo número por métrica, ese número es una mediana entre automotoras,
/// hace falta un mínimo de automotoras aportando para publicarlo, y nunca viaja un nombre,
/// un id ni un extremo. Sin cualquiera de las cuatro, esto pasa a ser una filtración con
/// forma de reporte.
/// </para>
/// <para>
/// El agregado se arma sobre la mediana de cada automotora y no sobre todos los vehículos
/// juntos. Poniendo todo en la misma bolsa, la automotora con doscientas unidades define
/// el "mercado" y las demás se comparan contra ella; con la mediana entre automotoras, la
/// grande y la chica pesan lo mismo, que es lo que quiere saber quien pregunta.
/// </para>
/// </remarks>
[ApiController]
[Route("api/reportes/benchmark")]
[Authorize(Policy = Politicas.SoloOwner)]
[RequiereDelPlan(FuncionDelPlan.Benchmark)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class BenchmarkController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _reloj;

    public BenchmarkController(AppDbContext db, ITenantContext tenant, TimeProvider reloj)
    {
        _db = db;
        _tenant = tenant;
        _reloj = reloj;
    }

    [HttpGet]
    [ProducesResponseType(typeof(BenchmarkDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BenchmarkDto>> Obtener(
        [FromQuery] int? dias,
        CancellationToken cancellationToken)
    {
        var ventana = VentanaDeReporte.Normalizar(dias ?? ReglasDelBenchmark.DiasPorDefecto);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var desde = ahora.AddDays(-ventana);

        // El tenant sale del claim del token, como siempre. Que este endpoint lea de más
        // no cambia de dónde sale la identidad de quien pregunta.
        var propio = _tenant.TenantId;

        if (propio is null)
        {
            return Ok(NoDisponible(ventana, "No hay una automotora en la sesión."));
        }

        var gondolaPorTenant = await MedianaDeGondolaPorTenantAsync(ahora, cancellationToken)
            .ConfigureAwait(false);

        var pares = gondolaPorTenant.Keys.Where(id => id != propio.Value).ToList();

        if (pares.Count < ReglasDelBenchmark.AutomotorasMinimas)
        {
            return Ok(NoDisponible(
                ventana,
                $"Hacen falta al menos {ReglasDelBenchmark.AutomotorasMinimas} automotoras con stock " +
                "publicado para poder comparar sin exponer a ninguna."));
        }

        var ratioPorTenant = await RatioPorTenantAsync(desde, cancellationToken).ConfigureAwait(false);
        var ventaPorTenant = await DiasHastaLaVentaPorTenantAsync(desde, ahora, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new BenchmarkDto(
            ventana,
            Disponible: true,
            Motivo: null,
            pares.Count,
            Comparar(gondolaPorTenant, propio.Value, pares, mejorCuandoBaja: true),
            Comparar(ratioPorTenant, propio.Value, pares, mejorCuandoBaja: false),
            Comparar(ventaPorTenant, propio.Value, pares, mejorCuandoBaja: true)));
    }

    private static BenchmarkDto NoDisponible(int ventana, string motivo)
        => new(ventana, Disponible: false, motivo, AutomotorasEnLaMuestra: 0, null, null, null);

    /// <summary>
    /// Arma la comparación de una métrica: el valor propio y la mediana de los pares.
    /// </summary>
    /// <remarks>
    /// Los pares que no tienen el dato quedan afuera del cálculo en vez de contar como
    /// cero. Una automotora que no vendió nada en el período no tardó cero días en vender:
    /// no tiene el dato, y meterla como cero arrastraría la mediana del mercado hacia
    /// abajo hasta volverla mentira.
    /// </remarks>
    private static MetricaComparadaDto Comparar(
        IReadOnlyDictionary<int, decimal> porTenant,
        int propio,
        IReadOnlyList<int> pares,
        bool mejorCuandoBaja)
    {
        var deLosPares = pares
            .Where(porTenant.ContainsKey)
            .Select(id => porTenant[id])
            .ToList();

        // Si después de descartar a los que no tienen el dato queda una muestra chica, esa
        // métrica puntual no se publica aunque el resto del benchmark sí.
        var mercado = deLosPares.Count >= ReglasDelBenchmark.AutomotorasMinimas
            ? Mediana(deLosPares)
            : null;

        return new MetricaComparadaDto(
            porTenant.TryGetValue(propio, out var mio) ? mio : null,
            mercado,
            mejorCuandoBaja);
    }

    /// <summary>
    /// Mediana de días en góndola de lo publicado, por automotora.
    /// </summary>
    /// <remarks>
    /// Solo entran las automotoras activas y con un mínimo de unidades: una con un solo
    /// auto no describe una operación y es la más fácil de identificar, porque su mediana
    /// es su único vehículo.
    /// </remarks>
    private async Task<Dictionary<int, decimal>> MedianaDeGondolaPorTenantAsync(
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var publicados = await _db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.Tenant!.Activo)
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .Select(v => new { v.TenantId, v.FechaPublicacion })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return publicados
            .GroupBy(v => v.TenantId)
            .Where(g => g.Count() >= ReglasDelBenchmark.VehiculosMinimosParaAportar)
            .ToDictionary(
                g => g.Key,
                g => Mediana(g
                        .Select(v => (decimal)MapeosDeVehiculo.DiasEnGondola(v.FechaPublicacion, null, ahora))
                        .ToList())
                    ?? 0m);
    }

    /// <summary>Consultas cada cien vistas, por automotora.</summary>
    private async Task<Dictionary<int, decimal>> RatioPorTenantAsync(
        DateTime desde,
        CancellationToken cancellationToken)
    {
        var eventos = await _db.Eventos
            .IgnoreQueryFilters()
            .Where(e => e.CreatedAt >= desde)
            .Where(e => e.Tipo == TipoEvento.ViewFicha || EventosDeContacto.Tipos.Contains(e.Tipo))
            .GroupBy(e => new { e.TenantId, e.Tipo })
            .Select(g => new { g.Key.TenantId, g.Key.Tipo, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return eventos
            .GroupBy(e => e.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Vistas = g.Where(e => e.Tipo == TipoEvento.ViewFicha).Sum(e => e.Cantidad),
                Consultas = g.Where(e => EventosDeContacto.Tipos.Contains(e.Tipo)).Sum(e => e.Cantidad),
            })
            // Sin vistas no hay ratio, y un cero acá sería "nadie consulta" cuando en
            // realidad es "nadie entró todavía".
            .Where(x => x.Vistas > 0)
            .ToDictionary(
                x => x.TenantId,
                x => UmbralesDeDemanda.ConsultasPorCienVistas(x.Vistas, x.Consultas));
    }

    /// <summary>Promedio de días hasta vender, por automotora, sobre lo vendido en la ventana.</summary>
    private async Task<Dictionary<int, decimal>> DiasHastaLaVentaPorTenantAsync(
        DateTime desde,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var vendidos = await _db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.Tenant!.Activo)
            .Where(v => v.Estado == EstadoVehiculo.Vendido && v.FechaVenta != null && v.FechaVenta >= desde)
            .Select(v => new { v.TenantId, v.FechaPublicacion, v.FechaVenta })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return vendidos
            .GroupBy(v => v.TenantId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(
                    g.Average(v => (decimal)MapeosDeVehiculo.DiasEnGondola(v.FechaPublicacion, v.FechaVenta, ahora)),
                    1));
    }

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
            : Math.Round((ordenados[medio - 1] + ordenados[medio]) / 2m, 1);
    }
}
