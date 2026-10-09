using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using FluentValidation;

namespace AutomotoraSaaS.Core.Planes;

public sealed record PlanDto(
    int Id,
    string Codigo,
    string Nombre,
    decimal PrecioMensual,
    string Moneda,
    int? MaxVehiculos,
    int? MaxUsuarios,
    bool IncluyeReportes,
    bool IncluyeBenchmark,
    bool IncluyeDominioPropio,
    int HorasSoporteMes,
    bool Activo);

/// <summary>Alta o edición de un plan. El código no cambia una vez creado.</summary>
public sealed record GuardarPlanRequest(
    string Codigo,
    string Nombre,
    decimal PrecioMensual,
    string Moneda,
    int? MaxVehiculos,
    int? MaxUsuarios,
    bool IncluyeReportes,
    bool IncluyeBenchmark,
    bool IncluyeDominioPropio,
    int HorasSoporteMes,
    bool Activo);

public sealed record SuscripcionDto(
    int Id,
    PlanDto Plan,
    DateOnly Inicio,
    DateOnly? Fin,
    DateOnly PagaHasta,
    string? MotivoDeBaja);

public sealed record PagoDto(
    int Id,
    int SuscripcionId,
    DateOnly Fecha,
    decimal Monto,
    string Moneda,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    string Medio,
    string? Comprobante,
    string? Nota);

/// <summary>Cuánto se usa de un tope. <c>Tope</c> nulo es sin límite.</summary>
public sealed record UsoDto(int Usados, int? Tope)
{
    /// <summary>Si ya pasó el umbral de aviso. Sin tope, nunca.</summary>
    public bool CercaDelTope => Tope is { } tope && tope > 0 && Usados >= tope * SituacionDelPlan.UmbralDeAviso;
}

/// <summary>
/// El plan de una automotora visto desde afuera: el que paga, el estado del pago y cuánto
/// está usando. Lo usan el panel del Owner y el tablero de cobranza.
/// </summary>
public sealed record SituacionDelPlanDto(
    PlanDto? Plan,
    string Estado,
    DateOnly? PagaHasta,
    int? DiasParaVencer,
    UsoDto Vehiculos,
    UsoDto Usuarios);

/// <summary>Todo lo de cobranza de una automotora, para el SuperAdmin.</summary>
public sealed record SuscripcionDeTenantDto(
    int TenantId,
    string Nombre,
    SituacionDelPlanDto Situacion,
    SuscripcionDto? Vigente,
    IReadOnlyList<SuscripcionDto> Historial,
    IReadOnlyList<PagoDto> Pagos);

/// <summary>Una fila del tablero de cobranza.</summary>
public sealed record FilaDeCobranzaDto(
    int TenantId,
    string Nombre,
    string Slug,
    bool Activo,
    string? Plan,
    DateOnly? PagaHasta,
    int? DiasParaVencer,
    string Estado,
    UsoDto Vehiculos,
    UsoDto Usuarios);

/// <summary>Asignar un plan o cambiar de plan.</summary>
public sealed record CambiarPlanRequest(string Plan);

/// <summary>
/// Un cobro. Si no se dice desde cuándo cubre, cubre desde el día siguiente al último pago:
/// es el caso normal de pagar el mes que viene.
/// </summary>
public sealed record RegistrarPagoRequest(
    DateOnly Fecha,
    decimal Monto,
    DateOnly? PeriodoDesde,
    DateOnly PeriodoHasta,
    string Medio,
    string? Moneda = null,
    string? Comprobante = null,
    string? Nota = null);

/// <summary>Baja de la suscripción vigente. Los datos no se tocan.</summary>
public sealed record DarDeBajaRequest(DateOnly Fecha, string Motivo);

public static class MapeosDePlan
{
    public static PlanDto ADto(this Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new PlanDto(
            plan.Id,
            plan.Codigo,
            plan.Nombre,
            plan.PrecioMensual,
            plan.Moneda.ToString(),
            plan.MaxVehiculos,
            plan.MaxUsuarios,
            plan.IncluyeReportes,
            plan.IncluyeBenchmark,
            plan.IncluyeDominioPropio,
            plan.HorasSoporteMes,
            plan.Activo);
    }

    public static SuscripcionDto ADto(this Suscripcion suscripcion)
    {
        ArgumentNullException.ThrowIfNull(suscripcion);

        return new SuscripcionDto(
            suscripcion.Id,
            suscripcion.Plan!.ADto(),
            suscripcion.Inicio,
            suscripcion.Fin,
            suscripcion.PagaHasta,
            suscripcion.MotivoDeBaja);
    }

    public static PagoDto ADto(this Pago pago)
    {
        ArgumentNullException.ThrowIfNull(pago);

        return new PagoDto(
            pago.Id,
            pago.SuscripcionId,
            pago.Fecha,
            pago.Monto,
            pago.Moneda.ToString(),
            pago.PeriodoDesde,
            pago.PeriodoHasta,
            pago.Medio,
            pago.Comprobante,
            pago.Nota);
    }

    public static SituacionDelPlanDto ADto(this SituacionDelPlan situacion)
    {
        ArgumentNullException.ThrowIfNull(situacion);

        return new SituacionDelPlanDto(
            situacion.Plan?.ADto(),
            situacion.Estado.ToString(),
            situacion.Suscripcion?.PagaHasta,
            situacion.DiasParaVencer,
            new UsoDto(situacion.VehiculosPublicados, situacion.Plan?.MaxVehiculos),
            new UsoDto(situacion.UsuariosActivos, situacion.Plan?.MaxUsuarios));
    }

    public static void Volcar(this GuardarPlanRequest request, Plan plan)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);

        plan.Nombre = request.Nombre.Trim();
        plan.PrecioMensual = request.PrecioMensual;
        plan.Moneda = Enumeraciones.Parsear<Moneda>(request.Moneda);
        plan.MaxVehiculos = request.MaxVehiculos;
        plan.MaxUsuarios = request.MaxUsuarios;
        plan.IncluyeReportes = request.IncluyeReportes;
        plan.IncluyeBenchmark = request.IncluyeBenchmark;
        plan.IncluyeDominioPropio = request.IncluyeDominioPropio;
        plan.HorasSoporteMes = request.HorasSoporteMes;
        plan.Activo = request.Activo;
    }
}

public sealed class GuardarPlanRequestValidator : AbstractValidator<GuardarPlanRequest>
{
    public GuardarPlanRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().MaximumLength(30)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("El código va en minúsculas, con números y guiones.");

        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
        RuleFor(x => x.PrecioMensual).GreaterThanOrEqualTo(0).LessThan(10_000_000);

        RuleFor(x => x.Moneda)
            .Must(Enumeraciones.EsValido<Moneda>)
            .WithMessage("La moneda no es válida.");

        RuleFor(x => x.MaxVehiculos).GreaterThan(0).When(x => x.MaxVehiculos is not null);
        RuleFor(x => x.MaxUsuarios).GreaterThan(0).When(x => x.MaxUsuarios is not null);
        RuleFor(x => x.HorasSoporteMes).InclusiveBetween(0, 200);
    }
}

public sealed class CambiarPlanRequestValidator : AbstractValidator<CambiarPlanRequest>
{
    public CambiarPlanRequestValidator()
    {
        RuleFor(x => x.Plan).NotEmpty().MaximumLength(30);
    }
}

public sealed class RegistrarPagoRequestValidator : AbstractValidator<RegistrarPagoRequest>
{
    public RegistrarPagoRequestValidator()
    {
        RuleFor(x => x.Monto).GreaterThanOrEqualTo(0).LessThan(100_000_000);

        RuleFor(x => x.PeriodoHasta)
            .GreaterThanOrEqualTo(x => x.PeriodoDesde!.Value)
            .When(x => x.PeriodoDesde is not null)
            .WithMessage("El período no puede terminar antes de empezar.");

        RuleFor(x => x.Medio).NotEmpty().MaximumLength(60);

        RuleFor(x => x.Moneda)
            .Must(m => Enumeraciones.EsValido<Moneda>(m!))
            .When(x => x.Moneda is not null)
            .WithMessage("La moneda no es válida.");

        RuleFor(x => x.Comprobante).MaximumLength(120);
        RuleFor(x => x.Nota).MaximumLength(500);
    }
}

public sealed class DarDeBajaRequestValidator : AbstractValidator<DarDeBajaRequest>
{
    public DarDeBajaRequestValidator()
    {
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}
