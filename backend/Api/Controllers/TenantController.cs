using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Storage;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Configuración de la automotora: identidad visual y datos de contacto.
/// </summary>
/// <remarks>
/// La automotora que se edita es siempre la del token. No hay id en la ruta ni en el
/// cuerpo, así que no existe la pregunta de si un Owner puede editar otra.
/// </remarks>
[ApiController]
[Route("api/tenant")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TenantController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;
    private readonly ITenantContext _tenantContext;

    public TenantController(AppDbContext db, IImageStorage storage, ITenantContext tenantContext)
    {
        _db = db;
        _storage = storage;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ConfiguracionDeTenantDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfiguracionDeTenantDto>> Obtener(CancellationToken cancellationToken)
    {
        var tenant = await BuscarAsync(cancellationToken).ConfigureAwait(false);

        return tenant is null ? NotFound() : Ok(ADto(tenant));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ConfiguracionDeTenantDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfiguracionDeTenantDto>> Guardar(
        GuardarConfiguracionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await BuscarAsync(cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return NotFound();
        }

        tenant.Nombre = request.Nombre.Trim();
        tenant.ColorPrimario = Vacio(request.ColorPrimario);
        tenant.ColorSecundario = Vacio(request.ColorSecundario);
        tenant.Whatsapp = Vacio(request.Whatsapp);
        tenant.Telefono = Vacio(request.Telefono);
        tenant.Direccion = Vacio(request.Direccion);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(ADto(tenant));
    }

    /// <summary>Sube el logo y lo deja publicado en el sitio.</summary>
    [HttpPost("logo")]
    [RequestSizeLimit(ValidacionDeImagen.TamanoMaximoEnBytes + 4096)]
    [ProducesResponseType(typeof(ConfiguracionDeTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfiguracionDeTenantDto>> SubirLogo(
        IFormFile? imagen,
        CancellationToken cancellationToken)
    {
        if (imagen is null)
        {
            return Rechazo("Mandá la imagen en el campo 'imagen'.");
        }

        var tenant = await BuscarAsync(cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return NotFound();
        }

        await using var contenido = imagen.OpenReadStream();

        var encabezado = new byte[12];
        var leidos = await contenido.ReadAsync(encabezado, cancellationToken).ConfigureAwait(false);
        var validacion = ValidacionDeImagen.Validar(encabezado.AsSpan(0, leidos), imagen.Length);

        if (!validacion.EsValida)
        {
            return Rechazo(validacion.Error!);
        }

        contenido.Position = 0;

        var anterior = ClaveDelLogo(tenant.LogoUrl);

        var guardada = await _storage.GuardarAsync(
            contenido,
            GeneradorDeClaves.CarpetaDeLogo(tenant.Id),
            validacion.Extension,
            validacion.ContentType,
            cancellationToken).ConfigureAwait(false);

        tenant.LogoUrl = guardada.Url;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // El logo viejo se borra recién cuando el nuevo ya quedó guardado: al revés, un
        // fallo dejaría a la automotora sin logo en su propio sitio.
        if (anterior is not null)
        {
            await _storage.BorrarAsync(anterior, cancellationToken).ConfigureAwait(false);
        }

        return Ok(ADto(tenant));
    }

    private Task<Tenant?> BuscarAsync(CancellationToken cancellationToken)
        => _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId, cancellationToken);

    private static ConfiguracionDeTenantDto ADto(Tenant tenant)
        => new(
            tenant.Slug,
            tenant.Nombre,
            tenant.DominioCustom,
            tenant.LogoUrl,
            tenant.ColorPrimario,
            tenant.ColorSecundario,
            tenant.Whatsapp,
            tenant.Telefono,
            tenant.Direccion);

    private static string? Vacio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? ClaveDelLogo(string? url)
    {
        if (url is null)
        {
            return null;
        }

        var indice = url.IndexOf("tenants/", StringComparison.Ordinal);

        return indice < 0 ? null : url[indice..];
    }

    private ActionResult Rechazo(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status400BadRequest);
}
