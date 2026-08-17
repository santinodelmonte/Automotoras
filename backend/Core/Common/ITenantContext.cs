namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// Tenant del request en curso. Es la única fuente de verdad para los filtros globales
/// del <c>DbContext</c>.
/// </summary>
/// <remarks>
/// Quién lo resuelve y cómo es responsabilidad de la capa de entrada:
/// <list type="bullet">
///   <item>Panel privado: exclusivamente desde un claim del JWT. Jamás desde un header,
///   query param o body enviado por el cliente.</item>
///   <item>Sitio público: desde el <c>Host</c> (dominio custom) o el slug de la ruta,
///   siempre validado contra la tabla <c>tenants</c>.</item>
/// </list>
/// Cuando <see cref="TenantId"/> es <c>null</c> no hay tenant resuelto y los filtros
/// globales no devuelven nada. Eso es deliberado: ante la duda, cero filas.
/// </remarks>
public interface ITenantContext
{
    /// <summary>Id del tenant en curso, o <c>null</c> si todavía no se resolvió ninguno.</summary>
    int? TenantId { get; }
}
