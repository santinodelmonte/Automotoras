using AutomotoraSaaS.Core.Enums;
using FluentValidation;

namespace AutomotoraSaaS.Core.Common;

/// <summary>Un relevamiento de precio de mercado para un modelo y año.</summary>
public sealed record PrecioRelevadoDto(
    int ModeloId,
    int Anio,
    DateOnly Fecha,
    string Moneda,
    decimal Promedio,
    decimal Minimo,
    decimal Maximo,
    int Muestras,
    string Fuente);

/// <summary>
/// Cuerpo de <c>POST /api/jobs/precios-referencia</c>.
/// </summary>
/// <remarks>
/// Los precios los trae quien dispara el job, no los sale a buscar la API. El brief pedía
/// consultar la API pública de MercadoLibre; hacerlo desde el proceso web sería un
/// problema en shared hosting IIS, donde una llamada saliente colgada se lleva un hilo del
/// app pool que atiende a todos los tenants —y este relevamiento son cientos de consultas,
/// no una—. El cron externo ya tiene que existir para disparar el job; que además consulte
/// MercadoLibre no le agrega complejidad y saca la dependencia de red del proceso que
/// atiende a los compradores.
/// </remarks>
public sealed record RegistrarPreciosReferenciaRequest(IReadOnlyList<PrecioRelevadoDto> Precios);

public sealed class PrecioRelevadoDtoValidator : AbstractValidator<PrecioRelevadoDto>
{
    public PrecioRelevadoDtoValidator(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        RuleFor(x => x.ModeloId).GreaterThan(0);

        RuleFor(x => x.Anio).InclusiveBetween(1950, reloj.GetUtcNow().Year + 1);

        RuleFor(x => x.Moneda)
            .Must(Enumeraciones.EsValido<Moneda>)
            .WithMessage("La moneda no es válida.");

        RuleFor(x => x.Fuente).NotEmpty().MaximumLength(40);

        RuleFor(x => x.Muestras)
            .GreaterThan(0)
            .WithMessage("Un relevamiento sin publicaciones detrás no es un precio de mercado.");

        RuleFor(x => x.Promedio).GreaterThan(0m);
        RuleFor(x => x.Minimo).GreaterThan(0m);

        RuleFor(x => x.Maximo)
            .GreaterThanOrEqualTo(x => x.Minimo)
            .WithMessage("El máximo no puede ser menor que el mínimo.");

        // El promedio tiene que caer adentro del rango. Si no, el relevamiento está mal
        // armado y guardarlo contamina la serie histórica para siempre.
        RuleFor(x => x.Promedio)
            .InclusiveBetween(x => x.Minimo, x => x.Maximo)
            .WithMessage("El promedio tiene que estar entre el mínimo y el máximo.");

        RuleFor(x => x.Fecha)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime).AddDays(1))
            .WithMessage("El relevamiento no puede ser de una fecha futura.");
    }
}

public sealed class RegistrarPreciosReferenciaRequestValidator
    : AbstractValidator<RegistrarPreciosReferenciaRequest>
{
    /// <summary>Tope por request. Un lote gigante en shared hosting es un timeout.</summary>
    public const int MaximoPorLote = 500;

    public RegistrarPreciosReferenciaRequestValidator(TimeProvider reloj)
    {
        RuleFor(x => x.Precios)
            .NotEmpty().WithMessage("Mandá al menos un precio.");

        RuleFor(x => x.Precios)
            .Must(p => p.Count <= MaximoPorLote)
            .When(x => x.Precios is not null)
            .WithMessage($"Mandá hasta {MaximoPorLote} precios por request.");

        RuleForEach(x => x.Precios).SetValidator(new PrecioRelevadoDtoValidator(reloj));
    }
}
