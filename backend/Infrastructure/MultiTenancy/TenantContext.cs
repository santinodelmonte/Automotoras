using AutomotoraSaaS.Core.Common;

namespace AutomotoraSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Tenant del request en curso. Se registra con vida <c>Scoped</c>: uno por request.
/// </summary>
/// <remarks>
/// Se resuelve una sola vez y no se puede cambiar. Que <see cref="Resolver"/> lance al
/// segundo llamado no es rigidez: convierte "cambiar de tenant a mitad de un request" en
/// algo estructuralmente imposible, en vez de algo que hay que acordarse de no hacer.
/// <para>
/// Quién lo resuelve llega en el paso 3, junto con la autenticación: un middleware que lo
/// toma del claim del JWT en el panel privado, y del <c>Host</c> o del slug en el sitio
/// público. Mientras tanto queda sin resolver, y sin tenant resuelto no se lee ni se
/// escribe nada de ningún tenant.
/// </para>
/// </remarks>
public sealed class TenantContext : ITenantContext
{
    public int? TenantId { get; private set; }

    public void Resolver(int tenantId)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId), tenantId, "El id de tenant tiene que ser positivo.");
        }

        if (TenantId is not null)
        {
            throw new TenantIsolationException(
                $"El tenant del request ya estaba resuelto en {TenantId} y se intentó " +
                $"cambiarlo a {tenantId}.");
        }

        TenantId = tenantId;
    }
}
