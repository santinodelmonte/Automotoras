using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
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

    public AdminTenantsController(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
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

        var dominios = await _db.Dominios
            .IgnoreQueryFilters()
            .Where(d => d.EsPrincipal)
            .Select(d => new { d.TenantId, d.Dominio })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var principales = dominios.ToDictionary(d => d.TenantId, d => d.Dominio);

        return Ok(tenants
            .Select(t => ADto(
                t,
                principales.GetValueOrDefault(t.Id),
                usuarios.GetValueOrDefault(t.Id),
                vehiculos.GetValueOrDefault(t.Id)))
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

        return Ok(ADto(tenant, await PrincipalAsync(id, cancellationToken).ConfigureAwait(false), usuarios, vehiculos));
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
        var email = Emails.Normalizar(request.EmailDelOwner);

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay una automotora con ese slug.");
        }

        var emailTomado = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (emailTomado)
        {
            return Conflicto("Ya hay un usuario registrado con ese email.");
        }

        var tenant = new Tenant
        {
            Slug = slug,
            Nombre = request.Nombre.Trim(),
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // El escape cross-tenant, explícito y acotado a esta escritura: el Owner que se
        // está creando pertenece a una automotora que no es la del request, porque el
        // SuperAdmin no tiene ninguna.
        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            _db.Users.Add(new User
            {
                TenantId = tenant.Id,
                Email = email,
                Nombre = request.NombreDelOwner.Trim(),
                Rol = RolUsuario.Owner,
                PasswordHash = _hasher.Hash(request.PasswordDelOwner),
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return CreatedAtAction(
            nameof(Obtener),
            new { id = tenant.Id },
            ADto(tenant, dominioPrincipal: null, usuarios: 1, vehiculos: 0));
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

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug && t.Id != id, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay otra automotora con ese slug.");
        }

        tenant.Slug = slug;
        tenant.Nombre = request.Nombre.Trim();

        // Dar de baja una automotora le apaga el sitio público y le impide entrar al
        // panel. No se borra nada: los datos siguen, por si vuelve.
        tenant.Activo = request.Activo;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (usuarios, vehiculos) = await ContarAsync(id, cancellationToken).ConfigureAwait(false);

        return Ok(ADto(tenant, await PrincipalAsync(id, cancellationToken).ConfigureAwait(false), usuarios, vehiculos));
    }

    /// <summary>
    /// Dominio propio de la automotora, para mostrar. Cross-tenant porque el SuperAdmin no
    /// tiene tenant y sin el escape el filtro global devolvería siempre nulo.
    /// </summary>
    private async Task<string?> PrincipalAsync(int tenantId, CancellationToken cancellationToken)
        => await _db.Dominios
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.EsPrincipal)
            .Select(d => d.Dominio)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

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

    private static TenantAdminDto ADto(Tenant tenant, string? dominioPrincipal, int usuarios, int vehiculos)
        => new(
            tenant.Id,
            tenant.Slug,
            tenant.Nombre,
            dominioPrincipal,
            tenant.LogoUrl,
            tenant.ColorPrimario,
            tenant.ColorSecundario,
            tenant.Whatsapp,
            tenant.Telefono,
            tenant.Direccion,
            tenant.Activo,
            tenant.CreatedAt,
            usuarios,
            vehiculos);

    private ActionResult Conflicto(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoExiste(int id)
        => Problem(detail: $"No existe la automotora {id}.", statusCode: StatusCodes.Status404NotFound);
}
