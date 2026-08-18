using FluentValidation;

namespace AutomotoraSaaS.Core.Tenants;

/// <summary>
/// La automotora vista desde su propio panel.
/// </summary>
/// <remarks>
/// El slug y el dominio propio se muestran pero no se editan desde acá: son la puerta de
/// entrada al sitio público. Cambiarlos apaga la dirección por la que la automotora ya
/// está publicada y circulando, así que los toca el SuperAdmin por <c>/api/admin/*</c>,
/// que es donde se coordina el cambio de DNS.
/// </remarks>
public sealed record ConfiguracionDeTenantDto(
    string Slug,
    string Nombre,
    string? DominioCustom,
    string? LogoUrl,
    string? ColorPrimario,
    string? ColorSecundario,
    string? Whatsapp,
    string? Telefono,
    string? Direccion);

/// <summary>Lo que el Owner sí puede cambiar de su automotora.</summary>
public sealed record GuardarConfiguracionRequest(
    string Nombre,
    string? ColorPrimario,
    string? ColorSecundario,
    string? Whatsapp,
    string? Telefono,
    string? Direccion);

/// <summary>Formatos de los datos de identidad de una automotora.</summary>
public static class FormatosDeTenant
{
    /// <summary><c>#RRGGBB</c>. La forma corta y los nombres de color no: la columna tiene 7 caracteres.</summary>
    public const string Color = "^#[0-9a-fA-F]{6}$";

    /// <summary>
    /// Teléfono en formato internacional. WhatsApp arma el link con el número sin
    /// símbolos, y un número sin código de país no sirve fuera de Uruguay.
    /// </summary>
    public const string Telefono = @"^\+?[0-9][0-9\s\-]{6,24}$";

    public const string Slug = "^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$";

    public const string Dominio = @"^(?!-)[a-z0-9-]{1,63}(?<!-)(\.(?!-)[a-z0-9-]{1,63}(?<!-))+$";
}

public sealed class GuardarConfiguracionRequestValidator : AbstractValidator<GuardarConfiguracionRequest>
{
    public GuardarConfiguracionRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la automotora es obligatorio.")
            .MaximumLength(160);

        RuleFor(x => x.ColorPrimario)
            .Matches(FormatosDeTenant.Color)
            .When(x => !string.IsNullOrWhiteSpace(x.ColorPrimario))
            .WithMessage("El color tiene que ser #RRGGBB.");

        RuleFor(x => x.ColorSecundario)
            .Matches(FormatosDeTenant.Color)
            .When(x => !string.IsNullOrWhiteSpace(x.ColorSecundario))
            .WithMessage("El color tiene que ser #RRGGBB.");

        RuleFor(x => x.Whatsapp)
            .Matches(FormatosDeTenant.Telefono)
            .When(x => !string.IsNullOrWhiteSpace(x.Whatsapp))
            .WithMessage("El WhatsApp tiene que ser un número, con código de país.");

        RuleFor(x => x.Telefono)
            .Matches(FormatosDeTenant.Telefono)
            .When(x => !string.IsNullOrWhiteSpace(x.Telefono))
            .WithMessage("El teléfono tiene que ser un número, con código de país.");

        RuleFor(x => x.Direccion).MaximumLength(255);
    }
}
