namespace AutomotoraSaaS.Core.Users;

/// <summary>
/// Alta de un usuario dentro del tenant. El tenant no viaja en el cuerpo: lo pone el
/// servidor desde el contexto del request.
/// </summary>
/// <param name="Rol">
/// Solo <c>Seller</c>. Un Owner administra vendedores, no crea otros Owners: eso es alta
/// de tenant y vive en <c>/api/admin/*</c>.
/// </param>
public sealed record CrearUsuarioRequest(string Email, string Nombre, string Password, string Rol);

/// <summary>Edición de los datos de un usuario del tenant.</summary>
public sealed record ActualizarUsuarioRequest(string Nombre, bool Activo);

/// <summary>Cambio de contraseña de un usuario del tenant.</summary>
public sealed record CambiarPasswordRequest(string Password);
