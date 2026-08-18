using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using FluentValidation;

namespace AutomotoraSaaS.Core.Publico;

/// <summary>
/// Un evento de comportamiento reportado por el sitio público.
/// </summary>
/// <param name="Tipo">Nombre del <c>TipoEvento</c>: <c>ViewFicha</c>, <c>ClickWhatsapp</c>, …</param>
/// <param name="VehiculoId">El vehículo involucrado, cuando el evento es sobre uno.</param>
/// <param name="SessionId">
/// Identificador de la visita, generado y guardado por el cliente. Permite agrupar la
/// actividad de una misma persona sin saber quién es.
/// </param>
/// <remarks>
/// El tenant no viaja en el cuerpo: lo resuelve el servidor desde el dominio o el slug.
/// Si viajara, cualquiera podría inflarle las métricas a la automotora que quisiera.
/// </remarks>
public sealed record RegistrarEventoRequest(string Tipo, int? VehiculoId, string? SessionId);

public sealed class RegistrarEventoRequestValidator : AbstractValidator<RegistrarEventoRequest>
{
    public RegistrarEventoRequestValidator()
    {
        RuleFor(x => x.Tipo)
            .Must(Enumeraciones.EsValido<TipoEvento>)
            .WithMessage("El tipo de evento no es válido.");

        RuleFor(x => x.VehiculoId)
            .GreaterThan(0)
            .When(x => x.VehiculoId is not null)
            .WithMessage("El vehículo no es válido.");

        RuleFor(x => x.SessionId).MaximumLength(64);

        // Los eventos de ficha y de contacto no significan nada sin el vehículo: un
        // "clic en WhatsApp" suelto no se puede atribuir a ninguna unidad, y atribuir es
        // justamente para lo que existe la tabla.
        RuleFor(x => x.VehiculoId)
            .NotNull()
            .When(x => NecesitaVehiculo(x.Tipo))
            .WithMessage("Ese tipo de evento necesita el vehículo.");
    }

    private static bool NecesitaVehiculo(string tipo)
        => Enumeraciones.ParsearOpcional<TipoEvento>(tipo)
            is TipoEvento.ViewFicha or TipoEvento.ClickWhatsapp or TipoEvento.ClickTelefono;
}
