using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Tenants;
using FluentValidation;

namespace AutomotoraSaaS.Core.Admin;

/// <summary>Una automotora vista por el SuperAdmin, con el tamaño de su operación.</summary>
/// <param name="DominioPrincipal">
/// Solo para mirar. Los dominios propios los da de alta el dueño desde su panel y se
/// verifican por DNS; el SuperAdmin no los carga a mano, porque escribir un dominio en un
/// formulario no prueba que sea de quien lo escribe.
/// </param>
public sealed record TenantAdminDto(
    int Id,
    string Slug,
    string Nombre,
    string? DominioPrincipal,
    string? LogoUrl,
    string? ColorPrimario,
    string? ColorSecundario,
    string? Whatsapp,
    string? Telefono,
    string? Direccion,
    bool Activo,
    DateTime CreatedAt,
    int Usuarios,
    int Vehiculos);

/// <summary>
/// Alta de una automotora. Incluye a su Owner.
/// </summary>
/// <remarks>
/// Van juntos a propósito: una automotora sin nadie que pueda entrar no sirve para nada, y
/// dejarlo en dos pasos garantiza que alguna quede a medio crear.
/// </remarks>
public sealed record CrearTenantRequest(
    string Slug,
    string Nombre,
    string EmailDelOwner,
    string NombreDelOwner,
    string PasswordDelOwner);

/// <summary>Edición de la identidad de una automotora. El slug lo toca solo el SuperAdmin.</summary>
public sealed record ActualizarTenantRequest(string Slug, string Nombre, bool Activo);

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

        RuleFor(x => x.EmailDelOwner)
            .NotEmpty().MaximumLength(200).EmailAddress()
            .WithMessage("El email del dueño no tiene un formato válido.");

        RuleFor(x => x.NombreDelOwner).NotEmpty().MaximumLength(160);

        RuleFor(x => x.PasswordDelOwner)
            .Must(PoliticaDePassword.EsAceptable)
            .WithMessage(PoliticaDePassword.Mensaje);
    }
}

public sealed class ActualizarTenantRequestValidator : AbstractValidator<ActualizarTenantRequest>
{
    public ActualizarTenantRequestValidator()
    {
        RuleFor(x => x.Slug).Matches(FormatosDeTenant.Slug);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(160);
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
