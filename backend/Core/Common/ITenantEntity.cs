namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// Marca una entidad como perteneciente a un tenant. El <c>DbContext</c> aplica un
/// filtro global sobre toda entidad que implemente esta interfaz, de modo que olvidarse
/// un <c>WHERE tenant_id = ...</c> sea imposible por defecto y no una cuestión de
/// disciplina.
/// </summary>
/// <remarks>
/// <see cref="TenantId"/> es no anulable a propósito: una entidad de tenant siempre
/// pertenece a un tenant. Los usuarios son el único caso con tenant nulo (el SuperAdmin
/// es cross-tenant) y por eso <c>User</c> no implementa esta interfaz; su filtro se
/// configura aparte, con la misma severidad.
/// </remarks>
public interface ITenantEntity
{
    int TenantId { get; set; }
}
