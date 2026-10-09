using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Planes y estados de pago variados para las automotoras de desarrollo.
/// </summary>
/// <remarks>
/// Con todas en Full y al día, la pantalla de cobranza no muestra nada urgente, los topes
/// nunca se tocan y la página de mantenimiento no se ve nunca. Acá cada estado del ciclo
/// tiene al menos una automotora, con un historial de cobros creíble:
/// <list type="bullet">
///   <item><b>Norte</b> — Full, al día, con tres meses cobrados.</item>
///   <item><b>Sur</b> — Demanda, vence en cuatro días.</item>
///   <item><b>Costa</b> — Vidriera, vencida hace tres días (en gracia), sin reportes y con
///   los dos usuarios del plan ocupados.</item>
///   <item><b>Litoral</b> — Demanda, vencida hace veinte días: sitio en mantenimiento.</item>
/// </list>
/// Las demás quedan en Full al día, para que el benchmark siga teniendo muestra.
/// <para>
/// Se aplica una vez por automotora: los cobros que crea llevan una nota que lo marca, y
/// si ya está no se toca nada. Las fechas son relativas al día en que corre, así que con el
/// tiempo los estados avanzan solos, como en producción.
/// </para>
/// </remarks>
public static class SeedDeCobranza
{
    /// <summary>La nota de los cobros que crea el seed. Es también su marca de idempotencia.</summary>
    public const string Marca = "Dato de prueba del seed de desarrollo.";

    /// <summary>Vendedor que entra con contraseña provisoria, para probar el cambio obligatorio.</summary>
    public const string EmailConPasswordProvisoria = "nuevo@norte.uy";

    private static readonly (string Slug, string Plan, int DiasParaVencer, int MesesCobrados)[] Escenarios =
    [
        ("norte", CodigosDePlan.Full, 25, 3),
        ("sur", CodigosDePlan.Demanda, 4, 2),
        ("costa", CodigosDePlan.Vidriera, -3, 1),
        ("litoral", CodigosDePlan.Demanda, -20, 1),
    ];

    public static async Task EjecutarAsync(
        AppDbContext db,
        string hashDePassword,
        TimeProvider reloj,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(reloj);

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var planes = await db.Planes.ToDictionaryAsync(p => p.Codigo, cancellationToken).ConfigureAwait(false);

        foreach (var (slug, codigo, diasParaVencer, mesesCobrados) in Escenarios)
        {
            var tenantId = await db.Tenants
                .Where(t => t.Slug == slug)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (tenantId is not { } id)
            {
                continue;
            }

            var aplicado = await db.Pagos
                .IgnoreQueryFilters()
                .AnyAsync(p => p.TenantId == id && p.Nota == Marca, cancellationToken)
                .ConfigureAwait(false);

            if (!aplicado)
            {
                await AplicarAsync(db, id, planes[codigo], hoy, diasParaVencer, mesesCobrados, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await AsegurarUsuarioConPasswordProvisoriaAsync(db, hashDePassword, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AplicarAsync(
        AppDbContext db,
        int tenantId,
        Plan plan,
        DateOnly hoy,
        int diasParaVencer,
        int mesesCobrados,
        CancellationToken cancellationToken)
    {
        var pagaHasta = hoy.AddDays(diasParaVencer);

        // El período bonificado de los dos primeros meses termina justo antes de los meses
        // cobrados: la historia es "dos meses sin costo y después pagó N".
        var inicioCobrado = pagaHasta.AddDays(1).AddMonths(-mesesCobrados);
        var inicio = inicioCobrado.AddMonths(-Suscripciones.MesesBonificados);

        var vigente = await db.Suscripciones
            .IgnoreQueryFilters()
            .Include(s => s.Pagos)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Fin == null, cancellationToken)
            .ConfigureAwait(false);

        if (vigente is not null && vigente.PlanId != plan.Id)
        {
            // Cambio de plan, como lo haría el panel: se cierra la vigente y se abre otra.
            // En dos guardados, porque el índice único no admite dos vigentes ni un instante.
            vigente.Fin = hoy;
            vigente.MotivoDeBaja = $"Cambio al plan {plan.Nombre}.";
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            vigente = null;
        }

        if (vigente is null)
        {
            vigente = new Suscripcion { TenantId = tenantId, PlanId = plan.Id, Inicio = inicio, PagaHasta = pagaHasta };
            db.Suscripciones.Add(vigente);

            vigente.Pagos.Add(new Pago
            {
                TenantId = tenantId,
                Fecha = inicio,
                Monto = 0m,
                Moneda = plan.Moneda,
                PeriodoDesde = inicio,
                PeriodoHasta = inicioCobrado.AddDays(-1),
                Medio = Suscripciones.MedioBonificacion,
                Nota = $"{Suscripciones.MesesBonificados} primeros meses sin costo (condición de lanzamiento).",
            });
        }
        else
        {
            vigente.Inicio = inicio;
            vigente.PagaHasta = pagaHasta;

            // La bonificación que dejó la migración o el alta queda con las fechas de la
            // historia armada acá, para que el historial cierre.
            foreach (var bonificacion in vigente.Pagos.Where(p => p.Medio == Suscripciones.MedioBonificacion))
            {
                bonificacion.Fecha = inicio;
                bonificacion.PeriodoDesde = inicio;
                bonificacion.PeriodoHasta = inicioCobrado.AddDays(-1);
            }
        }

        for (var mes = 0; mes < mesesCobrados; mes++)
        {
            var desde = inicioCobrado.AddMonths(mes);

            vigente.Pagos.Add(new Pago
            {
                TenantId = tenantId,
                Fecha = desde.AddDays(-2),
                Monto = plan.PrecioMensual,
                Moneda = plan.Moneda,
                PeriodoDesde = desde,
                PeriodoHasta = desde.AddMonths(1).AddDays(-1),
                Medio = mes % 2 == 0 ? "Transferencia" : "Efectivo",
                Comprobante = mes % 2 == 0 ? $"TRF-{tenantId:D3}{mes + 1:D2}" : null,
                Nota = Marca,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AsegurarUsuarioConPasswordProvisoriaAsync(
        AppDbContext db,
        string hashDePassword,
        CancellationToken cancellationToken)
    {
        var norte = await db.Tenants
            .Where(t => t.Slug == "norte")
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var existe = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == EmailConPasswordProvisoria, cancellationToken)
            .ConfigureAwait(false);

        if (norte is null || existe)
        {
            return;
        }

        db.Users.Add(new User
        {
            TenantId = norte,
            Email = EmailConPasswordProvisoria,
            Nombre = "Vendedor Nuevo Norte",
            Rol = RolUsuario.Seller,
            PasswordHash = hashDePassword,
            DebeCambiarPassword = true,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
