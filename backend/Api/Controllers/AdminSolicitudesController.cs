using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Catalogo;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Aprobación de las altas de modelo que piden las automotoras.
/// </summary>
/// <remarks>
/// Es la pieza que hace vivible la regla de normalización. Sin ella, prohibirle al
/// vendedor cargar un modelo que falta lo deja trabado, y un vendedor trabado termina
/// cargando el vehículo con el modelo más parecido que encuentre —que es peor que el
/// texto libre, porque el dato queda mal y parece bien—.
/// </remarks>
[ApiController]
[Route("api/admin/solicitudes-modelo")]
[Authorize(Policy = Politicas.SoloSuperAdmin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AdminSolicitudesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;

    public AdminSolicitudesController(AppDbContext db, TimeProvider reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SolicitudModeloDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SolicitudModeloDto>>> Listar(
        [FromQuery] string? estado,
        CancellationToken cancellationToken)
    {
        // Cross-tenant explícito: las solicitudes son de cada automotora y el SuperAdmin no
        // tiene ninguna resuelta, así que sin el escape la lista vendría vacía siempre.
        var consulta = _db.SolicitudesModelo
            .IgnoreQueryFilters()
            .Include(s => s.Marca)
            .Include(s => s.SolicitadaPor)
            .Include(s => s.Tenant)
            .AsQueryable();

        if (Enumeraciones.ParsearOpcional<EstadoSolicitudModelo>(estado) is { } filtro)
        {
            consulta = consulta.Where(s => s.Estado == filtro);
        }

        var solicitudes = await consulta
            // Pendientes primero: son las que alguien está esperando.
            .OrderBy(s => s.Estado)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(solicitudes.Select(MapeosDeSolicitud.ADto).ToList());
    }

    [HttpPost("{id:int}/resolver")]
    [ProducesResponseType(typeof(SolicitudModeloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudModeloDto>> Resolver(
        int id,
        ResolverSolicitudRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var solicitud = await _db.SolicitudesModelo
            .IgnoreQueryFilters()
            .Include(s => s.Marca)
            .Include(s => s.SolicitadaPor)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (solicitud is null)
        {
            return Problem(
                detail: $"No existe la solicitud {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (solicitud.Estado != EstadoSolicitudModelo.Pendiente)
        {
            return Problem(
                detail: "Esa solicitud ya estaba resuelta.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;

        // El escape de escritura, explícito: la solicitud pertenece al tenant que la pidió
        // y el SuperAdmin no tiene ninguno resuelto.
        using var _ = _db.PermitirEscrituraCrossTenant();

        if (request.Aprobar)
        {
            var modelo = await AsegurarModeloAsync(solicitud, cancellationToken).ConfigureAwait(false);

            solicitud.Estado = EstadoSolicitudModelo.Aprobada;
            solicitud.ModeloCreadoId = modelo.Id;

            await CerrarDuplicadasAsync(solicitud, modelo.Id, ahora, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            solicitud.Estado = EstadoSolicitudModelo.Rechazada;
        }

        solicitud.NotaResolucion = string.IsNullOrWhiteSpace(request.Nota) ? null : request.Nota.Trim();
        solicitud.ResueltaEn = ahora;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(solicitud.ADto());
    }

    /// <summary>
    /// Devuelve el modelo del catálogo, creándolo si todavía no está. Que ya exista no es
    /// un error: pudo haberlo creado el SuperAdmin a mano mientras la solicitud esperaba.
    /// </summary>
    private async Task<Modelo> AsegurarModeloAsync(SolicitudModelo solicitud, CancellationToken cancellationToken)
    {
        var existente = await _db.Modelos
            .FirstOrDefaultAsync(
                m => m.MarcaId == solicitud.MarcaId && m.Nombre == solicitud.NombreModelo,
                cancellationToken)
            .ConfigureAwait(false);

        if (existente is not null)
        {
            return existente;
        }

        var modelo = new Modelo
        {
            MarcaId = solicitud.MarcaId,
            Nombre = solicitud.NombreModelo,
            Carroceria = solicitud.Carroceria,
        };

        _db.Modelos.Add(modelo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return modelo;
    }

    /// <summary>
    /// Cierra las solicitudes pendientes de otras automotoras por el mismo modelo.
    /// </summary>
    /// <remarks>
    /// Si tres automotoras pidieron el mismo modelo, al aprobarlo el modelo ya existe para
    /// las tres. Dejar las otras dos pendientes obligaría a rechazarlas a mano una por una
    /// y, mientras tanto, a esos vendedores les diría que su pedido sigue en cola cuando
    /// ya está resuelto.
    /// </remarks>
    private async Task CerrarDuplicadasAsync(
        SolicitudModelo aprobada,
        int modeloId,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var duplicadas = await _db.SolicitudesModelo
            .IgnoreQueryFilters()
            .Where(s => s.Id != aprobada.Id
                        && s.Estado == EstadoSolicitudModelo.Pendiente
                        && s.MarcaId == aprobada.MarcaId
                        && s.NombreModelo == aprobada.NombreModelo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var duplicada in duplicadas)
        {
            duplicada.Estado = EstadoSolicitudModelo.Aprobada;
            duplicada.ModeloCreadoId = modeloId;
            duplicada.ResueltaEn = ahora;
            duplicada.NotaResolucion = "El modelo se dio de alta a partir de otra solicitud igual.";
        }
    }
}
