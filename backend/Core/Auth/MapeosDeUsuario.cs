using AutomotoraSaaS.Core.Entities;

namespace AutomotoraSaaS.Core.Auth;

public static class MapeosDeUsuario
{
    /// <summary>
    /// Proyecta la entidad al DTO. Existe para que ningún camino de la API pueda devolver
    /// la entidad tal cual y filtrar el <c>PasswordHash</c> sin que nadie lo note.
    /// </summary>
    public static UsuarioDto ADto(this User usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return new UsuarioDto(
            usuario.Id,
            usuario.TenantId,
            usuario.Email,
            usuario.Nombre,
            usuario.Rol.ToString(),
            usuario.Activo);
    }
}
