using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using FluentValidation;

namespace AutomotoraSaaS.Core.Catalogo;

public sealed class CrearSolicitudModeloRequestValidator : AbstractValidator<CrearSolicitudModeloRequest>
{
    public CrearSolicitudModeloRequestValidator()
    {
        RuleFor(x => x.MarcaId)
            .GreaterThan(0).WithMessage("Elegí una marca.");

        RuleFor(x => x.NombreModelo)
            .NotEmpty().WithMessage("El nombre del modelo es obligatorio.")
            .MaximumLength(80);

        RuleFor(x => x.Carroceria)
            .Must(Enumeraciones.EsValido<Carroceria>)
            .WithMessage("La carrocería no es válida.");
    }
}

public sealed class ResolverSolicitudRequestValidator : AbstractValidator<ResolverSolicitudRequest>
{
    public ResolverSolicitudRequestValidator()
    {
        RuleFor(x => x.Nota).MaximumLength(500);

        // Rechazar sin decir por qué deja al vendedor sin saber qué corregir, y la
        // solicitud vuelve tal cual la semana que viene.
        RuleFor(x => x.Nota)
            .NotEmpty()
            .When(x => !x.Aprobar)
            .WithMessage("Al rechazar hay que decir por qué.");
    }
}
