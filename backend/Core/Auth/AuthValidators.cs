using FluentValidation;

namespace AutomotoraSaaS.Core.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .MaximumLength(200)
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

        // El login no valida la política de contraseña: si se endurece la política, los
        // usuarios existentes tienen que poder entrar igual para poder cambiarla.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(PoliticaDePassword.MaximoDeCaracteres);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Falta el refresh token.")
            .MaximumLength(512);
    }
}
