using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Infrastructure.Auth;

/// <summary>
/// Login, renovación y cierre de sesión.
/// </summary>
/// <remarks>
/// Es el único servicio que consulta usuarios con <c>IgnoreQueryFilters()</c>, y tiene
/// que serlo: cuando alguien tipea su email todavía no hay tenant resuelto —de hecho el
/// login es lo que produce el tenant que después usa todo lo demás—. El acceso está
/// acotado a buscar por email exacto y devolver un DTO; nunca lista usuarios ni deja
/// elegir el tenant desde el request.
/// </remarks>
public sealed class ServicioDeAutenticacion : IServicioDeAutenticacion
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly GeneradorDeTokens _tokens;
    private readonly TimeProvider _reloj;
    private readonly JwtOptions _opciones;

    public ServicioDeAutenticacion(
        AppDbContext db,
        IPasswordHasher hasher,
        GeneradorDeTokens tokens,
        TimeProvider reloj,
        IOptions<JwtOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _reloj = reloj;
        _opciones = opciones.Value;
    }

    public async Task<ResultadoDeAutenticacion> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Emails.Normalizar(request.Email);

        var usuario = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            _hasher.VerificarSenuelo(request.Password);
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.CredencialesInvalidas);
        }

        if (!_hasher.Verificar(request.Password, usuario.PasswordHash))
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.CredencialesInvalidas);
        }

        // El estado se mira después de verificar la contraseña, no antes: si un usuario
        // dado de baja recibiera "usuario inactivo" con cualquier contraseña, el mensaje
        // confirmaría que la cuenta existe.
        if (!usuario.Activo || usuario.Tenant is { Activo: false })
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.UsuarioInactivo);
        }

        return ResultadoDeAutenticacion.Ok(
            await AbrirSesionAsync(usuario, cancellationToken).ConfigureAwait(false));
    }

    public async Task<ResultadoDeAutenticacion> RefrescarAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.RefreshTokenInvalido);
        }

        var hash = GeneradorDeTokens.HashDe(refreshToken);

        var almacenado = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .ThenInclude(u => u!.Tenant)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (almacenado is null)
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.RefreshTokenInvalido);
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;

        if (almacenado.RevocadoEn is not null)
        {
            // Reuso de un token ya canjeado. O se filtró, o alguien está reproduciendo una
            // sesión vieja. En los dos casos la respuesta correcta es cortar todas las
            // sesiones del usuario, no solo rechazar esta.
            await RevocarTodosLosTokensAsync(almacenado.UserId, ahora, cancellationToken).ConfigureAwait(false);
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.RefreshTokenInvalido);
        }

        if (almacenado.ExpiraEn <= ahora)
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.RefreshTokenInvalido);
        }

        var usuario = almacenado.User;

        if (usuario is null)
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.RefreshTokenInvalido);
        }

        if (!usuario.Activo || usuario.Tenant is { Activo: false })
        {
            return ResultadoDeAutenticacion.Falla(ErrorDeAutenticacion.UsuarioInactivo);
        }

        // Rotación: el token presentado se quema al canjearlo. Un refresh token de vida
        // larga que se pudiera reutilizar sería, en la práctica, una contraseña eterna.
        almacenado.RevocadoEn = ahora;

        return ResultadoDeAutenticacion.Ok(
            await AbrirSesionAsync(usuario, cancellationToken).ConfigureAwait(false));
    }

    public async Task CerrarSesionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = GeneradorDeTokens.HashDe(refreshToken);

        var almacenado = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        // Idempotente: cerrar una sesión que ya no existe no es un error para quien llama,
        // y responder distinto convertiría el endpoint en un oráculo de tokens válidos.
        if (almacenado is null || almacenado.RevocadoEn is not null)
        {
            return;
        }

        almacenado.RevocadoEn = _reloj.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SesionDto> AbrirSesionAsync(User usuario, CancellationToken cancellationToken)
    {
        var (accessToken, expiraEn) = _tokens.CrearAccessToken(usuario);
        var refreshEnClaro = GeneradorDeTokens.CrearRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = usuario.Id,
            TokenHash = GeneradorDeTokens.HashDe(refreshEnClaro),
            ExpiraEn = _reloj.GetUtcNow().UtcDateTime.AddDays(_opciones.RefreshTokenDays),
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SesionDto(accessToken, expiraEn, refreshEnClaro, usuario.ADto());
    }

    private async Task RevocarTodosLosTokensAsync(int userId, DateTime ahora, CancellationToken cancellationToken)
    {
        var vigentes = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.RevocadoEn == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in vigentes)
        {
            token.RevocadoEn = ahora;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
