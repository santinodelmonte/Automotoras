namespace AutomotoraSaaS.Core.Planes;

/// <summary>
/// En qué punto del ciclo de cobro está una automotora.
/// </summary>
/// <remarks>
/// Los valores numéricos son explícitos por la misma razón que en el resto de los enums:
/// pueden terminar persistidos (el registro de avisos los guarda).
/// </remarks>
public enum EstadoDeCobro
{
    /// <summary>Pago al día y con margen. Todo funciona.</summary>
    Vigente = 1,

    /// <summary>Faltan pocos días para el vencimiento. Aviso en el panel; el sitio sigue igual.</summary>
    PorVencer = 2,

    /// <summary>Vencido hace pocos días. Aviso más visible; el sitio público sigue arriba.</summary>
    Gracia = 3,

    /// <summary>Pasada la gracia, o sin suscripción. El sitio público muestra mantenimiento.</summary>
    Suspendido = 4,
}

/// <summary>Umbrales del ciclo de cobro. Van en configuración (<c>Cobranza:*</c>).</summary>
public sealed class OpcionesDeCobranza
{
    public const string Seccion = "Cobranza";

    /// <summary>Con cuántos días de anticipación se avisa el vencimiento.</summary>
    public int DiasDeAviso { get; set; } = 7;

    /// <summary>Cuántos días después de vencido el sitio sigue publicado.</summary>
    public int DiasDeGracia { get; set; } = 10;
}

/// <summary>
/// El cálculo del estado de cobro.
/// </summary>
/// <remarks>
/// Es una función pura sobre <c>PagaHasta</c> y la fecha de hoy, y no un campo que un job
/// va actualizando: un job que no corre deja el estado mintiendo, y esto no puede mentir.
/// Registrar un pago mueve <c>PagaHasta</c>, y con eso el estado cambia solo, sin pasos
/// manuales.
/// </remarks>
public static class CicloDeCobro
{
    /// <param name="pagaHasta">
    /// Último día cubierto de la suscripción vigente, inclusive. Nulo si no hay suscripción
    /// vigente, que se trata como suspendido: no existe una automotora publicada sin plan.
    /// </param>
    public static EstadoDeCobro Evaluar(DateOnly? pagaHasta, DateOnly hoy, OpcionesDeCobranza opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        if (pagaHasta is not { } cubierto)
        {
            return EstadoDeCobro.Suspendido;
        }

        var diasRestantes = DiasParaVencer(cubierto, hoy);

        return diasRestantes switch
        {
            // El día del vencimiento todavía está pago: es el último día de "por vencer".
            _ when diasRestantes > opciones.DiasDeAviso => EstadoDeCobro.Vigente,
            >= 0 => EstadoDeCobro.PorVencer,
            _ when -diasRestantes <= opciones.DiasDeGracia => EstadoDeCobro.Gracia,
            _ => EstadoDeCobro.Suspendido,
        };
    }

    /// <summary>
    /// Días que faltan hasta el último día pago. Cero es "vence hoy"; negativo, cuántos
    /// días lleva vencido.
    /// </summary>
    public static int DiasParaVencer(DateOnly pagaHasta, DateOnly hoy) => pagaHasta.DayNumber - hoy.DayNumber;

    /// <summary>El sitio público se apaga solo en suspensión. En gracia sigue arriba.</summary>
    public static bool SitioPublicado(EstadoDeCobro estado) => estado != EstadoDeCobro.Suspendido;
}
