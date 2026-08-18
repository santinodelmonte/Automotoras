using AutomotoraSaaS.Core.Auth;
using FluentValidation;

namespace AutomotoraSaaS.Core.Users;

public sealed class CrearUsuarioRequestValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .MaximumLength(200)
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(160);

        RuleFor(x => x.Password)
            .Must(PoliticaDePassword.EsAceptable)
            .WithMessage(PoliticaDePassword.Mensaje);

        // Un Owner administra vendedores. Crear Owners es alta de tenant y vive en
        // /api/admin/*; crear SuperAdmins no lo hace ningún endpoint.
        RuleFor(x => x.Rol)
            .Equal(Roles.Seller)
            .WithMessage($"El único rol que se puede dar de alta desde el panel es {Roles.Seller}.");
    }
}

public sealed class ActualizarUsuarioRequestValidator : AbstractValidator<ActualizarUsuarioRequest>
{
    public ActualizarUsuarioRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(160);
    }
}

public sealed class CambiarPasswordRequestValidator : AbstractValidator<CambiarPasswordRequest>
{
    public CambiarPasswordRequestValidator()
    {
        RuleFor(x => x.Password)
            .Must(PoliticaDePassword.EsAceptable)
            .WithMessage(PoliticaDePassword.Mensaje);
    }
}
