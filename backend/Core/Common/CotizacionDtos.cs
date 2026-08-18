using FluentValidation;

namespace AutomotoraSaaS.Core.Common;

/// <summary>Cuerpo de <c>POST /api/jobs/cotizaciones</c>.</summary>
/// <param name="Fecha">Día de la cotización.</param>
/// <param name="UsdUyu">Pesos uruguayos por dólar.</param>
/// <remarks>
/// El valor lo trae quien dispara el job, no lo sale a buscar la API. Es deliberado: la
/// aplicación corre en shared hosting IIS, donde una llamada saliente que se cuelga se
/// lleva puesto un hilo del app pool que atiende a todos los tenants. El cron externo ya
/// tiene que existir para disparar el job; que además traiga el número no le agrega
/// trabajo, y saca la dependencia de red del proceso web.
/// </remarks>
public sealed record RegistrarCotizacionRequest(DateOnly Fecha, decimal UsdUyu);

/// <summary>Cotización tal como quedó guardada.</summary>
public sealed record CotizacionDto(DateOnly Fecha, decimal UsdUyu);

public sealed class RegistrarCotizacionRequestValidator : AbstractValidator<RegistrarCotizacionRequest>
{
    public RegistrarCotizacionRequestValidator(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        RuleFor(x => x.UsdUyu)
            .GreaterThan(0m)
            .LessThan(1_000_000m)
            .WithMessage("La cotización no parece real.");

        // Un día de tolerancia por la diferencia de zona horaria entre el cron y el
        // servidor. Más que eso ya es un error de quien llama.
        RuleFor(x => x.Fecha)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime).AddDays(1))
            .WithMessage("La cotización no puede ser de una fecha futura.");
    }
}
