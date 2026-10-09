using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Planes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Planes, suscripciones y cobros. Solo el SuperAdmin.
/// </summary>
/// <remarks>
/// El sistema no cobra: registra lo que se cobró afuera y sabe hasta cuándo está cubierta
/// cada automotora. Todo lo que escribe acá es de un tenant que no es el del request —el
/// SuperAdmin no tiene ninguno—, así que cada escritura pide el escape cross-tenant en la
/// línea donde hace falta.
/// </remarks>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Politicas.SoloSuperAdmin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AdminCobranzaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPoliticaDePlan _politica;
    private readonly TimeProvider _reloj;
    private readonly OpcionesDeCobranza _opciones;

    public AdminCobranzaController(
        AppDbContext db,
        IPoliticaDePlan politica,
        TimeProvider reloj,
        IOptions<OpcionesDeCobranza> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _db = db;
        _politica = politica;
        _reloj = reloj;
        _opciones = opciones.Value;
    }

    private DateOnly Hoy => PoliticaDePlanEnBase.Hoy(_reloj);

    // ---------------------------------------------------------------- Planes

    [HttpGet("planes")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> Planes(CancellationToken cancellationToken)
    {
        var planes = await _db.Planes
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ordenados en memoria: son tres o cuatro filas, y no todos los motores ordenan
        // por decimal (SQLite, el de los tests, no).
        return Ok(planes.OrderBy(p => p.PrecioMensual).Select(p => p.ADto()).ToList());
    }

    [HttpPost("planes")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanDto>> CrearPlan(GuardarPlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var codigo = request.Codigo.Trim();

        if (await _db.Planes.AnyAsync(p => p.Codigo == codigo, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto($"Ya hay un plan con el código '{codigo}'.");
        }

        var plan = new Plan { Codigo = codigo, Nombre = request.Nombre };
        request.Volcar(plan);

        _db.Planes.Add(plan);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(Planes), null, plan.ADto());
    }

    /// <remarks>
    /// Cambiar un precio o un tope afecta en el acto a todas las automotoras del plan. Es
    /// lo buscado: una promoción no puede exigir migrar suscripciones una por una.
    /// </remarks>
    [HttpPut("planes/{id:int}")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanDto>> ActualizarPlan(
        int id,
        GuardarPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return NoExiste($"No existe el plan {id}.");
        }

        // El código es lo que nombran el código fuente y las altas por API. Cambiarlo
        // rompería en silencio a quien lo use.
        if (!string.Equals(plan.Codigo, request.Codigo.Trim(), StringComparison.Ordinal))
        {
            return Conflicto("El código de un plan no se cambia. Creá un plan nuevo y desactivá este.");
        }

        request.Volcar(plan);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(plan.ADto());
    }

    // ---------------------------------------------------------------- Cobranza

    /// <summary>
    /// El tablero de cobranza: todas las automotoras, las más urgentes primero.
    /// </summary>
    /// <remarks>
    /// Lo primero que tiene que verse, sin filtrar ni buscar nada, es quién está suspendido,
    /// quién está en gracia y quién vence esta semana.
    /// </remarks>
    [HttpGet("cobranza")]
    [ProducesResponseType(typeof(IReadOnlyList<FilaDeCobranzaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FilaDeCobranzaDto>>> Cobranza(CancellationToken cancellationToken)
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vigentes = await _db.Suscripciones
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.Fin == null)
            .ToDictionaryAsync(s => s.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var publicados = await _db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .GroupBy(v => v.TenantId)
            .Select(g => new { TenantId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Cantidad, cancellationToken)
            .ConfigureAwait(false);

        var usuarios = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId != null && u.Activo)
            .GroupBy(u => u.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Cantidad, cancellationToken)
            .ConfigureAwait(false);

        var hoy = Hoy;

        var filas = tenants
            .Select(t =>
            {
                var suscripcion = vigentes.GetValueOrDefault(t.Id);
                var estado = CicloDeCobro.Evaluar(suscripcion?.PagaHasta, hoy, _opciones);

                return new FilaDeCobranzaDto(
                    t.Id,
                    t.Nombre,
                    t.Slug,
                    t.Activo,
                    suscripcion?.Plan?.Nombre,
                    suscripcion?.PagaHasta,
                    suscripcion is null ? null : CicloDeCobro.DiasParaVencer(suscripcion.PagaHasta, hoy),
                    estado.ToString(),
                    new UsoDto(publicados.GetValueOrDefault(t.Id), suscripcion?.Plan?.MaxVehiculos),
                    new UsoDto(usuarios.GetValueOrDefault(t.Id), suscripcion?.Plan?.MaxUsuarios));
            })
            .OrderBy(f => Urgencia(f.Estado))
            .ThenBy(f => f.DiasParaVencer ?? int.MinValue)
            .ThenBy(f => f.Nombre, StringComparer.CurrentCulture)
            .ToList();

        return Ok(filas);
    }

    [HttpGet("tenants/{id:int}/suscripcion")]
    [ProducesResponseType(typeof(SuscripcionDeTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SuscripcionDeTenantDto>> Suscripcion(int id, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExisteTenant(id);
        }

        return Ok(await ArmarAsync(tenant, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Asigna un plan o cambia de plan.
    /// </summary>
    /// <remarks>
    /// Cerrar la vigente y abrir otra, y no pisar el plan: el historial tiene que poder decir
    /// en qué plan estaba la automotora en marzo. Lo ya pagado se respeta, así que la nueva
    /// arranca con el mismo <c>PagaHasta</c>; la diferencia de precio, si la hay, se cobra
    /// afuera como cualquier otro cobro.
    /// <para>
    /// Sin suscripción vigente —una automotora que se había dado de baja y vuelve—, la nueva
    /// arranca sin nada pago: el sitio vuelve en gracia hasta que se registre el cobro.
    /// </para>
    /// </remarks>
    [HttpPost("tenants/{id:int}/suscripcion")]
    [ProducesResponseType(typeof(SuscripcionDeTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuscripcionDeTenantDto>> CambiarPlan(
        int id,
        CambiarPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExisteTenant(id);
        }

        var codigo = request.Plan.Trim().ToLowerInvariant();
        var plan = await _db.Planes
            .FirstOrDefaultAsync(p => p.Codigo == codigo && p.Activo, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            return Conflicto($"No hay ningún plan disponible con el código '{codigo}'.");
        }

        var vigente = await VigenteAsync(id, cancellationToken).ConfigureAwait(false);

        if (vigente?.PlanId == plan.Id)
        {
            return Conflicto($"{tenant.Nombre} ya está en el plan {plan.Nombre}.");
        }

        var hoy = Hoy;

        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            // Dos guardados en una transacción: el índice único no admite que en ningún
            // momento convivan dos vigentes, y EF no garantiza el orden entre el UPDATE de
            // la vieja y el INSERT de la nueva si van en el mismo SaveChanges.
            var transaccion = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await using (transaccion.ConfigureAwait(false))
            {
                if (vigente is not null)
                {
                    vigente.Fin = hoy;
                    vigente.MotivoDeBaja = $"Cambio al plan {plan.Nombre}.";
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                _db.Suscripciones.Add(new Suscripcion
                {
                    TenantId = id,
                    PlanId = plan.Id,
                    Inicio = hoy,
                    PagaHasta = vigente?.PagaHasta ?? hoy.AddDays(-1),
                });

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaccion.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return Ok(await ArmarAsync(tenant, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Registra un cobro y empuja el vencimiento.
    /// </summary>
    /// <remarks>
    /// Es lo único que mueve <c>PagaHasta</c>, y con eso reactiva en el acto a una
    /// automotora suspendida: el estado se calcula, no se guarda.
    /// </remarks>
    [HttpPost("tenants/{id:int}/pagos")]
    [ProducesResponseType(typeof(SuscripcionDeTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuscripcionDeTenantDto>> RegistrarPago(
        int id,
        RegistrarPagoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExisteTenant(id);
        }

        var vigente = await VigenteAsync(id, cancellationToken).ConfigureAwait(false);

        if (vigente is null)
        {
            return Conflicto($"{tenant.Nombre} no tiene un plan vigente. Asignale uno antes de registrar el cobro.");
        }

        var desde = request.PeriodoDesde ?? vigente.PagaHasta.AddDays(1);

        if (request.PeriodoHasta < desde)
        {
            return Conflicto(
                $"El período tiene que terminar después del {desde:dd/MM/yyyy}, que es desde cuando cubre.");
        }

        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            _db.Pagos.Add(new Pago
            {
                TenantId = id,
                SuscripcionId = vigente.Id,
                Fecha = request.Fecha,
                Monto = request.Monto,
                Moneda = request.Moneda is null ? vigente.Plan!.Moneda : Enumeraciones.Parsear<Moneda>(request.Moneda),
                PeriodoDesde = desde,
                PeriodoHasta = request.PeriodoHasta,
                Medio = request.Medio.Trim(),
                Comprobante = Limpio(request.Comprobante),
                Nota = Limpio(request.Nota),
            });

            // Nunca hacia atrás: registrar tarde un pago de un período viejo no puede
            // acortar la cobertura que ya estaba paga.
            if (request.PeriodoHasta > vigente.PagaHasta)
            {
                vigente.PagaHasta = request.PeriodoHasta;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Ok(await ArmarAsync(tenant, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Cierra la suscripción vigente.
    /// </summary>
    /// <remarks>
    /// No se borra nada y no se apaga el panel: sin suscripción el sitio público queda en
    /// mantenimiento, pero el dueño puede seguir entrando para exportar sus datos, que es
    /// lo que la propuesta promete ante una baja.
    /// </remarks>
    [HttpPost("tenants/{id:int}/baja")]
    [ProducesResponseType(typeof(SuscripcionDeTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuscripcionDeTenantDto>> DarDeBaja(
        int id,
        DarDeBajaRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExisteTenant(id);
        }

        var vigente = await VigenteAsync(id, cancellationToken).ConfigureAwait(false);

        if (vigente is null)
        {
            return Conflicto($"{tenant.Nombre} no tiene una suscripción vigente.");
        }

        // Una baja con fecha futura dejaría una suscripción "cerrada" que en realidad
        // sigue corriendo. El aviso de 30 días se respeta registrando la baja cuando se
        // hace efectiva.
        if (request.Fecha > Hoy || request.Fecha < vigente.Inicio)
        {
            return Conflicto(
                $"La fecha de baja tiene que estar entre el {vigente.Inicio:dd/MM/yyyy} y hoy.");
        }

        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            vigente.Fin = request.Fecha;
            vigente.MotivoDeBaja = request.Motivo.Trim();
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Ok(await ArmarAsync(tenant, cancellationToken).ConfigureAwait(false));
    }

    private Task<Suscripcion?> VigenteAsync(int tenantId, CancellationToken cancellationToken)
        => _db.Suscripciones
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Fin == null, cancellationToken);

    private async Task<SuscripcionDeTenantDto> ArmarAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var situacion = await _politica.SituacionAsync(tenant.Id, cancellationToken).ConfigureAwait(false);

        var historial = await _db.Suscripciones
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenant.Id)
            .OrderByDescending(s => s.Inicio)
            .ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pagos = await _db.Pagos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TenantId == tenant.Id)
            .OrderByDescending(p => p.Fecha)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SuscripcionDeTenantDto(
            tenant.Id,
            tenant.Nombre,
            situacion.ADto(),
            historial.FirstOrDefault(s => s.Fin == null)?.ADto(),
            historial.Select(s => s.ADto()).ToList(),
            pagos.Select(p => p.ADto()).ToList());
    }

    private static int Urgencia(string estado) => Enum.Parse<EstadoDeCobro>(estado) switch
    {
        EstadoDeCobro.Suspendido => 0,
        EstadoDeCobro.Gracia => 1,
        EstadoDeCobro.PorVencer => 2,
        _ => 3,
    };

    private static string? Limpio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private ActionResult Conflicto(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoExiste(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status404NotFound);

    private ActionResult NoExisteTenant(int id) => NoExiste($"No existe la automotora {id}.");
}
