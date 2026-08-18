namespace AutomotoraSaaS.Core.Auth;

/// <summary>
/// Normalización del email de login.
/// </summary>
/// <remarks>
/// Vive en un solo lugar porque tiene que ser la misma en el alta y en el login: si el
/// alta guardara <c>Juan@Norte.uy</c> y el login buscara <c>juan@norte.uy</c>, el usuario
/// quedaría creado y sin poder entrar.
/// </remarks>
public static class Emails
{
    public static string Normalizar(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email.Trim().ToLowerInvariant();
    }
}
