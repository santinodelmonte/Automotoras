using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Identidad de la automotora para su sitio público.
/// </summary>
/// <remarks>
/// No lleva ningún identificador de tenant en la ruta ni en el cuerpo. El tenant ya viene
/// resuelto por el middleware, desde el dominio o desde el slug de <c>/t/{slug}</c>, y
/// siempre validado contra la tabla <c>tenants</c>. Un endpoint público que aceptara el
/// tenant como parámetro sería un catálogo abierto de todos los clientes del SaaS.
/// </remarks>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicTenantController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PublicTenantController(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("tenant")]
    [ProducesResponseType(typeof(TenantPublicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantPublicoDto>> Get(CancellationToken cancellationToken)
    {
        // Si el middleware no resolvió nada, el request no debería haber llegado hasta acá.
        // El chequeo igual está: es barato y evita que un cambio futuro en el orden del
        // pipeline convierta un 404 en una respuesta con datos de quién sabe quién.
        if (_tenantContext.TenantId is not { } tenantId)
        {
            return NotFound();
        }

        var tenant = await _db.Tenants
            .Where(t => t.Id == tenantId && t.Activo)
            .Select(t => new TenantPublicoDto(
                t.Slug,
                t.Nombre,
                t.LogoUrl,
                t.ColorPrimario,
                t.ColorSecundario,
                t.Whatsapp,
                t.Telefono,
                t.Direccion))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return tenant is null ? NotFound() : Ok(tenant);
    }
}
