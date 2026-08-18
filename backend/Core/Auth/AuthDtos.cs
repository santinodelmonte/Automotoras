namespace AutomotoraSaaS.Core.Auth;

/// <summary>Credenciales del login del panel privado.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Cuerpo de <c>POST /api/auth/refresh</c> y <c>POST /api/auth/logout</c>.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Usuario tal como se expone en la API. Nunca lleva el hash de la contraseña.
/// </summary>
/// <param name="Rol">Nombre del rol: <c>SuperAdmin</c>, <c>Owner</c> o <c>Seller</c>.</param>
public sealed record UsuarioDto(
    int Id,
    int? TenantId,
    string Email,
    string Nombre,
    string Rol,
    bool Activo);

/// <summary>
/// Sesión abierta: el par de tokens y el usuario al que pertenecen.
/// </summary>
/// <remarks>
/// El access token es corto y sin estado; el refresh token es largo y revocable, y en la
/// base vive solo su hash. Si se filtra la tabla, los refresh tokens no son utilizables.
/// </remarks>
public sealed record SesionDto(
    string AccessToken,
    DateTimeOffset ExpiraEn,
    string RefreshToken,
    UsuarioDto Usuario);

/// <summary>Motivo por el que no se pudo abrir o renovar una sesión.</summary>
public enum ErrorDeAutenticacion
{
    /// <summary>Email inexistente o contraseña incorrecta. No se distingue cuál a propósito.</summary>
    CredencialesInvalidas = 1,

    /// <summary>El usuario existe y la contraseña es correcta, pero está dado de baja.</summary>
    UsuarioInactivo = 2,

    /// <summary>El refresh token no existe, ya se usó, se revocó o venció.</summary>
    RefreshTokenInvalido = 3,
}

/// <summary>
/// Resultado de una operación de autenticación: o hay sesión, o hay error. Nunca los dos.
/// </summary>
public sealed record ResultadoDeAutenticacion
{
    private ResultadoDeAutenticacion(SesionDto? sesion, ErrorDeAutenticacion? error)
    {
        Sesion = sesion;
        Error = error;
    }

    public SesionDto? Sesion { get; }

    public ErrorDeAutenticacion? Error { get; }

    public static ResultadoDeAutenticacion Ok(SesionDto sesion) => new(sesion, null);

    public static ResultadoDeAutenticacion Falla(ErrorDeAutenticacion error) => new(null, error);
}
