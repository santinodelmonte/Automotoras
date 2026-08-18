using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Users;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Gestión de los usuarios de la automotora. Solo el Owner.
/// </summary>
/// <remarks>
/// No hay ni un <c>WHERE tenant_id = ...</c> escrito a mano en todo el controller, y no
/// es un descuido: el filtro global del <c>DbContext</c> ya recorta las consultas al
/// tenant del token, y la política de escritura sella el tenant en las altas. Pedir por
/// id un usuario de otra automotora no devuelve una fila que después haya que acordarse
/// de descartar: no devuelve nada, y el endpoint responde 404.
/// </remarks>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _reloj;

    public UsersController(AppDbContext db, IPasswordHasher hasher, TimeProvider reloj)
    {
        _db = db;
        _hasher = hasher;
        _reloj = reloj;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(CancellationToken cancellationToken)
    {
        var usuarios = await _db.Users
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioDto(u.Id, u.TenantId, u.Email, u.Nombre, u.Rol.ToString(), u.Activo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(usuarios);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obtener(int id, CancellationToken cancellationToken)
    {
        var usuario = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        return usuario is null ? NoExiste(id) : Ok(usuario.ADto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Crear(CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Emails.Normalizar(request.Email);

        // El email es único en todo el sistema, no por tenant: es lo que se tipea en el
        // login, antes de que exista ningún tenant. Por eso la consulta salta los filtros:
        // un email ya tomado en otra automotora también está tomado acá, y descubrirlo
        // ahora es mejor que chocar contra el índice único con un 500.
        var tomado = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (tomado)
        {
            return Problem(
                detail: "Ya hay un usuario registrado con ese email.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var usuario = new User
        {
            // El tenant no se escribe a mano ni se acepta del cuerpo del request: lo sella
            // SaveChanges con el tenant del token.
            Email = email,
            Nombre = request.Nombre.Trim(),
            Rol = RolUsuario.Seller,
            PasswordHash = _hasher.Hash(request.Password),
        };

        _db.Users.Add(usuario);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(Obtener), new { id = usuario.Id }, usuario.ADto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Actualizar(
        int id,
        ActualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        if (usuario is null)
        {
            return NoExiste(id);
        }

        // Darse de baja a uno mismo deja a la automotora sin quien administre a nadie, y
        // el que se quedó afuera no tiene cómo volver a entrar a arreglarlo.
        if (!request.Activo && usuario.Id == User.IdDeUsuario())
        {
            return Problem(
                detail: "No podés darte de baja a vos mismo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        usuario.Nombre = request.Nombre.Trim();
        usuario.Activo = request.Activo;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(usuario.ADto());
    }

    [HttpPost("{id:int}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarPassword(
        int id,
        CambiarPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        if (usuario is null)
        {
            return NoExiste(id);
        }

        usuario.PasswordHash = _hasher.Hash(request.Password);

        // Cambiar la contraseña cierra las sesiones abiertas del usuario. Si el motivo del
        // cambio es que la contraseña se filtró, dejar vivos los refresh tokens emitidos
        // con la vieja haría que el cambio no sirviera de nada.
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        await _db.RefreshTokens
            .Where(r => r.UserId == usuario.Id && r.RevocadoEn == null)
            .ForEachAsync(r => r.RevocadoEn = ahora, cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    private Task<User?> BuscarAsync(int id, CancellationToken cancellationToken)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <summary>
    /// 404, no 403. El usuario de otra automotora y el usuario que no existe se responden
    /// igual a propósito: distinguirlos convertiría el endpoint en una forma de averiguar
    /// qué ids están ocupados en el resto del sistema.
    /// </summary>
    private ActionResult NoExiste(int id)
        => Problem(detail: $"No existe el usuario {id}.", statusCode: StatusCodes.Status404NotFound);
}
