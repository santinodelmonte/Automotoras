namespace AutomotoraSaaS.Core.Tenants;

/// <summary>
/// Identidad pública de una automotora: lo que el sitio necesita para pintarse con su
/// marca y ofrecer los canales de contacto.
/// </summary>
/// <remarks>
/// Es deliberadamente chico. Todo lo que no se muestra en el sitio —el estado del tenant,
/// sus usuarios, sus métricas— no sale por un endpoint sin autenticación.
/// </remarks>
public sealed record TenantPublicoDto(
    string Slug,
    string Nombre,
    string? LogoUrl,
    string? ColorPrimario,
    string? ColorSecundario,
    string? Whatsapp,
    string? Telefono,
    string? Direccion);
