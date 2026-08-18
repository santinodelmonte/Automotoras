using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Login, renovación y cierre de sesión del panel privado.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IServicioDeAutenticacion _auth;

    public AuthController(IServicioDeAutenticacion auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SesionDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _auth.LoginAsync(request, cancellationToken).ConfigureAwait(false);

        return resultado.Sesion is { } sesion ? Ok(sesion) : Rechazo(resultado.Error);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SesionDto>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resultado = await _auth.RefrescarAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);

        return resultado.Sesion is { } sesion ? Ok(sesion) : Rechazo(resultado.Error);
    }

    /// <summary>
    /// Revoca el refresh token. No requiere token de acceso: cerrar sesión tiene que
    /// funcionar también cuando el access token ya venció, que es justo cuando más falta
    /// hace.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _auth.CerrarSesionAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// El usuario de la sesión en curso.
    /// </summary>
    /// <remarks>
    /// Se arma con los claims del token, sin tocar la base. El token ya es la sesión: lo
    /// que dice es lo que el servidor firmó al abrirla. La contrapartida es que dar de
    /// baja un usuario no invalida su access token hasta que venza; por eso el access
    /// token dura minutos y el refresh, que sí se revoca, es el que vive mucho.
    /// </remarks>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UsuarioDto> Me()
    {
        if (User.IdDeUsuario() is not { } id || User.RolDelToken() is not { } rol)
        {
            return Unauthorized();
        }

        return Ok(new UsuarioDto(
            id,
            User.TenantIdDelToken(),
            User.EmailDelToken() ?? string.Empty,
            User.NombreDelToken() ?? string.Empty,
            rol,
            Activo: true));
    }

    /// <summary>
    /// Un login fallido responde 401 y un detalle que no distingue entre "el email no
    /// existe" y "la contraseña no es esa". Decir cuál de las dos es convierte el login en
    /// un verificador de qué cuentas existen.
    /// </summary>
    private ActionResult<SesionDto> Rechazo(ErrorDeAutenticacion? error)
    {
        var detalle = error switch
        {
            ErrorDeAutenticacion.UsuarioInactivo => "El usuario está dado de baja.",
            ErrorDeAutenticacion.RefreshTokenInvalido => "La sesión venció o ya se cerró. Volvé a entrar.",
            _ => "Email o contraseña incorrectos.",
        };

        return Problem(detail: detalle, statusCode: StatusCodes.Status401Unauthorized);
    }
}
