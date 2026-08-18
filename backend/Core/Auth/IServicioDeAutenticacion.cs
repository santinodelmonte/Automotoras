namespace AutomotoraSaaS.Core.Auth;

/// <summary>
/// Login, renovación y cierre de sesión del panel privado.
/// </summary>
/// <remarks>
/// Trabaja por fuera de los filtros globales del <c>DbContext</c>, y no puede ser de otra
/// manera: cuando alguien tipea su email todavía no hay ningún tenant resuelto. Es
/// justamente el servicio que produce el tenant que después usa todo lo demás.
/// </remarks>
public interface IServicioDeAutenticacion
{
    Task<ResultadoDeAutenticacion> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rota el refresh token: revoca el presentado y emite uno nuevo. Presentar uno ya
    /// revocado revoca toda la familia de tokens del usuario.
    /// </summary>
    Task<ResultadoDeAutenticacion> RefrescarAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca el refresh token. Es idempotente: cerrar sesión con un token que ya no
    /// existe no es un error para quien llama.
    /// </summary>
    Task CerrarSesionAsync(string refreshToken, CancellationToken cancellationToken = default);
}
