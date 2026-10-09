using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Tenants;
using FluentValidation;

namespace AutomotoraSaaS.Core.Admin;

/// <summary>Una automotora vista por el SuperAdmin, con el tamaño de su operación.</summary>
public sealed record TenantAdminDto(
    int Id,
    string Slug,
    string Nombre,
    string? DominioCustom,
    string? LogoUrl,
    string? ColorPrimario,
    string? ColorSecundario,
    string? Whatsapp,
    string? Telefono,
    string? Direccion,
    bool Activo,
    DateTime CreatedAt,
    int Usuarios,
    int Vehiculos,
    DateTime? DominioVerificadoEn);

/// <summary>
/// Resultado de comprobar si un dominio propio apunta a la aplicación.
/// </summary>
/// <param name="Resultado">Nombre del <c>ResultadoDeVerificacion</c>.</param>
/// <param name="Detalle">La explicación en castellano, para mostrarla tal cual.</param>
/// <param name="ApuntaA">
/// A dónde resuelve hoy el dominio. Va incluso cuando la verificación falla —sobre todo
/// cuando falla—: sin esto, quien está configurando el DNS ve "no funciona" y no tiene con
/// qué comparar lo que cargó.
/// </param>
/// <param name="DeberiaApuntarA">Las IP declaradas de la aplicación.</param>
public sealed record VerificacionDeDominioDto(
    string Resultado,
    string Detalle,
    DateTime? VerificadoEn,
    IReadOnlyList<string> ApuntaA,
    IReadOnlyList<string> DeberiaApuntarA);

/// <summary>
/// Alta de una automotora. Incluye a su Owner.
/// </summary>
/// <remarks>
/// Van juntos a propósito: una automotora sin nadie que pueda entrar no sirve para nada, y
/// dejarlo en dos pasos garantiza que alguna quede a medio crear. Por lo mismo sale con su
/// suscripción: ninguna automotora existe sin plan.
/// </remarks>
/// <param name="Plan">
/// Código del plan. Si no viene, <see cref="CodigosDePlan.PorDefecto"/>.
/// </param>
/// <remarks>
/// La identidad y el contacto son opcionales: si la automotora ya los mandó, se cargan en
/// el alta y el sitio sale con su marca desde el primer minuto. El logo se sube después,
/// desde Configuración, porque es un archivo.
/// </remarks>
public sealed record CrearTenantRequest(
    string Slug,
    string Nombre,
    string? DominioCustom,
    string EmailDelOwner,
    string NombreDelOwner,
    string PasswordDelOwner,
    string? Plan = null,
    string? ColorPrimario = null,
    string? ColorSecundario = null,
    string? Whatsapp = null,
    string? Telefono = null,
    string? Direccion = null);

/// <summary>Edición de la identidad de una automotora. Solo el SuperAdmin toca slug y dominio.</summary>
public sealed record ActualizarTenantRequest(string Slug, string Nombre, string? DominioCustom, bool Activo);

public sealed record GuardarMarcaRequest(string Nombre, bool Activo);

public sealed record GuardarModeloRequest(int MarcaId, string Nombre, string Carroceria, bool Activo);

public sealed record GuardarVersionRequest(int ModeloId, string Nombre, bool Activo);

public sealed class CrearTenantRequestValidator : AbstractValidator<CrearTenantRequest>
{
    public CrearTenantRequestValidator()
    {
        RuleFor(x => x.Slug)
            .Matches(FormatosDeTenant.Slug)
            .WithMessage("El slug va en minúsculas, con números y guiones, sin empezar ni terminar en guion.");

        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(160);

        RuleFor(x => x.DominioCustom)
            .Matches(FormatosDeTenant.Dominio)
            .When(x => !string.IsNullOrWhiteSpace(x.DominioCustom))
            .WithMessage("El dominio no tiene un formato válido.");

        RuleFor(x => x.EmailDelOwner)
            .NotEmpty().MaximumLength(200).EmailAddress()
            .WithMessage("El email del dueño no tiene un formato válido.");

        RuleFor(x => x.NombreDelOwner).NotEmpty().MaximumLength(160);

        RuleFor(x => x.PasswordDelOwner)
            .Must(PoliticaDePassword.EsAceptable)
            .WithMessage(PoliticaDePassword.Mensaje);

        RuleFor(x => x.Plan)
            .NotEmpty().MaximumLength(30)
            .When(x => x.Plan is not null)
            .WithMessage("El plan no puede venir vacío.");

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

public sealed class ActualizarTenantRequestValidator : AbstractValidator<ActualizarTenantRequest>
{
    public ActualizarTenantRequestValidator()
    {
        RuleFor(x => x.Slug).Matches(FormatosDeTenant.Slug);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(160);

        RuleFor(x => x.DominioCustom)
            .Matches(FormatosDeTenant.Dominio)
            .When(x => !string.IsNullOrWhiteSpace(x.DominioCustom))
            .WithMessage("El dominio no tiene un formato válido.");
    }
}

public sealed class GuardarMarcaRequestValidator : AbstractValidator<GuardarMarcaRequest>
{
    public GuardarMarcaRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
    }
}

public sealed class GuardarModeloRequestValidator : AbstractValidator<GuardarModeloRequest>
{
    public GuardarModeloRequestValidator()
    {
        RuleFor(x => x.MarcaId).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);

        RuleFor(x => x.Carroceria)
            .Must(Enumeraciones.EsValido<Carroceria>)
            .WithMessage("La carrocería no es válida.");
    }
}

public sealed class GuardarVersionRequestValidator : AbstractValidator<GuardarVersionRequest>
{
    public GuardarVersionRequestValidator()
    {
        RuleFor(x => x.ModeloId).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
    }
}
