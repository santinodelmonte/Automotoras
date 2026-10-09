using AutomotoraSaaS.Core.Entities;

namespace AutomotoraSaaS.Core.Planes;

/// <summary>
/// Reglas de alta de una suscripción.
/// </summary>
public static class Suscripciones
{
    /// <summary>
    /// "Los dos primeros meses de abono son sin costo, en cualquiera de los tres planes."
    /// </summary>
    public const int MesesBonificados = 2;

    public const string MedioBonificacion = "Bonificación";

    /// <summary>
    /// La primera suscripción de una automotora, con los meses bonificados ya registrados
    /// como un pago de monto cero.
    /// </summary>
    /// <remarks>
    /// La suscripción y su pago salen juntos: una suscripción con <c>PagaHasta</c> en el
    /// futuro y ningún pago que lo explique es exactamente el hueco que el registro de
    /// pagos existe para evitar.
    /// </remarks>
    public static Suscripcion IniciarConBonificacion(int tenantId, Plan plan, DateOnly inicio)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var pagaHasta = UltimoDiaCubierto(inicio, MesesBonificados);

        var suscripcion = new Suscripcion
        {
            TenantId = tenantId,
            PlanId = plan.Id,
            Inicio = inicio,
            PagaHasta = pagaHasta,
        };

        suscripcion.Pagos.Add(new Pago
        {
            TenantId = tenantId,
            Fecha = inicio,
            Monto = 0m,
            Moneda = plan.Moneda,
            PeriodoDesde = inicio,
            PeriodoHasta = pagaHasta,
            Medio = MedioBonificacion,
            Nota = $"{MesesBonificados} primeros meses sin costo (condición de lanzamiento).",
        });

        return suscripcion;
    }

    /// <summary>
    /// Último día que cubren <paramref name="meses"/> meses a partir de
    /// <paramref name="desde"/>, inclusive: del 8 de octubre, dos meses cubren hasta el
    /// 7 de diciembre.
    /// </summary>
    public static DateOnly UltimoDiaCubierto(DateOnly desde, int meses)
        => desde.AddMonths(meses).AddDays(-1);
}
