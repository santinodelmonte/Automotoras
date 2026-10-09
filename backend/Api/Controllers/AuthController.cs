using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Api.Filters;
using System.Globalization;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [PermitidoConPasswordProvisoria]
    [EnableRateLimiting(LimitesDeLogin.Politica)]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SesionDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _auth.LoginAsync(request, cancellationToken).ConfigureAwait(false);

        return resultado.Sesion is { } sesion ? Ok(sesion) : Rechazo(resultado.Error, resultado.ReintentarEn);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [PermitidoConPasswordProvisoria]
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
    [PermitidoConPasswordProvisoria]
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
    [PermitidoConPasswordProvisoria]
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
            Activo: true,
            DebeCambiarPassword: User.TienePasswordProvisoria()));
    }

    /// <summary>
    /// Cambio de la contraseña propia. Es lo único que se puede hacer con una contraseña
    /// provisoria, y devuelve una sesión nueva sin esa marca.
    /// </summary>
    [HttpPost("password")]
    [Authorize]
    [PermitidoConPasswordProvisoria]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SesionDto>> CambiarPassword(
        CambiarPasswordPropiaRequest request,
        CancellationToken cancellationToken)
    {
        if (User.IdDeUsuario() is not { } id)
        {
            return Unauthorized();
        }

        var resultado = await _auth.CambiarPasswordPropiaAsync(id, request, cancellationToken).ConfigureAwait(false);

        if (resultado.Sesion is { } sesion)
        {
            return Ok(sesion);
        }

        return resultado.Error switch
        {
            ErrorDeAutenticacion.PasswordRepetida => Problem(
                detail: "La contraseña nueva tiene que ser distinta de la actual.",
                statusCode: StatusCodes.Status400BadRequest),
            ErrorDeAutenticacion.CredencialesInvalidas => Problem(
                detail: "La contraseña actual no es correcta.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Rechazo(resultado.Error).Result!,
        };
    }

    /// <summary>
    /// Un login fallido responde 401 y un detalle que no distingue entre "el email no
    /// existe" y "la contraseña no es esa". Decir cuál de las dos es convierte el login en
    /// un verificador de qué cuentas existen.
    /// </summary>
    private ActionResult<SesionDto> Rechazo(ErrorDeAutenticacion? error, TimeSpan? reintentarEn = null)
    {
        if (error == ErrorDeAutenticacion.DemasiadosIntentos)
        {
            var minutos = Math.Max(1, (int)Math.Ceiling((reintentarEn ?? FrenoDeLogin.Ventana).TotalMinutes));
            Response.Headers.RetryAfter = (minutos * 60).ToString(CultureInfo.InvariantCulture);

            return Problem(
                detail: $"Demasiados intentos fallidos con este email. Probá de nuevo en {minutos} {(minutos == 1 ? "minuto" : "minutos")}.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var detalle = error switch
        {
            ErrorDeAutenticacion.UsuarioInactivo => "El usuario está dado de baja.",
            ErrorDeAutenticacion.RefreshTokenInvalido => "La sesión venció o ya se cerró. Volvé a entrar.",
            _ => "Email o contraseña incorrectos.",
        };

        return Problem(detail: detalle, statusCode: StatusCodes.Status401Unauthorized);
    }
}

/// <summary>
/// Tope de intentos de login por IP. El freno por cuenta está en <see cref="FrenoDeLogin"/>.
/// </summary>
/// <remarks>
/// Veinte por minuto alcanzan para una oficina entera entrando a la mañana detrás de la
/// misma IP, y le cortan a un script la posibilidad de recorrer una lista de emails. Se
/// puede subir por configuración (<c>Seguridad:LoginsPorMinutoPorIp</c>).
/// </remarks>
public static class LimitesDeLogin
{
    public const string Politica = "login";

    public const int LoginsPorMinutoPorDefecto = 20;
}
