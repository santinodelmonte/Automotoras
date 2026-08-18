using AutomotoraSaaS.Core.Tenants;

namespace AutomotoraSaaS.Core.Dominios;

/// <summary>
/// Alta, verificación y baja de los dominios propios de una automotora.
/// </summary>
/// <remarks>
/// Todo lo que no sea <see cref="ReverificarPendientesAsync"/> opera sobre el tenant del
/// request: los filtros globales del <c>DbContext</c> se encargan de que una automotora no
/// pueda tocar el dominio de otra.
/// </remarks>
public interface IServicioDeDominios
{
    Task<IReadOnlyList<DominioDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<ResultadoDeDominio> AgregarAsync(string dominio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta el DNS ahora mismo y actualiza el estado con lo que encuentre.
    /// </summary>
    /// <returns><c>null</c> si ese id no es de esta automotora, que acá es lo mismo que no existir.</returns>
    Task<ResultadoDeDominio?> VerificarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca cuál es el dominio de las URLs canónicas. Solo uno verificado puede serlo.
    /// </summary>
    /// <returns><c>null</c> si ese id no es de esta automotora.</returns>
    Task<ResultadoDeDominio?> MarcarPrincipalAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repasa los dominios de todas las automotoras y actualiza su estado. Lo dispara el
    /// cron, no el usuario.
    /// </summary>
    Task<ResumenDeVerificaciones> ReverificarPendientesAsync(CancellationToken cancellationToken = default);
}
