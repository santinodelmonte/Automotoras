using System.Text;
using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Vehiculos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Carga masiva de stock por CSV.
/// </summary>
/// <remarks>
/// En dos pasos con el mismo endpoint: primero sin <c>confirmar</c>, que valida y devuelve
/// los errores fila por fila sin escribir nada; después con <c>confirmar=true</c>, que
/// vuelve a validar y, si está todo bien, crea todos los vehículos de una.
/// <para>
/// La usa el dueño desde su panel y el SuperAdmin durante la implementación, que es cuando
/// se hace la carga inicial que incluye el precio.
/// </para>
/// </remarks>
[ApiController]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ImportacionDeStockController : ControllerBase
{
    private readonly ImportadorDeStock _importador;
    private readonly AppDbContext _db;

    public ImportacionDeStockController(ImportadorDeStock importador, AppDbContext db)
    {
        _importador = importador;
        _db = db;
    }

    /// <summary>La plantilla vacía, con un ejemplo.</summary>
    [HttpGet("api/vehiculos/importacion/plantilla")]
    [Authorize(Policy = Politicas.PanelDeTenant)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    public IActionResult Plantilla()
        => File(ImportacionDeStock.Plantilla().ABytes(), "text/csv", "plantilla-de-stock.csv");

    [HttpPost("api/vehiculos/importacion")]
    [Authorize(Policy = Politicas.SoloOwner)]
    [RequestSizeLimit(ImportacionDeStock.TamanioMaximo + 64 * 1024)]
    [ProducesResponseType(typeof(ResultadoDeImportacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoDeImportacionDto>> Importar(
        IFormFile? archivo,
        [FromQuery] bool confirmar,
        CancellationToken cancellationToken)
    {
        var tenantId = User.TenantIdDelToken()
                       ?? throw new InvalidOperationException("La importación requiere un tenant en el token.");

        if (await LeerAsync(archivo, cancellationToken).ConfigureAwait(false) is not { } contenido)
        {
            return ArchivoInvalido();
        }

        return Ok(await _importador
            .ProcesarAsync(tenantId, contenido, confirmar, User.PuedeVerCostos(), cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>La misma importación, hecha por el SuperAdmin sobre una automotora.</summary>
    [HttpPost("api/admin/tenants/{id:int}/importacion")]
    [Authorize(Policy = Politicas.SoloSuperAdmin)]
    [RequestSizeLimit(ImportacionDeStock.TamanioMaximo + 64 * 1024)]
    [ProducesResponseType(typeof(ResultadoDeImportacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultadoDeImportacionDto>> ImportarComoAdmin(
        int id,
        IFormFile? archivo,
        [FromQuery] bool confirmar,
        CancellationToken cancellationToken)
    {
        if (!await _db.Tenants.AnyAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return Problem(detail: $"No existe la automotora {id}.", statusCode: StatusCodes.Status404NotFound);
        }

        if (await LeerAsync(archivo, cancellationToken).ConfigureAwait(false) is not { } contenido)
        {
            return ArchivoInvalido();
        }

        // Los vehículos son de una automotora que no es la del request: el SuperAdmin no
        // tiene ninguna. El escape va acá, acotado a esta escritura.
        using var _ = _db.PermitirEscrituraCrossTenant();

        return Ok(await _importador
            .ProcesarAsync(id, contenido, confirmar, cargarCostos: true, cancellationToken)
            .ConfigureAwait(false));
    }

    private static async Task<string?> LeerAsync(IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0 || archivo.Length > ImportacionDeStock.TamanioMaximo)
        {
            return null;
        }

        using var lector = new StreamReader(archivo.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await lector.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private ActionResult ArchivoInvalido()
        => Problem(
            detail: $"Mandá un CSV de hasta {ImportacionDeStock.TamanioMaximo / 1024 / 1024} MB en el campo 'archivo'.",
            statusCode: StatusCodes.Status400BadRequest);
}
