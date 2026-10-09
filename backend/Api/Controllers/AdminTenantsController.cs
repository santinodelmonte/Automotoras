using AutomotoraSaaS.Api.Planes;
using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Core.Users;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Alta y edición de automotoras. Solo el SuperAdmin.
/// </summary>
/// <remarks>
/// Este es el único lugar del sistema que opera cross-tenant, y por eso vive bajo
/// <c>/api/admin/*</c> y no como un flag opcional de los endpoints normales. El escape de
/// escritura se pide explícitamente, en la línea donde hace falta y para lo que hace
/// falta: dar de alta al Owner de una automotora recién creada, que por definición no
/// pertenece al tenant de nadie todavía.
/// </remarks>
[ApiController]
[Route("api/admin/tenants")]
[Authorize(Policy = Politicas.SoloSuperAdmin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AdminTenantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IResolvedorDeDns _dns;
    private readonly IConfiguration _configuracion;
    private readonly TimeProvider _reloj;
    private readonly IPoliticaDePlan _politica;

    public AdminTenantsController(
        AppDbContext db,
        IPasswordHasher hasher,
        IResolvedorDeDns dns,
        IConfiguration configuracion,
        TimeProvider reloj,
        IPoliticaDePlan politica)
    {
        _db = db;
        _hasher = hasher;
        _dns = dns;
        _configuracion = configuracion;
        _reloj = reloj;
        _politica = politica;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantAdminDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantAdminDto>>> Listar(CancellationToken cancellationToken)
    {
        var tenants = await _db.Tenants
            .OrderBy(t => t.Nombre)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Los conteos van en consultas propias y no como subconsulta en la proyección:
        // IgnoreQueryFilters se aplica a la consulta entera, no a un Count anidado adentro
        // de un Select, y el SuperAdmin no tiene tenant resuelto, así que sin el escape
        // todos los conteos darían cero.
        var usuarios = await ContarPorTenantAsync(
            _db.Users.IgnoreQueryFilters().Where(u => u.TenantId != null).Select(u => u.TenantId!.Value),
            cancellationToken).ConfigureAwait(false);

        var vehiculos = await ContarPorTenantAsync(
            _db.Vehiculos.IgnoreQueryFilters().Select(v => v.TenantId),
            cancellationToken).ConfigureAwait(false);

        return Ok(tenants
            .Select(t => ADto(t, usuarios.GetValueOrDefault(t.Id), vehiculos.GetValueOrDefault(t.Id)))
            .ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TenantAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantAdminDto>> Obtener(int id, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExiste(id);
        }

        var (usuarios, vehiculos) = await ContarAsync(id, cancellationToken).ConfigureAwait(false);

        return Ok(ADto(tenant, usuarios, vehiculos));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TenantAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantAdminDto>> Crear(
        CrearTenantRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slug = request.Slug.Trim().ToLowerInvariant();
        var dominio = Dominio(request.DominioCustom);
        var email = Emails.Normalizar(request.EmailDelOwner);

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay una automotora con ese slug.");
        }

        if (dominio is not null
            && await _db.Tenants.AnyAsync(t => t.DominioCustom == dominio, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay una automotora con ese dominio.");
        }

        var emailTomado = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (emailTomado)
        {
            return Conflicto("Ya hay un usuario registrado con ese email.");
        }

        var codigoDePlan = request.Plan?.Trim().ToLowerInvariant() ?? CodigosDePlan.PorDefecto;

        var plan = await _db.Planes
            .FirstOrDefaultAsync(p => p.Codigo == codigoDePlan && p.Activo, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            return Conflicto($"No hay ningún plan disponible con el código '{codigoDePlan}'.");
        }

        if (dominio is not null && !plan.IncluyeDominioPropio)
        {
            return this.Rechazo(new RechazoDelPlan(
                "dominio-propio",
                $"El dominio propio no está incluido en el plan {plan.Nombre}. Elegí un plan que lo incluya o creá la automotora sin dominio.",
                plan.Nombre,
                Tope: null,
                Uso: null));
        }

        var tenant = new Tenant
        {
            Slug = slug,
            Nombre = request.Nombre.Trim(),
            DominioCustom = dominio,
            ColorPrimario = Opcional(request.ColorPrimario)?.ToLowerInvariant(),
            ColorSecundario = Opcional(request.ColorSecundario)?.ToLowerInvariant(),
            Whatsapp = Opcional(request.Whatsapp),
            Telefono = Opcional(request.Telefono),
            Direccion = Opcional(request.Direccion),
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // El escape cross-tenant, explícito y acotado a esta escritura: el Owner y la
        // suscripción que se están creando pertenecen a una automotora que no es la del
        // request, porque el SuperAdmin no tiene ninguna.
        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            var hoy = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);
            _db.Suscripciones.Add(Suscripciones.IniciarConBonificacion(tenant.Id, plan, hoy));

            _db.Users.Add(new User
            {
                TenantId = tenant.Id,
                Email = email,
                Nombre = request.NombreDelOwner.Trim(),
                Rol = RolUsuario.Owner,
                PasswordHash = _hasher.Hash(request.PasswordDelOwner),

                // La puso el SuperAdmin: el dueño la cambia en su primer ingreso.
                DebeCambiarPassword = true,
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return CreatedAtAction(nameof(Obtener), new { id = tenant.Id }, ADto(tenant, usuarios: 1, vehiculos: 0));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TenantAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantAdminDto>> Actualizar(
        int id,
        ActualizarTenantRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExiste(id);
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        var dominio = Dominio(request.DominioCustom);

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug && t.Id != id, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay otra automotora con ese slug.");
        }

        if (dominio is not null
            && await _db.Tenants.AnyAsync(t => t.DominioCustom == dominio && t.Id != id, cancellationToken)
                .ConfigureAwait(false))
        {
            return Conflicto("Ya hay otra automotora con ese dominio.");
        }

        // Cargar un dominio nuevo requiere que el plan lo incluya. Uno que ya estaba se
        // respeta aunque el plan haya bajado: bajar de plan no rompe nada de lo que hay.
        if (dominio is not null && !string.Equals(tenant.DominioCustom, dominio, StringComparison.Ordinal))
        {
            var situacion = await _politica.SituacionAsync(id, cancellationToken).ConfigureAwait(false);

            if (situacion.Usar(FuncionDelPlan.DominioPropio) is { } rechazo)
            {
                return this.Rechazo(rechazo);
            }
        }

        // Cambiar el dominio invalida la verificación anterior, que era sobre otro dominio.
        // Sin esto, alguien podría verificar un dominio propio y después reemplazarlo por
        // el de otra empresa quedándose con el sello.
        if (!string.Equals(tenant.DominioCustom, dominio, StringComparison.Ordinal))
        {
            tenant.DominioVerificadoEn = null;
        }

        tenant.Slug = slug;
        tenant.Nombre = request.Nombre.Trim();
        tenant.DominioCustom = dominio;

        // Dar de baja una automotora le apaga el sitio público y le impide entrar al
        // panel. No se borra nada: los datos siguen, por si vuelve.
        tenant.Activo = request.Activo;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (usuarios, vehiculos) = await ContarAsync(id, cancellationToken).ConfigureAwait(false);

        return Ok(ADto(tenant, usuarios, vehiculos));
    }

    /// <summary>
    /// Comprueba que el dominio propio de la automotora apunte a la aplicación, y lo
    /// habilita si es así.
    /// </summary>
    /// <remarks>
    /// Hasta que esto pasa, el sitio público no responde por ese dominio. Cargar un dominio
    /// es declarar una intención; servirlo requiere haber comprobado que quien lo declaró
    /// lo controla, y apuntarlo a estas IP es esa comprobación.
    /// <para>
    /// Se dispara a mano y no en cada request: una consulta de DNS por visita sería una
    /// llamada saliente en el camino caliente del sitio, y el dato cambia una vez en la
    /// vida del dominio.
    /// </para>
    /// <para>
    /// Cuando falla, no se pierde la verificación anterior. Un DNS que no contesta en el
    /// momento en que alguien aprieta el botón no es motivo para bajarle el sitio a una
    /// automotora que viene funcionando.
    /// </para>
    /// </remarks>
    [HttpPost("{id:int}/verificar-dominio")]
    [ProducesResponseType(typeof(VerificacionDeDominioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerificacionDeDominioDto>> VerificarDominio(
        int id,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return NoExiste(id);
        }

        var declaradas = IpsDeclaradas();

        var resueltas = string.IsNullOrWhiteSpace(tenant.DominioCustom)
            ? []
            : await _dns.DireccionesDeAsync(tenant.DominioCustom, cancellationToken).ConfigureAwait(false);

        var resultado = VerificacionDeDominio.Evaluar(tenant.DominioCustom, resueltas, declaradas);

        if (resultado == ResultadoDeVerificacion.Verificado)
        {
            tenant.DominioVerificadoEn = _reloj.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Ok(new VerificacionDeDominioDto(
            resultado.ToString(),
            VerificacionDeDominio.Explicacion(resultado),
            tenant.DominioVerificadoEn,
            resueltas,
            declaradas));
    }

    /// <summary>Las IP públicas de la aplicación, declaradas en la configuración.</summary>
    private string[] IpsDeclaradas()
        => _configuracion.GetSection(VerificacionDeDominio.ClaveDeIps).Get<string[]>() ?? [];

    private async Task<(int Usuarios, int Vehiculos)> ContarAsync(int tenantId, CancellationToken cancellationToken)
    {
        var usuarios = await _db.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var vehiculos = await _db.Vehiculos
            .IgnoreQueryFilters()
            .CountAsync(v => v.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        return (usuarios, vehiculos);
    }

    private static async Task<Dictionary<int, int>> ContarPorTenantAsync(
        IQueryable<int> tenantIds,
        CancellationToken cancellationToken)
    {
        var conteos = await tenantIds
            .GroupBy(id => id)
            .Select(g => new { TenantId = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return conteos.ToDictionary(c => c.TenantId, c => c.Cantidad);
    }

    private static TenantAdminDto ADto(Tenant tenant, int usuarios, int vehiculos)
        => new(
            tenant.Id,
            tenant.Slug,
            tenant.Nombre,
            tenant.DominioCustom,
            tenant.LogoUrl,
            tenant.ColorPrimario,
            tenant.ColorSecundario,
            tenant.Whatsapp,
            tenant.Telefono,
            tenant.Direccion,
            tenant.Activo,
            tenant.CreatedAt,
            usuarios,
            vehiculos,
            tenant.DominioVerificadoEn);

    private static string? Opcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Dominio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToLowerInvariant();

    /// <summary>
    /// Le pone una contraseña provisoria a un usuario de la automotora. Es el camino para
    /// el dueño que se olvidó la suya: a un vendedor se la puede restablecer su dueño, pero
    /// al dueño no tenía quién.
    /// </summary>
    /// <remarks>
    /// Provisoria siempre: con ella lo único que puede hacer es cambiarla, así que la que
    /// eligió el SuperAdmin no le sirve a nadie más que para ese primer ingreso. Cierra las
    /// sesiones abiertas, igual que cualquier cambio de contraseña. Se busca por email
    /// dentro de la automotora del id: un email de otra automotora responde 404.
    /// </remarks>
    [HttpPost("{id:int}/restablecer-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestablecerPassword(
        int id,
        RestablecerPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Emails.Normalizar(request.Email);

        var usuario = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == id && u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            return Problem(
                detail: $"La automotora {id} no tiene ningún usuario con el email {email}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;

        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            usuario.PasswordHash = _hasher.Hash(request.Password);
            usuario.DebeCambiarPassword = true;

            await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(r => r.UserId == usuario.Id && r.RevocadoEn == null)
                .ForEachAsync(r => r.RevocadoEn = ahora, cancellationToken)
                .ConfigureAwait(false);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return NoContent();
    }

    private ActionResult Conflicto(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoExiste(int id)
        => Problem(detail: $"No existe la automotora {id}.", statusCode: StatusCodes.Status404NotFound);
}
