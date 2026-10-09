using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Infrastructure.Planes;

/// <summary>
/// <see cref="IPoliticaDePlan"/> contra la base.
/// </summary>
/// <remarks>
/// Las consultas filtran por el tenant que se pide de forma explícita y saltean los filtros
/// globales, porque la misma política la usa el panel del tenant y el SuperAdmin, que no
/// tiene tenant resuelto. Solo lee: nunca escribe.
/// </remarks>
public sealed class PoliticaDePlanEnBase : IPoliticaDePlan
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;
    private readonly OpcionesDeCobranza _opciones;

    public PoliticaDePlanEnBase(AppDbContext db, TimeProvider reloj, IOptions<OpcionesDeCobranza> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _db = db;
        _reloj = reloj;
        _opciones = opciones.Value;
    }

    public async Task<SituacionDelPlan> SituacionAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var suscripcion = await _db.Suscripciones
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Fin == null, cancellationToken)
            .ConfigureAwait(false);

        var publicados = await _db.Vehiculos
            .IgnoreQueryFilters()
            .CountAsync(
                v => v.TenantId == tenantId
                     && (v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado),
                cancellationToken)
            .ConfigureAwait(false);

        var usuarios = await _db.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId && u.Activo, cancellationToken)
            .ConfigureAwait(false);

        var hoy = Hoy(_reloj);

        return new SituacionDelPlan(
            suscripcion?.Plan,
            suscripcion,
            CicloDeCobro.Evaluar(suscripcion?.PagaHasta, hoy, _opciones),
            suscripcion is null ? null : CicloDeCobro.DiasParaVencer(suscripcion.PagaHasta, hoy),
            publicados,
            usuarios);
    }

    /// <summary>
    /// El día de hoy para el ciclo de cobro. En UTC, como el resto de las fechas del
    /// sistema: tres horas de diferencia con Uruguay no cambian nada con diez días de gracia.
    /// </summary>
    public static DateOnly Hoy(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
    }
}
